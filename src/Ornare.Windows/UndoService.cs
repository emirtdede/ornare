using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Orchestrates safe, transactional Undo operations for folder customizations
/// triggered from File Explorer, CLI arguments, or URI protocol activations.
/// </summary>
public static class UndoService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TargetLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes an Undo operation on the target folder, restoring prior icon semantics
    /// or resetting to pre-Ornare state while respecting ownership epochs and external conflicts.
    /// </summary>
    public static async Task<OperationResult> UndoAsync(
        string folderPath,
        IUndoStorage storage,
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

        // 2. Synchronize concurrent operations on the same target folder
        var targetLock = TargetLocks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));
        await targetLock.WaitAsync(ct).ConfigureAwait(false);

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
            if (assignment == null)
            {
                return OperationResult.Failure(
                    ErrorCode.NoUndoAvailable,
                    $"No Ornare customization history found for folder '{canonicalPath}'.");
            }

            // 5. Inspect live desktop.ini semantics to guard against external modifications
            var liveSemantics = DesktopIniParser.ReadSemantics(canonicalPath);
            bool livePointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                                     liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

            string iniPath = Path.Combine(canonicalPath, OrnareConstants.DesktopIniFileName);
            if (!livePointsToOrnare && File.Exists(iniPath))
            {
                await storage.UpdateAssignmentHealthAsync(
                    canonicalPath,
                    FolderCustomizationState.ExternalIconConflict,
                    "Folder icon was modified by an external tool.").ConfigureAwait(false);

                return OperationResult.Failure(
                    ErrorCode.ExternalConflict,
                    "Folder icon was modified by an external tool. Undo cannot overwrite external changes without reapplication.");
            }

            // 6. If folder was renamed/moved on same volume, reconcile assignment path
            if (!string.Equals(assignment.FolderPath, canonicalPath, StringComparison.OrdinalIgnoreCase))
            {
                await storage.UpdateAssignmentPathAsync(assignment.FolderPath, canonicalPath).ConfigureAwait(false);
            }

            // 7. Retrieve prior baseline record
            FolderBaseline? baseline = null;
            if (!string.IsNullOrEmpty(assignment.Id))
            {
                baseline = await storage.GetLatestBaselineForAssignmentAsync(assignment.Id).ConfigureAwait(false);
            }

            if (baseline == null)
            {
                baseline = await storage.GetLatestBaselineAsync(canonicalPath).ConfigureAwait(false);
            }

            if (baseline == null)
            {
                return OperationResult.Failure(
                    ErrorCode.NoUndoAvailable,
                    $"No prior icon baseline exists to restore for folder '{canonicalPath}'.");
            }

            // 8. Durable operation journal entry before disk mutation
            var opId = Guid.NewGuid();
            var journal = new JournalEntry
            {
                OperationId = opId,
                Kind = OperationKind.Undo,
                TargetFolderPath = canonicalPath,
                State = JournalState.Prepared,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                PriorSemanticsJson = baseline.PriorSemantics.ToJson()
            };
            await storage.RecordJournalEntryAsync(journal).ConfigureAwait(false);
            await storage.UpdateJournalStateAsync(opId, JournalState.FilesystemStaged).ConfigureAwait(false);

            // 9. Execute filesystem undo/revert
            var undoResult = customizer.UndoIcon(
                canonicalPath,
                baseline,
                stage =>
                {
                    _ = storage.UpdateJournalStateAsync(opId, stage);
                });

            if (!undoResult.IsSuccess)
            {
                await storage.UpdateJournalStateAsync(opId, JournalState.NeedsRepair, undoResult.ErrorMessage).ConfigureAwait(false);
                return undoResult;
            }

            // 10. Update storage records post-revert
            bool hadPriorIcon = !string.IsNullOrEmpty(baseline.PriorSemantics.IconResource) ||
                                !string.IsNullOrEmpty(baseline.PriorSemantics.IconFile);

            if (!hadPriorIcon)
            {
                // Reverted to clean pre-Ornare state: remove assignment
                await storage.RemoveFolderAssignmentAsync(canonicalPath).ConfigureAwait(false);
            }
            else
            {
                // Reverted to previous Ornare icon: keep healthy
                await storage.UpdateAssignmentHealthAsync(canonicalPath, FolderCustomizationState.Healthy).ConfigureAwait(false);
            }

            // 11. Record history and mark journal complete
            await storage.RecordHistoryAsync(
                canonicalPath,
                opId,
                assignment.CurrentAssetHash,
                baseline.PriorSemantics.IconResource ?? string.Empty,
                "UndoIcon").ConfigureAwait(false);

            await storage.UpdateJournalStateAsync(opId, JournalState.Completed).ConfigureAwait(false);

            // 12. Targeted shell notification to refresh File Explorer icon
            ShellRefreshService.NotifyFolderChanged(canonicalPath);

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.FilesystemError, $"Undo failed due to an unexpected error: {ex.Message}");
        }
        finally
        {
            targetLock.Release();
        }
    }
}
