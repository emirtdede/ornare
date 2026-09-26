namespace Ornare.Core;

using System;
using System.Collections.Generic;

public sealed record StagedFolderItem
{
    public required string RawInputPath { get; init; }
    public required string CanonicalPath { get; init; }
    public required bool IsValid { get; init; }
    public string? RejectionReason { get; init; }
    public ErrorCode? ErrorCode { get; init; }
    public NtfsFolderIdentity? Identity { get; init; }
    public string? ExistingAssignmentId { get; init; }
    public FolderCustomizationState? CurrentState { get; init; }

    public string FolderName => Path.GetFileName(CanonicalPath.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : CanonicalPath;
    public bool IsAlreadyCustomized => !string.IsNullOrEmpty(ExistingAssignmentId);
}

public sealed record FolderStagingResult
{
    public required IReadOnlyList<StagedFolderItem> AcceptedItems { get; init; }
    public required IReadOnlyList<StagedFolderItem> RejectedItems { get; init; }
    public required int TotalRawCount { get; init; }
    public bool IsLimitExceeded { get; init; }
    public bool IsCancelled { get; init; }

    public int AcceptedCount => AcceptedItems.Count;
    public int RejectedCount => RejectedItems.Count;
}
