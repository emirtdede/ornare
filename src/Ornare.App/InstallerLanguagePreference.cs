using Microsoft.Win32;
using Ornare.Core;

namespace Ornare.App;

/// <summary>
/// Carries the setup language choice into the first launch. The installer writes the chosen
/// BCP-47 tag under the product key, and onboarding reads it once so a user who installed in
/// German is not sent back to whatever language setup detected on the machine.
/// </summary>
internal static class InstallerLanguagePreference
{
    private const string KeyPath = @"Software\Vellium\Ornare";
    private const string ValueName = "InstallerLanguage";

    /// <summary>
    /// Applies the setup language when onboarding has not run yet and no language has been
    /// chosen. Every other state is returned unchanged.
    /// </summary>
    public static UserSettings Apply(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.OnboardingCompleted
            || !string.Equals(settings.Language, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            return settings;
        }

        string? tag = ReadTag();
        return tag is null ? settings : settings with { Language = tag };
    }

    private static string? ReadTag()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(ValueName) is not string raw || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            // Resolve through the catalogue, so a stale or hand-edited value can never select a
            // locale the application does not ship.
            return LocaleCatalog.Resolve(raw);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // A locked-down registry is not a reason to fail first launch.
            return null;
        }
    }
}
