using System;
using System.Collections.Generic;

namespace Ornare.Core;

public sealed record UserSettings
{
    public const int MaxRecentCustomColors = 8;

    public string Theme { get; init; } = "System"; // System, Light, Dark
    public string Language { get; init; } = "System"; // System, tr-TR, en-US
    public IReadOnlyList<string> RecentCustomColors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Adds a custom color to the MRU list. Normalizes to uppercase 6-char hex, deduplicates by exact 8-bit RGB, and caps at MaxRecentCustomColors (8).
    /// </summary>
    public UserSettings WithRecentCustomColor(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return this;
        }

        string normalized = hexColor.Trim().ToUpperInvariant();
        if (!normalized.StartsWith('#'))
        {
            normalized = "#" + normalized;
        }

        if (normalized.Length != 7)
        {
            return this;
        }

        var list = new List<string> { normalized };
        foreach (var color in RecentCustomColors)
        {
            string existing = color.Trim().ToUpperInvariant();
            if (!existing.StartsWith('#'))
            {
                existing = "#" + existing;
            }

            if (!string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
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
}
