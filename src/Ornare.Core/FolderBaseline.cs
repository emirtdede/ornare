namespace Ornare.Core;

public sealed record FolderBaseline
{
    public required Guid FolderAssignmentId { get; init; }
    public required Guid OwnershipEpochId { get; init; }
    public required FolderIconSemantics PriorSemantics { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
}
