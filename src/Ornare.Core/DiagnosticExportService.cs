namespace Ornare.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public record DiagnosticReport(
    string OrnareVersion,
    string OsVersion,
    string ProcessArchitecture,
    string OsArchitecture,
    string DotNetVersion,
    bool IsPackaged,
    string CurrentLanguage,
    string CurrentTheme,
    int TotalCustomizedFolders,
    Dictionary<string, int> HealthStatusCounts,
    int TotalLibraryAssets,
    int TotalHistoryEntries,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<string> RedactedRecentErrors
);

public static class DiagnosticExportService
{
    private static readonly Regex UserPathRegex = new(
        @"(?<=[A-Za-z]:[\\/](?:Users|home)[\\/])[^\\/\r\n]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RedactUserPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return UserPathRegex.Replace(path, "<REDACTED_USER>");
    }

    public static DiagnosticReport BuildReport(
        UserSettings settings,
        int totalFolders,
        Dictionary<string, int> healthCounts,
        int totalAssets,
        int totalHistory,
        IReadOnlyList<string>? recentErrors = null)
    {
        bool isPackaged = false;
        try
        {
            // Detect packaged identity if available
            isPackaged = AppContext.BaseDirectory.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Default false
        }

        var redactedErrors = new List<string>();
        if (recentErrors != null)
        {
            foreach (var err in recentErrors)
            {
                redactedErrors.Add(RedactUserPath(err));
            }
        }

        return new DiagnosticReport(
            OrnareVersion: "1.0.0",
            OsVersion: Environment.OSVersion.VersionString,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            DotNetVersion: Environment.Version.ToString(),
            IsPackaged: isPackaged,
            CurrentLanguage: settings.Language,
            CurrentTheme: settings.Theme,
            TotalCustomizedFolders: totalFolders,
            HealthStatusCounts: healthCounts,
            TotalLibraryAssets: totalAssets,
            TotalHistoryEntries: totalHistory,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            RedactedRecentErrors: redactedErrors
        );
    }
}
