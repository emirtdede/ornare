using System;

namespace Ornare.Imaging.Templates;

/// <summary>
/// Immutable descriptor for a versioned folder style template (F03).
/// Allows zero-allocation catalog loading without parsing SVG content at startup.
/// </summary>
public sealed class FolderTemplateDescriptor
{
    private readonly Func<string> _svgLoader;
    private readonly Func<byte[]> _bytesLoader;

    public string Id { get; }
    public int Version { get; }
    public string DisplayNameResourceKey { get; }

    public FolderTemplateDescriptor(
        string id,
        int version,
        string displayNameResourceKey,
        Func<string> svgLoader,
        Func<byte[]> bytesLoader)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Version = version;
        DisplayNameResourceKey = displayNameResourceKey ?? throw new ArgumentNullException(nameof(displayNameResourceKey));
        _svgLoader = svgLoader ?? throw new ArgumentNullException(nameof(svgLoader));
        _bytesLoader = bytesLoader ?? throw new ArgumentNullException(nameof(bytesLoader));
    }

    /// <summary>
    /// Gets the template SVG XML string on demand.
    /// </summary>
    public string SvgContent => _svgLoader();

    /// <summary>
    /// Gets the template UTF-8 encoded bytes on demand.
    /// </summary>
    public byte[] GetBytes() => _bytesLoader();
}
