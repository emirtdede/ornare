using System.Diagnostics;
using System.IO;
using ImageMagick;
using Ornare.Imaging;
using Ornare.Windows;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

public class PerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline = new();
    private readonly FolderCustomizer _customizer = new();

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BenchmarkPerformanceMetrics()
    {
        // 1. Shell GetState latency measurement (100 iterations)
        List<double> getStateTimes = [];
        string sampleFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var attr = File.GetAttributes(sampleFolder);
            bool isDir = (attr & FileAttributes.Directory) != 0;
            bool isReparse = (attr & FileAttributes.ReparsePoint) != 0;
            sw.Stop();
            getStateTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // 2. Shell Invoke path resolution latency measurement (100 iterations)
        List<double> invokeTimes = [];
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            string canonical = Path.GetFullPath(sampleFolder);
            string args = $"--set-folder \"{canonical}\"";
            sw.Stop();
            invokeTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        // 3. Image Decode & 4. Multi-res ICO generation
        using var testImg = new MagickImage(MagickColors.SteelBlue, 512, 512);
        byte[] imgBytes = testImg.ToByteArray(MagickFormat.Png);

        List<double> decodeTimes = [];
        List<double> icoTimes = [];
        List<double> totalPipelineTimes = [];

        string tempIco = Path.Combine(Path.GetTempPath(), $"perf_ico_{Guid.NewGuid():N}.ico");
        string tempDir = Path.Combine(Path.GetTempPath(), $"perf_folder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Warm up JIT and native coders per shared measurement protocol
            for (int w = 0; w < 2; w++)
            {
                using var warmupDecoded = new MagickImage(imgBytes);
                _ = _pipeline.ProcessImageBytes(imgBytes, tempIco);
                _customizer.ApplyIcon(tempDir, tempIco);
            }

            for (int i = 0; i < 20; i++)
            {
                var swTotal = Stopwatch.StartNew();

                var swDecode = Stopwatch.StartNew();
                using var decoded = new MagickImage(imgBytes);
                decoded.AutoOrient();
                decoded.ColorSpace = ColorSpace.sRGB;
                swDecode.Stop();
                decodeTimes.Add(swDecode.Elapsed.TotalMilliseconds);

                var swIco = Stopwatch.StartNew();
                var result = _pipeline.ProcessImageBytes(imgBytes, tempIco);
                swIco.Stop();
                icoTimes.Add(swIco.Elapsed.TotalMilliseconds);

                _customizer.ApplyIcon(tempDir, tempIco);
                swTotal.Stop();
                totalPipelineTimes.Add(swTotal.Elapsed.TotalMilliseconds);
            }

            var proc = Process.GetCurrentProcess();
            proc.Refresh();
            long workingSetMb = proc.WorkingSet64 / (1024 * 1024);

            PrintMetric("Shell GetState", getStateTimes);
            PrintMetric("Shell Invoke", invokeTimes);
            PrintMetric("Image Decode", decodeTimes);
            PrintMetric("ICO 10-Frame Generation", icoTimes);
            PrintMetric("Total Drop-to-Visible-Icon", totalPipelineTimes);
            _output.WriteLine($"Working Set: {workingSetMb} MB");

            // Enforce performance thresholds per PERFORMANCE.md
            Assert.True(GetPercentile(getStateTimes, 0.95) < 10.0, "Shell GetState P95 must be < 10ms");
            Assert.True(GetPercentile(totalPipelineTimes, 0.95) < 500.0, "Total apply P95 must be < 500ms");
        }
        finally
        {
            if (File.Exists(tempIco)) File.Delete(tempIco);
            CleanupFolder(tempDir);
        }
    }

    private static void CleanupFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var dir = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };
                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void PrintMetric(string name, List<double> times)
    {
        double p50 = GetPercentile(times, 0.50);
        double p95 = GetPercentile(times, 0.95);
        _output.WriteLine($"{name}: P50 = {p50:F2} ms | P95 = {p95:F2} ms (Min = {times.Min():F2} ms, Max = {times.Max():F2} ms)");
    }

    private static double GetPercentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
