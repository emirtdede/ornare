namespace Ornare.Core;

public sealed record LibraryAsset
{
    public required string Id { get; init; }
    public required string Sha256 { get; init; }
    public required string CanonicalIcoPath { get; init; }
    public required string SourceFormat { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastUsedAt { get; init; }
    public bool IsFavorite { get; init; }
}
