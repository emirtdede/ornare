using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Ornare.Cli;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class CliExecutionIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _testRoot;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;

    public CliExecutionIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"ornare_cli_integ_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _storage = new SqliteStorageService();
        _customizer = new FolderCustomizer();
    }

    public async Task InitializeAsync()
    {
        await _storage.InitializeAsync();
    }

    public void Dispose()
    {
        _storage.Dispose();
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup in test directory
        }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var swOut = new StringWriter();
        using var swErr = new StringWriter();

        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);

            int code = await Program.Main(args);
            return (code, swOut.ToString().Trim(), swErr.ToString().Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task InspectNonExistentFolderReturnsExitCode3AndTargetNotFound()
    {
        string nonExistent = Path.Combine(_testRoot, "does_not_exist");
        var (code, stdOut, _) = await RunCliAsync("inspect", nonExistent, "--json");

        Assert.Equal(3, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("TargetNotFound", doc.RootElement.GetProperty("code").GetString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task ApplyNonExistentSourceFileReturnsExitCode3AndTargetNotFound()
    {
        string targetFolder = Path.Combine(_testRoot, "target1");
        Directory.CreateDirectory(targetFolder);
        string nonExistentFile = Path.Combine(_testRoot, "missing.png");

        var (code, stdOut, _) = await RunCliAsync("apply", targetFolder, "--source", nonExistentFile, "--json");

        Assert.Equal(3, code);
        using var doc = JsonDocument.Parse(stdOut);
        Assert.Equal("TargetNotFound", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExportIcoValidColorTemplateWritesIcoFileAndOutputsPureJson()
    {
        string outIco = Path.Combine(_testRoot, "exported.ico");
        var (code, stdOut, stdErr) = await RunCliAsync("export-ico", "--output", outIco, "--color", "#0078D4", "--template", "ornare-classic", "--json");

        Assert.Equal(0, code);
        Assert.True(File.Exists(outIco));
        Assert.True(new FileInfo(outIco).Length > 1000);
        Assert.Empty(stdErr);

        // Strict JSON purity: stdOut must be valid JSON with schemaVersion = 1
        using var doc = JsonDocument.Parse(stdOut);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("export-ico", root.GetProperty("command").GetString());
        var data = root.GetProperty("data");
        Assert.Equal(outIco, data.GetProperty("outputPath").GetString());
        Assert.True(data.GetProperty("fileSizeBytes").GetInt64() > 0);
    }

    [Fact]
    public async Task ExportPngValidSizesWritesPngAndOutputsPureJson()
    {
        foreach (int size in new[] { 256, 512, 1024 })
        {
            string outPng = Path.Combine(_testRoot, $"exported_{size}.png");
            var (code, stdOut, stdErr) = await RunCliAsync("export-png", "--output", outPng, "--preset", "status.done", "--size", size.ToString(CultureInfo.InvariantCulture), "--json");

            Assert.Equal(0, code);
            Assert.True(File.Exists(outPng));
            Assert.Empty(stdErr);

            byte[] bytes = await File.ReadAllBytesAsync(outPng);
            Assert.True(PngExportService.ValidatePngBytes(bytes, size));


            using var doc = JsonDocument.Parse(stdOut);
            Assert.Equal(size, doc.RootElement.GetProperty("data").GetProperty("width").GetInt32());
            Assert.Equal(size, doc.RootElement.GetProperty("data").GetProperty("height").GetInt32());
        }
    }

    [Fact]
    public async Task ApplyInspectApplyLastUndoResetLifecycleEquivalence()
    {
        string targetFolder = Path.Combine(_testRoot, "lifecycle_folder");
        Directory.CreateDirectory(targetFolder);

        // 1. Initial inspect: not customized
        var (inspCode0, inspOut0, _) = await RunCliAsync("inspect", targetFolder, "--json");
        Assert.Equal(0, inspCode0);
        using (var doc0 = JsonDocument.Parse(inspOut0))
        {
            Assert.False(doc0.RootElement.GetProperty("data").GetProperty("isCustomized").GetBoolean());
        }

        // 2. Apply icon via CLI
        var (applyCode, applyOut, _) = await RunCliAsync("apply", targetFolder, "--color", "#2563EB", "--template", "ornare-classic", "--json");
        Assert.Equal(0, applyCode);
        using (var docApply = JsonDocument.Parse(applyOut))
        {
            Assert.True(docApply.RootElement.GetProperty("success").GetBoolean());
            Assert.NotNull(docApply.RootElement.GetProperty("operationId").GetString());
        }

        // Verify desktop.ini and relative icon
        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        string icoPath = Path.Combine(targetFolder, ".ornare", "folder.ico");
        Assert.True(File.Exists(iniPath));
        Assert.True(File.Exists(icoPath));

        // 3. Inspect after apply: customized = true, state = Healthy
        var (inspCode1, inspOut1, _) = await RunCliAsync("inspect", targetFolder, "--json");
        Assert.Equal(0, inspCode1);
        using (var doc1 = JsonDocument.Parse(inspOut1))
        {
            var d = doc1.RootElement.GetProperty("data");
            Assert.True(d.GetProperty("isCustomized").GetBoolean());
            Assert.Equal("Healthy", d.GetProperty("state").GetString());
            Assert.True(d.GetProperty("hasBaseline").GetBoolean());
            Assert.True(d.GetProperty("hasAssignment").GetBoolean());
        }

        // 4. Undo via CLI
        var (undoCode, undoOut, _) = await RunCliAsync("undo", targetFolder, "--json");
        Assert.Equal(0, undoCode);
        using (var docUndo = JsonDocument.Parse(undoOut))
        {
            Assert.True(docUndo.RootElement.GetProperty("success").GetBoolean());
        }

        // Inspect after undo: customized = false
        var (inspCode2, inspOut2, _) = await RunCliAsync("inspect", targetFolder, "--json");
        Assert.Equal(0, inspCode2);
        using (var doc2 = JsonDocument.Parse(inspOut2))
        {
            Assert.False(doc2.RootElement.GetProperty("data").GetProperty("isCustomized").GetBoolean());
        }

        // 5. Apply-Last via CLI
        var (applyLastCode, applyLastOut, _) = await RunCliAsync("apply-last", targetFolder, "--json");
        Assert.Equal(0, applyLastCode);
        using (var docLast = JsonDocument.Parse(applyLastOut))
        {
            Assert.True(docLast.RootElement.GetProperty("success").GetBoolean());
        }

        // Inspect after apply-last: customized = true
        var (inspCode3, inspOut3, _) = await RunCliAsync("inspect", targetFolder, "--json");
        Assert.Equal(0, inspCode3);
        using (var doc3 = JsonDocument.Parse(inspOut3))
        {
            Assert.True(doc3.RootElement.GetProperty("data").GetProperty("isCustomized").GetBoolean());
        }

        // 6. Reset via CLI
        var (resetCode, resetOut, _) = await RunCliAsync("reset", targetFolder, "--json");
        Assert.Equal(0, resetCode);
        using (var docReset = JsonDocument.Parse(resetOut))
        {
            Assert.True(docReset.RootElement.GetProperty("success").GetBoolean());
        }

        // Inspect after reset: customized = false
        var (inspCode4, inspOut4, _) = await RunCliAsync("inspect", targetFolder, "--json");
        Assert.Equal(0, inspCode4);
        using (var doc4 = JsonDocument.Parse(inspOut4))
        {
            Assert.False(doc4.RootElement.GetProperty("data").GetProperty("isCustomized").GetBoolean());
        }
    }

    [Fact]
    public async Task ProcessExecutionSeparatesStdoutAndStderrWithoutLoadingWinUI()
    {
        string binDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ornare.Cli", "bin", "Release", "net10.0-windows10.0.26100.0"));
        string exePath = Path.Combine(binDir, "Ornare.Cli.exe");

        if (!File.Exists(exePath))
        {
            // Fallback for debug/development configuration
            binDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Ornare.Cli", "bin", "Debug", "net10.0-windows10.0.26100.0"));
            exePath = Path.Combine(binDir, "Ornare.Cli.exe");
        }

        if (!File.Exists(exePath))
        {
            // Skip external process check if binary not yet built in this configuration
            return;
        }

        var psi = new ProcessStartInfo(exePath, "inspect --json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(2, process.ExitCode);
        Assert.Empty(stderr);
        using var doc = JsonDocument.Parse(stdout.Trim());
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
    }
}
