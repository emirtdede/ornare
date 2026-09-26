using System;
using System.Collections.Concurrent;
using System.Text;
using ImageMagick;
using Ornare.Core;

namespace Ornare.Imaging.Badges;

/// <summary>
/// Authoritative badge compositor for status presets (F04).
/// Enforces fixed badge geometry, contrast-adapted backing disk, and small-frame omission rules.
/// Features thread-safe cached 256x256 rasterization with Lanczos resampling to prevent delegate thrashing.
/// </summary>
public static class StatusBadgeCompositor
{
    /// <summary>
    /// Badge box size ratio relative to canvas (0.34 x 0.34).
    /// </summary>
    public const double BadgeBoxRatio = 0.34;

    /// <summary>
    /// Badge center normalized coordinates (0.78, 0.78).
    /// </summary>
    public const double CenterRatio = 0.78;

    /// <summary>
    /// Circular backing disk diameter ratio relative to canvas (0.40).
    /// </summary>
    public const double DiskDiameterRatio = 0.40;

    /// <summary>
    /// Minimum outer safe margin ratio (0.05).
    /// </summary>
    public const double OuterMarginRatio = 0.05;

    /// <summary>
    /// Threshold frame size below which badges are omitted for legibility (32px).
    /// </summary>
    public const int MinimumBadgeFrameSize = 32;

    private static readonly ConcurrentDictionary<(string badgeAssetId, string diskHex, string glyphHex), byte[]> Badge256Cache = new();

    /// <summary>
    /// Determines high-contrast disk and glyph hex colors against the folder base color.
    /// </summary>
    public static (string diskHex, string glyphHex) DetermineBadgeColors(string folderBaseHex)
    {
        if (!ColorConversion.TryParseHex(folderBaseHex, out byte r, out byte g, out byte b))
        {
            return ("#FFFFFF", "#0F172A");
        }

        double contrastWithWhite = WcagContrast.ComputeContrastRatio(r, g, b, 255, 255, 255);
        double contrastWithDark = WcagContrast.ComputeContrastRatio(r, g, b, 15, 23, 42); // #0F172A

        if (contrastWithWhite >= contrastWithDark)
        {
            // White backing disk, dark glyph
            return ("#FFFFFF", "#0F172A");
        }
        else
        {
            // Dark backing disk, white glyph
            return ("#0F172A", "#FFFFFF");
        }
    }

    /// <summary>
    /// Composites a status badge onto a frame image if the frame size is >= 32px.
    /// Omitted for 16, 20, and 24 px frames per Fixed Decision 6.
    /// </summary>
    public static void CompositeBadge(MagickImage frame, string badgeAssetId, string folderBaseHex)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentException.ThrowIfNullOrWhiteSpace(badgeAssetId);

        // Fixed Decision 6: Badge is omitted from 16, 20 and 24 px frames
        if (frame.Width < MinimumBadgeFrameSize || frame.Height < MinimumBadgeFrameSize)
        {
            return;
        }

        if (!StatusBadgeAssets.TryGetPathData(badgeAssetId, out string? pathData) || string.IsNullOrEmpty(pathData))
        {
            return;
        }

        var (diskHex, glyphHex) = DetermineBadgeColors(folderBaseHex);

        if (frame.Width > 256)
        {
            double s = frame.Width;
            double center = s * CenterRatio;
            double radius = s * (DiskDiameterRatio / 2.0);
            double box = s * BadgeBoxRatio;
            double offset = center - (box / 2.0);
            double scale = box / 24.0;

            string badgeSvg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 {frame.Width} {frame.Height}"" width=""{frame.Width}"" height=""{frame.Height}"">
  <circle cx=""{center.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}"" cy=""{center.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}"" r=""{radius.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}"" fill=""{diskHex}"" />
  <g transform=""translate({offset.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, {offset.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}) scale({scale.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)})"">
    <path d=""{pathData}"" fill=""{glyphHex}"" />
  </g>
</svg>";

            var highResSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = (uint)frame.Width,
                Height = (uint)frame.Height,
                BackgroundColor = MagickColors.Transparent
            };

            lock (SvgRasterizationGate.Lock)
            {
                byte[] svgBytes = Encoding.UTF8.GetBytes(badgeSvg);
                using var highResLayer = new MagickImage(svgBytes, highResSettings);
                frame.Composite(highResLayer, 0, 0, CompositeOperator.Over);
            }
            return;
        }

        var cacheKey = (badgeAssetId, diskHex, glyphHex);

        byte[] badge256Bytes;
        if (!Badge256Cache.TryGetValue(cacheKey, out badge256Bytes!))
        {
            lock (SvgRasterizationGate.Lock)
            {
                if (!Badge256Cache.TryGetValue(cacheKey, out badge256Bytes!))
                {
                    // 256x256 normalized geometry:
                    // Center: 256 * 0.78 = 199.68
                    // Disk diameter: 256 * 0.40 = 102.4, radius = 51.2
                    // Badge box: 256 * 0.34 = 87.04, top-left = 199.68 - 43.52 = 156.16
                    // Scale from 24x24 viewBox: 87.04 / 24 = 3.6266667
                    string badgeSvg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"" width=""256"" height=""256"">
  <circle cx=""199.68"" cy=""199.68"" r=""51.2"" fill=""{diskHex}"" />
  <g transform=""translate(156.16, 156.16) scale(3.6266667)"">
    <path d=""{pathData}"" fill=""{glyphHex}"" />
  </g>
</svg>";

                    var readSettings = new MagickReadSettings
                    {
                        Format = MagickFormat.Svg,
                        Width = 256,
                        Height = 256,
                        BackgroundColor = MagickColors.Transparent
                    };

                    byte[] svgBytes = Encoding.UTF8.GetBytes(badgeSvg);
                    using var baseLayer = new MagickImage(svgBytes, readSettings);
                    baseLayer.Format = MagickFormat.Png;
                    badge256Bytes = baseLayer.ToByteArray();
                    Badge256Cache[cacheKey] = badge256Bytes;
                }
            }
        }

        using var badgeLayer = new MagickImage(badge256Bytes);
        if (frame.Width != 256 || frame.Height != 256)
        {
            badgeLayer.FilterType = FilterType.Lanczos;
            badgeLayer.Resize((uint)frame.Width, (uint)frame.Height);
        }

        frame.Composite(badgeLayer, 0, 0, CompositeOperator.Over);
    }
}
