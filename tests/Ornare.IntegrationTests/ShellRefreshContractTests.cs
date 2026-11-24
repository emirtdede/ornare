using System.IO;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Guards INV-CON-004: a folder icon change invalidates only that folder's cache. A global
/// <c>SHCNE_ASSOCCHANGED</c> rebuilds the entire Shell icon and thumbnail cache, freezing every
/// open Explorer window for hundreds of milliseconds, so it must never appear in the product.
/// </summary>
public class ShellRefreshContractTests
{
    [Fact]
    public void ShellRefreshNeverRequestsGlobalAssociationChange()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Ornare.Windows", "ShellRefreshService.cs"));

        // The identifier and the numeric association-changed flag must not appear in code;
        // a prose comment explaining why is allowed.
        Assert.DoesNotContain("ShcneAssocChanged", source, StringComparison.Ordinal);
        Assert.DoesNotContain("0x08000000", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(SHCNE_ID)0x08000000", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShcneUpdateItem", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, "src", "Ornare.Windows")))
            {
                return current;
            }

            current = Path.GetDirectoryName(
                current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
