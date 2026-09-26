using System;

namespace Ornare.Core;

public sealed record RenderCacheEntry
{
    public string RenderKey { get; init; } = string.Empty;
    public int RecipeVersion { get; init; } = 1;
    public string RecipeJson { get; init; } = string.Empty;
    public string SourceAssetHash { get; init; } = string.Empty;
    public string CanonicalIcoPath { get; init; } = string.Empty;
    public string FrameSizes { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; init; } = DateTimeOffset.UtcNow;
}
