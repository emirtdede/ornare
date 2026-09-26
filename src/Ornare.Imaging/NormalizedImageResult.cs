namespace Ornare.Imaging;

public sealed record NormalizedImageResult
{
    public required string ContentHash { get; init; }
    public required int SourceWidth { get; init; }
    public required int SourceHeight { get; init; }
    public required string SourceFormat { get; init; }
    public required string GeneratedIcoPath { get; init; }
    public required IReadOnlyList<int> FrameSizes { get; init; }
    public string? RenderKey { get; init; }
    public bool IsFromCache { get; init; }
}
