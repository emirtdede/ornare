using System;
using System.Collections.Generic;
using System.Linq;

namespace Ornare.Imaging.Templates;

/// <summary>
/// Authoritative catalog of versioned folder style templates (F03).
/// Exposes lazy descriptors without parsing SVG bodies at startup.
/// </summary>
public static class FolderTemplateCatalog
{
    public const string DefaultTemplateId = OrnareClassicTemplate.TemplateId;
    public const int DefaultTemplateVersion = OrnareClassicTemplate.TemplateVersion;

    private static readonly FolderTemplateDescriptor ClassicV1 = new(
        OrnareClassicTemplate.TemplateId,
        OrnareClassicTemplate.TemplateVersion,
        "Template_Classic",
        () => OrnareClassicTemplate.SvgContent,
        OrnareClassicTemplate.GetTemplateBytes);

    private static readonly FolderTemplateDescriptor SoftV1 = new(
        OrnareSoftTemplate.TemplateId,
        OrnareSoftTemplate.TemplateVersion,
        "Template_Soft",
        () => OrnareSoftTemplate.SvgContent,
        OrnareSoftTemplate.GetTemplateBytes);

    private static readonly FolderTemplateDescriptor FlatV1 = new(
        OrnareFlatTemplate.TemplateId,
        OrnareFlatTemplate.TemplateVersion,
        "Template_Flat",
        () => OrnareFlatTemplate.SvgContent,
        OrnareFlatTemplate.GetTemplateBytes);

    private static readonly List<FolderTemplateDescriptor> TemplateList = [ClassicV1, SoftV1, FlatV1];

    /// <summary>
    /// Gets all current production templates in the catalog.
    /// </summary>
    public static IReadOnlyList<FolderTemplateDescriptor> All => TemplateList;

    /// <summary>
    /// Gets the default template (ornare-classic v1).
    /// </summary>
    public static FolderTemplateDescriptor DefaultTemplate => ClassicV1;

    /// <summary>
    /// Resolves a template by identifier and version. Falls back to version 1 or default if not found.
    /// </summary>
    public static FolderTemplateDescriptor GetTemplate(string? id, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return DefaultTemplate;
        }

        var match = TemplateList.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase) && t.Version == version);

        if (match != null)
        {
            return match;
        }

        // Fallback to highest available version for this id
        var idMatch = TemplateList.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        return idMatch ?? DefaultTemplate;
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

        descriptor = TemplateList.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase) && t.Version == version);

        return descriptor != null;
    }
}
