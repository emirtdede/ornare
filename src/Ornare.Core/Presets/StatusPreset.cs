using System;

namespace Ornare.Core.Presets;

/// <summary>
/// Immutable definition of a semantic work-status preset (F04).
/// Binds a stable preset identity, base color hex, vector badge asset, and localized string keys.
/// </summary>
public sealed record StatusPreset
{
    public string Id { get; }
    public int Version { get; }
    public string BaseHex { get; }
    public string BadgeAssetId { get; }
    public int BadgeVersion { get; }
    public string DisplayNameResourceKey { get; }
    public string DescriptionResourceKey { get; }

    public StatusPreset(
        string id,
        int version,
        string baseHex,
        string badgeAssetId,
        int badgeVersion,
        string displayNameResourceKey,
        string descriptionResourceKey)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Version = version;
        BaseHex = baseHex ?? throw new ArgumentNullException(nameof(baseHex));
        BadgeAssetId = badgeAssetId ?? throw new ArgumentNullException(nameof(badgeAssetId));
        BadgeVersion = badgeVersion;
        DisplayNameResourceKey = displayNameResourceKey ?? throw new ArgumentNullException(nameof(displayNameResourceKey));
        DescriptionResourceKey = descriptionResourceKey ?? throw new ArgumentNullException(nameof(descriptionResourceKey));
    }
}
