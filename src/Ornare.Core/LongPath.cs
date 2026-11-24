using System.IO;

namespace Ornare.Core;

/// <summary>
/// Path handling for folders deeper than <c>MAX_PATH</c>.
/// <para>
/// .NET already prefixes managed <c>System.IO</c> calls with <c>\\?\</c> when a path needs it,
/// but Win32 P/Invoke calls do not: <c>MoveFileExW</c> and <c>DeleteFileW</c> fail with
/// <c>ERROR_PATH_NOT_FOUND</c> for a 300-character path unless the caller supplies the
/// extended prefix. Every native call therefore goes through <see cref="ToExtended"/>.
/// </para>
/// <para>
/// <see cref="MaxSupportedPathLength"/> is the hard ceiling the Windows object manager allows
/// (32,767 wide characters, including the terminating null). It exists to reject pathological
/// input, not to keep the product on <c>MAX_PATH</c>.
/// </para>
/// </summary>
public static class LongPath
{
    public const int MaxSupportedPathLength = 32767;

    private const string ExtendedPrefix = @"\\?\";
    private const string ExtendedUncPrefix = @"\\?\UNC\";

    /// <summary>
    /// Returns <paramref name="path"/> with the extended-length prefix Win32 needs for paths
    /// at or beyond <c>MAX_PATH</c>. Short paths and already-prefixed paths are returned
    /// unchanged, so the helper is safe to call unconditionally.
    /// </summary>
    public static string ToExtended(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (path.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
        {
            return path;
        }

        // Only absolute DOS paths can be extended. A relative path has no meaning to the
        // object manager, and prefixing a device path ("\\.\") would change its target.
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return ExtendedUncPrefix + path[2..];
        }

        // Shorter than MAX_PATH: the plain form is what every API expects, and Explorer shows
        // it verbatim in some error dialogs.
        return path.Length < 260 ? path : ExtendedPrefix + path;
    }

    /// <summary>
    /// True when the path is longer than the legacy <c>MAX_PATH</c> budget. Callers use this
    /// to explain a limitation in user copy instead of surfacing a generic failure.
    /// </summary>
    public static bool IsExtendedRequired(string path) => path.Length >= 260;
}
