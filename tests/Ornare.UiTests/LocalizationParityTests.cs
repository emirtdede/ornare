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

    private static string FindAppDirectory()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "src", "Ornare.App");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        throw new DirectoryNotFoundException("Could not find src/Ornare.App from BaseDirectory: " + AppContext.BaseDirectory);
    }

    [Fact]
    public void EnglishAndTurkishResourcesHaveMatchingKeys()
    {
        string appDir = FindAppDirectory();


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

        // Verify all values are non-empty
        foreach (var el in enDoc.Root!.Elements("data"))
        {
            string val = el.Element("value")?.Value ?? "";
            Assert.False(string.IsNullOrWhiteSpace(val), $"Empty English value for key '{el.Attribute("name")?.Value}'");
        }

        foreach (var el in trDoc.Root!.Elements("data"))
        {
            string val = el.Element("value")?.Value ?? "";
            Assert.False(string.IsNullOrWhiteSpace(val), $"Empty Turkish value for key '{el.Attribute("name")?.Value}'");
            // Verify no UTF-8 replacement / mojibake characters
            Assert.DoesNotContain("\uFFFD", val);
            Assert.DoesNotContain("Ã", val);
        }

        // Verify F26/F27 essential keys exist
        string[] requiredF26F27Keys =
        [
            "SearchBox_Placeholder",
            "KeyboardShortcuts_Help",
            "Shortcut_Search",
            "Shortcut_QuickPanel",
            "Shortcut_Inspect",
            "Shortcut_Apply",
            "Shortcut_Reset",
            "Shortcut_Undo",
            "Shortcut_Escape",
            "Shortcut_SelectAll",
            "Shortcut_DeleteStaged",
            "StagedFolders_DropHint",
            "StagedFolders_PortableNtfs",
            "StagedFolders_AlreadyCustomized"
        ];

        foreach (var reqKey in requiredF26F27Keys)
        {
            Assert.True(enKeys.Contains(reqKey), $"English missing required key: {reqKey}");
            Assert.True(trKeys.Contains(reqKey), $"Turkish missing required key: {reqKey}");
        }
    }

    [Fact]
    public void TurkishTypographyContainsSpecificDiacritics()
    {
        string appDir = FindAppDirectory();
        string trResw = Path.Combine(appDir, "Strings", "tr-TR", "Resources.resw");
        var trDoc = XDocument.Load(trResw);
        string allValues = string.Join(" ", trDoc.Root!.Elements("data").Select(e => e.Element("value")?.Value ?? ""));

        // Turkish alphabet distinctive characters: ç, ğ, ı, ö, ş, ü, İ
        Assert.Contains("ç", allValues, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ğ", allValues, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ı", allValues, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ö", allValues, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ş", allValues, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ü", allValues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllSixLocalesHaveMatchingKeysAndNonEmptyValues()
    {
        string appDir = FindAppDirectory();
        string[] locales = ["en-US", "tr-TR", "de-DE", "fr-FR", "es-ES", "pt-BR"];
        string enResw = Path.Combine(appDir, "Strings", "en-US", "Resources.resw");
        Assert.True(File.Exists(enResw), $"English resw missing at {enResw}");
        var enDoc = XDocument.Load(enResw);
        var enKeys = enDoc.Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();
        var enPlaceholders = enDoc.Root!.Elements("data").ToDictionary(
            e => e.Attribute("name")!.Value,
            e => System.Text.RegularExpressions.Regex.Matches(e.Element("value")?.Value ?? "", @"\{(\d+)\}").Select(m => m.Value).OrderBy(x => x).ToList());

        foreach (var loc in locales)
        {
            string locResw = Path.Combine(appDir, "Strings", loc, "Resources.resw");
            Assert.True(File.Exists(locResw), $"Locale {loc} resw missing at {locResw}");

            var locDoc = XDocument.Load(locResw);
            var locKeys = locDoc.Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();

            // Verify key count and parity
            Assert.Equal(enKeys.Count, locKeys.Count);
            foreach (var key in enKeys)
            {
                Assert.True(locKeys.Contains(key), $"Locale {loc} missing key '{key}'");
            }

            // Verify non-empty and no mojibake
            foreach (var el in locDoc.Root!.Elements("data"))
            {
                string key = el.Attribute("name")!.Value;
                string val = el.Element("value")?.Value ?? "";
                Assert.False(string.IsNullOrWhiteSpace(val), $"Empty value in {loc} for key '{key}'");
                Assert.DoesNotContain("\uFFFD", val);
                Assert.DoesNotContain("Ã", val);

                // Check placeholder parity
                var locMatches = System.Text.RegularExpressions.Regex.Matches(val, @"\{(\d+)\}").Select(m => m.Value).OrderBy(x => x).ToList();
                var expected = enPlaceholders[key];
                Assert.Equal(expected, locMatches);
            }
        }
    }

    [Fact]
    public void AppResourcesHasMatchingKeysForAllSixLocales()
    {
        string appDir = FindAppDirectory();
        string appResourcesPath = Path.Combine(appDir, "AppResources.cs");
        Assert.True(File.Exists(appResourcesPath), "AppResources.cs missing");

        string enResw = Path.Combine(appDir, "Strings", "en-US", "Resources.resw");
        var enDoc = XDocument.Load(enResw);
        var enKeys = enDoc.Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();

        string content = File.ReadAllText(appResourcesPath);
        string[] locales = ["en-US", "tr-TR", "de-DE", "fr-FR", "es-ES", "pt-BR"];

        foreach (var loc in locales)
        {
            // Find section for locale
            string marker = $"[\"{loc}\"] = new(StringComparer.Ordinal)";
            int startIdx = content.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(startIdx >= 0, $"Locale marker {marker} not found in AppResources.cs");

            int endIdx = content.IndexOf("},", startIdx, StringComparison.Ordinal);
            Assert.True(endIdx > startIdx, $"Locale closing brace for {loc} not found");

            string section = content.Substring(startIdx, endIdx - startIdx);
            foreach (var key in enKeys)
            {
                string keyEntry = $"[\"{key}\"] = ";
                Assert.True(section.Contains(keyEntry), $"AppResources locale {loc} is missing key: {key}");
            }
        }
    }
}
