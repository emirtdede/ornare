using System;
using System.Collections.Generic;

namespace Ornare.Core;

public sealed record FolderColorEntry(string Id, string HexValue, string ResourceKey);

/// <summary>
/// Authoritative catalog of the 12 curated folder colors specified in F01 Fixed Decision 2.
/// IDs and hex values are stored once in neutral data; localized display names are provided via resource strings.
/// </summary>
public static class BuiltInColors
{
    public static readonly IReadOnlyList<FolderColorEntry> Entries =
    [
        new("color.amber", "#E8A317", "Color_Amber"),
        new("color.orange", "#F97316", "Color_Orange"),
        new("color.red", "#DC3545", "Color_Red"),
        new("color.rose", "#E11D48", "Color_Rose"),
        new("color.magenta", "#C026D3", "Color_Magenta"),
        new("color.purple", "#7C3AED", "Color_Purple"),
        new("color.indigo", "#4F46E5", "Color_Indigo"),
        new("color.blue", "#2563EB", "Color_Blue"),
        new("color.cyan", "#0891B2", "Color_Cyan"),
        new("color.teal", "#0F766E", "Color_Teal"),
        new("color.green", "#16A34A", "Color_Green"),
        new("color.slate", "#64748B", "Color_Slate")
    ];

    public static void ValidateCatalog()
    {
        var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hexSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
        {
            if (!idSet.Add(entry.Id))
            {
                throw new InvalidOperationException($"Duplicate color ID detected in catalog: '{entry.Id}'");
            }
            if (!hexSet.Add(entry.HexValue))
            {
                throw new InvalidOperationException($"Duplicate color hex value detected in catalog: '{entry.HexValue}'");
            }
        }
    }
}
