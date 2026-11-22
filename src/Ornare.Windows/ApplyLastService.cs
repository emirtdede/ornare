using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Orchestrates applying the most recently durably committed icon render recipe
/// to an eligible folder without loading Storage or Imaging code inside File Explorer (F21).
/// </summary>
public static class ApplyLastService
{
    public static async Task<OperationResult> ApplyLastAsync(
        string folderPath,
        IApplyLastStorage storage,
        IFolderCustomizer customizer,
        Func<RenderCacheEntry, Task<string?>>? rebuildIcoFunc = null)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, "Target folder path cannot be empty.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(folderPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, $"Invalid path format: {ex.Message}");
        }

        if (File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            return OperationResult.Failure(ErrorCode.ItemIsFileNotFolder, "Target path is a file, not a directory.");
        }

        if (!Directory.Exists(fullPath))
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, $"Target directory '{fullPath}' does not exist.");
        }

        if (!customizer.IsSupportedPath(fullPath))
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, $"Target directory '{fullPath}' is not supported (e.g. root, UNC, or reparse point).");
        }

        // Concurrency serialization: rapid duplicate activations on the same folder are serialized safely
        using var lockHandle = await PerTargetSyncLock.TryAcquireAsync(fullPath, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            return OperationResult.Failure(ErrorCode.OperationBusy, "Another operation is already running on this folder.");
        }

        // 1. Resolve LastAppliedRenderKey
        string? renderKey = await storage.GetLastAppliedRenderKeyAsync();
        if (string.IsNullOrWhiteSpace(renderKey))
        {
            return OperationResult.Failure(ErrorCode.NoLastAppliedRecipe, "No previously applied icon recipe found.");
        }

        // 2. Lookup render cache entry
        RenderCacheEntry? entry = await storage.GetRenderCacheEntryAsync(renderKey);
        if (entry == null)
        {
            // Corrupt or dangling pointer: transactionally clear pointer and return typed error
            await storage.ClearLastAppliedRenderKeyAsync();
            return OperationResult.Failure(ErrorCode.NoLastAppliedRecipe, $"Referenced recipe '{renderKey}' does not exist in cache. Pointer cleared.");
        }

        // 3. Resolve valid canonical ICO
        string? icoPath = entry.CanonicalIcoPath;
        bool isIcoValid = !string.IsNullOrWhiteSpace(icoPath) && File.Exists(icoPath);
        if (isIcoValid)
        {
            try
            {
                var fi = new FileInfo(icoPath!);
                if (fi.Length < 22)
                {
                    isIcoValid = false;
                }
            }
            catch
            {
                isIcoValid = false;
            }
        }

        if (!isIcoValid)
        {
            // Derived ICO is missing or invalid from disk; attempt to rebuild from retained recipe
            if (rebuildIcoFunc != null)
            {
                try
                {
                    string? rebuiltPath = await rebuildIcoFunc(entry);
                    if (!string.IsNullOrWhiteSpace(rebuiltPath) && File.Exists(rebuiltPath))
                    {
                        icoPath = rebuiltPath;
                        isIcoValid = true;
                    }
                }
                catch
                {
                    isIcoValid = false;
                }
            }

            if (!isIcoValid)
            {
                // Missing and cannot be rebuilt: transactionally clear pointer and return typed error
                await storage.ClearLastAppliedRenderKeyAsync();
                return OperationResult.Failure(ErrorCode.NoLastAppliedRecipe, "Canonical icon file is missing and could not be rebuilt from retained recipe. Pointer cleared.");
            }
        }

        // 4. Prepare durable apply transaction
        Guid operationId = Guid.NewGuid();
        JournalEntry journal = new()
        {
            OperationId = operationId,
            Kind = OperationKind.Apply,
            TargetFolderPath = fullPath,
            State = JournalState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TargetAssetPath = icoPath
        };

        await storage.RecordJournalEntryAsync(journal);
        await storage.UpdateJournalStateAsync(operationId, JournalState.AssetGenerated);

        // 5. Baseline capture
        FolderIconSemantics priorSemantics = DesktopIniParser.ReadSemantics(fullPath);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = priorSemantics,
            CapturedAt = DateTimeOffset.UtcNow
        };

        // 6. Apply icon to folder. Buffer journal transitions and flush them in order so
        // a late fire-and-forget write cannot land after the final Completed write.
        var journalStages = new System.Collections.Concurrent.ConcurrentQueue<JournalState>();
        var applyResult = customizer.ApplyIcon(fullPath, icoPath!, journalStages.Enqueue);

        while (journalStages.TryDequeue(out JournalState stage))
        {
            await storage.UpdateJournalStateAsync(operationId, stage);
        }

        if (!applyResult.IsSuccess || applyResult.Value == null)
        {
            await storage.UpdateJournalStateAsync(operationId, JournalState.NeedsRepair, applyResult.ErrorMessage);
            return OperationResult.Failure(applyResult.ErrorCode, applyResult.ErrorMessage ?? "Failed to apply icon to folder.");
        }

        // 7. Post-commit: save assignment, history, NTFS identity, and complete journal.
        // A metadata failure after the icon is on disk must become NeedsRepair, never Completed.
        try
        {
            await storage.SaveFolderAssignmentAsync(fullPath, epochId, applyResult.Value, baseline);
            await storage.RecordHistoryAsync(fullPath, operationId, null, entry.SourceAssetHash, "apply-last");

            var ntfsId = FolderIdentityService.GetNtfsIdentity(fullPath);
            if (ntfsId.HasValue)
            {
                await storage.UpdateAssignmentIdentityAsync(fullPath, ntfsId.Value.VolumeSerialNumber.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), ntfsId.Value.FileId64.ToString("X16", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                var (vol, fid) = FolderIdentityService.GetFolderIdentity(fullPath);
                await storage.UpdateAssignmentIdentityAsync(fullPath, vol, fid);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Post-apply-last metadata persistence failed for '{fullPath}': {ex}");

            await storage.UpdateJournalStateAsync(
                operationId,
                JournalState.NeedsRepair,
                $"Icon applied to disk, but metadata persistence failed: {ex.Message}");

            return OperationResult.Failure(
                ErrorCode.ReconciliationFailed,
                "The folder icon was applied, but Ornare could not save its records. Use Repair to bring them back in sync.");
        }

        await storage.UpdateJournalStateAsync(operationId, JournalState.Completed);

        // Update LastAppliedRenderKey pointer post-commit
        await storage.SetLastAppliedRenderKeyAsync(renderKey);

        // Notify shell refresh
        ShellRefreshService.NotifyFolderChanged(fullPath);

        return OperationResult.Success();
    }
}
