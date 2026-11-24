using System;
using System.IO;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

/// <summary>
/// Audit finding 2.1. A Turkish installation opened the first-run wizard in German because the
/// installer language was read from the registry without any notion of when it was written, so a
/// leftover from an earlier install won forever.
/// </summary>
public sealed class InstallerLanguagePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FreshSetupChoiceIsApplied()
    {
        Assert.True(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: false,
            currentLanguage: UserSettings.SystemValue,
            tag: "de-DE",
            writtenAt: Now.AddMinutes(-2),
            now: Now));
    }

    [Fact]
    public void StaleLeftoverIsRejected()
    {
        // The Turkish machine's registry value was months old, not seconds old.
        Assert.False(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: false,
            currentLanguage: UserSettings.SystemValue,
            tag: "de-DE",
            writtenAt: Now.AddDays(-120),
            now: Now));
    }

    [Fact]
    public void MissingTimestampIsRejected()
    {
        // A tag written by an older installer has no timestamp, so it cannot be proven recent.
        Assert.False(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: false,
            currentLanguage: UserSettings.SystemValue,
            tag: "de-DE",
            writtenAt: null,
            now: Now));
    }

    [Fact]
    public void CompletedOnboardingAlwaysWins()
    {
        Assert.False(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: true,
            currentLanguage: UserSettings.SystemValue,
            tag: "de-DE",
            writtenAt: Now.AddMinutes(-1),
            now: Now));
    }

    [Fact]
    public void DeliberateLanguageChoiceIsNeverOverwritten()
    {
        Assert.False(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: false,
            currentLanguage: "tr-TR",
            tag: "de-DE",
            writtenAt: Now.AddMinutes(-1),
            now: Now));
    }

    [Fact]
    public void ImplausibleFutureTimestampIsRejected()
    {
        Assert.False(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: false,
            currentLanguage: UserSettings.SystemValue,
            tag: "de-DE",
            writtenAt: Now.AddDays(30),
            now: Now));
    }

    [Fact]
    public void SmallClockSkewIsTolerated()
    {
        Assert.True(InstallerLanguagePolicy.ShouldApply(
            onboardingCompleted: false,
            currentLanguage: UserSettings.SystemValue,
            tag: "de-DE",
            writtenAt: Now.AddMinutes(1),
            now: Now));
    }

    /// <summary>
    /// The installer must write the timestamp next to the tag, and the app must delete both.
    /// Without the pair the freshness rule has nothing to read, which is how the defect existed.
    /// </summary>
    [Fact]
    public void InstallerWritesAndAppConsumesTheWholePair()
    {
        string root = FindRepositoryRoot();
        string installer = File.ReadAllText(Path.Combine(root, "src", "Ornare.Installer", "main.cpp"));
        string preference = File.ReadAllText(Path.Combine(root, "src", "Ornare.App", "InstallerLanguagePreference.cs"));

        Assert.Contains("InstallerLanguageWrittenUtc", installer);
        Assert.Contains("GetSystemTime(&utc)", installer);

        Assert.Contains("InstallerLanguageWrittenUtc", preference);
        Assert.Contains("key.DeleteValue(ValueName, throwOnMissingValue: false);", preference);
        Assert.Contains("key.DeleteValue(WrittenValueName, throwOnMissingValue: false);", preference);
        Assert.Contains("InstallerLanguagePolicy.ShouldApply(", preference);
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Ornare.slnx")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException("Could not find the repository root from " + AppContext.BaseDirectory);
    }
}
