using System;

namespace Ornare.Imaging;

/// <summary>
/// WCAG 2.2 Relative Luminance and Contrast Ratio calculations.
/// Supports non-text graphical indicator contrast selection (WCAG 1.4.11, 3:1 minimum).
/// References:
/// https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
/// https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
/// </summary>
public static class WcagContrast
{
    public const double NonTextContrastThreshold = 3.0;

    /// <summary>
    /// Computes the WCAG relative luminance of an sRGB color.
    /// Range: [0.0, 1.0] where black is 0.0 and white is 1.0.
    /// </summary>
    public static double ComputeRelativeLuminance(byte r, byte g, byte b)
    {
        double rLin = ColorConversion.SrgbToLinear(r / 255.0);
        double gLin = ColorConversion.SrgbToLinear(g / 255.0);
        double bLin = ColorConversion.SrgbToLinear(b / 255.0);

        return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
    }

    /// <summary>
    /// Computes the WCAG contrast ratio between two sRGB colors.
    /// Ratio ranges from 1:1 to 21:1.
    /// </summary>
    public static double ComputeContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        double lum1 = ComputeRelativeLuminance(r1, g1, b1);
        double lum2 = ComputeRelativeLuminance(r2, g2, b2);

        double lighter = Math.Max(lum1, lum2);
        double darker = Math.Min(lum1, lum2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Evaluates badge foreground selection against a sampled background color.
    /// Chooses black or white by the higher contrast ratio.
    /// If direct contrast is below 3:1 (e.g. against complex/unusual surfaces), flags that
    /// the circular-backing rule (F04 backing disk) must be invoked.
    /// </summary>
    public static BadgeContrastResult SelectBadgeForeground(byte bgR, byte bgG, byte bgB)
    {
        double bgLum = ComputeRelativeLuminance(bgR, bgG, bgB);

        // Luminance of white is 1.0, black is 0.0
        double contrastWithWhite = (1.0 + 0.05) / (bgLum + 0.05);
        double contrastWithBlack = (bgLum + 0.05) / (0.0 + 0.05);

        string chosenColor;
        double chosenRatio;
        if (contrastWithWhite >= contrastWithBlack)
        {
            chosenColor = "#FFFFFF";
            chosenRatio = contrastWithWhite;
        }
        else
        {
            chosenColor = "#000000";
            chosenRatio = contrastWithBlack;
        }

        bool requiresBacking = chosenRatio < NonTextContrastThreshold;

        return new BadgeContrastResult(
            ForegroundColor: chosenColor,
            ContrastRatio: chosenRatio,
            RequiresCircularBacking: requiresBacking,
            BackingDiskColor: requiresBacking ? (contrastWithWhite >= contrastWithBlack ? "#000000" : "#FFFFFF") : null,
            BackingGlyphColor: requiresBacking ? (contrastWithWhite >= contrastWithBlack ? "#FFFFFF" : "#000000") : null
        );
    }
}

public sealed record BadgeContrastResult(
    string ForegroundColor,
    double ContrastRatio,
    bool RequiresCircularBacking,
    string? BackingDiskColor,
    string? BackingGlyphColor
);
