using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public class UndoFromExplorerIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;
    private readonly ImagePipeline _pipeline;

    public UndoFromExplorerIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Ornare_F22_Undo_" + Guid.NewGuid().ToString("N"));
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
            // Ignore cleanup errors in temp
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UndoWhenNoHistoryExistsReturnsNoUndoAvailableWithoutMutation()
    {
        string folder = Path.Combine(_testRoot, "PristineFolder");
        Directory.CreateDirectory(folder);

        string iniPath = Path.Combine(folder, "desktop.ini");
        File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nInfoTip=UntouchedFolder\r\n");

        var result = await UndoService.UndoAsync(folder, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NoUndoAvailable, result.ErrorCode);

        // Verify desktop.ini was untouched
        string contentAfter = File.ReadAllText(iniPath);
        Assert.Contains("InfoTip=UntouchedFolder", contentAfter);
        Assert.False(Directory.Exists(Path.Combine(folder, ".ornare")));

        // Verify zero folder assignments in storage
        var assignment = await _storage.GetAssignmentByPathAsync(folder);
        Assert.Null(assignment);
    }

    [Fact]
    public async Task UndoAfterFolderApplyRestoresPreOrnareSemanticsAndCleansUpAssignment()
    {
        string folder = Path.Combine(_testRoot, "CustomizedFolder");
        Directory.CreateDirectory(folder);

        string iniPath = Path.Combine(folder, "desktop.ini");
        File.WriteAllText(iniPath, "[ViewState]\r\nFolderType=Generic\r\n[.ShellClassInfo]\r\nInfoTip=PreservedDescription\r\n");

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
                ntfsId.Value.VolumeSerialNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ntfsId.Value.FileId64.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // Verify icon is applied
        Assert.True(File.Exists(Path.Combine(folder, @".ornare\folder.ico")));
        var semanticsAfterApply = DesktopIniParser.ReadSemantics(folder);
        Assert.Equal(@".ornare\folder.ico,0", semanticsAfterApply.IconResource);

        // 2. Perform Undo
        var undoResult = await UndoService.UndoAsync(folder, _storage, _customizer);
        Assert.True(undoResult.IsSuccess, undoResult.ErrorMessage);

        // 3. Verify .ornare\folder.ico removed and desktop.ini restored without Ornare icon
        Assert.False(File.Exists(Path.Combine(folder, @".ornare\folder.ico")));

        string restoredIni = File.ReadAllText(iniPath);
        Assert.DoesNotContain(@".ornare\folder.ico", restoredIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InfoTip=PreservedDescription", restoredIni, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FolderType=Generic", restoredIni, StringComparison.OrdinalIgnoreCase);

        // 4. Verify assignment removed from storage
        var assignment = await _storage.GetAssignmentByPathAsync(folder);
        Assert.Null(assignment);
    }

    [Fact]
    public async Task UndoWhenExternalConflictOccurredDoesNotOverwriteExternalState()
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

        // Execute Undo
        var undoResult = await UndoService.UndoAsync(folder, _storage, _customizer);

        // Must fail with ExternalConflict
        Assert.False(undoResult.IsSuccess);
        Assert.Equal(ErrorCode.ExternalConflict, undoResult.ErrorCode);

        // Must NOT overwrite the external desktop.ini
        string liveIni = File.ReadAllText(iniPath);
        Assert.Contains(@"C:\External\CustomIcon.ico,0", liveIni);

        // Assignment health must be updated to ExternalIconConflict
        var assignment = await _storage.GetAssignmentByPathAsync(folder);
        Assert.NotNull(assignment);
        Assert.Equal(FolderCustomizationState.ExternalIconConflict, assignment.Status);
    }

    [Fact]
    public async Task UndoWhenFolderRenamedResolvesByNtfsIdentityAndRestoresSemantics()
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

        // Perform Undo on the RENAMED folder path
        var undoResult = await UndoService.UndoAsync(renamedFolder, _storage, _customizer);

        // If on NTFS volume, NTFS identity resolves assignment and Undo succeeds!
        if (ntfsId.HasValue && ntfsId.Value.VolumeSerialNumber != 0)
        {
            Assert.True(undoResult.IsSuccess, undoResult.ErrorMessage);
            Assert.False(File.Exists(Path.Combine(renamedFolder, @".ornare\folder.ico")));
        }
        else
        {
            // On non-NTFS temp filesystems without file IDs, returns NoUndoAvailable because path changed
            Assert.False(undoResult.IsSuccess);
        }
    }

    [Fact]
    public async Task RapidDuplicateUndoCallsSerializeDeterministically()
    {
        string folder = Path.Combine(_testRoot, "ConcurrentFolder");
        Directory.CreateDirectory(folder);

        string stagedIco = Path.Combine(_testRoot, "staged_concurrent.ico");
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

        // Launch two rapid concurrent calls
        var task1 = UndoService.UndoAsync(folder, _storage, _customizer);
        var task2 = UndoService.UndoAsync(folder, _storage, _customizer);

        var results = await Task.WhenAll(task1, task2);

        // Exactly one should succeed, and the other should safely return NoUndoAvailable
        int successCount = 0;
        int noUndoCount = 0;

        foreach (var r in results)
        {
            if (r.IsSuccess) successCount++;
            else if (r.ErrorCode == ErrorCode.NoUndoAvailable) noUndoCount++;
        }

        Assert.Equal(1, successCount);
        Assert.Equal(1, noUndoCount);

        // Clean state preserved
        Assert.False(File.Exists(Path.Combine(folder, @".ornare\folder.ico")));
    }

    [Fact]
    public async Task UndoOnInvalidTargetsRejectsWithStructuredErrorCode()
    {
        string nonExistent = Path.Combine(_testRoot, "DoesNotExist_" + Guid.NewGuid().ToString("N"));
        var result = await UndoService.UndoAsync(nonExistent, _storage, _customizer);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.TargetNotFound, result.ErrorCode);

        string testFile = Path.Combine(_testRoot, "RegularFile.txt");
        File.WriteAllText(testFile, "Hello World");
        var fileResult = await UndoService.UndoAsync(testFile, _storage, _customizer);
        Assert.False(fileResult.IsSuccess);
        Assert.Equal(ErrorCode.TargetNotFound, fileResult.ErrorCode);
    }
}
