using System;
using System.Collections.Generic;

namespace Ornare.Core;

/// <summary>
/// One selectable application icon. The mark is the Ornare glyph; only the ink color
/// changes, so every variant stays on brand regardless of theme or accent choice.
/// </summary>
/// <param name="Id">Stable identifier persisted in user settings.</param>
/// <param name="DisplayResourceKey">Localization key for the picker label.</param>
/// <param name="HexColor">Ink color, or <see cref="UserSettings.SystemValue"/> when the variant follows the accent color.</param>
public sealed record AppIconVariant(string Id, string DisplayResourceKey, string HexColor)
{
    /// <summary>True when the ink color is derived from the user's accent color.</summary>
    public bool FollowsAccent => string.Equals(HexColor, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The built-in application icon set. Kept in Core so settings validation, snapshot
/// restore and the UI all agree on the same identifiers.
/// </summary>
public static class AppIconCatalog
{
    public const string DefaultId = "ornare-ink";

    public static IReadOnlyList<AppIconVariant> All { get; } = new[]
    {
        new AppIconVariant("ornare-ink", "Icon_OrnareInk", "#09090B"),
        new AppIconVariant("ornare-accent", "Icon_OrnareAccent", UserSettings.SystemValue),
        new AppIconVariant("ornare-ocean", "Icon_OrnareOcean", "#0F6CBD"),
        new AppIconVariant("ornare-forest", "Icon_OrnareForest", "#1F7A4C"),
        new AppIconVariant("ornare-ember", "Icon_OrnareEmber", "#C2410C"),
        new AppIconVariant("ornare-plum", "Icon_OrnarePlum", "#6B21A8"),
        new AppIconVariant("ornare-graphite", "Icon_OrnareGraphite", "#3F3F46"),
        new AppIconVariant("ornare-gold", "Icon_OrnareGold", "#A16207"),
    };

    /// <summary>
    /// Resolves an identifier to a shipped variant, falling back to the default so a
    /// hand-edited settings file can never leave the app without an icon.
    /// </summary>
    public static AppIconVariant Resolve(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            foreach (AppIconVariant variant in All)
            {
                if (string.Equals(variant.Id, id, StringComparison.Ordinal))
                {
                    return variant;
                }
            }
        }

        return All[0];
    }

    /// <summary>True when <paramref name="id"/> names a shipped variant.</summary>
    public static bool IsKnown(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        foreach (AppIconVariant variant in All)
        {
            if (string.Equals(variant.Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
