namespace Ornare.Core;

public enum JournalState
{
    Prepared = 0,
    AssetGenerated = 1,
    FilesystemStaged = 2,
    FilesystemCommitted = 3,
    ShellRefreshRequested = 4,
    MetadataCommitted = 5,
    Completed = 6,
    Aborted = 7,
    RolledBack = 8,
    NeedsRepair = 9
}

public enum OperationKind
{
    Apply = 0,
    Reset = 1,
    Undo = 2,
    Repair = 3
}

public sealed record JournalEntry
{
    public required Guid OperationId { get; init; }
    public required OperationKind Kind { get; init; }
    public required string TargetFolderPath { get; init; }
    public required JournalState State { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public string? TargetAssetPath { get; init; }
    public string? PriorSemanticsJson { get; init; }
    public string? NewSemanticsJson { get; init; }
    public string? ErrorDetails { get; init; }
}
