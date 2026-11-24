using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ornare.UiTests;

/// <summary>
/// Reads user-facing copy from its single source: the <c>AppResources</c> runtime
/// dictionaries.
/// <para>
/// The project used to ship a parallel <c>Strings/*/Resources.resw</c> tree "verified" by
/// tests that only compared the mirror against itself. It had drifted to 259 keys against
/// 712 real ones and was not compiled into a resources.pri, so it was a second truth that
/// nobody read. Tests now parse the dictionary source directly, which is what the app
/// actually renders.
/// </para>
/// </summary>
internal static class RuntimeResourceSource
{
    /// <summary>The six locales that carry a complete dictionary.</summary>
    public static readonly string[] CompleteLocales = ["en-US", "tr-TR", "de-DE", "fr-FR", "es-ES", "pt-BR"];

    private static readonly Regex LocaleMarker = new(
        "\\[\"(?<loc1>[a-z]{2,3}-[A-Za-z]{2,4})\"\\]\\s*=\\s*new\\(StringComparer\\.Ordinal\\)" +
        "|Add\\(\"(?<loc2>[a-z]{2,3}-[A-Za-z]{2,4})\"\\s*,\\s*new Dictionary<string,\\s*string>\\(StringComparer\\.Ordinal\\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex Entry = new(
        "\\[\"(?<key>[^\"]+)\"\\]\\s*=\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Locates <c>src/Ornare.App</c> by walking up from the test binary, so the tests do not
    /// depend on the working directory of the test host.
    /// </summary>
    public static string FindAppDirectory()
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

    /// <summary>
    /// Returns every locale dictionary, with C# string escapes resolved so assertions can
    /// compare against the text a user actually sees.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> Load()
    {
        string appDir = FindAppDirectory();
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (string fileName in new[] { "AppResources.cs", "AppResources.Extended.cs" })
        {
            string path = Path.Combine(appDir, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            MergeInto(File.ReadAllText(path), result);
        }

        return result;
    }

    /// <summary>Returns one locale's dictionary, failing loudly when it is absent.</summary>
    public static Dictionary<string, string> LoadLocale(string locale)
    {
        Dictionary<string, Dictionary<string, string>> all = Load();
        if (!all.TryGetValue(locale, out Dictionary<string, string>? dictionary))
        {
            throw new InvalidOperationException($"Runtime dictionary for {locale} was not found.");
        }

        return dictionary;
    }

    private static void MergeInto(string content, Dictionary<string, Dictionary<string, string>> target)
    {
        MatchCollection markers = LocaleMarker.Matches(content);
        for (int i = 0; i < markers.Count; i++)
        {
            Match marker = markers[i];
            string locale = marker.Groups["loc1"].Success
                ? marker.Groups["loc1"].Value
                : marker.Groups["loc2"].Value;

            int start = marker.Index + marker.Length;
            int end = i + 1 < markers.Count ? markers[i + 1].Index : content.Length;

            if (!target.TryGetValue(locale, out Dictionary<string, string>? dictionary))
            {
                dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
                target[locale] = dictionary;
            }

            foreach (Match entry in Entry.Matches(content[start..end]))
            {
                dictionary[entry.Groups["key"].Value] = Unescape(entry.Groups["value"].Value);
            }
        }
    }

    private static string Unescape(string value)
    {
        if (!value.Contains('\\', StringComparison.Ordinal))
        {
            return value;
        }

        return value
            .Replace("\\\\", "\u0000", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\u0000", "\\", StringComparison.Ordinal);
    }
}
