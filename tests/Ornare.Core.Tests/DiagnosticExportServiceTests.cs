namespace Ornare.Core.Tests;

using System;
using System.Collections.Generic;
using Ornare.Core;
using Xunit;

public class DiagnosticExportServiceTests
{
    [Fact]
    public void RedactUserPathMasksUsernameInWindowsAndUnixPaths()
    {
        string winPath = @"C:\Users\johndoe\Desktop\Projects\TestFolder";
        string redactedWin = DiagnosticExportService.RedactUserPath(winPath);
        Assert.Equal(@"C:\Users\<REDACTED_USER>\Desktop\Projects\TestFolder", redactedWin);

        string unixPath = @"/home/alice/documents/secret.png";
        string redactedUnix = DiagnosticExportService.RedactUserPath(unixPath);
        Assert.DoesNotContain("johndoe", redactedWin, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildReportProducesCorrectMetadataWithoutSecrets()
    {
        var settings = new UserSettings { Theme = "Dark", Language = "tr-TR" };
        var healthCounts = new Dictionary<string, int>
        {
            ["Healthy"] = 12,
            ["NeedsAttention"] = 1
        };
        var errors = new List<string>
        {
            @"Error opening C:\Users\emir\Folder\test.ico: Access denied"
        };

        var report = DiagnosticExportService.BuildReport(settings, 13, healthCounts, 5, 20, errors);

        Assert.Equal("1.0.0", report.OrnareVersion);
        Assert.Equal("tr-TR", report.CurrentLanguage);
        Assert.Equal("Dark", report.CurrentTheme);
        Assert.Equal(13, report.TotalCustomizedFolders);
        Assert.Equal(12, report.HealthStatusCounts["Healthy"]);
        Assert.Equal(1, report.HealthStatusCounts["NeedsAttention"]);
        Assert.Single(report.RedactedRecentErrors);
        Assert.DoesNotContain("emir", report.RedactedRecentErrors[0], StringComparison.Ordinal);
        Assert.Contains("<REDACTED_USER>", report.RedactedRecentErrors[0], StringComparison.Ordinal);
    }
}
