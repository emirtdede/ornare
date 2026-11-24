using System;

namespace Ornare.Core;

/// <summary>
/// Decides whether the language chosen during setup may seed the first run.
/// <para>
/// The installer writes the chosen BCP-47 tag plus the UTC instant it was written. Onboarding
/// reads the pair once and deletes it. Keeping the decision here, as a pure function, means the
/// rule can be tested without a registry and without a WinUI process.
/// </para>
/// <para>
/// This exists because a Turkish machine opened the first-run wizard in German: a stale
/// <c>InstallerLanguage</c> value from an earlier installation was trusted forever. Freshness
/// is the discriminator, not presence.
/// </para>
/// </summary>
public static class InstallerLanguagePolicy
{
    /// <summary>
    /// How long a setup language tag stays authoritative. Setup and first launch normally sit
    /// seconds apart; anything older is residue from a previous installation.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    /// <summary>
    /// Clock skew allowance for the "written in the future" rejection. A machine with a badly
    /// set clock should not be treated as hostile, but an implausible timestamp must not make a
    /// value permanently fresh either.
    /// </summary>
    public static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether the setup language should replace the current setting.
    /// </summary>
    /// <param name="onboardingCompleted">True once the welcome flow has run; the choice then belongs to the user.</param>
    /// <param name="currentLanguage">The stored language; <see cref="UserSettings.SystemValue"/> means "not chosen yet".</param>
    /// <param name="tag">The BCP-47 tag the installer wrote, or null when there is none.</param>
    /// <param name="writtenAt">The UTC instant the installer wrote, or null when unreadable.</param>
    /// <param name="now">Current UTC time, injected so the rule is testable.</param>
    public static bool ShouldApply(
        bool onboardingCompleted,
        string? currentLanguage,
        string? tag,
        DateTimeOffset? writtenAt,
        DateTimeOffset now)
    {
        if (onboardingCompleted)
        {
            return false;
        }

        if (!string.Equals(currentLanguage, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        if (writtenAt is null)
        {
            // No timestamp means no way to prove the tag is from this installation.
            return false;
        }

        if (now < writtenAt.Value - ClockSkewAllowance)
        {
            return false;
        }

        return now - writtenAt.Value <= MaxAge;
    }
}
