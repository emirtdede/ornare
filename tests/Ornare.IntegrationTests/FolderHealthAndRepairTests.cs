using System;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public class FolderHealthAndRepairTests
{
    private readonly ImagePipeline _pipeline = new();
    private readonly FolderCustomizer _customizer = new();

    [Fact]
    public void FolderHealthEngineReportsCorrectStates()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"ornare_health_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);

        try
        {
            // 1. Missing target
            string missingPath = Path.Combine(testRoot, "NonExistentFolder");
            var missingHealth = FolderHealthEngine.CheckHealth(missingPath);
            Assert.Equal(FolderCustomizationState.MissingTarget, missingHealth.State);
            Assert.False(missingHealth.CanAutoRepair);

            // 2. Healthy folder
            string folderPath = Path.Combine(testRoot, "HealthyFolder");
            Directory.CreateDirectory(folderPath);

            string tempIco = Path.Combine(testRoot, "temp.ico");
            using (var img = new MagickImage(MagickColors.DeepSkyBlue, 256, 256))
            {
                byte[] bytes = img.ToByteArray(MagickFormat.Png);
                _pipeline.ProcessImageBytes(bytes, tempIco);
            }

            var applyResult = _customizer.ApplyIcon(folderPath, tempIco);
            Assert.True(applyResult.IsSuccess);

            var healthyResult = FolderHealthEngine.CheckHealth(folderPath);
            Assert.Equal(FolderCustomizationState.Healthy, healthyResult.State);
            Assert.False(healthyResult.CanAutoRepair);

            // 3. NeedsReconciliation (e.g. folder ReadOnly attribute stripped)
            DirectoryInfo dirInfo = new(folderPath);
            dirInfo.Attributes &= ~FileAttributes.ReadOnly;

            var strippedHealth = FolderHealthEngine.CheckHealth(folderPath);
            Assert.Equal(FolderCustomizationState.NeedsReconciliation, strippedHealth.State);
            Assert.True(strippedHealth.CanAutoRepair);

            // 4. ExternalIconConflict (e.g. desktop.ini modified to point to system shell32.dll)
            string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
            File.SetAttributes(iniPath, FileAttributes.Normal);
            File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=C:\\Windows\\System32\\shell32.dll,4\r\n", System.Text.Encoding.Unicode);
            File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

            var conflictHealth = FolderHealthEngine.CheckHealth(folderPath);
            Assert.Equal(FolderCustomizationState.ExternalIconConflict, conflictHealth.State);
            Assert.False(conflictHealth.CanAutoRepair);
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void RepairEngineFixesDamagedCustomizationAndRespectsExternalConflict()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"ornare_repair_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);

        try
        {
            string folderPath = Path.Combine(testRoot, "RepairableFolder");
            Directory.CreateDirectory(folderPath);

            string tempIco = Path.Combine(testRoot, "temp.ico");
            using (var img = new MagickImage(MagickColors.Crimson, 256, 256))
            {
                byte[] bytes = img.ToByteArray(MagickFormat.Png);
                _pipeline.ProcessImageBytes(bytes, tempIco);
            }

            _customizer.ApplyIcon(folderPath, tempIco);

            // Damage folder by removing ReadOnly attribute and corrupting desktop.ini
            DirectoryInfo dirInfo = new(folderPath);
            dirInfo.Attributes &= ~FileAttributes.ReadOnly;

            string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
            File.SetAttributes(iniPath, FileAttributes.Normal);
            File.Delete(iniPath);

            var preRepairHealth = FolderHealthEngine.CheckHealth(folderPath);
            Assert.Equal(FolderCustomizationState.NeedsReconciliation, preRepairHealth.State);
            Assert.True(preRepairHealth.CanAutoRepair);

            // Execute repair
            var repairResult = RepairEngine.RepairFolder(folderPath, tempIco);
            Assert.True(repairResult.IsSuccess);

            // Verify healthy state restored
            var postRepairHealth = FolderHealthEngine.CheckHealth(folderPath);
            Assert.Equal(FolderCustomizationState.Healthy, postRepairHealth.State);

            // Now introduce an external conflict
            File.SetAttributes(iniPath, FileAttributes.Normal);
            File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=C:\\OtherApp\\custom.ico,0\r\n", System.Text.Encoding.Unicode);
            File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

            // Attempt repair without force flag -> MUST FAIL
            var failedRepair = RepairEngine.RepairFolder(folderPath, tempIco, forceExternalConflictResolution: false);
            Assert.False(failedRepair.IsSuccess);
            Assert.Equal(ErrorCode.ExternalConflict, failedRepair.ErrorCode);

            // Attempt repair with force flag -> SUCCEEDS
            var forcedRepair = RepairEngine.RepairFolder(folderPath, tempIco, forceExternalConflictResolution: true);
            Assert.True(forcedRepair.IsSuccess);

            var postForcedHealth = FolderHealthEngine.CheckHealth(folderPath);
            Assert.Equal(FolderCustomizationState.Healthy, postForcedHealth.State);
        }
        finally
        {
            CleanupFolder(testRoot);
        }
    }

    [Fact]
    public void FolderIdentityServiceRetrievesLocalVolumeAndFileId()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ornare_id_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var (volSerial, fileId) = FolderIdentityService.GetFolderIdentity(tempDir);
            Assert.NotNull(volSerial);
            Assert.NotEmpty(volSerial);
            Assert.NotNull(fileId);
            Assert.NotEmpty(fileId);
        }
        finally
        {
            CleanupFolder(tempDir);
        }
    }

    private static void CleanupFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var dir = new DirectoryInfo(path);
                dir.Attributes = FileAttributes.Normal;

                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { file.Attributes = FileAttributes.Normal; } catch { }
                }
                foreach (var sub in dir.GetDirectories("*", SearchOption.AllDirectories))
                {
                    try { sub.Attributes = FileAttributes.Normal; } catch { }
                }

                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
