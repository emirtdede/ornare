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
        Assert.Equal(@"/home/<REDACTED_USER>/documents/secret.png", redactedUnix);
    }

    [Theory]
    [InlineData(@"C:\Users\Şevket Çetin\Documents\secret.png", "Şevket Çetin")]
    [InlineData(@"C:\Users\Ömer Faruk\Desktop\Folder", "Ömer Faruk")]
    [InlineData(@"C:\Users\İpek.Aydın\Pictures\test.ico", "İpek.Aydın")]
    [InlineData(@"C:\Users\user with spaces and (special) [chars]\Data", "user with spaces and (special) [chars]")]
    [InlineData(@"D:\Users\nested.user.name\sub\folder\path\deep\file.txt", "nested.user.name")]
    public void RedactUserPathAdversarialPathsMaskPersonalIdentifiers(string inputPath, string sensitiveSegment)
    {
        string redacted = DiagnosticExportService.RedactUserPath(inputPath);
        Assert.DoesNotContain(sensitiveSegment, redacted, StringComparison.Ordinal);
        Assert.Contains("<REDACTED_USER>", redacted, StringComparison.Ordinal);
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
