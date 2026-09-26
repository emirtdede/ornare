using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Orchestrates safe, transactional icon application to a target folder
/// with baseline preservation, journal tracking, library registration, and shell refresh (F24).
/// </summary>
public static class ApplyService
{
    public static async Task<OperationResult<ApplyOperationResult>> ApplyAsync(
        string folderPath,
        string stagedIcoPath,
        string contentHash,
        string? renderKey,
        RenderRecipe? recipe,
        IApplyStorage storage,
        IFolderCustomizer customizer,
        string sourceFormat = "ico",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedIcoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        // 1. Validate target
        var validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            return OperationResult.Failure<ApplyOperationResult>(validation.ErrorCode, validation.ErrorMessage ?? "Invalid target folder.");
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(folderPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<ApplyOperationResult>(ErrorCode.InvalidTarget, $"Failed to resolve canonical path: {ex.Message}");
        }

        if (!File.Exists(stagedIcoPath))
        {
            return OperationResult.Failure<ApplyOperationResult>(ErrorCode.IcoGenerationFailed, "Staged ICO file does not exist.");
        }

        // 2. Concurrency serialization: per-target lock
        using var lockHandle = await PerTargetSyncLock.TryAcquireAsync(canonicalPath, TimeSpan.FromSeconds(5), ct);
        if (lockHandle == null)
        {
            return OperationResult.Failure<ApplyOperationResult>(ErrorCode.FilesystemError, "Target folder is currently locked by another operation.");
        }

        // Re-validate inside lock
        if (!Directory.Exists(canonicalPath))
        {
            return OperationResult.Failure<ApplyOperationResult>(ErrorCode.TargetNotFound, $"Target folder does not exist: '{canonicalPath}'.");
        }

        // 3. Prepare durable apply transaction
        Guid operationId = Guid.NewGuid();
        JournalEntry journal = new()
        {
            OperationId = operationId,
            Kind = OperationKind.Apply,
            TargetFolderPath = canonicalPath,
            State = JournalState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TargetAssetPath = stagedIcoPath
        };

        await storage.RecordJournalEntryAsync(journal);
        await storage.UpdateJournalStateAsync(operationId, JournalState.AssetGenerated);

        // 4. Baseline capture: preserve prior baseline if exists, or capture prior semantics
        var existingBaseline = await storage.GetLatestBaselineAsync(canonicalPath);
        Guid epochId = Guid.NewGuid();
        FolderBaseline baseline;

        if (existingBaseline != null)
        {
            baseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = epochId,
                PriorSemantics = existingBaseline.PriorSemantics,
                CapturedAt = existingBaseline.CapturedAt
            };
        }
        else
        {
            FolderIconSemantics priorSemantics = DesktopIniParser.ReadSemantics(canonicalPath);
            baseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = epochId,
                PriorSemantics = priorSemantics,
                CapturedAt = DateTimeOffset.UtcNow
            };
        }

        // 5. Apply icon to folder
        var applyResult = customizer.ApplyIcon(canonicalPath, stagedIcoPath, state =>
        {
            _ = storage.UpdateJournalStateAsync(operationId, state);
        });

        if (!applyResult.IsSuccess || applyResult.Value == null)
        {
            await storage.UpdateJournalStateAsync(operationId, JournalState.NeedsRepair, applyResult.ErrorMessage);
            return OperationResult.Failure<ApplyOperationResult>(applyResult.ErrorCode, applyResult.ErrorMessage ?? "Failed to apply icon to folder.");
        }

        // 6. Post-commit: save assignment, library asset, render cache, history, NTFS identity, and complete journal
        await storage.SaveFolderAssignmentAsync(canonicalPath, epochId, applyResult.Value, baseline);

        string canonicalIcoPath = string.Empty;
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string libraryDir = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library");
            Directory.CreateDirectory(libraryDir);
            canonicalIcoPath = Path.Combine(libraryDir, $"{contentHash}.ico");
            if (!File.Exists(canonicalIcoPath))
            {
                File.Copy(stagedIcoPath, canonicalIcoPath, overwrite: true);
            }

            await storage.AddOrUpdateAssetAsync(contentHash, canonicalIcoPath, sourceFormat);

            if (!string.IsNullOrWhiteSpace(renderKey))
            {
                var existingCache = await storage.GetRenderCacheEntryAsync(renderKey);
                if (existingCache == null)
                {
                    var fi = new FileInfo(canonicalIcoPath);
                    await storage.SaveRenderCacheEntryAsync(new RenderCacheEntry
                    {
                        RenderKey = renderKey,
                        RecipeVersion = recipe?.RecipeVersion ?? 1,
                        RecipeJson = recipe?.ToJson() ?? string.Empty,
                        SourceAssetHash = contentHash,
                        CanonicalIcoPath = canonicalIcoPath,
                        FrameSizes = string.Join(",", recipe?.TargetFrameSizes ?? [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]),
                        FileSizeBytes = fi.Exists ? fi.Length : 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        LastUsedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            await storage.RecordHistoryAsync(canonicalPath, operationId, null, contentHash, "apply");

            var ntfsId = FolderIdentityService.GetNtfsIdentity(canonicalPath);
            if (ntfsId.HasValue)
            {
                await storage.UpdateAssignmentIdentityAsync(canonicalPath, ntfsId.Value.VolumeSerialNumber.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), ntfsId.Value.FileId64.ToString("X16", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                var (vol, fid) = FolderIdentityService.GetFolderIdentity(canonicalPath);
                await storage.UpdateAssignmentIdentityAsync(canonicalPath, vol, fid);
            }
        }
        catch
        {
            // Non-critical metadata / tracking failure
        }

        await storage.UpdateJournalStateAsync(operationId, JournalState.Completed);

        if (!string.IsNullOrWhiteSpace(renderKey))
        {
            try
            {
                await storage.SetLastAppliedRenderKeyAsync(renderKey);
            }
            catch { }
        }

        // Notify shell refresh
        ShellRefreshService.NotifyFolderChanged(canonicalPath);

        return OperationResult.Success(new ApplyOperationResult
        {
            OperationId = operationId,
            CanonicalIcoPath = !string.IsNullOrEmpty(canonicalIcoPath) ? canonicalIcoPath : stagedIcoPath,
            RenderKey = renderKey,
            ContentHash = contentHash
        });
    }
}
