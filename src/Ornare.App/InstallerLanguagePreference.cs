using System.Globalization;
using Microsoft.Win32;
using Ornare.Core;

namespace Ornare.App;

/// <summary>
/// Carries the setup language choice into the first launch. The installer writes the chosen
/// BCP-47 tag, plus the UTC instant it was written, under the product key. Onboarding reads the
/// pair once so a user who installed in German is not sent back to whatever language setup
/// detected on the machine.
/// <para>
/// The pair is consumed: both values are deleted after the first read. A stale tag left by an
/// earlier installation must never seed a later one, which is exactly how a Turkish machine once
/// opened the first-run wizard in German. The decision rule itself lives in
/// <see cref="InstallerLanguagePolicy"/> so it can be tested without a registry.
/// </para>
/// </summary>
internal static class InstallerLanguagePreference
{
    private const string KeyPath = OrnareConstants.ProductRegistryKeyPath;
    private const string ValueName = "InstallerLanguage";
    private const string WrittenValueName = "InstallerLanguageWrittenUtc";

    /// <summary>
    /// Applies the setup language when onboarding has not run yet and no language has been
    /// chosen. Every other state is returned unchanged, and the stored value is consumed either
    /// way so a rejected leftover cannot resurface.
    /// </summary>
    public static UserSettings Apply(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.OnboardingCompleted)
        {
            return settings;
        }

        (string? tag, DateTimeOffset? writtenAt) = ReadAndConsume();

        if (!InstallerLanguagePolicy.ShouldApply(
                settings.OnboardingCompleted,
                settings.Language,
                tag,
                writtenAt,
                DateTimeOffset.UtcNow))
        {
            return settings;
        }

        return settings with { Language = tag! };
    }

    /// <summary>
    /// Reads both values and deletes them in the same pass, so the preference is spent exactly
    /// once. A read that fails is not deleted, leaving the next launch a chance to try again.
    /// </summary>
    private static (string? Tag, DateTimeOffset? WrittenAt) ReadAndConsume()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string raw || string.IsNullOrWhiteSpace(raw))
            {
                return (null, null);
            }

            string? writtenRaw = key.GetValue(WrittenValueName) as string;

            // Resolve through the catalogue, so a stale or hand-edited value can never select a
            // locale the application does not ship.
            string resolved = LocaleCatalog.Resolve(raw);

            DateTimeOffset? writtenAt = DateTimeOffset.TryParse(
                writtenRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed)
                ? parsed
                : null;

            key.DeleteValue(ValueName, throwOnMissingValue: false);
            key.DeleteValue(WrittenValueName, throwOnMissingValue: false);

            return (resolved, writtenAt);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // A locked-down registry is not a reason to fail first launch.
            return (null, null);
        }
    }
}
