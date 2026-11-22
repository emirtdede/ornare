using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ornare.Core;

/// <summary>
/// Durable, user-visible application preferences. Every value here is persisted by
/// <c>IStorageService</c> and is part of the cloud backup snapshot, so new members must
/// stay backward compatible: a missing key always resolves to the documented default.
/// </summary>
public sealed record UserSettings
{
    public const int MaxRecentCustomColors = 8;
    public const int MinUiScalePercent = 80;
    public const int MaxUiScalePercent = 200;

    /// <summary>
    /// Interface scale choices offered in the pickers. Every value is inside the clamp range
    /// so a manual settings edit can never produce an unreachable selection.
    /// </summary>
    public static IReadOnlyList<int> UiScalePresets { get; } = new[] { 80, 90, 100, 110, 125, 150, 175, 200 };

    /// <summary>Sentinel meaning "follow the Windows system value".</summary>
    public const string SystemValue = "System";

    /// <summary>System, Light or Dark.</summary>
    public string Theme { get; init; } = SystemValue;

    /// <summary>System or a BCP-47 locale shipped with the app.</summary>
    public string Language { get; init; } = SystemValue;

    public string DefaultTemplateId { get; init; } = "ornare-classic";
    public int DefaultTemplateVersion { get; init; } = 1;
    public IReadOnlyList<string> RecentCustomColors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Application accent color: <see cref="SystemValue"/> to follow Windows, otherwise a
    /// normalized upper-case <c>#RRGGBB</c> value chosen by the user.
    /// </summary>
    public string AccentColor { get; init; } = SystemValue;

    /// <summary>Identifier of the selected application icon (window, taskbar, shortcut).</summary>
    public string AppIconId { get; init; } = AppIconCatalog.DefaultId;

    /// <summary>Interface zoom percentage, clamped to 80..200.</summary>
    public int UiScalePercent { get; init; } = 100;

    /// <summary>True once the user finishes the first-run walkthrough.</summary>
    public bool OnboardingCompleted { get; init; }

    /// <summary>True when the user opted into automatic cloud folder synchronization.</summary>
    public bool CloudSyncEnabled { get; init; }

    /// <summary>Provider identifier: None, OneDrive, GoogleDrive, Dropbox or Custom.</summary>
    public string CloudSyncProvider { get; init; } = "None";

    /// <summary>Optional custom synchronization root chosen by the user.</summary>
    public string? CloudSyncRoot { get; init; }

    /// <summary>UTC timestamp of the last successful snapshot write, ISO 8601.</summary>
    public string? CloudSyncLastSyncUtc { get; init; }

    /// <summary>
    /// Adds a custom color to the MRU list. Normalizes to uppercase 6-char hex, deduplicates by exact 8-bit RGB, and caps at MaxRecentCustomColors (8).
    /// </summary>
    public UserSettings WithRecentCustomColor(string hexColor)
    {
        if (!TryNormalizeHexColor(hexColor, out string normalized))
        {
            return this;
        }

        var list = new List<string> { normalized };
        foreach (var color in RecentCustomColors)
        {
            if (TryNormalizeHexColor(color, out string existing) &&
                !string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(existing);
            }

            if (list.Count >= MaxRecentCustomColors)
            {
                break;
            }
        }

        return this with { RecentCustomColors = list };
    }

    /// <summary>
    /// Validates an accent color. Accepts <c>#RRGGBB</c>, <c>RRGGBB</c> and the
    /// <see cref="SystemValue"/> sentinel; everything else is rejected so a stored value
    /// can never reach the XAML brush factory as a malformed string.
    /// </summary>
    public static bool TryNormalizeAccentColor(string? raw, out string normalized)
    {
        if (string.Equals(raw?.Trim(), SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            normalized = SystemValue;
            return true;
        }

        return TryNormalizeHexColor(raw, out normalized);
    }

    /// <summary>Normalizes a 6-digit RGB hex string to <c>#RRGGBB</c> upper case.</summary>
    public static bool TryNormalizeHexColor(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string value = raw.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        normalized = "#" + value.ToUpperInvariant();
        return true;
    }

    /// <summary>Clamps a requested zoom percentage into the supported range.</summary>
    public static int ClampScalePercent(int requested) =>
        Math.Clamp(requested, MinUiScalePercent, MaxUiScalePercent);

    /// <summary>
    /// True when this settings record carries no user intent beyond shipped defaults.
    /// Used by cloud synchronization to refuse overwriting a real backup with blank data.
    /// </summary>
    public bool IsDefault() =>
        string.Equals(Theme, SystemValue, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Language, SystemValue, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(AccentColor, SystemValue, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(AppIconId, AppIconCatalog.DefaultId, StringComparison.Ordinal) &&
        UiScalePercent == 100 &&
        RecentCustomColors.Count == 0 &&
        !OnboardingCompleted &&
        !CloudSyncEnabled;

    /// <summary>Formats a hex color for localized display (never locale-sensitive).</summary>
    public static string FormatHex(byte r, byte g, byte b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
}
