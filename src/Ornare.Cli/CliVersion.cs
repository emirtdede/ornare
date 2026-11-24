using System;
using System.Reflection;

namespace Ornare.Cli;

/// <summary>
/// Reports the version of the CLI an automation script is talking to.
/// <para>
/// The value is read from the assembly's informational version, so it follows the product
/// version declared in the build instead of drifting as a hand-maintained string. The command
/// schema version is printed too, because that is what a script should actually branch on:
/// the product version changes with every release, the schema only when the JSON contract does.
/// </para>
/// </summary>
internal static class CliVersion
{
    /// <summary>Version of the machine-readable output contract.</summary>
    public const int SchemaVersion = 1;

    public static string Describe()
    {
        string product = ProductVersion();
        return $"ornare {product} (schema {SchemaVersion})";
    }

    private static string ProductVersion()
    {
        Assembly assembly = typeof(CliVersion).Assembly;

        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // The SDK appends "+<commit sha>" when SourceLink metadata is present. A version
            // string a user reads should not carry a build hash.
            int plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        Version? version = assembly.GetName().Version;
        return version is null
            ? "unknown"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
