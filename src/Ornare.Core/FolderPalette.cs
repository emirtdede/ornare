using System;

namespace Ornare.Core;

/// <summary>
/// Immutable 5-tone folder palette derived from a user-selected base color.
/// Semantic roles: Deep, Shadow, Base, Light, Highlight + Badge foreground.
/// Preserves the exact user base color without alteration.
/// </summary>
public sealed record FolderPalette
{
    public const int CurrentPaletteAlgorithmVersion = 1;

    public int AlgorithmVersion { get; init; } = CurrentPaletteAlgorithmVersion;
    public string BaseColor { get; init; } = "#0078D4";
    public string LightColor { get; init; } = "#2B88D8";
    public string HighlightColor { get; init; } = "#50E6FF";
    public string ShadowColor { get; init; } = "#005A9E";
    public string DeepColor { get; init; } = "#004578";
    public string BadgeColor { get; init; } = "#FFFFFF";
    public double BadgeContrastRatio { get; init; } = 21.0;
    public bool RequiresBackingDisk { get; init; }

    public SemanticColorPalette ToSemanticColorPalette() => new()
    {
        BaseColor = BaseColor,
        LightColor = LightColor,
        HighlightColor = HighlightColor,
        ShadowColor = ShadowColor,
        DeepColor = DeepColor,
        BadgeColor = BadgeColor
    };
}
