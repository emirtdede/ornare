using System.Text;
using ImageMagick;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Ornare.Core;
using Windows.UI;

namespace Ornare.App;

/// <summary>
/// Applies user appearance choices (accent color, interface scale, application icon) at
/// runtime. All work is local, deterministic and reversible: nothing is written outside the
/// app's own cache folder.
/// </summary>
internal static class AppearanceService
{
    private const string IconCacheFolderName = "app-icons";

    /// <summary>
    /// Resolves the accent color that should be used for chrome. A custom color wins;
    /// otherwise the Windows system accent is read so "System" is a real choice.
    /// </summary>
    public static Color ResolveAccent(string? accentSetting)
    {
        if (UserSettings.TryNormalizeAccentColor(accentSetting, out string normalized) &&
            !string.Equals(normalized, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            return FromHex(normalized);
        }

        return SystemAccent();
    }

    /// <summary>Hex form of <see cref="ResolveAccent"/> for icon generation.</summary>
    public static string ResolveAccentHex(string? accentSetting)
    {
        Color color = ResolveAccent(accentSetting);
        return UserSettings.FormatHex(color.R, color.G, color.B);
    }

    /// <summary>
    /// Overrides the accent brushes Windows UI reads. Theme dictionaries resolve these
    /// keys at control creation time, so applying them before a window is created (or
    /// re-creating the window afterwards) gives a consistent accent across the app.
    /// </summary>
    public static void ApplyAccent(Application? app, string? accentSetting)
    {
        if (app is null)
        {
            return;
        }

        Color accent = ResolveAccent(accentSetting);
        var resources = app.Resources;

        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorLight1"] = Shade(accent, 0.16);
        resources["SystemAccentColorLight2"] = Shade(accent, 0.32);
        resources["SystemAccentColorLight3"] = Shade(accent, 0.48);
        resources["SystemAccentColorDark1"] = Shade(accent, -0.14);
        resources["SystemAccentColorDark2"] = Shade(accent, -0.28);
        resources["SystemAccentColorDark3"] = Shade(accent, -0.42);

        resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accent);
        resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(WithOpacity(accent, 0xE6));
        resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(WithOpacity(accent, 0xCC));
        resources["AccentButtonBackground"] = new SolidColorBrush(accent);
        resources["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(Shade(accent, 0.10));
        resources["AccentButtonBackgroundPressed"] = new SolidColorBrush(Shade(accent, -0.12));
        resources["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(accent);
        resources["HyperlinkButtonForeground"] = new SolidColorBrush(accent);
    }

    /// <summary>
    /// Applies interface zoom. The surface is laid out at <c>viewport / scale</c> logical
    /// pixels and then scaled back up, which widens the effective content area instead of
    /// merely stretching pixels, so text reflows like a browser zoom level.
    /// </summary>
    public static void ApplyZoom(FrameworkElement? viewport, FrameworkElement? surface, int scalePercent)
    {
        if (viewport is null || surface is null)
        {
            return;
        }

        int percent = UserSettings.ClampScalePercent(scalePercent);
        double scale = percent / 100.0;

        if (percent == 100)
        {
            surface.ClearValue(FrameworkElement.WidthProperty);
            surface.ClearValue(FrameworkElement.HeightProperty);
            surface.RenderTransform = null;
            return;
        }

        surface.RenderTransformOrigin = new global::Windows.Foundation.Point(0, 0);
        surface.RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale };

        double width = viewport.ActualWidth;
        double height = viewport.ActualHeight;
        if (width > 0 && height > 0)
        {
            surface.Width = width / scale;
            surface.Height = height / scale;
        }
    }

    /// <summary>
    /// Returns the path of a multi-resolution ICO for the requested icon variant, creating
    /// it on first use. The file name includes the ink color, so switching back to a
    /// previous color reuses a valid cached file instead of fighting the Windows icon cache.
    /// </summary>
    public static string EnsureIconFile(AppIconVariant variant, string accentHex)
    {
        string ink = variant.FollowsAccent ? accentHex : variant.HexColor;
        if (!UserSettings.TryNormalizeHexColor(ink, out string normalizedInk))
        {
            normalizedInk = "#09090B";
        }

        string cacheDirectory = Path.Combine(IconCacheRoot(), IconCacheFolderName);
        Directory.CreateDirectory(cacheDirectory);

        string fileName = $"ornare-{variant.Id}-{normalizedInk.TrimStart('#').ToLowerInvariant()}.ico";
        string fullPath = Path.Combine(cacheDirectory, fileName);
        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 1024)
        {
            return fullPath;
        }

        try
        {
            WriteIco(fullPath, normalizedInk);
            return fullPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MagickException or InvalidOperationException)
        {
            App.Log($"Icon generation failed for {variant.Id}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>PNG preview used by the settings icon picker.</summary>
    public static string EnsureIconPreview(AppIconVariant variant, string accentHex)
    {
        string ink = variant.FollowsAccent ? accentHex : variant.HexColor;
        if (!UserSettings.TryNormalizeHexColor(ink, out string normalizedInk))
        {
            normalizedInk = "#09090B";
        }

        string cacheDirectory = Path.Combine(IconCacheRoot(), IconCacheFolderName);
        Directory.CreateDirectory(cacheDirectory);

        string fullPath = Path.Combine(
            cacheDirectory,
            $"preview-{variant.Id}-{normalizedInk.TrimStart('#').ToLowerInvariant()}.png");

        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 256)
        {
            return fullPath;
        }

        try
        {
            using var image = RenderGlyph(normalizedInk, 128);
            image.Format = MagickFormat.Png32;
            image.Write(fullPath);
            return fullPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MagickException or InvalidOperationException)
        {
            App.Log($"Icon preview generation failed for {variant.Id}: {ex.Message}");
            return string.Empty;
        }
    }

    private static string IconCacheRoot()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, OrnareConstants.ApplicationName);
    }

    private static void WriteIco(string targetPath, string inkHex)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        using var baseImage = RenderGlyph(inkHex, 256);
        using var collection = new MagickImageCollection();
        foreach (int size in sizes)
        {
            var frame = (MagickImage)baseImage.Clone();
            frame.Resize(new MagickGeometry((uint)size, (uint)size) { IgnoreAspectRatio = true });
            frame.Format = MagickFormat.Ico;
            collection.Add(frame);
        }

        string tempPath = targetPath + ".tmp";
        collection.Write(tempPath, MagickFormat.Ico);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private static MagickImage RenderGlyph(string inkHex, uint canvas)
    {
        string svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" width="{canvas}" height="{canvas}">
              <defs>
                <mask id="gap-cut">
                  <rect width="120" height="120" fill="white" />
                  <rect x="36" y="-10" width="28" height="28" fill="black" />
                  <circle cx="50" cy="18" r="14" fill="black" />
                </mask>
              </defs>
              <circle cx="60" cy="60" r="58" fill="#FFFFFF" stroke="#E4E4E7" stroke-width="1.5" />
              <g transform="translate(4,4) scale(1.12) rotate(45 50 50)">
                <path d="M 50 8 A 42 42 0 1 0 50 92 A 42 42 0 1 0 50 8 Z M 50 28 A 22 22 0 1 1 50 72 A 22 22 0 1 1 50 28 Z"
                      fill="{inkHex}" mask="url(#gap-cut)" />
                <circle cx="50" cy="18" r="10" fill="{inkHex}" />
              </g>
            </svg>
            """;

        var settings = new MagickReadSettings
        {
            Format = MagickFormat.Svg,
            ColorSpace = ColorSpace.sRGB,
            BackgroundColor = MagickColors.Transparent
        };

        return new MagickImage(Encoding.UTF8.GetBytes(svg), settings);
    }

    /// <summary>Builds a Color from <c>#RRGGBB</c>; falls back to the system accent.</summary>
    public static Color FromHex(string hex)
    {
        if (!UserSettings.TryNormalizeHexColor(hex, out string normalized))
        {
            return SystemAccent();
        }

        return Color.FromArgb(
            255,
            Convert.ToByte(normalized.Substring(1, 2), 16),
            Convert.ToByte(normalized.Substring(3, 2), 16),
            Convert.ToByte(normalized.Substring(5, 2), 16));
    }

    /// <summary>Lightens (positive) or darkens (negative) a color in linear sRGB space.</summary>
    public static Color Shade(Color color, double amount)
    {
        static byte Apply(byte channel, double delta)
        {
            double target = delta >= 0 ? 255 : 0;
            double value = channel + ((target - channel) * Math.Abs(delta));
            return (byte)Math.Clamp(Math.Round(value), 0, 255);
        }

        return Color.FromArgb(color.A, Apply(color.R, amount), Apply(color.G, amount), Apply(color.B, amount));
    }

    /// <summary>Returns a readable foreground (black or white) for the given background.</summary>
    public static Color ContrastForeground(Color background)
    {
        double luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255.0;
        return luminance > 0.55 ? Colors.Black : Colors.White;
    }

    private static Color WithOpacity(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color SystemAccent()
    {
        try
        {
            return new global::Windows.UI.ViewManagement.UISettings().GetColorValue(
                global::Windows.UI.ViewManagement.UIColorType.Accent);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return Color.FromArgb(255, 0x0F, 0x6C, 0xBD);
        }
    }
}
