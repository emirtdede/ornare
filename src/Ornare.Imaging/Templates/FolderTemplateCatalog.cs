using System;
using System.Collections.Generic;
using System.Linq;
using Ornare.Core;

namespace Ornare.Imaging.Templates;

/// <summary>
/// Authoritative catalog of versioned folder style templates (F03/Section 7).
/// Exposes lazy descriptors for the exact nine production templates without parsing SVG bodies at startup.
/// Preserves legacy V1 descriptors for backward compatibility with existing assignments.
/// </summary>
public static class FolderTemplateCatalog
{
    public const string DefaultTemplateId = OrnareClassicTemplate.TemplateId;
    public const int DefaultTemplateVersion = OrnareClassicTemplate.TemplateVersion; // 2

    private static readonly NormalizedRect DefaultEmblemSafeZone = new(0.26, 0.40, 0.30, 0.30);
    private static readonly NormalizedRect[] DefaultBadgeZones = [new(0.58, 0.58, 0.40, 0.40)];

    // 1. Classic (v2)
    private static readonly FolderTemplateDescriptor ClassicV2 = new(
        OrnareClassicTemplate.TemplateId,
        OrnareClassicTemplate.TemplateVersion,
        "Template_Classic",
        () => OrnareClassicTemplate.SvgContent,
        OrnareClassicTemplate.GetTemplateBytes,
        "assets/folders/ornare-classic.svg",
        sortOrder: 1,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 2. Soft (v2)
    private static readonly FolderTemplateDescriptor SoftV2 = new(
        OrnareSoftTemplate.TemplateId,
        OrnareSoftTemplate.TemplateVersion,
        "Template_Soft",
        () => OrnareSoftTemplate.SvgContent,
        OrnareSoftTemplate.GetTemplateBytes,
        "assets/folders/ornare-soft.svg",
        sortOrder: 2,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 3. Flat (v2)
    private static readonly FolderTemplateDescriptor FlatV2 = new(
        OrnareFlatTemplate.TemplateId,
        OrnareFlatTemplate.TemplateVersion,
        "Template_Flat",
        () => OrnareFlatTemplate.SvgContent,
        OrnareFlatTemplate.GetTemplateBytes,
        "assets/folders/ornare-flat.svg",
        sortOrder: 3,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 4. Glass (v1)
    private static readonly FolderTemplateDescriptor GlassV1 = new(
        OrnareGlassTemplate.TemplateId,
        OrnareGlassTemplate.TemplateVersion,
        "Template_Glass",
        () => OrnareGlassTemplate.SvgContent,
        OrnareGlassTemplate.GetTemplateBytes,
        "assets/folders/ornare-glass.svg",
        sortOrder: 4,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 5. Aqua (v1)
    private static readonly FolderTemplateDescriptor AquaV1 = new(
        OrnareAquaTemplate.TemplateId,
        OrnareAquaTemplate.TemplateVersion,
        "Template_Aqua",
        () => OrnareAquaTemplate.SvgContent,
        OrnareAquaTemplate.GetTemplateBytes,
        "assets/folders/ornare-aqua.svg",
        sortOrder: 5,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 6. Grove (v1)
    private static readonly FolderTemplateDescriptor GroveV1 = new(
        OrnareGroveTemplate.TemplateId,
        OrnareGroveTemplate.TemplateVersion,
        "Template_Grove",
        () => OrnareGroveTemplate.SvgContent,
        OrnareGroveTemplate.GetTemplateBytes,
        "assets/folders/ornare-grove.svg",
        sortOrder: 6,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 7. Air (v1)
    private static readonly FolderTemplateDescriptor AirV1 = new(
        OrnareAirTemplate.TemplateId,
        OrnareAirTemplate.TemplateVersion,
        "Template_Air",
        () => OrnareAirTemplate.SvgContent,
        OrnareAirTemplate.GetTemplateBytes,
        "assets/folders/ornare-air.svg",
        sortOrder: 7,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 8. Flow (v1)
    private static readonly FolderTemplateDescriptor FlowV1 = new(
        OrnareFlowTemplate.TemplateId,
        OrnareFlowTemplate.TemplateVersion,
        "Template_Flow",
        () => OrnareFlowTemplate.SvgContent,
        OrnareFlowTemplate.GetTemplateBytes,
        "assets/folders/ornare-flow.svg",
        sortOrder: 8,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // 9. Geo (v1)
    private static readonly FolderTemplateDescriptor GeoV1 = new(
        OrnareGeoTemplate.TemplateId,
        OrnareGeoTemplate.TemplateVersion,
        "Template_Geo",
        () => OrnareGeoTemplate.SvgContent,
        OrnareGeoTemplate.GetTemplateBytes,
        "assets/folders/ornare-geo.svg",
        sortOrder: 9,
        emblemSafeZone: DefaultEmblemSafeZone,
        reservedBadgeZones: DefaultBadgeZones);

    // Legacy V1 descriptors for backward compatibility
    private static readonly FolderTemplateDescriptor ClassicV1 = new(
        OrnareClassicTemplate.TemplateId,
        1,
        "Template_Classic",
        () => OrnareClassicTemplate.LegacyV1SvgContent,
        OrnareClassicTemplate.GetLegacyV1TemplateBytes,
        sortOrder: 101);

    private static readonly FolderTemplateDescriptor SoftV1 = new(
        OrnareSoftTemplate.TemplateId,
        1,
        "Template_Soft",
        () => OrnareSoftTemplate.LegacyV1SvgContent,
        OrnareSoftTemplate.GetLegacyV1TemplateBytes,
        sortOrder: 102);

    private static readonly FolderTemplateDescriptor FlatV1 = new(
        OrnareFlatTemplate.TemplateId,
        1,
        "Template_Flat",
        () => OrnareFlatTemplate.LegacyV1SvgContent,
        OrnareFlatTemplate.GetLegacyV1TemplateBytes,
        sortOrder: 103);

    // The exactly nine production templates in sort order
    private static readonly List<FolderTemplateDescriptor> ProductionTemplates =
    [
        ClassicV2, SoftV2, FlatV2, GlassV1, AquaV1, GroveV1, AirV1, FlowV1, GeoV1
    ];

    // All available descriptors including legacy versions for resolution
    private static readonly List<FolderTemplateDescriptor> AllResolutionTemplates =
    [
        ClassicV2, SoftV2, FlatV2, GlassV1, AquaV1, GroveV1, AirV1, FlowV1, GeoV1,
        ClassicV1, SoftV1, FlatV1
    ];

    /// <summary>
    /// Gets all current production templates in the catalog (exactly nine templates).
    /// </summary>
    public static IReadOnlyList<FolderTemplateDescriptor> All => ProductionTemplates;

    /// <summary>
    /// Gets the default template (ornare-classic v2).
    /// </summary>
    public static FolderTemplateDescriptor DefaultTemplate => ClassicV2;

    /// <summary>
    /// Resolves a template by identifier and version.
    /// If version is 0 or unassigned, returns the current production template.
    /// Falls back to highest available version or default if not found.
    /// </summary>
    public static FolderTemplateDescriptor GetTemplate(string? id, int version = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return DefaultTemplate;
        }

        if (version > 0)
        {
            var exactMatch = AllResolutionTemplates.FirstOrDefault(t =>
                string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase) && t.Version == version);
            if (exactMatch != null)
            {
                return exactMatch;
            }
        }

        // Return latest production template for this id
        var prodMatch = ProductionTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        if (prodMatch != null)
        {
            return prodMatch;
        }

        // Fallback to any matching template
        var anyMatch = AllResolutionTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        return anyMatch ?? DefaultTemplate;
    }

    /// <summary>
    /// Attempts to find a matching template by identifier and version.
    /// </summary>
    public static bool TryGetTemplate(string? id, int version, out FolderTemplateDescriptor? descriptor)
    {
        descriptor = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (version > 0)
        {
            descriptor = AllResolutionTemplates.FirstOrDefault(t =>
                string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase) && t.Version == version);
            if (descriptor != null)
            {
                return true;
            }
        }

        descriptor = ProductionTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        return descriptor != null;
    }
}
