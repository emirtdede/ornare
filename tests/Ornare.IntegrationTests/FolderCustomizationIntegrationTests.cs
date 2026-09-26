using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public class FolderCustomizationIntegrationTests
{
    private readonly ImagePipeline _imagePipeline = new();
    private readonly FolderCustomizer _customizer = new();

    [Fact]
    public void EndToEndApplyAndOriginalSourceDeletion()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "ornare_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        string imagePath = Path.Combine(testRoot, "source.png");
        string stagedIco = Path.Combine(testRoot, "staged.ico");

        try
        {
            // 1. Create a synthetic source image
            using (var img = new MagickImage(MagickColors.Crimson, 64, 64))
            {
                img.Write(imagePath, MagickFormat.Png);
            }

            // 2. Process image to multi-res ICO
            var processResult = _imagePipeline.ProcessImage(imagePath, stagedIco);
            Assert.True(processResult.IsSuccess);
            Assert.True(File.Exists(stagedIco));

            // 3. Apply to folder
            var applyResult = _customizer.ApplyIcon(testRoot, stagedIco);
            Assert.True(applyResult.IsSuccess);

            // 4. Verify folder attributes
            var dirInfo = new DirectoryInfo(testRoot);
            Assert.True((dirInfo.Attributes & FileAttributes.ReadOnly) != 0);

            // 5. Verify folder-owned hidden ICO
            string ownedIco = Path.Combine(testRoot, @".ornare\folder.ico");
            Assert.True(File.Exists(ownedIco));
            var icoAttr = File.GetAttributes(ownedIco);
            Assert.True((icoAttr & FileAttributes.Hidden) != 0);

            // 6. Verify desktop.ini
            string iniPath = Path.Combine(testRoot, "desktop.ini");
            Assert.True(File.Exists(iniPath));
            string iniContent = File.ReadAllText(iniPath);
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniContent, StringComparison.OrdinalIgnoreCase);

            // 7. Test INV-FS-004: Deleting original source image does not affect folder customization
            File.Delete(imagePath);
            Assert.False(File.Exists(imagePath));
            Assert.True(File.Exists(ownedIco));
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void ApplyPreservesUnrelatedDesktopIniContent()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "ornare_preserve_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        string iniPath = Path.Combine(testRoot, "desktop.ini");
        string existingIni = "[ViewState]\r\nFolderType=Generic\r\nMode=4\r\n[.ShellClassInfo]\r\nInfoTip=My special folder\r\n";
        File.WriteAllText(iniPath, existingIni);

        string stagedIco = Path.Combine(testRoot, "icon.ico");

        try
        {
            using (var img = new MagickImage(MagickColors.RoyalBlue, 32, 32))
            {
                img.Write(stagedIco, MagickFormat.Ico);
            }

            var applyResult = _customizer.ApplyIcon(testRoot, stagedIco);
            Assert.True(applyResult.IsSuccess);

            string updatedIni = File.ReadAllText(iniPath);
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", updatedIni, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[ViewState]", updatedIni, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FolderType=Generic", updatedIni, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("InfoTip=My special folder", updatedIni, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void ResetRestoresPreOrnareSemanticsAndCleansUpOwnedAssets()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "ornare_reset_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        string stagedIco = Path.Combine(testRoot, "temp.ico");

        try
        {
            using (var img = new MagickImage(MagickColors.DarkGreen, 32, 32))
            {
                img.Write(stagedIco, MagickFormat.Ico);
            }

            var applyResult = _customizer.ApplyIcon(testRoot, stagedIco);
            Assert.True(applyResult.IsSuccess);
            Assert.NotNull(applyResult.Value);

            // Baseline records that Ornare introduced attributes
            var prior = new FolderIconSemantics
            {
                AttributesIntroduced = applyResult.Value.AttributesIntroduced
            };

            var baseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = Guid.NewGuid(),
                PriorSemantics = prior,
                CapturedAt = DateTimeOffset.UtcNow
            };

            // Now Reset
            var resetResult = _customizer.ResetIcon(testRoot, baseline);
            Assert.True(resetResult.IsSuccess);

            // Verify owned ICO was deleted
            string ownedIco = Path.Combine(testRoot, @".ornare\folder.ico");
            Assert.False(File.Exists(ownedIco));

            // Verify folder ReadOnly attribute reverted
            var dirInfo = new DirectoryInfo(testRoot);
            dirInfo.Refresh();
            Assert.False((dirInfo.Attributes & FileAttributes.ReadOnly) != 0);
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void ExternalConflictIsDetectedAndPreventsSilentReset()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "ornare_conflict_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        string stagedIco = Path.Combine(testRoot, "temp.ico");

        try
        {
            using (var img = new MagickImage(MagickColors.Coral, 32, 32))
            {
                img.Write(stagedIco, MagickFormat.Ico);
            }

            var prior = DesktopIniParser.ReadSemantics(testRoot);
            var baseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = Guid.NewGuid(),
                PriorSemantics = prior,
                CapturedAt = DateTimeOffset.UtcNow
            };

            var applyResult = _customizer.ApplyIcon(testRoot, stagedIco);
            Assert.True(applyResult.IsSuccess);

            // User or another tool changes the icon externally!
            string iniPath = Path.Combine(testRoot, "desktop.ini");
            File.SetAttributes(iniPath, FileAttributes.Normal);
            File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=C:\\Windows\\System32\\shell32.dll,22\r\n");

            // Check state
            var state = _customizer.GetFolderState(testRoot, baseline);
            Assert.Equal(FolderCustomizationState.ExternalIconConflict, state);

            // Reset must fail with ExternalConflict
            var resetResult = _customizer.ResetIcon(testRoot, baseline);
            Assert.False(resetResult.IsSuccess);
            Assert.Equal(ErrorCode.ExternalConflict, resetResult.ErrorCode);
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void UndoRestoresPreviousIconSemantics()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "ornare_undo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        string stagedIcoA = Path.Combine(testRoot, "tempA.ico");
        string stagedIcoB = Path.Combine(testRoot, "tempB.ico");

        try
        {
            using (var imgA = new MagickImage(MagickColors.Crimson, 32, 32))
            {
                imgA.Write(stagedIcoA, MagickFormat.Ico);
            }
            using (var imgB = new MagickImage(MagickColors.DodgerBlue, 32, 32))
            {
                imgB.Write(stagedIcoB, MagickFormat.Ico);
            }

            // 1. Pre-customization baseline
            var prior = DesktopIniParser.ReadSemantics(testRoot);
            var baseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = Guid.NewGuid(),
                PriorSemantics = prior,
                CapturedAt = DateTimeOffset.UtcNow
            };

            // 2. Apply Icon A
            var applyA = _customizer.ApplyIcon(testRoot, stagedIcoA);
            Assert.True(applyA.IsSuccess);
            string iniPath = Path.Combine(testRoot, "desktop.ini");
            string iniA = File.ReadAllText(iniPath);
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniA, StringComparison.OrdinalIgnoreCase);

            // Capture baseline before applying B
            var baselineForB = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = Guid.NewGuid(),
                PriorSemantics = applyA.Value!,
                CapturedAt = DateTimeOffset.UtcNow
            };

            // 3. Apply Icon B
            var applyB = _customizer.ApplyIcon(testRoot, stagedIcoB);
            Assert.True(applyB.IsSuccess);

            // 4. Undo to restore state before B (which was A)
            var undoResult = _customizer.UndoIcon(testRoot, baselineForB);
            Assert.True(undoResult.IsSuccess);

            // 5. Verify filesystem semantics: .ornare\folder.ico is present and desktop.ini points to it
            Assert.True(File.Exists(Path.Combine(testRoot, @".ornare\folder.ico")));
            string iniAfterUndo = File.ReadAllText(iniPath);
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniAfterUndo, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void FolderRenameAndMovePreserveRelativeIconSemantics()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "ornare_mv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        string originalFolder = Path.Combine(baseDir, "OriginalFolder");
        string renamedFolder = Path.Combine(baseDir, "RenamedFolder");
        string movedFolder = Path.Combine(baseDir, "SubDir", "MovedFolder");
        Directory.CreateDirectory(originalFolder);
        Directory.CreateDirectory(Path.Combine(baseDir, "SubDir"));

        string stagedIco = Path.Combine(baseDir, "temp.ico");

        try
        {
            using (var img = new MagickImage(MagickColors.Gold, 32, 32))
            {
                img.Write(stagedIco, MagickFormat.Ico);
            }

            var applyResult = _customizer.ApplyIcon(originalFolder, stagedIco);
            Assert.True(applyResult.IsSuccess);

            // Rename folder
            Directory.Move(originalFolder, renamedFolder);
            Assert.True(Directory.Exists(renamedFolder));
            Assert.True(File.Exists(Path.Combine(renamedFolder, @".ornare\folder.ico")));
            string iniRenamed = File.ReadAllText(Path.Combine(renamedFolder, "desktop.ini"));
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniRenamed, StringComparison.OrdinalIgnoreCase);

            // Move folder
            Directory.Move(renamedFolder, movedFolder);
            Assert.True(Directory.Exists(movedFolder));
            Assert.True(File.Exists(Path.Combine(movedFolder, @".ornare\folder.ico")));
            string iniMoved = File.ReadAllText(Path.Combine(movedFolder, "desktop.ini"));
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniMoved, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupFolder(baseDir);
        }
    }

    [Fact]
    public async Task LibraryDeleteInvariantDoesNotAffectCustomizedFolder()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "ornare_lib_del_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        string dbPath = Path.Combine(testRoot, "test.db");
        using var storage = new Ornare.Storage.SqliteStorageService();
        await storage.InitializeAsync(dbPath);

        string imagePath = Path.Combine(testRoot, "icon_source.png");
        string stagedIco = Path.Combine(testRoot, "staged.ico");
        string canonicalIco = Path.Combine(testRoot, "canonical.ico");

        try
        {
            using (var img = new MagickImage(MagickColors.DodgerBlue, 64, 64))
            {
                img.Write(imagePath, MagickFormat.Png);
            }

            var proc = _imagePipeline.ProcessImage(imagePath, stagedIco);
            Assert.True(proc.IsSuccess);
            string hash = proc.Value!.ContentHash;

            File.Copy(stagedIco, canonicalIco);
            await storage.AddOrUpdateAssetAsync(hash, canonicalIco, "PNG");

            // Apply to folder
            var applyResult = _customizer.ApplyIcon(testRoot, stagedIco);
            Assert.True(applyResult.IsSuccess);

            string folderIco = Path.Combine(testRoot, @".ornare\folder.ico");
            string iniPath = Path.Combine(testRoot, "desktop.ini");
            Assert.True(File.Exists(folderIco));
            Assert.True(File.Exists(iniPath));

            // Section 9 Invariant: Delete Library asset
            bool deleted = await storage.DeleteAssetAsync(hash);
            Assert.True(deleted);

            // Verify Library no longer has the asset
            var asset = await storage.GetAssetByHashAsync(hash);
            Assert.Null(asset);

            // Crucial invariant: The folder's owned .ornare\folder.ico and desktop.ini remain fully intact!
            Assert.True(File.Exists(folderIco), "Folder-owned .ornare\\folder.ico must remain intact after Library asset deletion");
            Assert.True(File.Exists(iniPath), "Folder desktop.ini must remain intact after Library asset deletion");
            string iniContent = File.ReadAllText(iniPath);
            Assert.Contains(@"IconResource=.ornare\folder.ico,0", iniContent, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void StagedPreviewDoesNotMutateTargetFolderUntilExplicitlyApplied()
    {
        string targetFolder = Path.Combine(Path.GetTempPath(), "ornare_stage_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetFolder);

        string sourceImage = Path.Combine(Path.GetTempPath(), "ornare_source_" + Guid.NewGuid().ToString("N") + ".png");
        string stagedIco = Path.Combine(Path.GetTempPath(), "ornare_staged_" + Guid.NewGuid().ToString("N") + ".ico");

        try
        {
            // 1. Generate image bytes
            using (var img = new MagickImage(MagickColors.Azure, 64, 64))
            {
                img.Write(sourceImage, MagickFormat.Png);
            }

            // 2. Stage preview (generate ICO without touching target folder)
            var stageResult = _imagePipeline.ProcessImage(sourceImage, stagedIco);
            Assert.True(stageResult.IsSuccess);
            Assert.True(File.Exists(stagedIco));

            // Invariant: Target folder MUST NOT have any desktop.ini or .ornare directory
            Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")), "Target folder desktop.ini must not exist before user confirms apply");
            Assert.False(Directory.Exists(Path.Combine(targetFolder, ".ornare")), "Target folder .ornare directory must not exist before user confirms apply");

            // 3. User cancels preview simulation: staged file deleted, target still 100% clean
            File.Delete(stagedIco);
            Assert.False(File.Exists(stagedIco));
            Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
            Assert.False(Directory.Exists(Path.Combine(targetFolder, ".ornare")));

            // 4. Re-stage and now user confirms apply
            var reStageResult = _imagePipeline.ProcessImage(sourceImage, stagedIco);
            Assert.True(reStageResult.IsSuccess);

            var applyResult = _customizer.ApplyIcon(targetFolder, stagedIco);
            Assert.True(applyResult.IsSuccess);

            // Now target folder has customization
            Assert.True(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
            Assert.True(File.Exists(Path.Combine(targetFolder, @".ornare\folder.ico")));
        }
        finally
        {
            if (File.Exists(sourceImage)) File.Delete(sourceImage);
            if (File.Exists(stagedIco)) File.Delete(stagedIco);
            CleanupFolder(targetFolder);
        }
    }

    private static void CleanupFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                // Clear ReadOnly/Hidden attributes so deletion succeeds cleanly
                var dir = new DirectoryInfo(path);
                dir.Attributes = FileAttributes.Normal;

                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }

                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
