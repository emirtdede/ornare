using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Ornare.Cli;
using Ornare.Core;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class DryRunIntegrationTests : IDisposable
{
    private readonly string _testRoot;

    public DryRunIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"ornare_dryrun_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort test cleanup
        }
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
    public async Task InspectRejectsDryRunFlagWithExitCode2()
    {
        string targetFolder = Path.Combine(_testRoot, "inspect_folder");
        Directory.CreateDirectory(targetFolder);

        // 1. Machine JSON mode
        var (codeJson, stdOutJson, _) = await RunCliAsync("inspect", targetFolder, "--dry-run", "--json");
        Assert.Equal(2, codeJson);
        using (var doc = JsonDocument.Parse(stdOutJson))
        {
            Assert.Equal("InvalidArguments", doc.RootElement.GetProperty("code").GetString());
            Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("intrinsically read-only", doc.RootElement.GetProperty("messageKey").GetString());
        }

        // 2. Human text mode
        var (codeHuman, _, stdErrHuman) = await RunCliAsync("inspect", targetFolder, "--dry-run");
        Assert.Equal(2, codeHuman);
        Assert.Contains("intrinsically read-only", stdErrHuman);
    }

    [Fact]
    public async Task ApplyDryRunZeroMutationVerification()
    {
        string targetFolder = Path.Combine(_testRoot, "apply_dryrun_folder");
        Directory.CreateDirectory(targetFolder);

        var (code, stdOut, stdErr) = await RunCliAsync("apply", targetFolder, "--color", "#10B981", "--template", "ornare-classic", "--dry-run", "--json");

        Assert.Equal(0, code);
        Assert.Empty(stdErr);

        using var doc = JsonDocument.Parse(stdOut);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("apply", root.GetProperty("command").GetString());

        var data = root.GetProperty("data");
        Assert.Equal(0, data.GetProperty("status").GetInt32()); // WouldSucceed
        Assert.True(data.GetProperty("plannedActions").GetArrayLength() > 0);
        Assert.Equal(0, data.GetProperty("blockers").GetArrayLength());

        // Zero-mutation assertions:
        string iniPath = Path.Combine(targetFolder, OrnareConstants.DesktopIniFileName);
        string ornareDir = Path.Combine(targetFolder, OrnareConstants.CanonicalFolderName);

        Assert.False(File.Exists(iniPath), "desktop.ini must not exist after dry run");
        Assert.False(Directory.Exists(ornareDir), ".ornare directory must not exist after dry run");

        var dirAttrs = File.GetAttributes(targetFolder);
        Assert.False(dirAttrs.HasFlag(FileAttributes.System), "System attribute must not be set by dry run");
        Assert.False(dirAttrs.HasFlag(FileAttributes.ReadOnly), "ReadOnly attribute must not be set by dry run");
    }

    [Fact]
    public async Task ApplyLastDryRunReportsBlockerWhenNoPreviousApply()
    {
        string targetFolder = Path.Combine(_testRoot, "apply_last_dryrun_target");
        Directory.CreateDirectory(targetFolder);

        // Ensure database has no last applied recipe
        using (var storage = new SqliteStorageService())
        {
            await storage.InitializeAsync();
            await storage.ClearLastAppliedRenderKeyAsync();
        }

        var (code, stdOut, _) = await RunCliAsync("apply-last", targetFolder, "--dry-run", "--json");

        Assert.Equal(4, code); // CliExitCodes.Conflict (4)
        using var doc = JsonDocument.Parse(stdOut);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("NoLastAppliedRecipe", root.GetProperty("code").GetString());

        var data = root.GetProperty("data");
        Assert.Equal(1, data.GetProperty("status").GetInt32()); // WouldFail
        Assert.True(data.GetProperty("blockers").GetArrayLength() > 0);

        // Zero mutation
        Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(targetFolder, ".ornare")));
    }

    [Fact]
    public async Task ApplyLastDryRunWouldSucceedAfterRealApply()
    {
        string folder1 = Path.Combine(_testRoot, "apply_real_source");
        string folder2 = Path.Combine(_testRoot, "apply_last_dryrun_target2");
        Directory.CreateDirectory(folder1);
        Directory.CreateDirectory(folder2);

        // 1. Real apply to establish last applied recipe
        var (applyCode, _, _) = await RunCliAsync("apply", folder1, "--color", "#6366F1", "--template", "ornare-classic", "--json");
        Assert.Equal(0, applyCode);

        // 2. Dry run apply-last to folder2
        var (dryCode, dryOut, _) = await RunCliAsync("apply-last", folder2, "--dry-run", "--json");
        Assert.Equal(0, dryCode);

        using var doc = JsonDocument.Parse(dryOut);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(0, data.GetProperty("status").GetInt32()); // WouldSucceed
        Assert.Equal("CacheHit", data.GetProperty("cacheExpectation").GetString());

        // Zero mutation on folder2
        Assert.False(File.Exists(Path.Combine(folder2, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(folder2, ".ornare")));
    }

    [Fact]
    public async Task ResetDryRunZeroMutationOnUncustomizedAndCustomizedFolders()
    {
        string uncustomized = Path.Combine(_testRoot, "reset_uncustomized");
        Directory.CreateDirectory(uncustomized);

        // 1. Uncustomized folder: reports NotCustomized blocker
        var (code1, out1, _) = await RunCliAsync("reset", uncustomized, "--dry-run", "--json");
        Assert.Equal(4, code1); // Conflict
        using (var doc1 = JsonDocument.Parse(out1))
        {
            Assert.Equal("NotCustomized", doc1.RootElement.GetProperty("code").GetString());
            var data1 = doc1.RootElement.GetProperty("data");
            Assert.Equal(1, data1.GetProperty("status").GetInt32()); // WouldFail
        }

        // 2. Real apply to customize folder
        var (applyCode, _, _) = await RunCliAsync("apply", uncustomized, "--color", "#EC4899", "--json");
        Assert.Equal(0, applyCode);

        string iniPath = Path.Combine(uncustomized, "desktop.ini");
        string icoPath = Path.Combine(uncustomized, ".ornare", "folder.ico");
        Assert.True(File.Exists(iniPath));
        Assert.True(File.Exists(icoPath));

        // 3. Dry run reset on customized folder: reports WouldSucceed but preserves customization
        var (code2, out2, _) = await RunCliAsync("reset", uncustomized, "--dry-run", "--json");
        Assert.Equal(0, code2);
        using (var doc2 = JsonDocument.Parse(out2))
        {
            var data2 = doc2.RootElement.GetProperty("data");
            Assert.Equal(0, data2.GetProperty("status").GetInt32()); // WouldSucceed
        }

        // Ensure folder STILL has custom icon and files
        Assert.True(File.Exists(iniPath), "desktop.ini must still exist after reset dry-run");
        Assert.True(File.Exists(icoPath), "folder.ico must still exist after reset dry-run");
    }

    [Fact]
    public async Task UndoDryRunZeroMutationOnUncustomizedAndCustomizedFolders()
    {
        string targetFolder = Path.Combine(_testRoot, "undo_folder");
        Directory.CreateDirectory(targetFolder);

        // 1. Uncustomized: reports NoUndoAvailable blocker
        var (code1, out1, _) = await RunCliAsync("undo", targetFolder, "--dry-run", "--json");
        Assert.Equal(4, code1); // Conflict
        using (var doc1 = JsonDocument.Parse(out1))
        {
            Assert.Equal("NoUndoAvailable", doc1.RootElement.GetProperty("code").GetString());
            var data1 = doc1.RootElement.GetProperty("data");
            Assert.Equal(1, data1.GetProperty("status").GetInt32()); // WouldFail
        }

        // 2. Real apply to customize
        var (applyCode, _, _) = await RunCliAsync("apply", targetFolder, "--color", "#F59E0B", "--json");
        Assert.Equal(0, applyCode);

        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        string icoPath = Path.Combine(targetFolder, ".ornare", "folder.ico");
        Assert.True(File.Exists(iniPath));
        Assert.True(File.Exists(icoPath));

        // 3. Dry run undo: reports WouldSucceed but preserves folder customization
        var (code2, out2, _) = await RunCliAsync("undo", targetFolder, "--dry-run", "--json");
        Assert.Equal(0, code2);
        using (var doc2 = JsonDocument.Parse(out2))
        {
            var data2 = doc2.RootElement.GetProperty("data");
            Assert.Equal(0, data2.GetProperty("status").GetInt32()); // WouldSucceed
        }

        Assert.True(File.Exists(iniPath), "desktop.ini must still exist after undo dry-run");
        Assert.True(File.Exists(icoPath), "folder.ico must still exist after undo dry-run");
    }

    [Fact]
    public async Task ExportIcoDryRunZeroMutationVerification()
    {
        string targetIco = Path.Combine(_testRoot, "dryrun_export.ico");

        var (code, stdOut, stdErr) = await RunCliAsync("export-ico", "--output", targetIco, "--color", "#8B5CF6", "--template", "ornare-classic", "--dry-run", "--json");

        Assert.Equal(0, code);
        Assert.Empty(stdErr);

        using var doc = JsonDocument.Parse(stdOut);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(0, data.GetProperty("status").GetInt32()); // WouldSucceed
        Assert.Equal(targetIco, data.GetProperty("expectedArtifact").GetString());

        // Zero mutation: file must NOT exist
        Assert.False(File.Exists(targetIco), "Export ICO file must not be created during dry run");
    }

    [Fact]
    public async Task ExportPngDryRunZeroMutationVerification()
    {
        string targetPng = Path.Combine(_testRoot, "dryrun_export.png");

        var (code, stdOut, stdErr) = await RunCliAsync("export-png", "--output", targetPng, "--preset", "status.done", "--size", "1024", "--dry-run", "--json");

        Assert.Equal(0, code);
        Assert.Empty(stdErr);

        using var doc = JsonDocument.Parse(stdOut);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(0, data.GetProperty("status").GetInt32()); // WouldSucceed
        Assert.Equal(targetPng, data.GetProperty("expectedArtifact").GetString());

        // Zero mutation: file must NOT exist
        Assert.False(File.Exists(targetPng), "Export PNG file must not be created during dry run");
    }

    [Fact]
    public async Task BlockerVerificationForInvalidInputsAndTargets()
    {
        // 1. Non-existent folder on apply dry-run
        string nonExistentFolder = Path.Combine(_testRoot, "missing_dir_xyz");
        var (code1, out1, _) = await RunCliAsync("apply", nonExistentFolder, "--color", "#14B8A6", "--dry-run", "--json");
        Assert.Equal(3, code1); // TargetNotFound
        using (var doc1 = JsonDocument.Parse(out1))
        {
            Assert.Equal("TargetNotFound", doc1.RootElement.GetProperty("code").GetString());
        }

        // 2. Missing source image file
        string targetFolder = Path.Combine(_testRoot, "valid_dir");
        Directory.CreateDirectory(targetFolder);
        string missingImg = Path.Combine(_testRoot, "missing.png");
        var (code2, out2, _) = await RunCliAsync("apply", targetFolder, "--source", missingImg, "--dry-run", "--json");
        Assert.Equal(3, code2); // TargetNotFound
        using (var doc2 = JsonDocument.Parse(out2))
        {
            Assert.Equal("TargetNotFound", doc2.RootElement.GetProperty("code").GetString());
        }

        // 3. Corrupt/Unsupported image format
        string corruptFile = Path.Combine(_testRoot, "corrupted.png");
        await File.WriteAllTextAsync(corruptFile, "THIS IS NOT A VALID PNG IMAGE HEADER");
        var (code3, out3, _) = await RunCliAsync("apply", targetFolder, "--source", corruptFile, "--dry-run", "--json");
        Assert.Equal(3, code3); // UnsupportedImageFormat or ImageCorrupted
        using (var doc3 = JsonDocument.Parse(out3))
        {
            Assert.False(doc3.RootElement.GetProperty("success").GetBoolean());
        }

        // 4. Invalid PNG size on export-png dry-run
        string pngOut = Path.Combine(_testRoot, "bad_size.png");
        var (code4, out4, _) = await RunCliAsync("export-png", "--output", pngOut, "--preset", "status.done", "--size", "300", "--dry-run", "--json");
        Assert.Equal(2, code4); // InvalidArguments
        using (var doc4 = JsonDocument.Parse(out4))
        {
            Assert.Equal("InvalidArguments", doc4.RootElement.GetProperty("code").GetString());
        }

        // 5. Invalid ICO output extension on export-ico dry-run
        string badExtIco = Path.Combine(_testRoot, "not_an_ico.bmp");
        var (code5, out5, _) = await RunCliAsync("export-ico", "--output", badExtIco, "--color", "#EF4444", "--dry-run", "--json");
        Assert.Equal(2, code5); // InvalidArguments
        using (var doc5 = JsonDocument.Parse(out5))
        {
            Assert.Equal("InvalidArguments", doc5.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task ToctouRevalidationAndLockIndependence()
    {
        string targetFolder = Path.Combine(_testRoot, "toctou_folder");
        Directory.CreateDirectory(targetFolder);

        // 1. Dry run apply first
        var (dryCode, _, _) = await RunCliAsync("apply", targetFolder, "--color", "#3B82F6", "--dry-run", "--json");
        Assert.Equal(0, dryCode);

        // 2. Immediately verify no lock held: write a file to the folder
        string testFile = Path.Combine(targetFolder, "user_data.txt");
        await File.WriteAllTextAsync(testFile, "Hello World");
        Assert.True(File.Exists(testFile));

        // 3. Real apply revalidates and commits
        var (realCode, _, _) = await RunCliAsync("apply", targetFolder, "--color", "#3B82F6", "--json");
        Assert.Equal(0, realCode);
        Assert.True(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
        Assert.True(File.Exists(testFile)); // User file untouched

        // 4. Dry run undo
        var (dryUndoCode, _, _) = await RunCliAsync("undo", targetFolder, "--dry-run", "--json");
        Assert.Equal(0, dryUndoCode);

        // 5. Real undo revalidates and commits
        var (realUndoCode, _, _) = await RunCliAsync("undo", targetFolder, "--json");
        Assert.Equal(0, realUndoCode);
        Assert.False(File.Exists(Path.Combine(targetFolder, ".ornare", "folder.ico")));
    }
}
