using System;

namespace Ornare.Core;

/// <summary>
/// Immutable specification for folder icon background composition (F08).
/// </summary>
public readonly record struct BackgroundSpec
{
    private readonly string? _colorHex;

    /// <summary>
    /// Gets the background compositing mode.
    /// </summary>
    public BackgroundMode Mode { get; init; }

    /// <summary>
    /// Gets the normalized uppercase hex color (#RRGGBB) when in SolidColor mode, or empty string when Transparent.
    /// </summary>
    public string ColorHex
    {
        get => _colorHex ?? string.Empty;
        init => _colorHex = value;
    }

    /// <summary>
    /// Transparent background specification (default).
    /// </summary>
    public static BackgroundSpec Transparent => new() { Mode = BackgroundMode.Transparent, ColorHex = string.Empty };

    /// <summary>
    /// Creates a solid color background specification with normalized #RRGGBB.
    /// </summary>
    public static BackgroundSpec Solid(string hex)
    {
        string normalized = NormalizeHex(hex);
        return new() { Mode = BackgroundMode.SolidColor, ColorHex = normalized };
    }

    /// <summary>
    /// Normalizes a hex color string into uppercase #RRGGBB format.
    /// Defaults to #FFFFFF if invalid or empty.
    /// </summary>
    public static string NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return "#FFFFFF";
        }

        string trimmed = hex.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 3)
        {
            trimmed = $"{trimmed[0]}{trimmed[0]}{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}";
        }

        if (trimmed.Length != 6 || !uint.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            return "#FFFFFF";
        }

        return $"#{trimmed.ToUpperInvariant()}";
    }

    /// <summary>
    /// Determines whether the provided hex string represents a valid 3-character or 6-character RGB hex code.
    /// </summary>
    public static bool IsValidHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        string trimmed = hex.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 3 && trimmed.Length != 6)
        {
            return false;
        }

        return uint.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out _);
    }
}
