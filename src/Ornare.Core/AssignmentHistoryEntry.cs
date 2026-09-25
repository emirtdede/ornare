namespace Ornare.Core;

public sealed record AssignmentHistoryEntry
{
    public long Id { get; init; }
    public required string FolderPath { get; init; }
    public required Guid OperationId { get; init; }
    public string? PreviousAssetHash { get; init; }
    public required string NewAssetHash { get; init; }
    public required string OperationType { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
