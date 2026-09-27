using System;

namespace Ornare.Core;

/// <summary>
/// Defines the type of center emblem applied to a folder template.
/// </summary>
public enum EmblemKind
{
    None = 0,
    VectorIcon = 1,
    Emoji = 2
}

/// <summary>
/// Immutable unified center-emblem recipe for folder personalization.
/// Exactly one center emblem can be active at a time (VectorIcon or Emoji).
/// </summary>
public sealed record FolderEmblemRecipe
{
    public static readonly FolderEmblemRecipe None = new()
    {
        Kind = EmblemKind.None,
        ContentId = string.Empty,
        GraphemeSequence = string.Empty,
        ForegroundColor = "Auto",
        Scale = 1.0,
        OffsetX = 0.0,
        OffsetY = 0.0
    };

    public EmblemKind Kind { get; init; } = EmblemKind.None;
    public string ContentId { get; init; } = string.Empty;
    public string GraphemeSequence { get; init; } = string.Empty;
    public string ForegroundColor { get; init; } = "Auto";
    public double Scale { get; init; } = 1.0;
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }

    public bool HasEmblem => Kind != EmblemKind.None &&
        ((Kind == EmblemKind.VectorIcon && !string.IsNullOrWhiteSpace(ContentId)) ||
         (Kind == EmblemKind.Emoji && !string.IsNullOrWhiteSpace(GraphemeSequence)));

    /// <summary>
    /// Creates a vector emblem recipe, clearing any existing emoji state.
    /// </summary>
    public static FolderEmblemRecipe CreateVectorIcon(
        string contentId,
        string foregroundColor = "Auto",
        double scale = 1.0,
        double offsetX = 0.0,
        double offsetY = 0.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        return new FolderEmblemRecipe
        {
            Kind = EmblemKind.VectorIcon,
            ContentId = contentId.Trim(),
            GraphemeSequence = string.Empty,
            ForegroundColor = string.IsNullOrWhiteSpace(foregroundColor) ? "Auto" : foregroundColor.Trim(),
            Scale = Math.Clamp(scale, 0.2, 2.0),
            OffsetX = Math.Clamp(offsetX, -0.5, 0.5),
            OffsetY = Math.Clamp(offsetY, -0.5, 0.5)
        };
    }

    /// <summary>
    /// Creates an emoji emblem recipe, clearing any existing vector icon state.
    /// Preserves full multi-codepoint Unicode grapheme cluster.
    /// </summary>
    public static FolderEmblemRecipe CreateEmoji(
        string graphemeSequence,
        double scale = 1.0,
        double offsetX = 0.0,
        double offsetY = 0.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphemeSequence);
        return new FolderEmblemRecipe
        {
            Kind = EmblemKind.Emoji,
            ContentId = string.Empty,
            GraphemeSequence = graphemeSequence.Trim(),
            ForegroundColor = string.Empty, // Native color emoji
            Scale = Math.Clamp(scale, 0.2, 2.0),
            OffsetX = Math.Clamp(offsetX, -0.5, 0.5),
            OffsetY = Math.Clamp(offsetY, -0.5, 0.5)
        };
    }
}

/// <summary>
/// Individual vector emblem entry from the curated local catalog (Section 18).
/// </summary>
public sealed record VectorEmblemEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("shortId")]
    public string ShortId { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("svgAssetPath")]
    public string SvgAssetPath { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("sourceProject")]
    public string SourceProject { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("releaseTag")]
    public string ReleaseTag { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("license")]
    public string License { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("upstreamCommit")]
    public string UpstreamCommit { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("upstreamAssetPath")]
    public string UpstreamAssetPath { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("acquisitionDate")]
    public string AcquisitionDate { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("licenseBasis")]
    public string LicenseBasis { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("viewBox")]
    public string ViewBox { get; init; } = "0 0 24 24";

    [System.Text.Json.Serialization.JsonPropertyName("pathData")]
    public string PathData { get; init; } = string.Empty;
}

/// <summary>
/// Root manifest for the curated vector emblem catalog (Section 18 &amp; 19).
/// </summary>
public sealed record VectorEmblemManifest
{
    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [System.Text.Json.Serialization.JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("family")]
    public string Family { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("license")]
    public string License { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("upstreamVersion")]
    public string UpstreamVersion { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("upstreamCommit")]
    public string UpstreamCommit { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("upstreamRepository")]
    public string UpstreamRepository { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("acquisitionDate")]
    public string AcquisitionDate { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("licenseBasis")]
    public string LicenseBasis { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("categories")]
    public string[] Categories { get; init; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("emblems")]
    public List<VectorEmblemEntry> Emblems { get; init; } = [];
}

