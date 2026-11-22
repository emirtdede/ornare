using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ornare.Core;

/// <summary>
/// A selectable interface language. <paramref name="Tag"/> is a BCP-47 tag,
/// <paramref name="NativeName"/> is the endonym shown in pickers (never translated),
/// and <paramref name="IsComplete"/> tells whether every resource string exists for the
/// locale instead of falling back to English.
/// </summary>
public sealed record AppLocale(string Tag, string EnglishName, string NativeName, bool IsComplete);

/// <summary>
/// The single catalogue of interface languages shipped by Ornare.
/// <para>
/// Ordering is intentional: the first six entries are the fully translated, first-class
/// locales, followed by the remaining supported languages in alphabetical order. Windows
/// language detection resolves the operating system UI language against this list and
/// falls back to English when there is no match.
/// </para>
/// </summary>
public static class LocaleCatalog
{
    /// <summary>Locale used when nothing better is available.</summary>
    public const string FallbackTag = "en-US";

    private static readonly AppLocale[] Entries =
    {
        new("en-US", "English (United States)", "English", true),
        new("tr-TR", "Turkish", "Türkçe", true),
        new("de-DE", "German", "Deutsch", true),
        new("fr-FR", "French", "Français", true),
        new("es-ES", "Spanish", "Español", true),
        new("pt-BR", "Portuguese (Brazil)", "Português (Brasil)", true),
        new("af-ZA", "Afrikaans", "Afrikaans", false),
        new("am-ET", "Amharic", "አማርኛ", false),
        new("ar-SA", "Arabic", "العربية", false),
        new("az-Latn-AZ", "Azerbaijani", "Azərbaycan", false),
        new("bg-BG", "Bulgarian", "Български", false),
        new("bn-IN", "Bengali", "বাংলা", false),
        new("bs-Latn-BA", "Bosnian", "Bosanski", false),
        new("ca-ES", "Catalan", "Català", false),
        new("cs-CZ", "Czech", "Čeština", false),
        new("cy-GB", "Welsh", "Cymraeg", false),
        new("da-DK", "Danish", "Dansk", false),
        new("el-GR", "Greek", "Ελληνικά", false),
        new("en-GB", "English (United Kingdom)", "English (United Kingdom)", false),
        new("es-MX", "Spanish (Mexico)", "Español (México)", false),
        new("et-EE", "Estonian", "Eesti", false),
        new("eu-ES", "Basque", "Euskara", false),
        new("fa-IR", "Persian", "فارسی", false),
        new("fi-FI", "Finnish", "Suomi", false),
        new("fil-PH", "Filipino", "Filipino", false),
        new("ga-IE", "Irish", "Gaeilge", false),
        new("gl-ES", "Galician", "Galego", false),
        new("gu-IN", "Gujarati", "ગુજરાતી", false),
        new("he-IL", "Hebrew", "עברית", false),
        new("hi-IN", "Hindi", "हिन्दी", false),
        new("hr-HR", "Croatian", "Hrvatski", false),
        new("hu-HU", "Hungarian", "Magyar", false),
        new("hy-AM", "Armenian", "Հայերեն", false),
        new("id-ID", "Indonesian", "Bahasa Indonesia", false),
        new("is-IS", "Icelandic", "Íslenska", false),
        new("it-IT", "Italian", "Italiano", false),
        new("ja-JP", "Japanese", "日本語", false),
        new("ka-GE", "Georgian", "ქართული", false),
        new("kk-KZ", "Kazakh", "Қазақша", false),
        new("kn-IN", "Kannada", "ಕನ್ನಡ", false),
        new("ko-KR", "Korean", "한국어", false),
        new("ky-KG", "Kyrgyz", "Кыргызча", false),
        new("lt-LT", "Lithuanian", "Lietuvių", false),
        new("lv-LV", "Latvian", "Latviešu", false),
        new("mk-MK", "Macedonian", "Македонски", false),
        new("ml-IN", "Malayalam", "മലയാളം", false),
        new("mn-MN", "Mongolian", "Монгол", false),
        new("mr-IN", "Marathi", "मराठी", false),
        new("ms-MY", "Malay", "Bahasa Melayu", false),
        new("nb-NO", "Norwegian Bokmål", "Norsk Bokmål", false),
        new("ne-NP", "Nepali", "नेपाली", false),
        new("nl-NL", "Dutch", "Nederlands", false),
        new("pa-IN", "Punjabi", "ਪੰਜਾਬੀ", false),
        new("pl-PL", "Polish", "Polski", false),
        new("pt-PT", "Portuguese (Portugal)", "Português (Portugal)", false),
        new("ro-RO", "Romanian", "Română", false),
        new("ru-RU", "Russian", "Русский", false),
        new("si-LK", "Sinhala", "සිංහල", false),
        new("sk-SK", "Slovak", "Slovenčina", false),
        new("sl-SI", "Slovenian", "Slovenščina", false),
        new("sq-AL", "Albanian", "Shqip", false),
        new("sr-Latn-RS", "Serbian", "Srpski", false),
        new("sv-SE", "Swedish", "Svenska", false),
        new("sw-KE", "Swahili", "Kiswahili", false),
        new("ta-IN", "Tamil", "தமிழ்", false),
        new("te-IN", "Telugu", "తెలుగు", false),
        new("th-TH", "Thai", "ไทย", false),
        new("uk-UA", "Ukrainian", "Українська", false),
        new("ur-PK", "Urdu", "اردو", false),
        new("uz-Latn-UZ", "Uzbek", "Oʻzbekcha", false),
        new("vi-VN", "Vietnamese", "Tiếng Việt", false),
        new("zh-CN", "Chinese (Simplified)", "中文（简体）", false),
        new("zh-TW", "Chinese (Traditional)", "中文（繁體）", false)
    };

    /// <summary>Every shipped language, first-class locales first.</summary>
    public static IReadOnlyList<AppLocale> All => Entries;

    /// <summary>BCP-47 tags in picker order.</summary>
    public static string[] Tags { get; } = Entries.Select(e => e.Tag).ToArray();

    /// <summary>
    /// Resolves any language tag to a shipped locale. Exact tag, then language-only and
    /// language-region matches are tried, then English.
    /// </summary>
    public static string Resolve(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return FallbackTag;
        }

        string clean = tag.Trim().Replace('_', '-');
        foreach (AppLocale entry in Entries)
        {
            if (string.Equals(entry.Tag, clean, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Tag;
            }
        }

        string language = LanguageOf(clean);
        if (language.Length == 0)
        {
            return FallbackTag;
        }

        foreach (AppLocale entry in Entries)
        {
            if (string.Equals(LanguageOf(entry.Tag), language, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Tag;
            }
        }

        return FallbackTag;
    }

    /// <summary>
    /// The locale Windows should start with: the operating system UI language when it is
    /// shipped, otherwise English.
    /// </summary>
    public static string ResolveSystemLocale()
    {
        try
        {
            return Resolve(CultureInfo.CurrentUICulture.Name);
        }
        catch (CultureNotFoundException)
        {
            return FallbackTag;
        }
    }

    /// <summary>True when the language is written right to left.</summary>
    public static bool IsRightToLeft(string? tag)
    {
        string language = LanguageOf(Resolve(tag));
        return language is "ar" or "he" or "fa" or "ur";
    }

    /// <summary>Native endonym for a tag, falling back to English.</summary>
    public static string NativeNameOf(string? tag)
    {
        string resolved = Resolve(tag);
        foreach (AppLocale entry in Entries)
        {
            if (string.Equals(entry.Tag, resolved, StringComparison.Ordinal))
            {
                return entry.NativeName;
            }
        }

        return "English";
    }

    /// <summary>True when every resource string exists for the locale.</summary>
    public static bool IsComplete(string? tag)
    {
        string resolved = Resolve(tag);
        foreach (AppLocale entry in Entries)
        {
            if (string.Equals(entry.Tag, resolved, StringComparison.Ordinal))
            {
                return entry.IsComplete;
            }
        }

        return false;
    }

    private static string LanguageOf(string tag)
    {
        int dash = tag.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? tag[..dash] : tag;
    }
}
