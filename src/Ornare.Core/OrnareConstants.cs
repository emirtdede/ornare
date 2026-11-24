namespace Ornare.Core;

public static class OrnareConstants
{
    public const string ApplicationName = "Ornare";

    /// <summary>
    /// Real package family name of the published MSIX. It is <c>Name_PublisherHash</c> as
    /// declared in <c>Package.appxmanifest</c>; the CLI uses it to find the installed package's
    /// redirected data root.
    /// </summary>
    public const string PackageFamilyName = "Ornare_vs3ntcdctdza8";
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

    /// <summary>
    /// Per-user registry key (HKCU) the installer writes the setup language to. Must stay in
    /// sync with <c>kProductRegistryKeyPath</c> in <c>src/Ornare.Installer/main.cpp</c>.
    /// </summary>
    public const string ProductRegistryKeyPath = @"Software\Vellium\Ornare";

    /// <summary>
    /// Environment variable an unpackaged build or a test harness sets to point the whole
    /// process at a single data root. Without it a raw <c>Ornare.Cli.exe</c> run outside the
    /// package writes <c>%LOCALAPPDATA%\Ornare</c> while the packaged app writes
    /// <c>...\Packages\Ornare_...\LocalCache\Local\Ornare</c>, producing two databases and two
    /// "truths" for the same folder.
    /// </summary>
    public const string DataRootOverrideVariable = "ORNARE_DATA_ROOT";

    /// <summary>Root of the per-user application data (LocalAppData\\Ornare).</summary>
    public static string AppDataRoot
    {
        get
        {
            string? overrideRoot = Environment.GetEnvironmentVariable(DataRootOverrideVariable);
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return overrideRoot;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ApplicationName);
        }
    }

    /// <summary>Full path to the content-addressed library directory.</summary>
    public static string LibraryDirectory => Path.Combine(AppDataRoot, LibraryDirectoryName);

    /// <summary>Full path to the canonical source-icon directory.</summary>
    public static string IconsDirectory => Path.Combine(AppDataRoot, IconsDirectoryName);

    /// <summary>Full path to the render-cache directory.</summary>
    public static string CacheDirectory => Path.Combine(AppDataRoot, CacheDirectoryName);

    /// <summary>Full path to the opt-in diagnostic session directory.</summary>
    public static string DiagnosticsDirectory => Path.Combine(AppDataRoot, DiagnosticsDirectoryName);

    /// <summary>Sub-directory holding transient UI artefacts (preview thumbnails, staged ICOs).</summary>
    public const string TempDirectoryName = "temp";

    /// <summary>
    /// Largest source image accepted for normalisation, in bytes. The Magick pipeline caps
    /// decoded pixels and its own memory, but the panel copies the compressed file into a
    /// single <c>byte[]</c> before that policy can run, so the read itself needs a bound.
    /// 64 MB comfortably covers a 48 megapixel photo and rejects anything that would push
    /// the large object heap into an unrecoverable state on a small machine.
    /// </summary>
    public const long MaxInputImageBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Per-user scratch directory for transient panel artefacts. Keeping these files out of
    /// the root of <c>%TEMP%</c> prevents them from colliding with, or being mistaken for,
    /// another process's files and makes cleanup a single directory delete.
    /// </summary>
    public static string TempDirectory => Path.Combine(AppDataRoot, TempDirectoryName);

    /// <summary>
    /// Absolute path of the content-addressed canonical ICO for a render hash.
    /// Centralised so callers cannot drift to a different library layout.
    /// </summary>
    public static string LibraryIcoPath(string contentHash) =>
        Path.Combine(LibraryDirectory, $"{contentHash}.ico");
}
