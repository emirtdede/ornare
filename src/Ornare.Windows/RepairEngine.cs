namespace Ornare.Windows;

using System;
using System.IO;
using Ornare.Core;

public static class RepairEngine
{
    private const string RelativeIconSubPath = @".ornare\folder.ico";

    public static OperationResult RepairFolder(
        string folderPath,
        string? canonicalIcoBackupPath = null,
        bool forceExternalConflictResolution = false)
    {
        var health = FolderHealthEngine.CheckHealth(folderPath);

        if (health.State == FolderCustomizationState.Healthy)
        {
            return OperationResult.Success();
        }

        if (health.State == FolderCustomizationState.MissingTarget)
        {
            return OperationResult.Failure(ErrorCode.TargetNotFound, "Cannot repair folder because directory does not exist.");
        }

        if (health.State == FolderCustomizationState.ExternalIconConflict && !forceExternalConflictResolution)
        {
            return OperationResult.Failure(ErrorCode.ExternalConflict,
                "Cannot repair folder because an external icon conflict is active and force resolution was not requested.");
        }

        try
        {
            string ornareDir = Path.Combine(folderPath, OrnareConstants.CanonicalFolderName);
            string targetIcoPath = Path.Combine(folderPath, RelativeIconSubPath);
            string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);

            // 1. Ensure .ornare directory exists
            if (!Directory.Exists(ornareDir))
            {
                DirectoryInfo ornareDirInfo = Directory.CreateDirectory(ornareDir);
                ornareDirInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }

            // 2. Ensure .ornare\folder.ico exists
            if (!File.Exists(targetIcoPath))
            {
                if (!string.IsNullOrEmpty(canonicalIcoBackupPath) && File.Exists(canonicalIcoBackupPath))
                {
                    File.Copy(canonicalIcoBackupPath, targetIcoPath, overwrite: true);
                    File.SetAttributes(targetIcoPath, FileAttributes.Hidden | FileAttributes.System);
                }
                else
                {
                    return OperationResult.Failure(ErrorCode.IcoGenerationFailed,
                        "Cannot restore .ornare\\folder.ico: no backup or canonical ICO path available.");
                }
            }
            else
            {
                File.SetAttributes(targetIcoPath, FileAttributes.Hidden | FileAttributes.System);
            }

            // 3. Ensure desktop.ini is merged with relative icon path
            string currentIniContent = File.Exists(iniPath) ? DesktopIniParser.ReadFileWithEncodingFallback(iniPath) : string.Empty;
            string updatedIniContent = DesktopIniParser.MergeSemanticsForApply(currentIniContent, RelativeIconSubPath);

            if (File.Exists(iniPath))
            {
                File.SetAttributes(iniPath, FileAttributes.Normal);
            }
            File.WriteAllText(iniPath, updatedIniContent, System.Text.Encoding.Unicode);
            File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

            // 4. Ensure folder has ReadOnly attribute
            DirectoryInfo dirInfo = new(folderPath);
            if ((dirInfo.Attributes & FileAttributes.ReadOnly) == 0 && (dirInfo.Attributes & FileAttributes.System) == 0)
            {
                dirInfo.Attributes |= FileAttributes.ReadOnly;
            }

            // 5. Targeted shell refresh
            ShellRefreshService.NotifyFolderChanged(folderPath);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.FilesystemError, $"Failed to repair folder: {ex.Message}");
        }
    }
}
