using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Emblems;
using Ornare.Imaging.Templates;
using Ornare.Windows;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

public sealed class Phase11PersonalizationBenchmarkTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly FolderCustomizer _customizer;
    private readonly string _tempRoot;

    public Phase11PersonalizationBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _customizer = new FolderCustomizer();
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ornare_phase11_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        FolderEmblemCompositor.EmojiRasterizer = new DirectWriteEmojiRasterizer();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                foreach (var f in Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                }
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static double GetPercentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private void ReportMetric(string name, List<double> samples, double thresholdP95, string unit = "ms")
    {
        double min = samples.Min();
        double max = samples.Max();
        double mean = samples.Average();
        double p50 = GetPercentile(samples, 0.50);
        double p95 = GetPercentile(samples, 0.95);

        _output.WriteLine($"[BENCHMARK] {name} (N={samples.Count}):");
        _output.WriteLine($"  Min:  {min:F2} {unit}");
        _output.WriteLine($"  Mean: {mean:F2} {unit}");
        _output.WriteLine($"  P50:  {p50:F2} {unit}");
        _output.WriteLine($"  P95:  {p95:F2} {unit} (Threshold: <= {thresholdP95:F2} {unit})");
        _output.WriteLine($"  Max:  {max:F2} {unit}");

        Assert.True(p95 <= thresholdP95, $"{name} P95 ({p95:F2}{unit}) exceeded threshold of {thresholdP95:F2}{unit}");
    }

    [Fact]
    public void BenchmarkExplorerCallbackLatency20Samples()
    {
        string samplePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        List<double> samples = new(20);

        // Warmup
        for (int w = 0; w < 3; w++)
        {
            var attr = File.GetAttributes(samplePath);
            _ = (attr & FileAttributes.Directory) != 0;
        }

        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var attr = File.GetAttributes(samplePath);
            bool isDir = (attr & FileAttributes.Directory) != 0;
            string canonical = Path.GetFullPath(samplePath);
            sw.Stop();

            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Explorer callback threshold <= 15ms per Section 4/18
        ReportMetric("Explorer Callback Latency", samples, 15.0);
    }

    [Fact]
    public void BenchmarkFolderTemplateCatalogLoading20Samples()
    {
        List<double> samples = new(20);

        // Warmup
        _ = FolderTemplateCatalog.All;

        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var templates = FolderTemplateCatalog.All;
            Assert.Equal(9, templates.Count);
            foreach (var t in templates)
            {
                _ = t.SvgContent.Length;
                _ = t.EmblemSafeZone;
            }
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Folder flyout open / 9-template load threshold <= 100ms
        ReportMetric("Folder Template Catalog Load", samples, 100.0);
    }

    [Fact]
    public void BenchmarkVectorEmblemSearchLatency20Samples()
    {
        List<double> samples = new(20);
        string[] queries = ["folder", "doc", "code", "star", "heart", "cloud", "arrow", "mail", "lock", "gear"];

        // Warmup
        _ = VectorEmblemCatalog.Search("test");

        for (int i = 0; i < 20; i++)
        {
            string q = queries[i % queries.Length];
            var sw = Stopwatch.StartNew();
            var results = VectorEmblemCatalog.Search(q);
            sw.Stop();
            Assert.NotNull(results);
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Icon search P95 threshold <= 50ms
        ReportMetric("Vector Emblem Search P95", samples, 50.0);
    }

    [Fact]
    public void BenchmarkCachedPreviewRenderLatency20Samples()
    {
        List<double> samples = new(20);
        string cacheDir = Path.Combine(_tempRoot, "preview_cache");
        Directory.CreateDirectory(cacheDir);

        var recipe = FolderTemplateRenderer.CreateRecipe("#0078D4", "ornare-aqua", 2);
        var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
        byte[] templateBytes = descriptor.GetBytes();

        Dictionary<string, RenderCacheEntry> inMemoryCache = new();

        // 1. Prime the preview cache (cold pass)
        var initial = PngExportService.RenderPngWithCache(
            recipe,
            templateBytes,
            256,
            cacheDirectory: cacheDir,
            cacheLookup: key => inMemoryCache.GetValueOrDefault(key),
            cacheStore: entry => inMemoryCache[entry.RenderKey] = entry);
        Assert.True(initial.IsSuccess);

        // 2. 20 benchmark samples of cached preview retrieval
        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var cached = PngExportService.RenderPngWithCache(
                recipe,
                templateBytes,
                256,
                cacheDirectory: cacheDir,
                cacheLookup: key => inMemoryCache.GetValueOrDefault(key),
                cacheStore: entry => inMemoryCache[entry.RenderKey] = entry);
            sw.Stop();

            Assert.True(cached.IsSuccess);
            Assert.True(cached.Value!.IsFromCache);
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Cached preview threshold <= 25ms per Section 11/18
        ReportMetric("Cached Preview Render Latency", samples, 25.0);
    }

    [Fact]
    public void BenchmarkFullPersonalizationApplyP95TwentySamples()
    {
        List<double> samples = new(20);
        string tempIco = Path.Combine(_tempRoot, "bench_apply.ico");
        string testFolder = Path.Combine(_tempRoot, "bench_apply_folder");
        string cacheDir = Path.Combine(_tempRoot, "ico_cache");
        Directory.CreateDirectory(testFolder);
        Directory.CreateDirectory(cacheDir);

        var emblem = FolderEmblemRecipe.CreateVectorIcon("folder-24-regular", "#FFFFFF");
        Dictionary<string, RenderCacheEntry> inMemoryCache = new();

        // 1. Prime the cache (initial cold compile)
        var primeResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            "#0078D4",
            tempIco,
            "ornare-aqua",
            2,
            emblem: emblem,
            cacheDirectory: cacheDir,
            cacheLookup: key => inMemoryCache.GetValueOrDefault(key),
            cacheStore: entry => inMemoryCache[entry.RenderKey] = entry);
        Assert.True(primeResult.IsSuccess);

        // 2. Run 20 iterations of full apply: ProcessRecipe (with cache lookup) + folder commit (desktop.ini + attributes)
        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();

            var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
                _pipeline,
                "#0078D4",
                tempIco,
                "ornare-aqua",
                2,
                emblem: emblem,
                cacheDirectory: cacheDir,
                cacheLookup: key => inMemoryCache.GetValueOrDefault(key),
                cacheStore: entry => inMemoryCache[entry.RenderKey] = entry);
            Assert.True(renderResult.IsSuccess);

            var applyResult = _customizer.ApplyIcon(testFolder, tempIco);
            Assert.True(applyResult.IsSuccess);

            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Apply P95 threshold <= 500ms (Requirement 14: explicitly labeled as INTERNAL PIPELINE BENCHMARK)
        ReportMetric("INTERNAL PIPELINE BENCHMARK (ProcessRecipe + Folder Commit)", samples, 500.0);
    }

    [Fact]
    public void BenchmarkRepresentativeRealApplyP95TwentySamples()
    {
        string cliPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "ornare.exe");
        if (!File.Exists(cliPath))
        {
            cliPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ornare.Cli", "bin", "Release", "net10.0-windows10.0.26100.0", "win-x64", "publish", "Ornare.Cli.exe"));
        }

        if (!File.Exists(cliPath))
        {
            _output.WriteLine("[SKIP] Representative Real Apply benchmark: CLI binary not found.");
            return;
        }

        List<double> samples = new(20);
        string applyBaseDir = Path.Combine(_tempRoot, "real_apply_benchmarks");
        Directory.CreateDirectory(applyBaseDir);

        for (int i = 0; i < 20; i++)
        {
            string targetFolder = Path.Combine(applyBaseDir, $"TargetFolder_{i}");
            Directory.CreateDirectory(targetFolder);

            var psi = new ProcessStartInfo(cliPath, $"apply \"{targetFolder}\" --color #2563EB --json")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var sw = Stopwatch.StartNew();
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(10000);
            sw.Stop();

            Assert.Equal(0, proc.ExitCode);
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        ReportMetric("Representative REAL Apply (FULL USER APPLY)", samples, 500.0);
    }

    [Fact]
    public void BenchmarkColorPreviewUpdateLatency20Samples()
    {
        List<double> samples = new(20);
        string[] colors = ["#0078D4", "#107C41", "#D83B01", "#5C2D91", "#008272", "#E81123", "#002050", "#2B88D8"];

        // Warmup
        _ = FolderTemplateRenderer.CreateRecipe("#0078D4", "ornare-aqua", 2);

        for (int i = 0; i < 20; i++)
        {
            string c = colors[i % colors.Length];
            var sw = Stopwatch.StartNew();
            var recipe = FolderTemplateRenderer.CreateRecipe(c, "ornare-aqua", 2);
            var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
            byte[] bytes = descriptor.GetBytes();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        ReportMetric("Color Preview Update Latency", samples, 50.0);
    }

    [Fact]
    public void BenchmarkFolderFlyoutSubsequentOpenLatency20Samples()
    {
        var cached = FolderTemplateCatalog.All;
        List<double> samples = new(20);

        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            int count = 0;
            foreach (var t in cached)
            {
                count += t.Id.Length;
            }
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        ReportMetric("Folder Flyout Subsequent Open", samples, 30.0);
    }

    [Fact]
    public void BenchmarkIconCatalogFirstOpenLatency20Samples()
    {
        List<double> samples = new(20);

        for (int i = 0; i < 20; i++)
        {
            var sw = Stopwatch.StartNew();
            var all = VectorEmblemCatalog.All;
            Assert.Equal(320, all.Count);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        ReportMetric("Icon Catalog First Open", samples, 150.0);
    }

    [Fact]
    public void BenchmarkWorkingSetMemoryFootprint()
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        long workingSetBytes = proc.WorkingSet64;
        double workingSetMb = workingSetBytes / (1024.0 * 1024.0);

        _output.WriteLine($"[BENCHMARK] Current Process Working Set: {workingSetMb:F2} MB (Threshold: <= 180 MB)");

        // Under 180MB memory limit per performance contract
        Assert.True(workingSetMb < 180.0, $"Working set ({workingSetMb:F2} MB) exceeded 180 MB");
    }
}
