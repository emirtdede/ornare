using System;
using System.Text;
using ImageMagick;
using Ornare.Core;

namespace Ornare.Imaging.Emblems;

/// <summary>
/// Authoritative compositor for folder center emblems (Section 16, 17, 20 &amp; 21).
/// Enforces template safe-zone constraints, adaptive contrast foreground selection,
/// and deterministic multi-resolution vector scaling.
/// </summary>
public static class FolderEmblemCompositor
{
    /// <summary>
    /// Determines the effective foreground color for a vector emblem against a folder base hex color.
    /// Reuses existing F10 WCAG contrast logic without creating a parallel contrast engine.
    /// </summary>
    public static string DetermineForegroundColor(string folderBaseHex, string requestedColor = "Auto")
    {
        if (!string.IsNullOrWhiteSpace(requestedColor) &&
            !string.Equals(requestedColor.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (ColorConversion.TryParseHex(requestedColor, out byte cr, out byte cg, out byte cb))
            {
                return ColorConversion.ToHex(cr, cg, cb);
            }
        }

        // Auto contrast: evaluate folder base color against white and black
        if (ColorConversion.TryParseHex(folderBaseHex, out byte r, out byte g, out byte b))
        {
            var badgeContrast = WcagContrast.SelectBadgeForeground(r, g, b);
            return badgeContrast.ForegroundColor;
        }

        return "#FFFFFF";
    }

    /// <summary>
    /// Composites a center emblem recipe onto a target frame image.
    /// </summary>
    public static void CompositeEmblem(
        MagickImage targetImage,
        FolderEmblemRecipe? emblem,
        string folderBaseHex,
        NormalizedRect safeZone)
    {
        ArgumentNullException.ThrowIfNull(targetImage);

        if (emblem == null || !emblem.HasEmblem)
        {
            return;
        }

        if (emblem.Kind == EmblemKind.VectorIcon)
        {
            var entry = VectorEmblemCatalog.GetById(emblem.ContentId);
            if (entry == null || string.IsNullOrWhiteSpace(entry.PathData))
            {
                return;
            }

            CompositeVectorEmblem(
                targetImage,
                entry,
                folderBaseHex,
                safeZone,
                scale: emblem.Scale,
                offsetX: emblem.OffsetX,
                offsetY: emblem.OffsetY,
                requestedForegroundColor: emblem.ForegroundColor);
        }
    }

    /// <summary>
    /// Composites an individual vector emblem entry onto the target image.
    /// </summary>
    public static void CompositeVectorEmblem(
        MagickImage targetImage,
        VectorEmblemEntry emblem,
        string folderBaseHex,
        NormalizedRect safeZone,
        double scale = 1.0,
        double offsetX = 0.0,
        double offsetY = 0.0,
        string requestedForegroundColor = "Auto")
    {
        ArgumentNullException.ThrowIfNull(targetImage);
        ArgumentNullException.ThrowIfNull(emblem);

        if (string.IsNullOrWhiteSpace(emblem.PathData))
        {
            return;
        }

        double canvasW = targetImage.Width;
        double canvasH = targetImage.Height;

        // Calculate safe zone bounds on target canvas
        double safeX = safeZone.X * canvasW;
        double safeY = safeZone.Y * canvasH;
        double safeW = safeZone.Width * canvasW;
        double safeH = safeZone.Height * canvasH;

        double centerSafeX = safeX + (safeW / 2.0);
        double centerSafeY = safeY + (safeH / 2.0);

        double clampedScale = Math.Clamp(scale <= 0.0 ? 1.0 : scale, 0.2, 2.0);
        double emblemBox = Math.Min(safeW, safeH) * clampedScale;

        double clampedOffsetX = Math.Clamp(offsetX, -0.5, 0.5);
        double clampedOffsetY = Math.Clamp(offsetY, -0.5, 0.5);

        double targetCenterX = centerSafeX + (clampedOffsetX * safeW);
        double targetCenterY = centerSafeY + (clampedOffsetY * safeH);

        int destX = (int)Math.Round(targetCenterX - (emblemBox / 2.0));
        int destY = (int)Math.Round(targetCenterY - (emblemBox / 2.0));
        int boxSize = (int)Math.Max(1, Math.Round(emblemBox));

        string fgHex = DetermineForegroundColor(folderBaseHex, requestedForegroundColor);

        string emblemSvg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""{emblem.ViewBox}"" width=""{boxSize}"" height=""{boxSize}""><path fill=""{fgHex}"" d=""{emblem.PathData}""/></svg>";
        byte[] svgBytes = Encoding.UTF8.GetBytes(emblemSvg);

        var readSettings = new MagickReadSettings
        {
            Format = MagickFormat.Svg,
            Width = (uint)boxSize,
            Height = (uint)boxSize,
            BackgroundColor = MagickColors.Transparent
        };

        lock (SvgRasterizationGate.Lock)
        {
            using var emblemImage = new MagickImage(svgBytes, readSettings);
            targetImage.Composite(emblemImage, destX, destY, CompositeOperator.Over);
        }
    }
}
