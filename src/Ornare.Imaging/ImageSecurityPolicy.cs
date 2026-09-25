using ImageMagick;

namespace Ornare.Imaging;

public static class ImageSecurityPolicy
{
    private static readonly HashSet<MagickFormat> AllowedFormats = new()
    {
        MagickFormat.Png,
        MagickFormat.Png8,
        MagickFormat.Png24,
        MagickFormat.Png32,
        MagickFormat.Jpeg,
        MagickFormat.Jpg,
        MagickFormat.WebP,
        MagickFormat.Bmp,
        MagickFormat.Bmp2,
        MagickFormat.Bmp3,
        MagickFormat.Ico,
        MagickFormat.Icon,
        MagickFormat.Tiff,
        MagickFormat.Tif,
        MagickFormat.Gif
    };

    private static bool _initialized;
    private static readonly object InitLock = new();

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            // Enforce hard resource caps per docs/IMAGE_PIPELINE.md
            ResourceLimits.Width = 16384;
            ResourceLimits.Height = 16384;
            ResourceLimits.Area = 100_000_000;
            ResourceLimits.Memory = 256 * 1024 * 1024; // 256 MB cap
            ResourceLimits.Disk = 512 * 1024 * 1024;   // 512 MB temp disk cap

            _initialized = true;
        }
    }

    public static bool IsFormatAllowed(MagickFormat format)
    {
        return AllowedFormats.Contains(format);
    }
}
