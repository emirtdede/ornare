using System;
using System.Globalization;

namespace Ornare.Imaging;

/// <summary>
/// High-precision, zero-allocation color space conversion between sRGB, linear sRGB, OKLab, and OKLCH.
/// Implements Björn Ottosson's published 2020 OKLab specification with double precision.
/// Reference: https://bottosson.github.io/posts/oklab/
/// </summary>
public static class ColorConversion
{
    private const double SrgbGammaThreshold = 0.04045;
    private const double LinearGammaThreshold = 0.0031308;
    private const double ChromaThreshold = 1e-4;
    private const double GamutEpsilon = 1e-7;
    private const int MaxGamutIterations = 24;

    /// <summary>
    /// Converts standard non-linear sRGB channel [0, 1] to linear sRGB.
    /// </summary>
    public static double SrgbToLinear(double c)
    {
        if (double.IsNaN(c) || double.IsInfinity(c)) return 0.0;
        c = Math.Clamp(c, 0.0, 1.0);
        return c <= SrgbGammaThreshold
            ? c / 12.92
            : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Converts linear sRGB channel [0, 1] to standard non-linear sRGB.
    /// </summary>
    public static double LinearToSrgb(double c)
    {
        if (double.IsNaN(c) || double.IsInfinity(c)) return 0.0;
        c = Math.Clamp(c, 0.0, 1.0);
        return c <= LinearGammaThreshold
            ? c * 12.92
            : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
    }

    /// <summary>
    /// Converts linear sRGB (r, g, b) in [0, 1] to OKLab (L, a, b).
    /// </summary>
    public static OklabColor LinearSrgbToOklab(double r, double g, double b)
    {
        double l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
        double m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
        double s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

        double l_ = Math.Cbrt(l);
        double m_ = Math.Cbrt(m);
        double s_ = Math.Cbrt(s);

        return new OklabColor(
            0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_
        );
    }

    /// <summary>
    /// Converts OKLab (L, a, b) to linear sRGB (r, g, b).
    /// Output channels may exceed [0, 1] if the color is out of sRGB gamut.
    /// </summary>
    public static (double r, double g, double b) OklabToLinearSrgb(OklabColor lab)
    {
        double l_ = lab.L + 0.3963377774 * lab.A + 0.2158037573 * lab.B;
        double m_ = lab.L - 0.1055613458 * lab.A - 0.0638541728 * lab.B;
        double s_ = lab.L - 0.0894841775 * lab.A - 1.2914855480 * lab.B;

        double l = l_ * l_ * l_;
        double m = m_ * m_ * m_;
        double s = s_ * s_ * s_;

        return (
            +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s
        );
    }

    /// <summary>
    /// Converts OKLab to cylindrical OKLCH.
    /// Treats hue as undefined (0.0) when chroma is below 1e-4.
    /// </summary>
    public static OklchColor OklabToOklch(OklabColor lab)
    {
        double c = Math.Sqrt(lab.A * lab.A + lab.B * lab.B);
        if (c < ChromaThreshold)
        {
            return new OklchColor(lab.L, 0.0, 0.0);
        }

        double h = Math.Atan2(lab.B, lab.A) * (180.0 / Math.PI);
        if (h < 0.0)
        {
            h += 360.0;
        }

        return new OklchColor(lab.L, c, h);
    }

    /// <summary>
    /// Converts cylindrical OKLCH to OKLab.
    /// </summary>
    public static OklabColor OklchToOklab(OklchColor lch)
    {
        if (lch.C < ChromaThreshold)
        {
            return new OklabColor(lch.L, 0.0, 0.0);
        }

        double hRad = lch.H * (Math.PI / 180.0);
        return new OklabColor(
            lch.L,
            lch.C * Math.Cos(hRad),
            lch.C * Math.Sin(hRad)
        );
    }

    /// <summary>
    /// Converts standard 8-bit sRGB (r, g, b) to OKLCH.
    /// </summary>
    public static OklchColor RgbToOklch(byte r, byte g, byte b)
    {
        double rLin = SrgbToLinear(r / 255.0);
        double gLin = SrgbToLinear(g / 255.0);
        double bLin = SrgbToLinear(b / 255.0);

        OklabColor lab = LinearSrgbToOklab(rLin, gLin, bLin);
        return OklabToOklch(lab);
    }

    /// <summary>
    /// Checks if the given OKLCH color is within the sRGB gamut within specified epsilon.
    /// </summary>
    public static bool IsInGamut(double l, double c, double h, double epsilon = GamutEpsilon)
    {
        OklabColor lab = OklchToOklab(new OklchColor(l, c, h));
        var (rLin, gLin, bLin) = OklabToLinearSrgb(lab);

        return rLin >= -epsilon && rLin <= 1.0 + epsilon &&
               gLin >= -epsilon && gLin <= 1.0 + epsilon &&
               bLin >= -epsilon && bLin <= 1.0 + epsilon;
    }

    /// <summary>
    /// Maps an OKLCH color to the sRGB gamut by monotonically reducing chroma via binary search.
    /// Preserves exact hue and target lightness. Returns 8-bit sRGB channels.
    /// Never solves gamut by first clipping RGB channels.
    /// </summary>
    public static (byte r, byte g, byte b) MapLchToRgb(double l, double c, double h)
    {
        if (double.IsNaN(l) || double.IsInfinity(l)) l = 0.5;
        if (double.IsNaN(c) || double.IsInfinity(c) || c < ChromaThreshold)
        {
            c = 0.0;
            h = 0.0;
        }

        double finalC = c;
        if (c > 0.0 && !IsInGamut(l, c, h))
        {
            double low = 0.0;
            double high = c;
            for (int iter = 0; iter < MaxGamutIterations; iter++)
            {
                double mid = (low + high) * 0.5;
                if (IsInGamut(l, mid, h))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }
            finalC = low;
        }

        OklabColor finalLab = OklchToOklab(new OklchColor(l, finalC, h));
        var (rLin, gLin, bLin) = OklabToLinearSrgb(finalLab);

        rLin = Math.Clamp(rLin, 0.0, 1.0);
        gLin = Math.Clamp(gLin, 0.0, 1.0);
        bLin = Math.Clamp(bLin, 0.0, 1.0);

        double rSrgb = LinearToSrgb(rLin);
        double gSrgb = LinearToSrgb(gLin);
        double bSrgb = LinearToSrgb(bLin);

        byte rByte = (byte)Math.Clamp((int)Math.Round(rSrgb * 255.0), 0, 255);
        byte gByte = (byte)Math.Clamp((int)Math.Round(gSrgb * 255.0), 0, 255);
        byte bByte = (byte)Math.Clamp((int)Math.Round(bSrgb * 255.0), 0, 255);

        return (rByte, gByte, bByte);
    }

    /// <summary>
    /// Parses a 6-character hex string into 8-bit RGB channels.
    /// </summary>
    public static bool TryParseHex(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string trimmed = hex.Trim();
        if (trimmed.StartsWith('#')) trimmed = trimmed[1..];
        if (trimmed.Length != 6) return false;

        if (byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
            byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
            byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats RGB channels as uppercase #RRGGBB.
    /// </summary>
    public static string ToHex(byte r, byte g, byte b)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
    }
}
