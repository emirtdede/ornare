using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public class ResetFromExplorerIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;
    private readonly ImagePipeline _pipeline;

    public ResetFromExplorerIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Ornare_F23_Reset_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        string dbPath = Path.Combine(_testRoot, "ornare_test.db");
        _storage = new SqliteStorageService();
        _storage.InitializeAsync(dbPath).GetAwaiter().GetResult();

        _customizer = new FolderCustomizer();
        _pipeline = new ImagePipeline();
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
            // Ignore cleanup errors in temp directory
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ResetWhenFolderNotCustomizedReturnsNotCustomizedWithoutMutation()
    {
        string folder = Path.Combine(_testRoot, "PristineFolder");
        Directory.CreateDirectory(folder);

        string iniPath = Path.Combine(folder, "desktop.ini");
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nInfoTip=UntouchedFolder\r\n");

        var result = await ResetService.ResetAsync(folder, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotCustomized, result.ErrorCode);

        // Verify desktop.ini was untouched
        string contentAfter = File.ReadAllText(iniPath);
        Assert.Contains("InfoTip=UntouchedFolder", contentAfter);
        Assert.False(Directory.Exists(Path.Combine(folder, ".ornare")));

        // Verify zero folder assignments in storage
        var assignment = await _storage.GetAssignmentByPathAsync(folder);
        Assert.Null(assignment);
    }

    [Fact]
    public async Task ResetAfterFolderApplyRevertsToBaselineAndPreservesUnrelatedIniContent()
    {
        string folder = Path.Combine(_testRoot, "CustomizedFolder");
        Directory.CreateDirectory(folder);

        string iniPath = Path.Combine(folder, "desktop.ini");
        string initialIni = "[ViewState]\r\nFolderType=Generic\r\nMode=1\r\nVid=\r\n[HarmlessEnterpriseMetadata]\r\nDeptId=Engineering-42\r\nAuditTag=DO_NOT_DELETE\r\n[.ShellClassInfo]\r\nInfoTip=PreservedDescription\r\n";
        File.WriteAllText(iniPath, initialIni);

        string stagedIco = Path.Combine(_testRoot, "staged_blue.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#0078D4", stagedIco);
        Assert.True(renderResult.IsSuccess);

        // 1. Capture baseline and apply icon
        FolderIconSemantics prior = DesktopIniParser.ReadSemantics(folder);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var applyResult = _customizer.ApplyIcon(folder, stagedIco);
        Assert.True(applyResult.IsSuccess);

        await _storage.SaveFolderAssignmentAsync(folder, epochId, applyResult.Value!, baseline);

        var ntfsId = FolderIdentityService.GetNtfsIdentity(folder);
        if (ntfsId.HasValue)
        {
            await _storage.UpdateAssignmentIdentityAsync(
                folder,
                ntfsId.Value.VolumeSerialNumber,
                ntfsId.Value.FileId64);
        }

        // Verify icon is applied
        Assert.True(File.Exists(Path.Combine(folder, @".ornare\folder.ico")));
        var semanticsAfterApply = DesktopIniParser.ReadSemantics(folder);
        Assert.Equal(@".ornare\folder.ico,0", semanticsAfterApply.IconResource);

        // 2. Perform Reset
        var resetResult = await ResetService.ResetAsync(folder, _storage, _customizer);
        Assert.True(resetResult.IsSuccess, resetResult.ErrorMessage);

        // 3. Verify .ornare\folder.ico removed and desktop.ini restored without Ornare icon
        Assert.False(File.Exists(Path.Combine(folder, @".ornare\folder.ico")));
        Assert.False(Directory.Exists(Path.Combine(folder, ".ornare")));

        string restoredIni = File.ReadAllText(iniPath);
        Assert.DoesNotContain(@".ornare\folder.ico", restoredIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InfoTip=PreservedDescription", restoredIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FolderType=Generic", restoredIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DeptId=Engineering-42", restoredIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AuditTag=DO_NOT_DELETE", restoredIni, StringComparison.OrdinalIgnoreCase);

        // 4. Verify assignment removed from storage
        var assignment = await _storage.GetAssignmentByPathAsync(folder);
        Assert.Null(assignment);
    }

    [Fact]
    public async Task ResetWhenExternalConflictOccurredDoesNotOverwriteExternalState()
    {
        string folder = Path.Combine(_testRoot, "ConflictedFolder");
        Directory.CreateDirectory(folder);

        string stagedIco = Path.Combine(_testRoot, "staged_test.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#107C41", stagedIco);
        Assert.True(renderResult.IsSuccess);

        FolderIconSemantics prior = DesktopIniParser.ReadSemantics(folder);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var applyResult = _customizer.ApplyIcon(folder, stagedIco);
        Assert.True(applyResult.IsSuccess);
        await _storage.SaveFolderAssignmentAsync(folder, epochId, applyResult.Value!, baseline);

        // Simulate external tool replacing the folder's desktop.ini
        string iniPath = Path.Combine(folder, "desktop.ini");
        File.SetAttributes(iniPath, FileAttributes.Normal);
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=C:\\External\\CustomIcon.ico,0\r\n");

        // Execute Reset
        var resetResult = await ResetService.ResetAsync(folder, _storage, _customizer);

        // Must fail with ExternalConflict
        Assert.False(resetResult.IsSuccess);
        Assert.Equal(ErrorCode.ExternalConflict, resetResult.ErrorCode);

        // Must NOT overwrite the external desktop.ini
        string liveIni = File.ReadAllText(iniPath);
        Assert.Contains(@"C:\External\CustomIcon.ico,0", liveIni);

        // Assignment health must be updated to ExternalIconConflict
        var assignment = await _storage.GetAssignmentByPathAsync(folder);
        Assert.NotNull(assignment);
        Assert.Equal(FolderCustomizationState.ExternalIconConflict, assignment.Status);
    }

    [Fact]
    public async Task ResetWhenFolderRenamedResolvesByNtfsIdentityAndResetsSemantics()
    {
        string originalFolder = Path.Combine(_testRoot, "OriginalFolderName");
        Directory.CreateDirectory(originalFolder);

        string stagedIco = Path.Combine(_testRoot, "staged_purple.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#8764B8", stagedIco);
        Assert.True(renderResult.IsSuccess);

        FolderIconSemantics prior = DesktopIniParser.ReadSemantics(originalFolder);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var applyResult = _customizer.ApplyIcon(originalFolder, stagedIco);
        Assert.True(applyResult.IsSuccess);
        await _storage.SaveFolderAssignmentAsync(originalFolder, epochId, applyResult.Value!, baseline);

        var ntfsId = FolderIdentityService.GetNtfsIdentity(originalFolder);
        if (ntfsId.HasValue)
        {
            await _storage.UpdateAssignmentIdentityAsync(
                originalFolder,
                ntfsId.Value.VolumeSerialNumber,
                ntfsId.Value.FileId64);
        }

        // Rename folder on disk
        string renamedFolder = Path.Combine(_testRoot, "RenamedFolderName");
        Directory.Move(originalFolder, renamedFolder);

        // Perform Reset on the RENAMED folder path
        var resetResult = await ResetService.ResetAsync(renamedFolder, _storage, _customizer);

        if (ntfsId.HasValue && ntfsId.Value.VolumeSerialNumber != 0)
        {
            Assert.True(resetResult.IsSuccess, resetResult.ErrorMessage);
            Assert.False(File.Exists(Path.Combine(renamedFolder, @".ornare\folder.ico")));
        }
        else
        {
            // On non-NTFS temp filesystems without file IDs, returns NotCustomized because path changed
            Assert.False(resetResult.IsSuccess);
        }
    }

    [Fact]
    public async Task ResetWhenTargetFolderIdentityReplacedReturnsTargetChanged()
    {
        string folder = Path.Combine(_testRoot, "ReplacedFolder");
        Directory.CreateDirectory(folder);

        string stagedIco = Path.Combine(_testRoot, "staged_green.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#107C41", stagedIco);
        Assert.True(renderResult.IsSuccess);

        FolderIconSemantics prior = DesktopIniParser.ReadSemantics(folder);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var applyResult = _customizer.ApplyIcon(folder, stagedIco);
        Assert.True(applyResult.IsSuccess);
        await _storage.SaveFolderAssignmentAsync(folder, epochId, applyResult.Value!, baseline);

        // Artificially store an identity with different volume or fileId in the database
        await _storage.UpdateAssignmentIdentityAsync(folder, 0x12345678, 0x9999999999999999);

        var ntfsId = FolderIdentityService.GetNtfsIdentity(folder);
        if (ntfsId.HasValue && ntfsId.Value.VolumeSerialNumber != 0)
        {
            // Now invoke Reset on the folder: physical identity won't match 0x12345678 / 0x9999999999999999
            var resetResult = await ResetService.ResetAsync(folder, _storage, _customizer);

            Assert.False(resetResult.IsSuccess);
            Assert.Equal(ErrorCode.TargetChanged, resetResult.ErrorCode);
        }
    }

    [Fact]
    public async Task RapidConcurrentResetCallsSerializeCleanly()
    {
        string folder = Path.Combine(_testRoot, "ConcurrentFolder");
        Directory.CreateDirectory(folder);

        string stagedIco = Path.Combine(_testRoot, "staged_orange.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#D83B01", stagedIco);
        Assert.True(renderResult.IsSuccess);

        FolderIconSemantics prior = DesktopIniParser.ReadSemantics(folder);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var applyResult = _customizer.ApplyIcon(folder, stagedIco);
        Assert.True(applyResult.IsSuccess);
        await _storage.SaveFolderAssignmentAsync(folder, epochId, applyResult.Value!, baseline);

        // Launch 2 concurrent Reset calls
        var task1 = ResetService.ResetAsync(folder, _storage, _customizer);
        var task2 = ResetService.ResetAsync(folder, _storage, _customizer);

        var results = await Task.WhenAll(task1, task2);

        int successCount = 0;
        int notCustomizedCount = 0;

        foreach (var r in results)
        {
            if (r.IsSuccess)
            {
                successCount++;
            }
            else if (r.ErrorCode == ErrorCode.NotCustomized)
            {
                notCustomizedCount++;
            }
        }

        // Exactly one succeeded, second safely reported NotCustomized
        Assert.Equal(1, successCount);
        Assert.Equal(1, notCustomizedCount);

        // Folder is cleanly reverted
        Assert.False(File.Exists(Path.Combine(folder, @".ornare\folder.ico")));
        Assert.Null(await _storage.GetAssignmentByPathAsync(folder));
    }

    [Fact]
    public async Task ResetOnInvalidTargetRejectsWithAppropriateError()
    {
        string nonExistent = Path.Combine(_testRoot, "DoesNotExist_" + Guid.NewGuid().ToString("N"));
        var result = await ResetService.ResetAsync(nonExistent, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.TargetNotFound, result.ErrorCode);

        // Volume root rejection
        string volumeRoot = Path.GetPathRoot(Environment.SystemDirectory)!;
        var rootResult = await ResetService.ResetAsync(volumeRoot, _storage, _customizer);
        Assert.False(rootResult.IsSuccess);
        Assert.Equal(ErrorCode.TargetIsVolumeRoot, rootResult.ErrorCode);
    }
}
