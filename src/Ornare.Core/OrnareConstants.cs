namespace Ornare.Core;

public static class OrnareConstants
{
    public const string ApplicationName = "Ornare";
    public const string PackageFamilyName = "Ornare.FolderIcons";
    public const string DesktopIniFileName = "desktop.ini";
    public const string CustomIconFileName = "folder.ico";
    public const string CanonicalFolderName = ".ornare";

    /// <summary>Library sub-directory holding canonical, content-addressed ICO files.</summary>
    public const string LibraryDirectoryName = "library";

    /// <summary>Sub-directory holding imported/canonical source icons.</summary>
    public const string IconsDirectoryName = "icons";

    /// <summary>Sub-directory holding generated render-cache artefacts.</summary>
    public const string CacheDirectoryName = "cache";

    /// <summary>Sub-directory holding opt-in diagnostic-session logs.</summary>
    public const string DiagnosticsDirectoryName = "diagnostics";

    /// <summary>Temporary directory for preset staging ICO files.</summary>
    public const string PresetTempDirectoryName = "OrnarePresets";

    /// <summary>Root of the per-user application data (LocalAppData\\Ornare).</summary>
    public static string AppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationName);

    /// <summary>Full path to the content-addressed library directory.</summary>
    public static string LibraryDirectory => Path.Combine(AppDataRoot, LibraryDirectoryName);

    /// <summary>Full path to the canonical source-icon directory.</summary>
    public static string IconsDirectory => Path.Combine(AppDataRoot, IconsDirectoryName);

    /// <summary>Full path to the render-cache directory.</summary>
    public static string CacheDirectory => Path.Combine(AppDataRoot, CacheDirectoryName);

    /// <summary>Full path to the opt-in diagnostic session directory.</summary>
    public static string DiagnosticsDirectory => Path.Combine(AppDataRoot, DiagnosticsDirectoryName);

    /// <summary>
    /// Absolute path of the content-addressed canonical ICO for a render hash.
    /// Centralised so callers cannot drift to a different library layout.
    /// </summary>
    public static string LibraryIcoPath(string contentHash) =>
        Path.Combine(LibraryDirectory, $"{contentHash}.ico");
}
