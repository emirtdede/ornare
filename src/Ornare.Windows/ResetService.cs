using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Orchestrates safe, transactional Reset operations for folder customizations
/// triggered from File Explorer, CLI arguments, or URI protocol activations.
/// </summary>
public static class ResetService
{
    /// <summary>
    /// Executes a Reset operation on the target folder, safely reverting Ornare-owned icon semantics
    /// to the pre-customization baseline or Windows default without clobbering external customizations.
    /// </summary>
    public static async Task<OperationResult> ResetAsync(
        string folderPath,
        IResetStorage storage,
        IFolderCustomizer customizer,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        // 1. Validate target folder suitability and accessibility
        var validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(folderPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, $"Failed to resolve canonical path: {ex.Message}");
        }

        // 2. Synchronize concurrent operations on the same target folder. This must be the
        // same lock Apply/ApplyLast use, otherwise Apply and Reset can mutate one folder
        // concurrently (INV-CON-001).
        using var lockHandle = await PerTargetSyncLock
            .TryAcquireAsync(canonicalPath, TimeSpan.FromSeconds(5), ct)
            .ConfigureAwait(false);
        if (lockHandle is null)
        {
            return OperationResult.Failure(ErrorCode.OperationBusy, "Another operation is already running on this folder.");
        }

        try
        {
            // Re-validate existence inside lock
            if (!Directory.Exists(canonicalPath))
            {
                return OperationResult.Failure(ErrorCode.TargetNotFound, $"Target folder does not exist: '{canonicalPath}'.");
            }

            // 3. Resolve live NTFS identity to track moved/renamed folders
            var identity = FolderIdentityService.GetNtfsIdentity(canonicalPath);

            // 4. Locate active folder assignment in storage
            var assignment = await storage.FindAssignmentAsync(identity, canonicalPath, ct).ConfigureAwait(false);

            // If an assignment was found by path but physical identity does not match, target was replaced
            if (assignment != null && identity.HasValue && assignment.VolumeSerialNumber.HasValue && assignment.FileId64.HasValue)
            {
                if (assignment.VolumeSerialNumber.Value != identity.Value.VolumeSerialNumber ||
                    assignment.FileId64.Value != identity.Value.FileId64)
                {
                    return OperationResult.Failure(
                        ErrorCode.TargetChanged,
                        $"Target folder identity changed (expected Volume: {assignment.VolumeSerialNumber.Value:X8}, FileId: {assignment.FileId64.Value:X16}; got Volume: {identity.Value.VolumeSerialNumber:X8}, FileId: {identity.Value.FileId64:X16}). Reset aborted to prevent accidental mutation of a replaced directory.");
                }
            }

            // 5. Inspect live desktop.ini semantics to guard against external modifications and verify customization state
            var liveSemantics = DesktopIniParser.ReadSemantics(canonicalPath);
            bool livePointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                                     liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

            string iniPath = Path.Combine(canonicalPath, OrnareConstants.DesktopIniFileName);
            bool iniExists = File.Exists(iniPath);
            bool hasLiveCustomIcon = !string.IsNullOrEmpty(liveSemantics.IconResource) || !string.IsNullOrEmpty(liveSemantics.IconFile);

            // If no assignment exists and live semantics do not point to Ornare
            if (assignment == null && !livePointsToOrnare)
            {
                if (hasLiveCustomIcon)
                {
                    return OperationResult.Failure(
                        ErrorCode.ExternalConflict,
                        "Folder icon was modified by an external tool. Reset cannot overwrite external changes without reapplication.");
                }

                return OperationResult.Failure(
                    ErrorCode.NotCustomized,
                    $"Folder '{canonicalPath}' is not customized by Ornare.");
            }

            // If assignment exists but live semantics point to an external icon
            if (assignment != null && iniExists && !livePointsToOrnare)
            {
                await storage.UpdateAssignmentHealthAsync(
                    canonicalPath,
                    FolderCustomizationState.ExternalIconConflict,
                    "Folder icon was modified by an external tool.").ConfigureAwait(false);

                return OperationResult.Failure(
                    ErrorCode.ExternalConflict,
                    "Folder icon was modified by an external tool. Reset cannot overwrite external changes without reapplication.");
            }

            // 6. If folder was renamed/moved on same volume, reconcile assignment path
            if (assignment != null && !string.Equals(assignment.FolderPath, canonicalPath, StringComparison.OrdinalIgnoreCase))
            {
                await storage.UpdateAssignmentPathAsync(assignment.FolderPath, canonicalPath).ConfigureAwait(false);
            }

            // 7. Retrieve prior baseline record or construct a clean Windows default baseline
            FolderBaseline? baseline = null;
            if (assignment != null && !string.IsNullOrEmpty(assignment.Id))
            {
                baseline = await storage.GetLatestBaselineForAssignmentAsync(assignment.Id).ConfigureAwait(false);
            }

            if (baseline == null)
            {
                baseline = await storage.GetLatestBaselineAsync(canonicalPath).ConfigureAwait(false);
            }

            if (baseline == null)
            {
                baseline = new FolderBaseline
                {
                    FolderAssignmentId = assignment != null && Guid.TryParse(assignment.Id, out var aid) ? aid : Guid.NewGuid(),
                    OwnershipEpochId = Guid.NewGuid(),
                    PriorSemantics = new FolderIconSemantics
                    {
                        IconResource = null,
                        IconFile = null,
                        IconIndex = 0,
                        AttributesIntroduced = false
                    },
                    CapturedAt = DateTimeOffset.UtcNow
                };
            }

            // 8. Durable operation journal entry before disk mutation
            var opId = Guid.NewGuid();
            var journal = new JournalEntry
            {
                OperationId = opId,
                Kind = OperationKind.Reset,
                TargetFolderPath = canonicalPath,
                State = JournalState.Prepared,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                PriorSemanticsJson = baseline.PriorSemantics.ToJson()
            };
            await storage.RecordJournalEntryAsync(journal).ConfigureAwait(false);
            await storage.UpdateJournalStateAsync(opId, JournalState.FilesystemStaged).ConfigureAwait(false);

            // 9. Execute filesystem reset. Journal transitions are buffered and flushed in
            // order: fire-and-forget writes could land after the final Completed write and
            // regress a successfully reset folder (INV-TX-002).
            var journalStages = new ConcurrentQueue<JournalState>();
            var resetResult = customizer.ResetIcon(canonicalPath, baseline, journalStages.Enqueue);

            while (journalStages.TryDequeue(out JournalState stage))
            {
                await storage.UpdateJournalStateAsync(opId, stage).ConfigureAwait(false);
            }

            if (!resetResult.IsSuccess)
            {
                await storage.UpdateJournalStateAsync(opId, JournalState.NeedsRepair, resetResult.ErrorMessage).ConfigureAwait(false);
                return resetResult;
            }

            // 10. Clean up storage assignment, record history and complete the journal.
            // The filesystem is already reverted, so a metadata failure must be
            // repairable state, never a silent Completed (INV-FS-006, INV-TX-002).
            try
            {
                await storage.RemoveFolderAssignmentAsync(canonicalPath).ConfigureAwait(false);

                await storage.RecordHistoryAsync(
                    canonicalPath,
                    opId,
                    assignment?.CurrentAssetHash,
                    baseline.PriorSemantics.IconResource ?? string.Empty,
                    "ResetIcon").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Post-reset metadata persistence failed for '{canonicalPath}': {ex}");

                await storage.UpdateJournalStateAsync(
                    opId,
                    JournalState.NeedsRepair,
                    $"Folder reverted on disk, but metadata persistence failed: {ex.Message}").ConfigureAwait(false);

                return OperationResult.Failure(
                    ErrorCode.ReconciliationFailed,
                    "The folder was reset, but Ornare could not save its records. Use Repair to bring them back in sync.");
            }

            await storage.UpdateJournalStateAsync(opId, JournalState.Completed).ConfigureAwait(false);

            // 12. Targeted shell notification to refresh File Explorer icon
            ShellRefreshService.NotifyFolderChanged(canonicalPath);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.FilesystemError, $"Reset failed due to an unexpected error: {ex.Message}");
        }
    }
}
