using System;
using Ornare.Core;

namespace Ornare.Imaging;

/// <summary>
/// Generates deterministic 5-tone folder palettes using OKLab/OKLCH color science (F10).
/// Preserves the exact user base color without modification.
/// Derives:
///   deep      = L - 0.18
///   shadow    = L - 0.11
///   light     = L + 0.08
///   highlight = L + 0.14
/// Clamps derived tone lightness to [0.02, 0.98].
/// Monotonically maps chroma to sRGB gamut via binary search preserving hue.
/// Computes WCAG non-text badge foreground contrast (≥ 3:1).
/// </summary>
public static class FolderPaletteGenerator
{
    public const int PaletteAlgorithmVersion = 1;

    public const double DeepDeltaL = -0.18;
    public const double ShadowDeltaL = -0.11;
    public const double LightDeltaL = +0.08;
    public const double HighlightDeltaL = +0.14;

    public const double MinDerivedL = 0.02;
    public const double MaxDerivedL = 0.98;

    /// <summary>
    /// Generates a FolderPalette from a 6-character hex string (e.g. "#0078D4" or "0078D4").
    /// </summary>
    public static FolderPalette GenerateFromHex(string baseHex)
    {
        if (!ColorConversion.TryParseHex(baseHex, out byte r, out byte g, out byte b))
        {
            throw new ArgumentException($"Invalid 6-character hex color '{baseHex}'.", nameof(baseHex));
        }

        return GenerateFromRgb(r, g, b);
    }

    /// <summary>
    /// Generates a deterministic 5-tone FolderPalette from exact 8-bit sRGB base channels.
    /// </summary>
    public static FolderPalette GenerateFromRgb(byte baseR, byte baseG, byte baseB)
    {
        // 1. Exact base RGB is never altered for base role or storage
        string exactBaseHex = ColorConversion.ToHex(baseR, baseG, baseB);

        // 2. Convert base RGB to OKLCH
        OklchColor baseLch = ColorConversion.RgbToOklch(baseR, baseG, baseB);

        // 3. Compute target lightnesses for derived tones and clamp to [0.02, 0.98]
        double deepTargetL = Math.Clamp(baseLch.L + DeepDeltaL, MinDerivedL, MaxDerivedL);
        double shadowTargetL = Math.Clamp(baseLch.L + ShadowDeltaL, MinDerivedL, MaxDerivedL);
        double lightTargetL = Math.Clamp(baseLch.L + LightDeltaL, MinDerivedL, MaxDerivedL);
        double highlightTargetL = Math.Clamp(baseLch.L + HighlightDeltaL, MinDerivedL, MaxDerivedL);

        // 4. Map derived tones to sRGB via binary search gamut mapping preserving hue and chroma
        var (deepR, deepG, deepB) = ColorConversion.MapLchToRgb(deepTargetL, baseLch.C, baseLch.H);
        var (shadowR, shadowG, shadowB) = ColorConversion.MapLchToRgb(shadowTargetL, baseLch.C, baseLch.H);
        var (lightR, lightG, lightB) = ColorConversion.MapLchToRgb(lightTargetL, baseLch.C, baseLch.H);
        var (highR, highG, highB) = ColorConversion.MapLchToRgb(highlightTargetL, baseLch.C, baseLch.H);

        // 5. Compute WCAG badge foreground contrast against base
        var badgeResult = WcagContrast.SelectBadgeForeground(baseR, baseG, baseB);

        return new FolderPalette
        {
            AlgorithmVersion = PaletteAlgorithmVersion,
            BaseColor = exactBaseHex,
            DeepColor = ColorConversion.ToHex(deepR, deepG, deepB),
            ShadowColor = ColorConversion.ToHex(shadowR, shadowG, shadowB),
            LightColor = ColorConversion.ToHex(lightR, lightG, lightB),
            HighlightColor = ColorConversion.ToHex(highR, highG, highB),
            BadgeColor = badgeResult.ForegroundColor,
            BadgeContrastRatio = badgeResult.ContrastRatio,
            RequiresBackingDisk = badgeResult.RequiresCircularBacking
        };
    }
}
