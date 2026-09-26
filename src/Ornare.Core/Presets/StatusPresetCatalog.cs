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

    // F05 Priority Presets
    public static readonly StatusPreset PriorityHigh = new(
        id: "priority.high",
        version: 1,
        baseHex: "#DC3545",
        badgeAssetId: "ornare_priority_high_24",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_PriorityHigh",
        descriptionResourceKey: "Preset_PriorityHigh_Desc");

    public static readonly StatusPreset PriorityMedium = new(
        id: "priority.medium",
        version: 1,
        baseHex: "#E8A317",
        badgeAssetId: "ornare_priority_medium_24",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_PriorityMedium",
        descriptionResourceKey: "Preset_PriorityMedium_Desc");

    public static readonly StatusPreset PriorityLow = new(
        id: "priority.low",
        version: 1,
        baseHex: "#2563EB",
        badgeAssetId: "ornare_priority_low_24",
        badgeVersion: 1,
        displayNameResourceKey: "Preset_PriorityLow",
        descriptionResourceKey: "Preset_PriorityLow_Desc");

    private static readonly List<StatusPreset> StatusList = [InProgress, Done, Archive, Important];
    private static readonly List<StatusPreset> PriorityList = [PriorityHigh, PriorityMedium, PriorityLow];
    private static readonly List<StatusPreset> PresetList = [InProgress, Done, Archive, Important, PriorityHigh, PriorityMedium, PriorityLow];

    /// <summary>
    /// Gets all current production presets (both workflow status and priority).
    /// </summary>
    public static IReadOnlyList<StatusPreset> All => PresetList;

    /// <summary>
    /// Gets workflow status presets.
    /// </summary>
    public static IReadOnlyList<StatusPreset> StatusPresets => StatusList;

    /// <summary>
    /// Gets visual priority presets (F05).
    /// </summary>
    public static IReadOnlyList<StatusPreset> PriorityPresets => PriorityList;

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
