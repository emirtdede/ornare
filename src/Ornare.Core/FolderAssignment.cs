namespace Ornare.Core;

public sealed record FolderAssignment
{
    public required string Id { get; init; }
    public required string FolderPath { get; init; }
    public string? VolumeSerial { get; init; }
    public string? FileId { get; init; }
    public string? CurrentAssetHash { get; init; }
    public required Guid OwnershipEpochId { get; init; }
    public required string IconRelativePath { get; init; }
    public FolderCustomizationState Status { get; init; }
    public string? ConflictState { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
