using System;
using System.Collections.Generic;

namespace Ornare.Core;

/// <summary>
/// Immutable manifest entry describing a versioned folder template.
/// Conforms to the Section 15 template manifest contract.
/// </summary>
public sealed record FolderTemplateManifestEntry
{
    public string Id { get; init; } = string.Empty;
    public string DisplayNameResourceKey { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public string SvgAssetPath { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool SupportsColor { get; init; } = true;
    public bool SupportsEmblem { get; init; } = true;
    public string ViewBox { get; init; } = "0 0 256 256";
    public NormalizedRect EmblemSafeZone { get; init; } = new(0.25, 0.35, 0.50, 0.50);
    public NormalizedRect[] ReservedBadgeZones { get; init; } = [];
    public string DefaultPalettePolicy { get; init; } = "Oklab5Tone";
    public int RenderPolicyVersion { get; init; } = 1;
}

/// <summary>
/// Root manifest for shipped folder style templates.
/// </summary>
public sealed record FolderTemplateManifest
{
    public int Version { get; init; } = 1;
    public List<FolderTemplateManifestEntry> Templates { get; init; } = [];
}
