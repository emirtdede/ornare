using System;
using System.Collections.Generic;
using System.Globalization;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

/// <summary>
/// Guards the shipped language catalogue. The pickers, the Windows language detection and the
/// resource fallback chain all read this single list, so a silent edit here would either drop a
/// language from the installer or advertise a translation that does not exist.
/// </summary>
public sealed class LocaleCatalogTests
{
    [Fact]
    public void CatalogueShipsSeventyThreeUniqueTags()
    {
        IReadOnlyList<AppLocale> all = LocaleCatalog.All;
        Assert.Equal(73, all.Count);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (AppLocale locale in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(locale.Tag));
            Assert.False(string.IsNullOrWhiteSpace(locale.NativeName));
            Assert.False(string.IsNullOrWhiteSpace(locale.EnglishName));
            Assert.True(seen.Add(locale.Tag), $"Duplicate locale tag: {locale.Tag}");
        }
    }

    [Fact]
    public void TagsPropertyMatchesCatalogueOrder()
    {
        string[] tags = LocaleCatalog.Tags;
        IReadOnlyList<AppLocale> all = LocaleCatalog.All;

        Assert.Equal(all.Count, tags.Length);
        for (int i = 0; i < all.Count; i++)
        {
            Assert.Equal(all[i].Tag, tags[i]);
        }
    }

    [Fact]
    public void ExactlySixLocalesAreFullyTranslated()
    {
        var complete = new List<string>();
        foreach (AppLocale locale in LocaleCatalog.All)
        {
            if (locale.IsComplete)
            {
                complete.Add(locale.Tag);
            }
        }

        string expected = "en-US,tr-TR,de-DE,fr-FR,es-ES,pt-BR";
        Assert.Equal(expected, string.Join(",", complete));
    }

    [Fact]
    public void CompleteLocalesComeFirstSoPickersOpenWithTranslatedLanguages()
    {
        IReadOnlyList<AppLocale> all = LocaleCatalog.All;
        bool seenIncomplete = false;

        foreach (AppLocale locale in all)
        {
            if (!locale.IsComplete)
            {
                seenIncomplete = true;
                continue;
            }

            Assert.False(seenIncomplete, $"Complete locale {locale.Tag} appears after an incomplete one.");
        }
    }

    [Fact]
    public void EveryShippedTagResolvesToItself()
    {
        foreach (string tag in LocaleCatalog.Tags)
        {
            Assert.Equal(tag, LocaleCatalog.Resolve(tag));
        }
    }

    [Fact]
    public void ResolveFallsBackToLanguageThenEnglish()
    {
        // Exact match wins.
        Assert.Equal("de-DE", LocaleCatalog.Resolve("de-DE"));

        // An unshipped region reuses the shipped language variant.
        Assert.Equal("de-DE", LocaleCatalog.Resolve("de-AT"));
        Assert.Equal("es-ES", LocaleCatalog.Resolve("es-AR"));
        Assert.Equal("en-US", LocaleCatalog.Resolve("en-AU"));

        // A shipped region keeps its own tag rather than collapsing to the base language.
        Assert.Equal("es-MX", LocaleCatalog.Resolve("es-MX"));
        Assert.Equal("pt-PT", LocaleCatalog.Resolve("pt-PT"));

        // Underscore tags come from older configuration files.
        Assert.Equal("pt-BR", LocaleCatalog.Resolve("pt_BR"));

        // Unknown or empty input lands on English.
        Assert.Equal("en-US", LocaleCatalog.Resolve("xx-YY"));
        Assert.Equal("en-US", LocaleCatalog.Resolve(""));
        Assert.Equal("en-US", LocaleCatalog.Resolve(null));
        Assert.Equal("en-US", LocaleCatalog.Resolve("   "));
    }

    [Fact]
    public void IsCompleteReflectsTheTranslatedSet()
    {
        Assert.True(LocaleCatalog.IsComplete("en-US"));
        Assert.True(LocaleCatalog.IsComplete("pt-BR"));
        Assert.False(LocaleCatalog.IsComplete("ja-JP"));
        Assert.False(LocaleCatalog.IsComplete("ar-SA"));

        // Unshipped tags resolve before the flag is read, so they report English completeness.
        Assert.True(LocaleCatalog.IsComplete("de-AT"));
    }

    [Fact]
    public void NativeNameReturnsTheEndonym()
    {
        Assert.Equal("Türkçe", LocaleCatalog.NativeNameOf("tr-TR"));
        Assert.Equal("Deutsch", LocaleCatalog.NativeNameOf("de-DE"));
        Assert.Equal("日本語", LocaleCatalog.NativeNameOf("ja-JP"));

        // Unknown tags fall back to English, never to the raw tag.
        Assert.Equal("English", LocaleCatalog.NativeNameOf("xx-YY"));
    }

    [Fact]
    public void RightToLeftIsReportedForRtlLanguagesOnly()
    {
        Assert.True(LocaleCatalog.IsRightToLeft("ar-SA"));
        Assert.True(LocaleCatalog.IsRightToLeft("he-IL"));
        Assert.True(LocaleCatalog.IsRightToLeft("fa-IR"));
        Assert.True(LocaleCatalog.IsRightToLeft("ur-PK"));

        Assert.False(LocaleCatalog.IsRightToLeft("en-US"));
        Assert.False(LocaleCatalog.IsRightToLeft("tr-TR"));
        Assert.False(LocaleCatalog.IsRightToLeft(null));
    }

    [Fact]
    public void SystemLocaleAlwaysResolvesToAShippedTag()
    {
        string resolved = LocaleCatalog.ResolveSystemLocale();

        Assert.Contains(resolved, LocaleCatalog.Tags);

        // The override only changes which culture is read, never the outcome space.
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
            Assert.Equal("ja-JP", LocaleCatalog.ResolveSystemLocale());

            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Assert.Equal("en-US", LocaleCatalog.ResolveSystemLocale());
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
