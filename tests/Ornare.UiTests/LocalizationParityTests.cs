using System.IO;
using System.Xml.Linq;
using Ornare.DesignSystem;
using Xunit;

namespace Ornare.UiTests;

public class LocalizationParityTests
{
    /// <summary>
    /// The picker offers every language in <see cref="Ornare.Core.LocaleCatalog"/>; only the
    /// locales present in the runtime dictionary are actually translated. If those two sets
    /// drift, a language is either advertised without translation or translated without being
    /// selectable.
    /// </summary>
    [Fact]
    public void RuntimeDictionaryCoversExactlyTheCompleteLocales()
    {
        string content = File.ReadAllText(Path.Combine(FindAppDirectory(), "AppResources.cs"));
        var runtimeLocales = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
            content,
            "\\[\"([a-z]{2,3}-[A-Za-z]{2,4}(?:-[A-Za-z]{2,4})?)\"\\] = new\\(StringComparer\\.Ordinal\\)"))
        {
            runtimeLocales.Add(m.Groups[1].Value);
        }

        var complete = new List<string>();
        foreach (Ornare.Core.AppLocale locale in Ornare.Core.LocaleCatalog.All)
        {
            if (locale.IsComplete)
            {
                complete.Add(locale.Tag);
            }

            Assert.Contains(locale.Tag, Ornare.Core.LocaleCatalog.Tags);
        }

        Assert.Equal(complete.Count, runtimeLocales.Count);
        foreach (string locale in complete)
        {
            Assert.Contains(locale, runtimeLocales);
        }

        Assert.Equal(73, Ornare.Core.LocaleCatalog.Tags.Length);
    }

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
        Dictionary<string, string> en = RuntimeResourceSource.LoadLocale("en-US");
        Dictionary<string, string> tr = RuntimeResourceSource.LoadLocale("tr-TR");

        Assert.NotEmpty(en);
        Assert.Equal(en.Count, tr.Count);

        foreach (string key in en.Keys)
        {
            Assert.True(tr.ContainsKey(key), $"Turkish dictionary is missing key '{key}'.");
        }

        // Verify all values are non-empty
        foreach ((string key, string value) in en)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"Empty English value for key '{key}'");
        }

        foreach ((string key, string value) in tr)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"Empty Turkish value for key '{key}'");
            // Verify no UTF-8 replacement / mojibake characters
            Assert.DoesNotContain("\uFFFD", value);
            Assert.DoesNotContain("Ã", value);
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

        foreach (string reqKey in requiredF26F27Keys)
        {
            Assert.True(en.ContainsKey(reqKey), $"English missing required key: {reqKey}");
            Assert.True(tr.ContainsKey(reqKey), $"Turkish missing required key: {reqKey}");
        }
    }

    /// <summary>
    /// The runtime dictionaries - not the .resw files - are what the app renders. This gate
    /// proves every locale carries exactly the same key set and the same placeholder set, so a
    /// newly added string can never ship translated into only one language or with a format
    /// placeholder the translation dropped.
    /// </summary>
    [Fact]
    public void RuntimeLocaleDictionariesHaveMatchingKeysAndPlaceholders()
    {
        string appDir = FindAppDirectory();
        var locales = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (string fileName in new[] { "AppResources.cs", "AppResources.Extended.cs" })
        {
            string content = File.ReadAllText(Path.Combine(appDir, fileName));
            MergeLocaleDictionaries(content, locales);
        }

        Assert.Equal(6, locales.Count);
        Assert.True(locales.TryGetValue("en-US", out var baseline), "en-US runtime dictionary missing.");

        foreach (var (tag, entries) in locales)
        {
            Assert.Equal(baseline.Count, entries.Count);

            foreach (var (key, englishValue) in baseline)
            {
                Assert.True(entries.ContainsKey(key), $"{tag} is missing runtime key '{key}'.");
                Assert.Equal(
                    ExtractPlaceholders(englishValue),
                    ExtractPlaceholders(entries[key]));
            }

            foreach (var key in entries.Keys)
            {
                Assert.True(baseline.ContainsKey(key), $"en-US is missing runtime key '{key}' present in {tag}.");
            }
        }
    }

    private static void MergeLocaleDictionaries(string content, Dictionary<string, Dictionary<string, string>> target)
    {
        var marker = new System.Text.RegularExpressions.Regex(
            "\\[\"(?<loc1>[a-z]{2}-[A-Z]{2})\"\\]\\s*=\\s*new\\(StringComparer\\.Ordinal\\)" +
            "|Add\\(\"(?<loc2>[a-z]{2}-[A-Z]{2})\"\\s*,\\s*new Dictionary<string,\\s*string>\\(StringComparer\\.Ordinal\\)");
        var entry = new System.Text.RegularExpressions.Regex(
            "\\[\"(?<key>[^\"]+)\"\\]\\s*=\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"");

        var matches = marker.Matches(content);
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            string locale = m.Groups["loc1"].Success ? m.Groups["loc1"].Value : m.Groups["loc2"].Value;
            int start = m.Index + m.Length;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;

            if (!target.TryGetValue(locale, out var dictionary))
            {
                dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
                target[locale] = dictionary;
            }

            foreach (System.Text.RegularExpressions.Match e in entry.Matches(content[start..end]))
            {
                dictionary[e.Groups["key"].Value] = e.Groups["value"].Value;
            }
        }
    }

    private static string ExtractPlaceholders(string value)
    {
        var placeholders = System.Text.RegularExpressions.Regex
            .Matches(value, "\\{\\d+\\}")
            .Select(m => m.Value)
            .OrderBy(v => v, StringComparer.Ordinal);
        return string.Join(",", placeholders);
    }

    [Fact]
    public void TurkishTypographyContainsSpecificDiacritics()
    {
        Dictionary<string, string> tr = RuntimeResourceSource.LoadLocale("tr-TR");
        string allValues = string.Join(" ", tr.Values);

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
        Dictionary<string, string> en = RuntimeResourceSource.LoadLocale("en-US");
        var enPlaceholders = en.ToDictionary(
            pair => pair.Key,
            pair => System.Text.RegularExpressions.Regex.Matches(pair.Value, @"\{(\d+)\}")
                .Select(m => m.Value)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList());

        foreach (string loc in RuntimeResourceSource.CompleteLocales)
        {
            Dictionary<string, string> dictionary = RuntimeResourceSource.LoadLocale(loc);

            // Verify key count and parity
            Assert.Equal(en.Count, dictionary.Count);
            foreach (string key in en.Keys)
            {
                Assert.True(dictionary.ContainsKey(key), $"Locale {loc} missing key '{key}'");
            }

            // Verify non-empty and no mojibake
            foreach ((string key, string value) in dictionary)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"Empty value in {loc} for key '{key}'");
                Assert.DoesNotContain("\uFFFD", value);
                Assert.DoesNotContain("Ã", value);

                // Check placeholder parity
                var locMatches = System.Text.RegularExpressions.Regex.Matches(value, @"\{(\d+)\}")
                    .Select(m => m.Value)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();
                Assert.Equal(enPlaceholders[key], locMatches);
            }
        }
    }

    private static Dictionary<string, HashSet<string>> ReadAppResourceSections(string content)
    {
        string[] locales = ["en-US", "tr-TR", "de-DE", "fr-FR", "es-ES", "pt-BR"];
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var loc in locales)
        {
            string marker = $"[\"{loc}\"] = new(StringComparer.Ordinal)";
            int startIdx = content.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(startIdx >= 0, $"Locale marker {marker} not found in AppResources.cs");

            int endIdx = content.IndexOf("},", startIdx, StringComparison.Ordinal);
            Assert.True(endIdx > startIdx, $"Locale closing brace for {loc} not found");

            string section = content.Substring(startIdx, endIdx - startIdx);
            var keys = System.Text.RegularExpressions.Regex
                .Matches(section, "\\[\"([A-Za-z0-9_]+)\"\\]\\s*=")
                .Select(m => m.Groups[1].Value)
                .ToList();

            result[loc] = keys.ToHashSet(StringComparer.Ordinal);
            Assert.Equal(keys.Count, result[loc].Count); // no duplicate key entries in one locale
        }

        return result;
    }

    /// <summary>
    /// Guards the regression that made de-DE, fr-FR, es-ES and pt-BR fall back to English:
    /// a key added to en-US but not translated would silently resolve to the English string.
    /// </summary>
    [Fact]
    public void AppResourceKeySetsAreIdenticalAcrossAllSixLocales()
    {
        string content = File.ReadAllText(Path.Combine(FindAppDirectory(), "AppResources.cs"));
        var sections = ReadAppResourceSections(content);

        var enKeys = sections["en-US"];
        Assert.NotEmpty(enKeys);

        foreach (var (locale, keys) in sections)
        {
            Assert.Equal(enKeys.Count, keys.Count);
            var missing = enKeys.Except(keys).OrderBy(k => k).ToList();
            var extra = keys.Except(enKeys).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"{locale} is missing keys: {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0, $"{locale} has keys absent from en-US: {string.Join(", ", extra)}");
        }
    }

    /// <summary>
    /// XAML literals are design-time fallbacks; the runtime must overwrite them through
    /// <c>UpdateLocalizedChrome</c> / <c>ApplyLanguage</c>. Any literal without such an
    /// assignment leaks English into the five non-English locales. Brand names and
    /// language endonyms are intentionally untranslated.
    /// </summary>
    [Fact]
    public void EveryXamlUserFacingLiteralIsLocalizedAtRuntime()
    {
        string appDir = FindAppDirectory();
        var code = new System.Text.StringBuilder();
        foreach (var file in Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            code.AppendLine(File.ReadAllText(file));
        }

        string codeText = code.ToString();
        // Localized surface: visible copy plus what screen readers announce.
        string[] localizedProperties =
        [
            "Text", "Content", "PlaceholderText", "Header",
            "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}AutomationProperties.Name"
        ];

        // Brand name never translates; language endonyms are shown in their own language
        // on purpose so users can find their language regardless of the current locale.
        var allowList = new HashSet<string>(StringComparer.Ordinal)
        {
            "Ornare",
            // Publisher identity and its URL are not translatable copy.
            "Vellium", "vellium.dev",
            // A hex colour sample is a format hint, not language. It reads the same in every locale.
            "#0F6CBD",
            "Türkçe", "English", "Deutsch", "Français", "Español", "Português (Brasil)"
        };

        var offenders = new List<string>();
        foreach (var xaml in Directory.EnumerateFiles(appDir, "*.xaml", SearchOption.AllDirectories))
        {
            if (xaml.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                xaml.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            XDocument doc = XDocument.Load(xaml);
            foreach (var element in doc.Descendants())
            {
                string? name = element.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "Name" && a.Name.NamespaceName.Contains("xaml", StringComparison.Ordinal))
                    ?.Value;

                foreach (var attr in element.Attributes())
                {
                    bool isLocalizedProperty = localizedProperties.Any(p =>
                        attr.Name.LocalName == p || attr.Name.ToString() == p);
                    if (!isLocalizedProperty)
                    {
                        continue;
                    }

                    string value = attr.Value.Trim();
                    if (value.Length == 0 || value.StartsWith('{') || allowList.Contains(value))
                    {
                        continue;
                    }

                    // A literal is acceptable only if the runtime overwrites that exact property.
                    // The earlier check accepted any assignment on the element, so a Text
                    // assignment silently excused an English PlaceholderText or an English
                    // AutomationProperties.Name that Narrator then read in every locale.
                    bool isAccessibleName = attr.Name.LocalName == "AutomationProperties.Name" ||
                        attr.Name.ToString().EndsWith("AutomationProperties.Name", StringComparison.Ordinal);
                    string overwritePattern = isAccessibleName
                        ? $@"AutomationProperties\.SetName\(\s*{System.Text.RegularExpressions.Regex.Escape(name!)}\b"
                        : $@"\b{System.Text.RegularExpressions.Regex.Escape(name!)}\.{attr.Name.LocalName}\s*=";
                    bool overwritten = name is not null &&
                        System.Text.RegularExpressions.Regex.IsMatch(codeText, overwritePattern);
                    if (!overwritten)
                    {
                        offenders.Add($"{Path.GetFileName(xaml)}: <{element.Name.LocalName} x:Name=\"{name}\" {attr.Name.LocalName}=\"{value}\">");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, $"Untranslated XAML literals: {string.Join(" | ", offenders)}");
    }

    /// <summary>
    /// Every resource key referenced from app code must exist, otherwise the UI silently
    /// renders the raw key identifier (AppResources.GetString returns the key as a fallback).
    /// </summary>
    [Fact]
    public void EveryReferencedResourceKeyExists()
    {
        string appDir = FindAppDirectory();
        var enKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string resourceFile in new[] { "AppResources.cs", "AppResources.Extended.cs" })
        {
            string content = File.ReadAllText(Path.Combine(appDir, resourceFile));
            if (resourceFile == "AppResources.Extended.cs")
            {
                string extended = Path.Combine(appDir, resourceFile);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(extended),
                    "\\[\"([A-Za-z0-9_]+)\"\\]\\s*="))
                {
                    enKeys.Add(m.Groups[1].Value);
                }

                continue;
            }

            foreach (string key in ReadAppResourceSections(content)["en-US"])
            {
                enKeys.Add(key);
            }
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(file),
                "(?:GetString|Format|\\bL)\\(\\s*\"([A-Za-z0-9_]+)\""))
            {
                referenced.Add(m.Groups[1].Value);
            }
        }

        Assert.NotEmpty(referenced);
        var missing = referenced.Except(enKeys).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"Referenced keys with no resource entry: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The two-way tr/en ternary was the original localization defect: users of the other
    /// four first-class locales saw English. This keeps that pattern from returning.
    /// </summary>
    [Fact]
    public void NoTwoWayTurkishEnglishTernariesRemainInAppCode()
    {
        string appDir = FindAppDirectory();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"isTr\s*\?") ||
                System.Text.RegularExpressions.Regex.IsMatch(text, @"_currentLanguage\.StartsWith\(\s*""tr"""))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, $"tr/en-only branching found in: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// Guards audit finding 3.4. A parallel <c>Strings/*/Resources.resw</c> tree used to sit
    /// beside the runtime dictionaries. It was neither compiled into a resources.pri nor
    /// complete (259 keys against 712), and the tests that "verified" it only compared the
    /// mirror against itself. A second copy of user-facing copy is a defect: one of the two
    /// always rots, and translators cannot tell which one ships.
    /// </summary>
    [Fact]
    public void NoStaleReswCopyOfTheRuntimeCopyExists()
    {
        string appDir = RuntimeResourceSource.FindAppDirectory();
        string stringsDirectory = Path.Combine(appDir, "Strings");

        Assert.False(
            Directory.Exists(stringsDirectory),
            "src/Ornare.App/Strings must not exist: AppResources is the only source of user-facing copy. " +
            "Edit the dictionaries in AppResources.cs and AppResources.Extended.cs.");
    }
}
