using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Emblems;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class PersonalizationApplyIntegrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _storageDbPath;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;
    private readonly ImagePipeline _pipeline;

    public PersonalizationApplyIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ornare_test_pers_apply_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _storageDbPath = Path.Combine(_tempRoot, "ornare_pers_test.db");
        _storage = new SqliteStorageService();
        _storage.InitializeAsync(_storageDbPath).GetAwaiter().GetResult();

        _customizer = new FolderCustomizer();
        _pipeline = new ImagePipeline();

        // Ensure DirectWrite emoji rasterizer is registered
        FolderEmblemCompositor.EmojiRasterizer = new DirectWriteEmojiRasterizer();
    }

    public void Dispose()
    {
        _storage.Dispose();
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempRoot))
        {
            try
            {
                foreach (var file in Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task StagingPersonalizationMutatesNoDiskOrDatabaseStateBeforeApply()
    {
        string targetFolder = Path.Combine(_tempRoot, "StagingFolder_NoDiskMutation");
        Directory.CreateDirectory(targetFolder);

        string sampleDoc = Path.Combine(targetFolder, "important_document.docx");
        await File.WriteAllTextAsync(sampleDoc, "User Important Content");

        var initialDirInfo = new DirectoryInfo(targetFolder);
        var initialAttrs = initialDirInfo.Attributes;

        // Stage Aqua template + custom color + Fluent vector emblem
        var staging = new PersonalizationStagingModel();
        staging.Initialize(PersonalizationMode.FolderTemplate);

        staging.StageFolderTemplate("ornare-aqua", 2);
        staging.StageFolderColor("#E91E63");
        staging.StageVectorEmblem("document-bullet-list-24-regular", "#FFFFFF");

        Assert.True(staging.CanApply);
        Assert.Equal(PersonalizationMode.FolderTemplate, staging.ActiveMode);
        Assert.Equal("ornare-aqua", staging.StagedTemplateState.TemplateId);
        Assert.Equal("#E91E63", staging.StagedTemplateState.FolderColorHex);
        Assert.Equal(EmblemKind.VectorIcon, staging.StagedTemplateState.Emblem.Kind);

        // Verify TARGET FOLDER ON DISK HAS ZERO MUTATIONS
        initialDirInfo.Refresh();
        Assert.Equal(initialAttrs, initialDirInfo.Attributes);
        Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(targetFolder, ".ornare")));
        Assert.True(File.Exists(sampleDoc));
        Assert.Equal("User Important Content", await File.ReadAllTextAsync(sampleDoc));

        // Verify DATABASE HAS ZERO MUTATIONS
        var pendingJournal = await _storage.GetPendingJournalEntriesAsync();
        Assert.Empty(pendingJournal);

        var assignment = await _storage.GetAssignmentByPathAsync(targetFolder);
        Assert.Null(assignment);
    }

    [Fact]
    public async Task FullPersonalizationApplyWithTemplateColorAndEmblemCreatesDurableIcoAndCompleteDatabaseRecords()
    {
        string targetFolder = Path.Combine(_tempRoot, "PersonalizedAquaFolder");
        Directory.CreateDirectory(targetFolder);

        string initialFile = Path.Combine(targetFolder, "project_notes.txt");
        await File.WriteAllTextAsync(initialFile, "Notes before styling");

        // 1. Staging model
        var staging = new PersonalizationStagingModel();
        staging.Initialize(PersonalizationMode.FolderTemplate);
        staging.StageFolderTemplate("ornare-aqua", 2);
        staging.StageFolderColor("#2B88D8");
        staging.StageVectorEmblem("document-bullet-list-24-regular", "#FFFFFF");

        var templateState = staging.StagedTemplateState;

        // 2. Multi-res ICO generation
        Guid opId = Guid.NewGuid();
        string tempIco = Path.Combine(_tempRoot, $"staged_{opId:N}.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            templateState.FolderColorHex,
            tempIco,
            templateState.TemplateId,
            templateState.TemplateVersion,
            emblem: templateState.Emblem);

        Assert.True(renderResult.IsSuccess, renderResult.ErrorMessage);
        Assert.NotNull(renderResult.Value);
        Assert.True(File.Exists(tempIco));

        // Verify 10 standard ICO frames
        using (var icoCollection = new MagickImageCollection(tempIco))
        {
            Assert.Equal(10, icoCollection.Count);
            int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
            for (int i = 0; i < expectedSizes.Length; i++)
            {
                Assert.Equal((uint)expectedSizes[i], icoCollection[i].Width);
                Assert.Equal((uint)expectedSizes[i], icoCollection[i].Height);
            }
        }

        // 3. Operation journal tracking
        var journal = new JournalEntry
        {
            OperationId = opId,
            Kind = OperationKind.Apply,
            TargetFolderPath = targetFolder,
            State = JournalState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TargetAssetPath = tempIco
        };
        await _storage.RecordJournalEntryAsync(journal);
        await _storage.UpdateJournalStateAsync(opId, JournalState.AssetGenerated);

        // 4. Capture baseline & apply to folder
        var prior = DesktopIniParser.ReadSemantics(targetFolder);
        Guid epochId = Guid.NewGuid();
        var baseline = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var applyResult = _customizer.ApplyIcon(targetFolder, tempIco, state =>
        {
            _storage.UpdateJournalStateAsync(opId, state).GetAwaiter().GetResult();
        });
        Assert.True(applyResult.IsSuccess, applyResult.ErrorMessage);
        Assert.NotNull(applyResult.Value);

        // 5. Database commits
        await _storage.SaveFolderAssignmentAsync(targetFolder, epochId, applyResult.Value, baseline);

        string canonicalIco = Path.Combine(_tempRoot, "canonical_lib", $"{renderResult.Value.ContentHash}.ico");
        Directory.CreateDirectory(Path.GetDirectoryName(canonicalIco)!);
        File.Copy(tempIco, canonicalIco, overwrite: true);

        await _storage.AddOrUpdateAssetAsync(renderResult.Value.ContentHash, canonicalIco, renderResult.Value.SourceFormat);

        var fi = new FileInfo(canonicalIco);
        await _storage.SaveRenderCacheEntryAsync(new RenderCacheEntry
        {
            RenderKey = renderResult.Value.RenderKey ?? string.Empty,
            RecipeVersion = 1,
            RecipeJson = string.Empty,
            SourceAssetHash = renderResult.Value.ContentHash,
            CanonicalIcoPath = canonicalIco,
            FrameSizes = string.Join(",", renderResult.Value.FrameSizes),
            FileSizeBytes = fi.Length,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        });

        await _storage.RecordHistoryAsync(targetFolder, opId, null, renderResult.Value.ContentHash, "apply");
        if (!string.IsNullOrWhiteSpace(renderResult.Value.RenderKey))
        {
            await _storage.SetLastAppliedRenderKeyAsync(renderResult.Value.RenderKey);
        }
        await _storage.UpdateJournalStateAsync(opId, JournalState.Completed);

        // 6. Verification of folder on disk
        var dirInfo = new DirectoryInfo(targetFolder);
        dirInfo.Refresh();
        Assert.True((dirInfo.Attributes & FileAttributes.ReadOnly) != 0 || (dirInfo.Attributes & FileAttributes.System) != 0);

        string ownedIco = Path.Combine(targetFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIco));
        var icoAttrs = File.GetAttributes(ownedIco);
        Assert.True((icoAttrs & FileAttributes.Hidden) != 0);

        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        Assert.True(File.Exists(iniPath));
        string iniText = await File.ReadAllTextAsync(iniPath);
        Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniText, StringComparison.OrdinalIgnoreCase);

        // User file remains untouched
        Assert.True(File.Exists(initialFile));
        Assert.Equal("Notes before styling", await File.ReadAllTextAsync(initialFile));

        // 7. Verification of database records
        var assignment = await _storage.GetAssignmentByPathAsync(targetFolder);
        Assert.NotNull(assignment);
        Assert.Equal(epochId, assignment.OwnershipEpochId);
        Assert.Equal(@".ornare\folder.ico,0", assignment.IconRelativePath);

        if (!string.IsNullOrWhiteSpace(renderResult.Value.RenderKey))
        {
            var cacheEntry = await _storage.GetRenderCacheEntryAsync(renderResult.Value.RenderKey);
            Assert.NotNull(cacheEntry);
            Assert.Equal(canonicalIco, cacheEntry.CanonicalIcoPath);
            Assert.Equal(fi.Length, cacheEntry.FileSizeBytes);

            string? lastAppliedKey = await _storage.GetLastAppliedRenderKeyAsync();
            Assert.Equal(renderResult.Value.RenderKey, lastAppliedKey);
        }

        var pending = await _storage.GetPendingJournalEntriesAsync();
        Assert.Empty(pending);
    }

    [Fact]
    public async Task PersonalizationApplyWithEmojiEmblemCreatesDurableIcoAndCachesCorrectly()
    {
        string targetFolder = Path.Combine(_tempRoot, "PersonalizedEmojiFolder");
        Directory.CreateDirectory(targetFolder);

        // Stage Soft template + orange color + 🚀 rocket emoji
        var staging = new PersonalizationStagingModel();
        staging.Initialize(PersonalizationMode.FolderTemplate);
        staging.StageFolderTemplate("ornare-soft", 2);
        staging.StageFolderColor("#FF9800");
        staging.StageEmojiEmblem("🚀");

        var templateState = staging.StagedTemplateState;

        Guid opId = Guid.NewGuid();
        string tempIco = Path.Combine(_tempRoot, $"staged_emoji_{opId:N}.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            templateState.FolderColorHex,
            tempIco,
            templateState.TemplateId,
            templateState.TemplateVersion,
            emblem: templateState.Emblem);

        Assert.True(renderResult.IsSuccess, renderResult.ErrorMessage);
        Assert.True(File.Exists(tempIco));

        using (var collection = new MagickImageCollection(tempIco))
        {
            Assert.Equal(10, collection.Count);
        }

        var applyResult = _customizer.ApplyIcon(targetFolder, tempIco);
        Assert.True(applyResult.IsSuccess);

        string ownedIco = Path.Combine(targetFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIco));
        Assert.True(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
    }

    [Fact]
    public async Task ResetPersonalizedFolderCleansUpOwnedAssetsAndRestoresFolderAttributes()
    {
        string targetFolder = Path.Combine(_tempRoot, "FolderToReset");
        Directory.CreateDirectory(targetFolder);

        string tempIco = Path.Combine(_tempRoot, "temp_for_reset.ico");
        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            "#0078D4",
            tempIco,
            "ornare-classic",
            2);
        Assert.True(renderResult.IsSuccess);

        // Capture prior baseline BEFORE applying
        var priorBeforeApply = DesktopIniParser.ReadSemantics(targetFolder);

        var applyResult = _customizer.ApplyIcon(targetFolder, tempIco);
        Assert.True(applyResult.IsSuccess);
        Assert.NotNull(applyResult.Value);

        var prior = priorBeforeApply with
        {
            AttributesIntroduced = applyResult.Value.AttributesIntroduced
        };
        Guid epochId = Guid.NewGuid();
        var baseline = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        await _storage.SaveFolderAssignmentAsync(targetFolder, epochId, applyResult.Value, baseline);

        // Verify customized state
        string ownedIco = Path.Combine(targetFolder, @".ornare\folder.ico");
        Assert.True(File.Exists(ownedIco));

        // Now execute Reset
        var resetResult = _customizer.ResetIcon(targetFolder, baseline);
        Assert.True(resetResult.IsSuccess, resetResult.ErrorMessage);

        // Verify owned ICO removed and folder attributes restored
        Assert.False(File.Exists(ownedIco));
        var dirInfo = new DirectoryInfo(targetFolder);
        dirInfo.Refresh();
        Assert.False((dirInfo.Attributes & FileAttributes.ReadOnly) != 0);

        // Update database assignment to cleared
        await _storage.RemoveFolderAssignmentAsync(targetFolder);
        var assignmentAfter = await _storage.GetAssignmentByPathAsync(targetFolder);
        Assert.Null(assignmentAfter);
    }

    [Fact]
    public async Task UndoPersonalizedFolderRestoresPreviousBaselineSemantics()
    {
        string targetFolder = Path.Combine(_tempRoot, "FolderToUndo");
        Directory.CreateDirectory(targetFolder);

        string icoA = Path.Combine(_tempRoot, "styleA.ico");
        string icoB = Path.Combine(_tempRoot, "styleB.ico");

        var resA = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#0078D4", icoA, "ornare-classic", 2);
        var resB = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#9C27B0", icoB, "ornare-geo", 2);
        Assert.True(resA.IsSuccess);
        Assert.True(resB.IsSuccess);

        // 1. Apply Style A
        var applyA = _customizer.ApplyIcon(targetFolder, icoA);
        Assert.True(applyA.IsSuccess);
        Assert.NotNull(applyA.Value);

        var baselineAfterA = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = Guid.NewGuid(),
            PriorSemantics = applyA.Value,
            CapturedAt = DateTimeOffset.UtcNow
        };

        // 2. Apply Style B
        var applyB = _customizer.ApplyIcon(targetFolder, icoB);
        Assert.True(applyB.IsSuccess);

        // 3. Undo back to Style A baseline
        var undoResult = _customizer.UndoIcon(targetFolder, baselineAfterA);
        Assert.True(undoResult.IsSuccess, undoResult.ErrorMessage);

        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        Assert.True(File.Exists(iniPath));
        string iniText = await File.ReadAllTextAsync(iniPath);
        Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FaultInjectionAtAssetGeneratedRollsBackCleanlyWithoutFolderCorruption()
    {
        string targetFolder = Path.Combine(_tempRoot, "FaultFolder_AssetGenerated");
        Directory.CreateDirectory(targetFolder);
        string userFile = Path.Combine(targetFolder, "data.csv");
        await File.WriteAllTextAsync(userFile, "col1,col2\r\nval1,val2");

        Guid opId = Guid.NewGuid();
        string tempIco = Path.Combine(_tempRoot, $"staged_fault_{opId:N}.ico");
        await File.WriteAllTextAsync(tempIco, "fake ico bytes for fault test");

        // Simulate crash right after AssetGenerated
        await _storage.RecordJournalEntryAsync(new JournalEntry
        {
            OperationId = opId,
            Kind = OperationKind.Apply,
            TargetFolderPath = targetFolder,
            TargetAssetPath = tempIco,
            State = JournalState.AssetGenerated,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var pending = await _storage.GetPendingJournalEntriesAsync();
        Assert.Single(pending);

        // Execute startup reconciliation
        int reconciled = await _storage.ReconcileStartupAsync();
        Assert.Equal(1, reconciled);

        // Verify journal entry marked RolledBack
        var pendingAfter = await _storage.GetPendingJournalEntriesAsync();
        Assert.Empty(pendingAfter);

        // Verify folder on disk remains 100% clean and uncorrupted
        Assert.False(Directory.Exists(Path.Combine(targetFolder, ".ornare")));
        Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
        Assert.True(File.Exists(userFile));
        Assert.Equal("col1,col2\r\nval1,val2", await File.ReadAllTextAsync(userFile));
    }

    [Fact]
    public async Task FaultInjectionAtFilesystemStagedRollsBackCleanly()
    {
        string targetFolder = Path.Combine(_tempRoot, "FaultFolder_FilesystemStaged");
        Directory.CreateDirectory(targetFolder);

        // Target folder had .ornare\folder.ico staged, but desktop.ini was not committed
        string ornareDir = Path.Combine(targetFolder, ".ornare");
        Directory.CreateDirectory(ornareDir);
        string stagedIco = Path.Combine(ornareDir, "folder.ico");
        await File.WriteAllTextAsync(stagedIco, "incomplete staged ico");

        Guid opId = Guid.NewGuid();
        await _storage.RecordJournalEntryAsync(new JournalEntry
        {
            OperationId = opId,
            Kind = OperationKind.Apply,
            TargetFolderPath = targetFolder,
            State = JournalState.FilesystemStaged,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        int reconciled = await _storage.ReconcileStartupAsync();
        Assert.Equal(1, reconciled);

        var pendingAfter = await _storage.GetPendingJournalEntriesAsync();
        Assert.Empty(pendingAfter);

        // desktop.ini must not exist, folder attributes must not be locked
        Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
    }

    [Fact]
    public async Task FaultInjectionAtFilesystemCommittedRecoversDeterministicallyToCompleted()
    {
        string targetFolder = Path.Combine(_tempRoot, "FaultFolder_Committed");
        Directory.CreateDirectory(targetFolder);

        // Target folder has desktop.ini committed
        string iniPath = Path.Combine(targetFolder, "desktop.ini");
        await File.WriteAllTextAsync(iniPath, "[.ShellClassInfo]\r\nIconResource=.ornare\\folder.ico,0\r\n");

        Guid opId = Guid.NewGuid();
        await _storage.RecordJournalEntryAsync(new JournalEntry
        {
            OperationId = opId,
            Kind = OperationKind.Apply,
            TargetFolderPath = targetFolder,
            State = JournalState.FilesystemCommitted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        int reconciled = await _storage.ReconcileStartupAsync();
        Assert.Equal(1, reconciled);

        var pendingAfter = await _storage.GetPendingJournalEntriesAsync();
        Assert.Empty(pendingAfter);

        // Verify idempotency (INV-TX-002)
        int secondRun = await _storage.ReconcileStartupAsync();
        Assert.Equal(0, secondRun);
    }
}
