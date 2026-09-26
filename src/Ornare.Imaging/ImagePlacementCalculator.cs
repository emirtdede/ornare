using System;
using Ornare.Core;

namespace Ornare.Imaging;

/// <summary>
/// Immutable, allocation-free placement geometry describing source crop and canvas destination rectangles.
/// </summary>
public readonly record struct PlacementGeometry(
    int SourceCropX,
    int SourceCropY,
    int SourceCropWidth,
    int SourceCropHeight,
    int DestX,
    int DestY,
    int DestWidth,
    int DestHeight,
    int CanvasWidth,
    int CanvasHeight
);

/// <summary>
/// Authoritative pure geometry engine for image fitting modes: Contain, Cover, Fill (F06).
/// Allocation-free and mathematically deterministic.
/// </summary>
public static class ImagePlacementCalculator
{
    /// <summary>
    /// Computes the exact source crop and destination placement geometry for a given source and canvas size.
    /// </summary>
    public static PlacementGeometry ComputePlacement(
        int sourceWidth,
        int sourceHeight,
        int canvasWidth,
        int canvasHeight,
        ImagePlacementMode mode = ImagePlacementMode.Contain,
        double paddingPercent = 0.0)
    {
        int srcW = Math.Max(1, sourceWidth);
        int srcH = Math.Max(1, sourceHeight);
        int canvasW = Math.Max(1, canvasWidth);
        int canvasH = Math.Max(1, canvasHeight);

        // Calculate padding offsets per side (F07): clamped to [0, 30]%
        double clampedPadding = Math.Clamp(paddingPercent, 0.0, 30.0);
        int padX = (int)Math.Round(canvasW * (clampedPadding / 100.0), MidpointRounding.AwayFromZero);
        int padY = (int)Math.Round(canvasH * (clampedPadding / 100.0), MidpointRounding.AwayFromZero);

        int usableW = Math.Max(1, canvasW - (padX * 2));
        int usableH = Math.Max(1, canvasH - (padY * 2));
        int offsetX = padX;
        int offsetY = padY;

        switch (mode)
        {
            case ImagePlacementMode.Cover:
                {
                    double srcAspect = (double)srcW / srcH;
                    double targetAspect = (double)usableW / usableH;

                    int cropX;
                    int cropY;
                    int cropW;
                    int cropH;

                    if (srcAspect > targetAspect)
                    {
                        // Source is wider than target aspect: crop sides, keep full height
                        cropH = srcH;
                        cropW = Math.Max(1, (int)Math.Round(srcH * targetAspect));
                        if (cropW > srcW) cropW = srcW;
                        cropX = (srcW - cropW) / 2;
                        cropY = 0;
                    }
                    else if (srcAspect < targetAspect)
                    {
                        // Source is taller than target aspect: crop top/bottom, keep full width
                        cropW = srcW;
                        cropH = Math.Max(1, (int)Math.Round(srcW / targetAspect));
                        if (cropH > srcH) cropH = srcH;
                        cropX = 0;
                        cropY = (srcH - cropH) / 2;
                    }
                    else
                    {
                        // Exact aspect match
                        cropX = 0;
                        cropY = 0;
                        cropW = srcW;
                        cropH = srcH;
                    }

                    return new PlacementGeometry(
                        SourceCropX: cropX,
                        SourceCropY: cropY,
                        SourceCropWidth: cropW,
                        SourceCropHeight: cropH,
                        DestX: offsetX,
                        DestY: offsetY,
                        DestWidth: usableW,
                        DestHeight: usableH,
                        CanvasWidth: canvasW,
                        CanvasHeight: canvasH);
                }

            case ImagePlacementMode.Fill:
                {
                    // Fill: full source stretched to usable canvas, no crop, independent X/Y scaling
                    return new PlacementGeometry(
                        SourceCropX: 0,
                        SourceCropY: 0,
                        SourceCropWidth: srcW,
                        SourceCropHeight: srcH,
                        DestX: offsetX,
                        DestY: offsetY,
                        DestWidth: usableW,
                        DestHeight: usableH,
                        CanvasWidth: canvasW,
                        CanvasHeight: canvasH);
                }

            case ImagePlacementMode.Contain:
            default:
                {
                    // Contain: fit full source inside usable canvas, preserving aspect ratio with letterboxing
                    double scale = Math.Min((double)usableW / srcW, (double)usableH / srcH);
                    int destW = Math.Max(1, (int)Math.Round(srcW * scale));
                    int destH = Math.Max(1, (int)Math.Round(srcH * scale));

                    int destX = offsetX + (usableW - destW) / 2;
                    int destY = offsetY + (usableH - destH) / 2;

                    return new PlacementGeometry(
                        SourceCropX: 0,
                        SourceCropY: 0,
                        SourceCropWidth: srcW,
                        SourceCropHeight: srcH,
                        DestX: destX,
                        DestY: destY,
                        DestWidth: destW,
                        DestHeight: destH,
                        CanvasWidth: canvasW,
                        CanvasHeight: canvasH);
                }
        }
    }
}
