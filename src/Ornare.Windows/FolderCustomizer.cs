using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;

namespace Ornare.Windows;

public sealed class FolderCustomizer : IFolderCustomizer
{
    private const string RelativeIconSubPath = @".ornare\folder.ico";

    public bool IsSupportedPath(string folderPath)
    {
        OperationResult result = TargetFolderValidator.ValidateTarget(folderPath);
        return result.IsSuccess;
    }

    public FolderCustomizationState GetFolderState(string folderPath, FolderBaseline? baseline)
    {
        if (!IsSupportedPath(folderPath))
        {
            return FolderCustomizationState.UnsupportedLocation;
        }

        string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
        string ownedIcoPath = Path.Combine(folderPath, RelativeIconSubPath);

        if (!File.Exists(iniPath))
        {
            return baseline != null ? FolderCustomizationState.MissingTarget : FolderCustomizationState.Healthy;
        }

        FolderIconSemantics liveSemantics = DesktopIniParser.ReadSemantics(folderPath);

        // Check for external icon conflict
        if (baseline != null)
        {
            bool livePointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                                     liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

            if (!livePointsToOrnare)
            {
                return FolderCustomizationState.ExternalIconConflict;
            }

            if (!File.Exists(ownedIcoPath))
            {
                return FolderCustomizationState.MissingOwnedIcon;
            }
        }

        return FolderCustomizationState.Healthy;
    }

    public OperationResult<FolderIconSemantics> ApplyIcon(
        string folderPath,
        string stagedIcoPath,
        Action<JournalState>? onJournalStageChanged = null)
    {
        OperationResult validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            return OperationResult.Failure<FolderIconSemantics>(validation.ErrorCode, validation.ErrorMessage ?? "Invalid target");
        }

        if (!File.Exists(stagedIcoPath))
        {
            return OperationResult.Failure<FolderIconSemantics>(
                ErrorCode.IcoGenerationFailed, "Staged ICO file does not exist.");
        }

        string ornareDir = Path.Combine(folderPath, OrnareConstants.CanonicalFolderName);
        string targetIcoPath = Path.Combine(folderPath, RelativeIconSubPath);
        string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);

        try
        {
            // 1. Capture baseline / current semantics before mutation
            FolderIconSemantics priorSemantics = DesktopIniParser.ReadSemantics(folderPath);

            // Check if folder already had ReadOnly attribute
            DirectoryInfo dirInfo = new(folderPath);
            bool folderAlreadyReadOnly = (dirInfo.Attributes & FileAttributes.ReadOnly) != 0;

            onJournalStageChanged?.Invoke(JournalState.FilesystemStaged);

            // 2. Stage destination directory
            if (!Directory.Exists(ornareDir))
            {
                DirectoryInfo ornareDirInfo = Directory.CreateDirectory(ornareDir);
                ornareDirInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }

            // Backup existing ICO if present for Undo support
            string prevIcoPath = Path.Combine(ornareDir, "folder.prev.ico");
            if (File.Exists(targetIcoPath))
            {
                File.SetAttributes(targetIcoPath, FileAttributes.Normal);
                if (File.Exists(prevIcoPath))
                {
                    File.SetAttributes(prevIcoPath, FileAttributes.Normal);
                }
                File.Copy(targetIcoPath, prevIcoPath, overwrite: true);
                File.SetAttributes(prevIcoPath, FileAttributes.Hidden | FileAttributes.System);
            }
            File.Copy(stagedIcoPath, targetIcoPath, overwrite: true);
            File.SetAttributes(targetIcoPath, FileAttributes.Hidden | FileAttributes.System);

            // 3. Merge desktop.ini
            string currentIniContent = File.Exists(iniPath) ? DesktopIniParser.ReadFileWithEncodingFallback(iniPath) : string.Empty;
            string updatedIniContent = DesktopIniParser.MergeSemanticsForApply(currentIniContent, RelativeIconSubPath);

            if (File.Exists(iniPath))
            {
                File.SetAttributes(iniPath, FileAttributes.Normal);
            }
            File.WriteAllText(iniPath, updatedIniContent, System.Text.Encoding.Unicode);
            File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

            // 4. Ensure folder has ReadOnly attribute (Windows requirement for custom folder icons)
            if (!folderAlreadyReadOnly)
            {
                dirInfo.Attributes |= FileAttributes.ReadOnly;
            }

            // Always touch timestamps and ensure ReadOnly attribute is asserted to trigger Shell invalidation
            try
            {
                File.SetAttributes(folderPath, File.GetAttributes(folderPath) | FileAttributes.ReadOnly);
                Directory.SetLastWriteTimeUtc(folderPath, DateTime.UtcNow);
                if (File.Exists(iniPath))
                {
                    File.SetLastWriteTimeUtc(iniPath, DateTime.UtcNow);
                }
                if (File.Exists(targetIcoPath))
                {
                    File.SetLastWriteTimeUtc(targetIcoPath, DateTime.UtcNow);
                }
            }
            catch
            {
                // Non-critical timestamp update failure
            }

            // 4b. Invoke native shell folder settings hook
            try
            {
                ShellRefreshService.SHFOLDERCUSTOMSETTINGS fcs = new()
                {
                    dwSize = (uint)Marshal.SizeOf<ShellRefreshService.SHFOLDERCUSTOMSETTINGS>(),
                    dwMask = ShellRefreshService.FcsmIconFile,
                    pszIconFile = RelativeIconSubPath,
                    iIconIndex = 0
                };
                int hr = ShellRefreshService.SHGetSetFolderCustomSettings(ref fcs, folderPath, ShellRefreshService.FcsForceWrite);
                if (hr != 0)
                {
                    // Non-zero HRESULT is acceptable as desktop.ini was already committed
                }
            }
            catch
            {
                // Non-critical native hook invocation
            }

            onJournalStageChanged?.Invoke(JournalState.FilesystemCommitted);

            // 5. Targeted Shell Refresh
            ShellRefreshService.NotifyFolderChanged(folderPath);
            onJournalStageChanged?.Invoke(JournalState.ShellRefreshRequested);

            FolderIconSemantics newSemantics = new()
            {
                IconResource = $"{RelativeIconSubPath},0",
                AttributesIntroduced = !folderAlreadyReadOnly,
                RawUnrelatedContent = priorSemantics.RawUnrelatedContent
            };

            onJournalStageChanged?.Invoke(JournalState.MetadataCommitted);
            onJournalStageChanged?.Invoke(JournalState.Completed);

            return OperationResult.Success(newSemantics);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<FolderIconSemantics>(
                ErrorCode.FilesystemError, $"Failed to customize folder: {ex.Message}");
        }
    }

    public OperationResult ResetIcon(
        string folderPath,
        FolderBaseline baseline,
        Action<JournalState>? onJournalStageChanged = null)
    {
        return RevertIconInternal(folderPath, baseline, onJournalStageChanged);
    }

    public OperationResult UndoIcon(
        string folderPath,
        FolderBaseline baseline,
        Action<JournalState>? onJournalStageChanged = null)
    {
        string prevIcoPath = Path.Combine(folderPath, OrnareConstants.CanonicalFolderName, "folder.prev.ico");
        string targetIcoPath = Path.Combine(folderPath, RelativeIconSubPath);

        // If prior semantics in baseline was an Ornare icon AND folder.prev.ico exists, restore it!
        if (File.Exists(prevIcoPath) && baseline.PriorSemantics.IconResource != null &&
            baseline.PriorSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(targetIcoPath))
            {
                File.SetAttributes(targetIcoPath, FileAttributes.Normal);
            }
            File.SetAttributes(prevIcoPath, FileAttributes.Normal);
            File.Move(prevIcoPath, targetIcoPath, overwrite: true);
            File.SetAttributes(targetIcoPath, FileAttributes.Hidden | FileAttributes.System);

            ShellRefreshService.NotifyFolderChanged(folderPath);
            return OperationResult.Success();
        }

        return RevertIconInternal(folderPath, baseline, onJournalStageChanged);
    }

    private static OperationResult RevertIconInternal(
        string folderPath,
        FolderBaseline baseline,
        Action<JournalState>? onJournalStageChanged)
    {
        OperationResult validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
        string targetIcoPath = Path.Combine(folderPath, RelativeIconSubPath);
        string ornareDir = Path.Combine(folderPath, OrnareConstants.CanonicalFolderName);

        try
        {
            // Verify no external conflict has occurred
            FolderIconSemantics liveSemantics = DesktopIniParser.ReadSemantics(folderPath);
            bool livePointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                                     liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

            if (!livePointsToOrnare && File.Exists(iniPath))
            {
                return OperationResult.Failure(
                    ErrorCode.ExternalConflict,
                    "Folder icon was modified by an external tool. Reset cannot overwrite external changes without reapplication.");
            }

            onJournalStageChanged?.Invoke(JournalState.FilesystemStaged);

            // Re-read live desktop.ini and merge baseline prior semantics
            string currentIniContent = File.Exists(iniPath) ? DesktopIniParser.ReadFileWithEncodingFallback(iniPath) : string.Empty;
            string restoredIniContent = DesktopIniParser.MergeSemanticsForReset(currentIniContent, baseline.PriorSemantics);

            bool hasRemainingContent = !string.IsNullOrWhiteSpace(restoredIniContent.Trim());

            if (hasRemainingContent)
            {
                if (File.Exists(iniPath))
                {
                    File.SetAttributes(iniPath, FileAttributes.Normal);
                }
                File.WriteAllText(iniPath, restoredIniContent, System.Text.Encoding.Unicode);
                File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
            }
            else if (File.Exists(iniPath))
            {
                File.SetAttributes(iniPath, FileAttributes.Normal);
                File.Delete(iniPath);
            }

            // Remove Ornare-owned icon files
            string prevIcoPath = Path.Combine(ornareDir, "folder.prev.ico");
            if (File.Exists(prevIcoPath))
            {
                File.SetAttributes(prevIcoPath, FileAttributes.Normal);
                File.Delete(prevIcoPath);
            }
            if (File.Exists(targetIcoPath))
            {
                File.SetAttributes(targetIcoPath, FileAttributes.Normal);
                File.Delete(targetIcoPath);
            }

            // Remove .ornare directory if empty
            if (Directory.Exists(ornareDir) && Directory.GetFileSystemEntries(ornareDir).Length == 0)
            {
                Directory.Delete(ornareDir, recursive: false);
            }

            // Revert folder ReadOnly attribute only if Ornare introduced it and no other desktop.ini remains
            if (baseline.PriorSemantics.AttributesIntroduced && !File.Exists(iniPath))
            {
                DirectoryInfo dirInfo = new(folderPath);
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }

            try
            {
                Directory.SetLastWriteTimeUtc(folderPath, DateTime.UtcNow);
            }
            catch
            {
                // Non-critical timestamp update failure
            }

            onJournalStageChanged?.Invoke(JournalState.FilesystemCommitted);

            // Targeted Shell Refresh
            ShellRefreshService.NotifyFolderChanged(folderPath);
            onJournalStageChanged?.Invoke(JournalState.ShellRefreshRequested);

            onJournalStageChanged?.Invoke(JournalState.MetadataCommitted);
            onJournalStageChanged?.Invoke(JournalState.Completed);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(
                ErrorCode.FilesystemError, $"Failed to revert folder icon: {ex.Message}");
        }
    }
}
