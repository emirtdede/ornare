namespace Ornare.Windows;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

public static class FolderStagingService
{
    public const int MaxDropItems = 1000;
    public const int MaxValidationConcurrency = 4;

    public static async Task<FolderStagingResult> StageFoldersAsync(
        IReadOnlyList<string>? rawPaths,
        IFolderAssignmentLookup? assignmentLookup = null,
        CancellationToken ct = default)
    {
        if (rawPaths == null || rawPaths.Count == 0)
        {
            return new FolderStagingResult
            {
                AcceptedItems = Array.Empty<StagedFolderItem>(),
                RejectedItems = Array.Empty<StagedFolderItem>(),
                TotalRawCount = 0,
                IsLimitExceeded = false,
                IsCancelled = false
            };
        }

        // F20-DEC-05: Reject the entire drop before deep validation if raw count exceeds 1000
        if (rawPaths.Count > MaxDropItems)
        {
            var rejectedItems = rawPaths.Select(path => new StagedFolderItem
            {
                RawInputPath = path,
                CanonicalPath = path,
                IsValid = false,
                RejectionReason = "Drop limit exceeded (maximum 1000 items)",
                ErrorCode = ErrorCode.DropLimitExceeded
            }).ToList();

            return new FolderStagingResult
            {
                AcceptedItems = Array.Empty<StagedFolderItem>(),
                RejectedItems = rejectedItems,
                TotalRawCount = rawPaths.Count,
                IsLimitExceeded = true,
                IsCancelled = false
            };
        }

        var completedItems = new ConcurrentBag<StagedFolderItem>();
        bool isCancelled = false;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxValidationConcurrency,
            CancellationToken = ct
        };

        try
        {
            await Parallel.ForEachAsync(rawPaths, parallelOptions, async (rawPath, token) =>
            {
                token.ThrowIfCancellationRequested();
                var item = await ValidateItemAsync(rawPath, assignmentLookup, token);
                completedItems.Add(item);
            });
        }
        catch (OperationCanceledException)
        {
            isCancelled = true;
        }

        // Process duplicate coalescing
        var acceptedList = new List<StagedFolderItem>();
        var rejectedList = new List<StagedFolderItem>();

        var seenIdentities = new HashSet<NtfsFolderIdentity>();
        var seenAcceptedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRejectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sort by CanonicalPath for deterministic output
        var orderedItems = completedItems.OrderBy(i => i.CanonicalPath, StringComparer.OrdinalIgnoreCase);

        foreach (var item in orderedItems)
        {
            if (item.IsValid)
            {
                if (item.Identity.HasValue)
                {
                    if (seenIdentities.Add(item.Identity.Value))
                    {
                        seenAcceptedPaths.Add(item.CanonicalPath);
                        acceptedList.Add(item);
                    }
                }
                else
                {
                    if (seenAcceptedPaths.Add(item.CanonicalPath))
                    {
                        acceptedList.Add(item);
                    }
                }
            }
            else
            {
                if (seenRejectedPaths.Add(item.CanonicalPath))
                {
                    rejectedList.Add(item);
                }
            }
        }

        return new FolderStagingResult
        {
            AcceptedItems = acceptedList,
            RejectedItems = rejectedList,
            TotalRawCount = rawPaths.Count,
            IsLimitExceeded = false,
            IsCancelled = isCancelled
        };
    }

    private static async Task<StagedFolderItem> ValidateItemAsync(
        string rawPath,
        IFolderAssignmentLookup? assignmentLookup,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return new StagedFolderItem
            {
                RawInputPath = rawPath ?? string.Empty,
                CanonicalPath = string.Empty,
                IsValid = false,
                RejectionReason = "Path is empty.",
                ErrorCode = ErrorCode.InvalidTarget
            };
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(rawPath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new StagedFolderItem
            {
                RawInputPath = rawPath,
                CanonicalPath = rawPath,
                IsValid = false,
                RejectionReason = "The folder path is not valid.",
                ErrorCode = ErrorCode.InvalidTarget
            };
        }

        // Reject files (must be a directory)
        if (File.Exists(canonicalPath) && !Directory.Exists(canonicalPath))
        {
            return new StagedFolderItem
            {
                RawInputPath = rawPath,
                CanonicalPath = canonicalPath,
                IsValid = false,
                RejectionReason = "Item is a file, not a folder.",
                ErrorCode = ErrorCode.ItemIsFileNotFolder
            };
        }

        // Validate target against security and system invariants
        var validation = TargetFolderValidator.ValidateTarget(canonicalPath);
        if (!validation.IsSuccess)
        {
            return new StagedFolderItem
            {
                RawInputPath = rawPath,
                CanonicalPath = canonicalPath,
                IsValid = false,
                RejectionReason = validation.ErrorMessage ?? "Target validation failed.",
                ErrorCode = validation.ErrorCode
            };
        }

        // Target folder is valid. Extract stable NTFS identity.
        NtfsFolderIdentity? identity = null;
        try
        {
            identity = FolderIdentityService.GetNtfsIdentity(canonicalPath);
        }
        catch
        {
            // Graceful fallback for filesystems that do not expose NTFS file IDs
        }

        // Check if folder is already customized in the database
        string? existingAssignmentId = null;
        FolderCustomizationState? state = null;

        if (assignmentLookup != null)
        {
            try
            {
                var existingAssignment = await assignmentLookup.FindAssignmentAsync(identity, canonicalPath, ct);
                if (existingAssignment != null)
                {
                    existingAssignmentId = existingAssignment.Id;
                    state = existingAssignment.Status;
                }
            }
            catch
            {
                // Storage lookup failure must not abort staging
            }
        }

        return new StagedFolderItem
        {
            RawInputPath = rawPath,
            CanonicalPath = canonicalPath,
            IsValid = true,
            Identity = identity,
            ExistingAssignmentId = existingAssignmentId,
            CurrentState = state
        };
    }
}
