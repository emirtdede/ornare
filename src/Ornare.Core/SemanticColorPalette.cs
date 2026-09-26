using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ornare.Core;

public sealed record SemanticColorPalette
{
    public string BaseColor { get; init; } = "#0078D4";
    public string LightColor { get; init; } = "#2B88D8";
    public string HighlightColor { get; init; } = "#50E6FF";
    public string ShadowColor { get; init; } = "#005A9E";
    public string DeepColor { get; init; } = "#004578";
    public string BadgeColor { get; init; } = "#FFB900";

    public string GetColor(SemanticSvgRole role) => role switch
    {
        SemanticSvgRole.Base => BaseColor,
        SemanticSvgRole.Light => LightColor,
        SemanticSvgRole.Highlight => HighlightColor,
        SemanticSvgRole.Shadow => ShadowColor,
        SemanticSvgRole.Deep => DeepColor,
        SemanticSvgRole.Badge => BadgeColor,
        _ => BaseColor
    };

    public static SemanticColorPalette FromUniformColor(string hexColor)
    {
        string normalized = NormalizeHexColor(hexColor);
        return new SemanticColorPalette
        {
            BaseColor = normalized,
            LightColor = normalized,
            HighlightColor = normalized,
            ShadowColor = normalized,
            DeepColor = normalized,
            BadgeColor = normalized
        };
    }

    public static SemanticColorPalette FromBaseColor(string baseHex)
    {
        string normalized = NormalizeHexColor(baseHex);
        ParseRgb(normalized, out byte r, out byte g, out byte b);

        return new SemanticColorPalette
        {
            BaseColor = normalized,
            LightColor = AdjustBrightness(r, g, b, 0.15f),
            HighlightColor = AdjustBrightness(r, g, b, 0.30f),
            ShadowColor = AdjustBrightness(r, g, b, -0.15f),
            DeepColor = AdjustBrightness(r, g, b, -0.30f),
            BadgeColor = "#FFB900"
        };
    }

    public static bool TryNormalizeHexColor(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 3)
        {
            // #RGB -> #RRGGBB
            char r = trimmed[0];
            char g = trimmed[1];
            char b = trimmed[2];
            trimmed = $"{r}{r}{g}{g}{b}{b}";
        }
        else if (trimmed.Length == 4)
        {
            // #RGBA -> #RRGGBBAA
            char r = trimmed[0];
            char g = trimmed[1];
            char b = trimmed[2];
            char a = trimmed[3];
            trimmed = $"{r}{r}{g}{g}{b}{b}{a}{a}";
        }

        if (trimmed.Length != 6 && trimmed.Length != 8)
        {
            return false;
        }

        foreach (char c in trimmed)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        normalized = "#" + trimmed.ToUpperInvariant();
        return true;
    }

    public static string NormalizeHexColor(string? input)
    {
        if (TryNormalizeHexColor(input, out string normalized))
        {
            return normalized;
        }

        throw new ArgumentException($"Invalid hex color string: '{input}'", nameof(input));
    }

    public static bool ParseRgb(string hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (!TryNormalizeHexColor(hex, out string normalized))
        {
            return false;
        }

        string raw = normalized[1..];
        r = byte.Parse(raw.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        g = byte.Parse(raw.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        b = byte.Parse(raw.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return true;
    }

    private static string AdjustBrightness(byte r, byte g, byte b, float factor)
    {
        int adjR = factor > 0
            ? (int)(r + ((255 - r) * factor))
            : (int)(r * (1.0f + factor));

        int adjG = factor > 0
            ? (int)(g + ((255 - g) * factor))
            : (int)(g * (1.0f + factor));

        int adjB = factor > 0
            ? (int)(b + ((255 - b) * factor))
            : (int)(b * (1.0f + factor));

        adjR = Math.Clamp(adjR, 0, 255);
        adjG = Math.Clamp(adjG, 0, 255);
        adjB = Math.Clamp(adjB, 0, 255);

        return $"#{adjR:X2}{adjG:X2}{adjB:X2}";
    }
}
