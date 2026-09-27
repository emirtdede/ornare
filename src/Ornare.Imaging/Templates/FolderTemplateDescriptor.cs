using System;
using System.Collections.Generic;
using Ornare.Core;

namespace Ornare.Imaging.Templates;

/// <summary>
/// Immutable descriptor for a versioned folder style template (F03).
/// Allows zero-allocation catalog loading without parsing SVG content at startup.
/// Conforms to the Section 15 template manifest contract.
/// </summary>
public sealed class FolderTemplateDescriptor
{
    private readonly Func<string> _svgLoader;
    private readonly Func<byte[]> _bytesLoader;

    public string Id { get; }
    public int Version { get; }
    public string DisplayNameResourceKey { get; }
    public string SvgAssetPath { get; }
    public int SortOrder { get; }
    public bool SupportsColor { get; }
    public bool SupportsEmblem { get; }
    public string ViewBox { get; }
    public NormalizedRect EmblemSafeZone { get; }
    public IReadOnlyList<NormalizedRect> ReservedBadgeZones { get; }
    public string DefaultPalettePolicy { get; }
    public int RenderPolicyVersion { get; }

    public FolderTemplateDescriptor(
        string id,
        int version,
        string displayNameResourceKey,
        Func<string> svgLoader,
        Func<byte[]> bytesLoader,
        string svgAssetPath = "",
        int sortOrder = 0,
        bool supportsColor = true,
        bool supportsEmblem = true,
        string viewBox = "0 0 256 256",
        NormalizedRect? emblemSafeZone = null,
        IReadOnlyList<NormalizedRect>? reservedBadgeZones = null,
        string defaultPalettePolicy = "Oklab5Tone",
        int renderPolicyVersion = 1)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Version = version;
        DisplayNameResourceKey = displayNameResourceKey ?? throw new ArgumentNullException(nameof(displayNameResourceKey));
        _svgLoader = svgLoader ?? throw new ArgumentNullException(nameof(svgLoader));
        _bytesLoader = bytesLoader ?? throw new ArgumentNullException(nameof(bytesLoader));

        SvgAssetPath = svgAssetPath ?? string.Empty;
        SortOrder = sortOrder;
        SupportsColor = supportsColor;
        SupportsEmblem = supportsEmblem;
        ViewBox = string.IsNullOrWhiteSpace(viewBox) ? "0 0 256 256" : viewBox;
        EmblemSafeZone = emblemSafeZone ?? new NormalizedRect(0.26, 0.40, 0.30, 0.30);
        ReservedBadgeZones = reservedBadgeZones ?? [new NormalizedRect(0.58, 0.58, 0.40, 0.40)]; // Bottom-right corner reserved for F04/F05 badges
        DefaultPalettePolicy = string.IsNullOrWhiteSpace(defaultPalettePolicy) ? "Oklab5Tone" : defaultPalettePolicy;
        RenderPolicyVersion = renderPolicyVersion;
    }

    /// <summary>
    /// Gets the template SVG XML string on demand.
    /// </summary>
    public string SvgContent => _svgLoader();

    /// <summary>
    /// Gets the template UTF-8 encoded bytes on demand.
    /// </summary>
    public byte[] GetBytes() => _bytesLoader();

    /// <summary>
    /// Converts this descriptor to a serializable manifest entry.
    /// </summary>
    public FolderTemplateManifestEntry ToManifestEntry() => new()
    {
        Id = Id,
        DisplayNameResourceKey = DisplayNameResourceKey,
        Version = Version,
        SvgAssetPath = SvgAssetPath,
        SortOrder = SortOrder,
        SupportsColor = SupportsColor,
        SupportsEmblem = SupportsEmblem,
        ViewBox = ViewBox,
        EmblemSafeZone = EmblemSafeZone,
        ReservedBadgeZones = [.. ReservedBadgeZones],
        DefaultPalettePolicy = DefaultPalettePolicy,
        RenderPolicyVersion = RenderPolicyVersion
    };
}
