using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Shared operation planning and preflight validation engine for Ornare (F25).
/// Produces deterministic OperationPlan for dry-run analysis and real command Prepare phases
/// with zero filesystem, database, cache, or shell side-effects.
/// </summary>
public static class OperationPlanningService
{
    private static readonly string[] StandardIndeterminateConditions =
    [
        "Target folder lock status and concurrent filesystem modification before commit",
        "Target volume free space availability for icon artifact storage",
        "Windows File Explorer icon cache refresh latency and shell notification delivery"
    ];

    public static async Task<OperationPlan> PlanApplyAsync(
        string folderPath,
        string sourceFormat,
        string? contentHash,
        string? sourceMetadata,
        RenderRecipe recipe,
        IApplyStorage storage,
        IFolderCustomizer customizer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        var actions = new List<string>();
        var blockers = new List<PlanBlocker>();
        var indeterminate = new List<string>(StandardIndeterminateConditions);

        string target = folderPath ?? string.Empty;
        string? targetIdentity = null;
        string? cacheExpectation = null;
        string? expectedArtifact = null;

        // 1. Target folder validation
        var folderValidation = TargetFolderValidator.ValidateTarget(target);
        if (!folderValidation.IsSuccess)
        {
            blockers.Add(new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder."));
        }

        string? canonicalPath = null;
        if (folderValidation.IsSuccess)
        {
            try
            {
                canonicalPath = Path.GetFullPath(target);
                target = canonicalPath;

                if (!Directory.Exists(canonicalPath))
                {
                    blockers.Add(new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Target folder does not exist: '{canonicalPath}'."));
                }
                else if (!customizer.IsSupportedPath(canonicalPath))
                {
                    blockers.Add(new PlanBlocker(ErrorCode.InvalidTarget.ToString(), $"Target directory '{canonicalPath}' is not supported (e.g. volume root, reparse point, or UNC path)."));
                }
                else
                {
                    var id = FolderIdentityService.GetNtfsIdentity(canonicalPath);
                    if (id.HasValue)
                    {
                        targetIdentity = $"Volume: {id.Value.VolumeSerialNumber:X8}, FileId: {id.Value.FileId64:X16}";
                    }
                }
            }
            catch (Exception ex)
            {
                blockers.Add(new PlanBlocker(ErrorCode.InvalidTarget.ToString(), $"Failed to resolve target path: {ex.Message}"));
            }
        }

        if (canonicalPath != null)
        {
            expectedArtifact = Path.Combine(canonicalPath, OrnareConstants.CanonicalFolderName, OrnareConstants.CustomIconFileName);
        }

        // 2. Cache expectation lookup
        string recipeKey = recipe.ComputeRenderKey();
        try
        {
            var cacheEntry = await storage.GetRenderCacheEntryAsync(recipeKey);
            if (cacheEntry != null && !string.IsNullOrEmpty(cacheEntry.CanonicalIcoPath) && File.Exists(cacheEntry.CanonicalIcoPath))
            {
                cacheExpectation = "CacheHit";
            }
            else
            {
                cacheExpectation = "CacheMiss";
            }
        }
        catch
        {
            cacheExpectation = "Indeterminate";
        }

        // 3. Planned actions
        actions.Add("Validate target directory eligibility, permissions, and reparse-point status");
        actions.Add($"Resolve render recipe and source parameters (Format: {sourceFormat})");
        actions.Add(cacheExpectation == "CacheHit"
            ? "Retrieve pre-rendered multi-resolution ICO artifact from local render cache"
            : "Generate multi-resolution Windows ICO artifact across 10 standard shell sizes (16..256)");
        actions.Add("Record durable transaction in operation journal (Prepare -> Staged -> Committed -> Completed)");
        actions.Add("Write canonical icon file to folder .ornare directory with hidden and system attributes");
        actions.Add("Update desktop.ini with relative IconResource pointer and IconIndex=0");
        actions.Add("Apply directory System (+S) attribute for Windows shell desktop.ini processing");
        actions.Add("Notify Windows File Explorer via SHChangeNotify(SHCNE_UPDATEITEM)");
        actions.Add("Persist folder assignment and pre-customization baseline snapshot in SQLite storage");

        PlanStatus status = blockers.Count > 0 ? PlanStatus.WouldFail : PlanStatus.WouldSucceed;

        return new OperationPlan(
            Command: "apply",
            Target: target,
            Status: status,
            PlannedActions: actions,
            Blockers: blockers,
            IndeterminateConditions: indeterminate,
            TargetIdentity: targetIdentity,
            SourceMetadata: sourceMetadata ?? sourceFormat,
            CacheExpectation: cacheExpectation,
            ExpectedArtifact: expectedArtifact
        );
    }

    public static async Task<OperationPlan> PlanApplyLastAsync(
        string folderPath,
        IApplyLastStorage storage,
        IFolderCustomizer customizer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        var actions = new List<string>();
        var blockers = new List<PlanBlocker>();
        var indeterminate = new List<string>(StandardIndeterminateConditions);

        string target = folderPath ?? string.Empty;
        string? targetIdentity = null;
        string? cacheExpectation = null;
        string? expectedArtifact = null;
        string? sourceMetadata = null;

        // 1. Target folder validation
        var folderValidation = TargetFolderValidator.ValidateTarget(target);
        if (!folderValidation.IsSuccess)
        {
            blockers.Add(new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder."));
        }

        string? canonicalPath = null;
        if (folderValidation.IsSuccess)
        {
            try
            {
                canonicalPath = Path.GetFullPath(target);
                target = canonicalPath;

                if (!Directory.Exists(canonicalPath))
                {
                    blockers.Add(new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Target folder does not exist: '{canonicalPath}'."));
                }
                else if (!customizer.IsSupportedPath(canonicalPath))
                {
                    blockers.Add(new PlanBlocker(ErrorCode.InvalidTarget.ToString(), $"Target directory '{canonicalPath}' is not supported."));
                }
                else
                {
                    var id = FolderIdentityService.GetNtfsIdentity(canonicalPath);
                    if (id.HasValue)
                    {
                        targetIdentity = $"Volume: {id.Value.VolumeSerialNumber:X8}, FileId: {id.Value.FileId64:X16}";
                    }
                }
            }
            catch (Exception ex)
            {
                blockers.Add(new PlanBlocker(ErrorCode.InvalidTarget.ToString(), $"Failed to resolve target path: {ex.Message}"));
            }
        }

        if (canonicalPath != null)
        {
            expectedArtifact = Path.Combine(canonicalPath, OrnareConstants.CanonicalFolderName, OrnareConstants.CustomIconFileName);
        }

        // 2. Storage probe: LastAppliedRenderKey
        string? renderKey = null;
        try
        {
            renderKey = await storage.GetLastAppliedRenderKeyAsync();
            if (string.IsNullOrWhiteSpace(renderKey))
            {
                blockers.Add(new PlanBlocker(ErrorCode.NoLastAppliedRecipe.ToString(), "No previously applied icon recipe found."));
            }
            else
            {
                sourceMetadata = $"RenderKey: {renderKey}";
                var entry = await storage.GetRenderCacheEntryAsync(renderKey);
                if (entry == null)
                {
                    blockers.Add(new PlanBlocker(ErrorCode.NoLastAppliedRecipe.ToString(), $"Referenced recipe '{renderKey}' does not exist in cache."));
                }
                else
                {
                    bool icoExists = !string.IsNullOrEmpty(entry.CanonicalIcoPath) && File.Exists(entry.CanonicalIcoPath);
                    cacheExpectation = icoExists ? "CacheHit" : "CacheMissRebuildRequired";
                }
            }
        }
        catch (Exception ex)
        {
            blockers.Add(new PlanBlocker(ErrorCode.FilesystemError.ToString(), $"Storage read failure: {ex.Message}"));
        }

        // 3. Planned actions
        actions.Add("Validate target directory eligibility, permissions, and reparse-point status");
        actions.Add("Retrieve active LastAppliedRenderKey pointer from user settings");
        actions.Add(cacheExpectation == "CacheHit"
            ? "Retrieve cached canonical ICO from render cache"
            : "Rebuild canonical multi-resolution ICO artifact from retained recipe definition");
        actions.Add("Record durable transaction in operation journal (Prepare -> Staged -> Committed -> Completed)");
        actions.Add("Stage canonical icon to folder .ornare directory with hidden/system attributes");
        actions.Add("Update desktop.ini with relative IconResource pointer and apply System (+S) directory attribute");
        actions.Add("Notify Windows File Explorer via SHChangeNotify(SHCNE_UPDATEITEM)");
        actions.Add("Commit folder assignment and pre-customization baseline snapshot in SQLite storage");

        PlanStatus status = blockers.Count > 0 ? PlanStatus.WouldFail : PlanStatus.WouldSucceed;

        return new OperationPlan(
            Command: "apply-last",
            Target: target,
            Status: status,
            PlannedActions: actions,
            Blockers: blockers,
            IndeterminateConditions: indeterminate,
            TargetIdentity: targetIdentity,
            SourceMetadata: sourceMetadata,
            CacheExpectation: cacheExpectation,
            ExpectedArtifact: expectedArtifact
        );
    }

    public static async Task<OperationPlan> PlanResetAsync(
        string folderPath,
        IResetStorage storage,
        IFolderCustomizer customizer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        var actions = new List<string>();
        var blockers = new List<PlanBlocker>();
        var indeterminate = new List<string>
        {
            "Concurrent desktop.ini modification by external tools or user before commit",
            "Windows File Explorer shell notification delivery and icon cache refresh timing"
        };

        string target = folderPath ?? string.Empty;
        string? targetIdentity = null;

        var folderValidation = TargetFolderValidator.ValidateTarget(target);
        if (!folderValidation.IsSuccess)
        {
            blockers.Add(new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder."));
        }

        string? canonicalPath = null;
        if (folderValidation.IsSuccess)
        {
            try
            {
                canonicalPath = Path.GetFullPath(target);
                target = canonicalPath;

                if (!Directory.Exists(canonicalPath))
                {
                    blockers.Add(new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Target folder does not exist: '{canonicalPath}'."));
                }
                else
                {
                    var identity = FolderIdentityService.GetNtfsIdentity(canonicalPath);
                    if (identity.HasValue)
                    {
                        targetIdentity = $"Volume: {identity.Value.VolumeSerialNumber:X8}, FileId: {identity.Value.FileId64:X16}";
                    }

                    var assignment = await storage.FindAssignmentAsync(identity, canonicalPath, ct);

                    // Check physical target replacement
                    if (assignment != null && identity.HasValue && assignment.VolumeSerialNumber.HasValue && assignment.FileId64.HasValue)
                    {
                        if (assignment.VolumeSerialNumber.Value != identity.Value.VolumeSerialNumber ||
                            assignment.FileId64.Value != identity.Value.FileId64)
                        {
                            blockers.Add(new PlanBlocker(ErrorCode.TargetChanged.ToString(), "Target folder physical identity does not match recorded assignment."));
                        }
                    }

                    var liveSemantics = DesktopIniParser.ReadSemantics(canonicalPath);
                    bool livePointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                                             liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

                    string iniPath = Path.Combine(canonicalPath, OrnareConstants.DesktopIniFileName);
                    bool hasLiveCustomIcon = !string.IsNullOrEmpty(liveSemantics.IconResource) || !string.IsNullOrEmpty(liveSemantics.IconFile);

                    if (assignment == null && !livePointsToOrnare)
                    {
                        if (hasLiveCustomIcon)
                        {
                            blockers.Add(new PlanBlocker(ErrorCode.ExternalConflict.ToString(), "Folder icon was modified by an external tool. Reset cannot overwrite external changes without reapplication."));
                        }
                        else
                        {
                            blockers.Add(new PlanBlocker(ErrorCode.NotCustomized.ToString(), $"Folder '{canonicalPath}' is not customized by Ornare."));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                blockers.Add(new PlanBlocker(ErrorCode.InvalidTarget.ToString(), $"Failed to resolve target path: {ex.Message}"));
            }
        }

        actions.Add("Inspect target folder desktop.ini semantics and active assignment");
        actions.Add("Revert desktop.ini icon semantics to pre-customization baseline or Windows default");
        actions.Add("Remove Ornare canonical icon directory and files (.ornare)");
        actions.Add("Remove folder assignment and baseline records from SQLite storage");
        actions.Add("Notify Windows File Explorer via SHChangeNotify(SHCNE_UPDATEITEM)");

        PlanStatus status = blockers.Count > 0 ? PlanStatus.WouldFail : PlanStatus.WouldSucceed;

        return new OperationPlan(
            Command: "reset",
            Target: target,
            Status: status,
            PlannedActions: actions,
            Blockers: blockers,
            IndeterminateConditions: indeterminate,
            TargetIdentity: targetIdentity
        );
    }

    public static async Task<OperationPlan> PlanUndoAsync(
        string folderPath,
        IUndoStorage storage,
        IFolderCustomizer customizer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        var actions = new List<string>();
        var blockers = new List<PlanBlocker>();
        var indeterminate = new List<string>
        {
            "Concurrent desktop.ini modification before commit",
            "Windows File Explorer icon cache refresh timing"
        };

        string target = folderPath ?? string.Empty;
        string? targetIdentity = null;

        var folderValidation = TargetFolderValidator.ValidateTarget(target);
        if (!folderValidation.IsSuccess)
        {
            blockers.Add(new PlanBlocker(folderValidation.ErrorCode.ToString(), folderValidation.ErrorMessage ?? "Invalid target folder."));
        }

        string? canonicalPath = null;
        if (folderValidation.IsSuccess)
        {
            try
            {
                canonicalPath = Path.GetFullPath(target);
                target = canonicalPath;

                if (!Directory.Exists(canonicalPath))
                {
                    blockers.Add(new PlanBlocker(ErrorCode.TargetNotFound.ToString(), $"Target folder does not exist: '{canonicalPath}'."));
                }
                else
                {
                    var identity = FolderIdentityService.GetNtfsIdentity(canonicalPath);
                    if (identity.HasValue)
                    {
                        targetIdentity = $"Volume: {identity.Value.VolumeSerialNumber:X8}, FileId: {identity.Value.FileId64:X16}";
                    }

                    var assignment = await storage.FindAssignmentAsync(identity, canonicalPath, ct);
                    if (assignment == null)
                    {
                        blockers.Add(new PlanBlocker(ErrorCode.NoUndoAvailable.ToString(), $"No Ornare customization history found for folder '{canonicalPath}'."));
                    }
                    else
                    {
                        var liveSemantics = DesktopIniParser.ReadSemantics(canonicalPath);
                        bool livePointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                                                 liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

                        string iniPath = Path.Combine(canonicalPath, OrnareConstants.DesktopIniFileName);
                        if (!livePointsToOrnare && File.Exists(iniPath))
                        {
                            blockers.Add(new PlanBlocker(ErrorCode.ExternalConflict.ToString(), "Folder icon was modified by an external tool. Undo cannot overwrite external changes without reapplication."));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                blockers.Add(new PlanBlocker(ErrorCode.InvalidTarget.ToString(), $"Failed to resolve target path: {ex.Message}"));
            }
        }

        actions.Add("Inspect active folder assignment and verify Ornare ownership");
        actions.Add("Retrieve prior folder baseline snapshot from SQLite storage");
        actions.Add("Merge pre-customization icon semantics into desktop.ini without clobbering external sections");
        actions.Add("Remove or reconcile folder assignment in SQLite storage");
        actions.Add("Notify Windows File Explorer via SHChangeNotify(SHCNE_UPDATEITEM)");

        PlanStatus status = blockers.Count > 0 ? PlanStatus.WouldFail : PlanStatus.WouldSucceed;

        return new OperationPlan(
            Command: "undo",
            Target: target,
            Status: status,
            PlannedActions: actions,
            Blockers: blockers,
            IndeterminateConditions: indeterminate,
            TargetIdentity: targetIdentity
        );
    }

    public static async Task<OperationPlan> PlanExportIcoAsync(
        string outputPath,
        string sourceDescription,
        RenderRecipe recipe,
        IApplyLastStorage? storage = null,
        CancellationToken ct = default)
    {
        var actions = new List<string>();
        var blockers = new List<PlanBlocker>();
        var indeterminate = new List<string>
        {
            "Destination volume free space availability",
            "Destination file write lock or access collision at commit time"
        };

        string target = outputPath ?? string.Empty;
        string? cacheExpectation = null;
        long? expectedSizeBytes = null;

        if (string.IsNullOrWhiteSpace(target))
        {
            blockers.Add(new PlanBlocker("InvalidArguments", "Output path cannot be empty."));
        }
        else
        {
            try
            {
                string fullPath = Path.GetFullPath(target);
                target = fullPath;

                string ext = Path.GetExtension(fullPath);
                if (!string.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    blockers.Add(new PlanBlocker("InvalidArguments", $"Invalid export file extension '{ext}'. Must be '.ico'."));
                }

                string? outDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(outDir) && Directory.Exists(outDir))
                {
                    var dirInfo = new DirectoryInfo(outDir);
                    if ((dirInfo.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        blockers.Add(new PlanBlocker(ErrorCode.AccessDenied.ToString(), $"Target directory '{outDir}' is marked read-only."));
                    }
                }
            }
            catch (Exception ex)
            {
                blockers.Add(new PlanBlocker("InvalidArguments", $"Invalid output path: {ex.Message}"));
            }
        }

        if (storage != null)
        {
            string recipeKey = recipe.ComputeRenderKey();
            try
            {
                var cacheEntry = await storage.GetRenderCacheEntryAsync(recipeKey);
                if (cacheEntry != null && cacheEntry.FileSizeBytes > 0)
                {
                    cacheExpectation = "CacheHit";
                    expectedSizeBytes = cacheEntry.FileSizeBytes;
                }
                else
                {
                    cacheExpectation = "CacheMiss";
                }
            }
            catch
            {
                cacheExpectation = "Indeterminate";
            }
        }

        actions.Add("Validate render recipe and source parameters");
        actions.Add("Render multi-resolution frames (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)");
        actions.Add($"Write atomic multi-frame ICO file to '{target}'");

        PlanStatus status = blockers.Count > 0 ? PlanStatus.WouldFail : PlanStatus.WouldSucceed;

        return new OperationPlan(
            Command: "export-ico",
            Target: target,
            Status: status,
            PlannedActions: actions,
            Blockers: blockers,
            IndeterminateConditions: indeterminate,
            SourceMetadata: sourceDescription,
            CacheExpectation: cacheExpectation,
            ExpectedArtifact: target,
            ExpectedArtifactSizeBytes: expectedSizeBytes
        );
    }

    public static Task<OperationPlan> PlanExportPngAsync(
        string outputPath,
        int size,
        string sourceDescription,
        RenderRecipe recipe,
        CancellationToken ct = default)
    {
        var actions = new List<string>();
        var blockers = new List<PlanBlocker>();
        var indeterminate = new List<string>
        {
            "Destination volume free space availability",
            "Destination file write lock or access collision at commit time"
        };

        string target = outputPath ?? string.Empty;

        if (string.IsNullOrWhiteSpace(target))
        {
            blockers.Add(new PlanBlocker("InvalidArguments", "Output path cannot be empty."));
        }
        else
        {
            try
            {
                string fullPath = Path.GetFullPath(target);
                target = fullPath;

                string ext = Path.GetExtension(fullPath);
                if (!string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
                {
                    blockers.Add(new PlanBlocker("InvalidArguments", $"Invalid export file extension '{ext}'. Must be '.png'."));
                }

                string? outDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(outDir) && Directory.Exists(outDir))
                {
                    var dirInfo = new DirectoryInfo(outDir);
                    if ((dirInfo.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        blockers.Add(new PlanBlocker(ErrorCode.AccessDenied.ToString(), $"Target directory '{outDir}' is marked read-only."));
                    }
                }
            }
            catch (Exception ex)
            {
                blockers.Add(new PlanBlocker("InvalidArguments", $"Invalid output path: {ex.Message}"));
            }
        }

        if (size != 256 && size != 512 && size != 1024)
        {
            blockers.Add(new PlanBlocker("InvalidArguments", $"Invalid PNG export size '{size}'. Allowed sizes are 256, 512, 1024."));
        }

        actions.Add("Validate render recipe and source parameters");
        actions.Add($"Render high-resolution PNG ({size}x{size})");
        actions.Add($"Write atomic PNG file to '{target}'");

        PlanStatus status = blockers.Count > 0 ? PlanStatus.WouldFail : PlanStatus.WouldSucceed;

        var plan = new OperationPlan(
            Command: "export-png",
            Target: target,
            Status: status,
            PlannedActions: actions,
            Blockers: blockers,
            IndeterminateConditions: indeterminate,
            SourceMetadata: $"{sourceDescription} [Size: {size}x{size}]",
            CacheExpectation: "Bypass",
            ExpectedArtifact: target
        );

        return Task.FromResult(plan);
    }
}
