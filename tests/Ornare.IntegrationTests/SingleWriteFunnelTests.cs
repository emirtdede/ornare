using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Architecture guards for the desktop.ini write path.
/// <para>
/// The pre-deployment audit found two independent atomic writers, and the June 2026 Explorer
/// hardening showed that atomicity alone is not enough for a visible result. These tests fail if
/// a future change reintroduces a second write path or drops the trust normalization, because
/// both defects are invisible in a normal apply-and-look test.
/// </para>
/// </summary>
public class SingleWriteFunnelTests
{
    [Fact]
    public void OnlyOneTypeDeclaresTheAtomicRenamePrimitive()
    {
        string windowsDir = Path.Combine(FindRepositoryRoot(), "src", "Ornare.Windows");
        var offenders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(windowsDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("EntryPoint = \"MoveFileExW\"", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(
            offenders.Count == 1,
            "atomic file commits must funnel through one writer; MoveFileExW is declared in: " +
            string.Join(", ", offenders));
        Assert.Equal("AtomicFileWriter.cs", offenders[0]);
    }

    [Fact]
    public void FolderCustomizerDoesNotWriteDesktopIniDirectly()
    {
        string path = Path.Combine(
            FindRepositoryRoot(), "src", "Ornare.Windows", "FolderCustomizer.cs");
        string text = File.ReadAllText(path);

        // The funnel is the only place allowed to persist config bytes. A direct write here
        // would silently skip the encoding preservation and the Mark of the Web cleanup.
        Assert.DoesNotContain("File.WriteAllBytes", text, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText", text, StringComparison.Ordinal);
        Assert.Contains("DesktopIniFileWriter.WriteMerged(", text, StringComparison.Ordinal);
        Assert.Contains("DesktopIniFileWriter.WriteMergedOrDelete(", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TrustGuardIsConsultedBeforeReportingHealthy()
    {
        string path = Path.Combine(
            FindRepositoryRoot(), "src", "Ornare.Windows", "FolderCustomizer.cs");
        string text = File.ReadAllText(path);

        Assert.Contains("FolderPresentationRisk.MarkOfTheWeb", text, StringComparison.Ordinal);
        Assert.Contains("FolderCustomizationState.ConfigMismatch", text, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "src", "Ornare.Windows");
            if (Directory.Exists(candidate))
            {
                return current;
            }

            current = Path.GetDirectoryName(
                current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException(
            "Could not find src/Ornare.Windows from BaseDirectory: " + AppContext.BaseDirectory);
    }
}
