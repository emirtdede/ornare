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

        // Explorer silently ignores a desktop.ini whose origin it cannot trust (Mark of the
        // Web), so a present file is not the same as a rendered icon. Reporting Healthy here
        // would be a false success; ConfigMismatch routes the user to Repair, which rewrites
        // the file and clears the marker.
        if (ShellTrustGuard.Diagnose(folderPath, iniPath) == FolderPresentationRisk.MarkOfTheWeb)
        {
            return FolderCustomizationState.ConfigMismatch;
        }

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

            // An Ornare-written icon can never legitimately be "downloaded from the internet".
            // Clearing the marker (the Unblock-File equivalent) prevents Explorer from refusing
            // to extract it after the June 2026 desktop.ini hardening.
            ShellTrustGuard.NormalizeOwnedFiles(folderPath, targetIcoPath);

            // 3. Merge desktop.ini atomically, preserving the file's original encoding, and
            //    clear any inherited Mark of the Web so Explorer will not ignore the result.
            OperationResult iniWrite = DesktopIniFileWriter.WriteMerged(
                iniPath,
                existing => DesktopIniParser.MergeSemanticsForApply(existing, RelativeIconSubPath));
            if (!iniWrite.IsSuccess)
            {
                return OperationResult.Failure<FolderIconSemantics>(
                    iniWrite.ErrorCode,
                    iniWrite.ErrorMessage ?? "desktop.ini could not be updated.",
                    iniWrite.DiagnosticDetail);
            }

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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Timestamp refresh is a Shell-invalidation hint, not part of the commit.
                // Record it so a persistent failure stays diagnosable instead of silent.
                System.Diagnostics.Trace.TraceWarning($"Folder timestamp refresh failed for '{folderPath}': {ex.Message}");
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
                    // Non-zero HRESULT is acceptable: desktop.ini is already committed, so the
                    // shell will still pick the icon up. Kept as a diagnostic hint only.
                    System.Diagnostics.Trace.TraceWarning(
                        $"SHGetSetFolderCustomSettings returned 0x{hr:X8} for '{folderPath}'.");
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // desktop.ini is already committed, so a failed native hook is not fatal;
                // it is still logged because it explains a stale Explorer icon.
                System.Diagnostics.Trace.TraceWarning($"SHGetSetFolderCustomSettings failed for '{folderPath}': {ex.Message}");
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

            // JournalState.Completed is deliberately NOT emitted here. The caller owns the
            // post-commit metadata writes (assignment, library asset, history, NTFS
            // identity) and must mark the journal complete only after those succeed.
            // Emitting Completed here would let a crash produce a "RolledBack" journal on
            // startup while the icon is already committed on disk (INV-FS-006).
            return OperationResult.Success(newSemantics);
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failure<FolderIconSemantics>(
                ErrorCode.AccessDenied,
                "Ornare does not have permission to change this folder's icon.",
                ex.ToString());
        }
        catch (IOException ex)
        {
            return OperationResult.Failure<FolderIconSemantics>(
                ErrorCode.FilesystemError,
                "The folder icon could not be written. The folder may be read-only or in use.",
                ex.ToString());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OperationResult.Failure<FolderIconSemantics>(
                ErrorCode.FilesystemError,
                "The folder icon could not be applied.",
                ex.ToString());
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

            // This fast path mutates the filesystem just like the general revert path, so
            // it must publish the same journal stages. Skipping them would leave a crash
            // between here and the caller's Completed write classified as "staged", which
            // startup reconciliation would wrongly roll back.
            onJournalStageChanged?.Invoke(JournalState.FilesystemCommitted);
            onJournalStageChanged?.Invoke(JournalState.ShellRefreshRequested);
            onJournalStageChanged?.Invoke(JournalState.MetadataCommitted);
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

            // Re-read live desktop.ini, merge baseline prior semantics, and commit atomically
            // while preserving the file's original encoding.
            OperationResult iniReset = DesktopIniFileWriter.WriteMergedOrDelete(
                iniPath,
                existing => DesktopIniParser.MergeSemanticsForReset(existing, baseline.PriorSemantics));
            if (!iniReset.IsSuccess)
            {
                return OperationResult.Failure(
                    iniReset.ErrorCode,
                    iniReset.ErrorMessage ?? "desktop.ini could not be restored.",
                    iniReset.DiagnosticDetail);
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Folder timestamp refresh failed for '{folderPath}': {ex.Message}");
            }

            onJournalStageChanged?.Invoke(JournalState.FilesystemCommitted);

            // Targeted Shell Refresh
            ShellRefreshService.NotifyFolderChanged(folderPath);
            onJournalStageChanged?.Invoke(JournalState.ShellRefreshRequested);

            onJournalStageChanged?.Invoke(JournalState.MetadataCommitted);

            // Completed is emitted by the calling service after its own metadata writes.
            return OperationResult.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failure(
                ErrorCode.AccessDenied,
                "Ornare does not have permission to restore this folder's icon.",
                ex.ToString());
        }
        catch (IOException ex)
        {
            return OperationResult.Failure(
                ErrorCode.FilesystemError,
                "The folder icon could not be restored. The folder may be read-only or in use.",
                ex.ToString());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OperationResult.Failure(
                ErrorCode.FilesystemError,
                "The folder icon could not be restored.",
                ex.ToString());
        }
    }
}
