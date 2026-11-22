using System.Reflection;

namespace Ornare.App;

/// <summary>
/// Single source of truth for build/version identity so UI copy never hardcodes a
/// version string that can drift from the assembly manifest.
/// </summary>
internal static class AppInfo
{
    /// <summary>
    /// Semantic version of the running assembly (e.g. <c>1.0.0</c>).
    /// Falls back to the assembly version and finally to a neutral placeholder.
    /// </summary>
    public static string Version { get; } = ResolveVersion();

    /// <summary>Canonical product website, kept out of XAML so it cannot drift.</summary>
    public const string WebsiteUrl = "https://vellium.dev";

    /// <summary>Human-readable site label shown in the UI.</summary>
    public const string WebsiteDisplayName = "vellium.dev";

    /// <summary>Public source repository.</summary>
    public const string RepositoryUrl = "https://github.com/emirtdede/ornare";

    /// <summary>Repository label shown in settings.</summary>
    public const string RepositoryDisplayName = "github.com/emirtdede/ornare";

    /// <summary>Sponsorship page.</summary>
    public const string SponsorUrl = "https://github.com/sponsors/emirtdede";

    /// <summary>Sponsorship label shown in settings.</summary>
    public const string SponsorDisplayName = "GitHub Sponsors";

    /// <summary>
    /// License reference. Points at the file that actually exists in the repository; the
    /// previous target (LICENSE) was a dead link, so the About panel opened a 404.
    /// </summary>
    public const string LicenseUrl = "https://github.com/emirtdede/ornare/blob/main/LICENSE.md";

    /// <summary>
    /// License label shown in settings. Ornare is proprietary, so advertising "MIT" here
    /// contradicted LICENSE.md and the README.
    /// </summary>
    public const string LicenseDisplayName = "Proprietary";

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(AppInfo).Assembly;

        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the source-revision suffix that SDK-style builds append ("1.0.0+abc123").
            int plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        Version? version = assembly.GetName().Version;
        return version is null
            ? "1.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
