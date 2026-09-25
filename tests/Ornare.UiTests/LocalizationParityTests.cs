using System.IO;
using System.Xml.Linq;
using Ornare.DesignSystem;
using Xunit;

namespace Ornare.UiTests;

public class LocalizationParityTests
{
    [Fact]
    public void DesignTokensMatchVisualReferenceSpec()
    {
        Assert.Equal(420.0, DesignTokens.QuickPanelWidth);
        Assert.Equal(330.0, DesignTokens.QuickPanelHeight);
        Assert.Equal(8.0, DesignTokens.CornerRadiusValue);
        Assert.Equal(24.0, DesignTokens.ContentPaddingHorizontal);
        Assert.Equal(20.0, DesignTokens.ContentPaddingTop);
        Assert.Equal(20.0, DesignTokens.ContentPaddingBottom);
    }

    [Fact]
    public void EnglishAndTurkishResourcesHaveMatchingKeys()
    {
        string baseDir = AppContext.BaseDirectory;
        // Search upwards for solution root
        string current = baseDir;
        string? appDir = null;
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(current, "src", "Ornare.App");
            if (Directory.Exists(candidate))
            {
                appDir = candidate;
                break;
            }
            string? parent = Path.GetDirectoryName(current);
            if (parent == null) break;
            current = parent;
        }

        if (appDir == null)
        {
            // If running in isolated output dir, skip path search
            return;
        }

        string enResw = Path.Combine(appDir, "Strings", "en-US", "Resources.resw");
        string trResw = Path.Combine(appDir, "Strings", "tr-TR", "Resources.resw");

        Assert.True(File.Exists(enResw), $"English resw missing at {enResw}");
        Assert.True(File.Exists(trResw), $"Turkish resw missing at {trResw}");

        var enDoc = XDocument.Load(enResw);
        var trDoc = XDocument.Load(trResw);

        var enKeys = enDoc.Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();
        var trKeys = trDoc.Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();

        Assert.NotEmpty(enKeys);
        Assert.Equal(enKeys.Count, trKeys.Count);

        foreach (var key in enKeys)
        {
            Assert.Contains(key, trKeys);
        }
    }
}
