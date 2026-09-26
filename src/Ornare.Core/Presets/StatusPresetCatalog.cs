using System;
using System.Collections.Generic;
using System.Linq;

namespace Ornare.Core.Presets;

/// <summary>
/// Authoritative catalog of standard work-status presets (F04).
/// Contains exactly four release-approved status presets using MIT Fluent UI System Icons badges.
/// </summary>
public static class StatusPresetCatalog
{
    public static readonly StatusPreset InProgress = new(
        id: "status.in-progress",
        version: 1,
        baseHex: "#2563EB",
        badgeAssetId: "ic_fluent_clock_24_regular",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_InProgress",
        descriptionResourceKey: "Preset_InProgress_Desc");

    public static readonly StatusPreset Done = new(
        id: "status.done",
        version: 1,
        baseHex: "#16A34A",
        badgeAssetId: "ic_fluent_checkmark_circle_24_regular",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_Done",
        descriptionResourceKey: "Preset_Done_Desc");

    public static readonly StatusPreset Archive = new(
        id: "status.archive",
        version: 1,
        baseHex: "#64748B",
        badgeAssetId: "ic_fluent_archive_24_regular",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_Archive",
        descriptionResourceKey: "Preset_Archive_Desc");

    public static readonly StatusPreset Important = new(
        id: "status.important",
        version: 1,
        baseHex: "#DC3545",
        badgeAssetId: "ic_fluent_important_24_regular",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_Important",
        descriptionResourceKey: "Preset_Important_Desc");

    private static readonly List<StatusPreset> PresetList = [InProgress, Done, Archive, Important];

    /// <summary>
    /// Gets all current production work-status presets.
    /// </summary>
    public static IReadOnlyList<StatusPreset> All => PresetList;

    /// <summary>
    /// Resolves a status preset by identifier and version.
    /// </summary>
    public static StatusPreset? GetPreset(string? id, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return PresetList.FirstOrDefault(p =>
            string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase) && p.Version == version);
    }

    /// <summary>
    /// Tries to resolve a status preset by identifier and version.
    /// </summary>
    public static bool TryGetPreset(string? id, int version, out StatusPreset? preset)
    {
        preset = GetPreset(id, version);
        return preset != null;
    }
}
