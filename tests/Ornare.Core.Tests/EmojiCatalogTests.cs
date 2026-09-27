using System.Linq;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class EmojiCatalogTests
{
    [Fact]
    public void CatalogLoadsCuratedEntries()
    {
        var all = EmojiCatalog.All;
        Assert.NotNull(all);
        Assert.True(all.Count >= 180, $"Expected >= 180 curated emoji, found {all.Count}");

        foreach (var entry in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Grapheme));
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Category));
            Assert.NotEmpty(entry.Keywords);
        }
    }

    [Theory]
    [InlineData("Smileys")]
    [InlineData("People")]
    [InlineData("Nature")]
    [InlineData("Food")]
    [InlineData("Activities")]
    [InlineData("Travel")]
    [InlineData("Objects")]
    [InlineData("Symbols")]
    public void CatalogHasEntriesForEveryCategory(string category)
    {
        var inCategory = EmojiCatalog.Search(string.Empty, category);
        Assert.NotEmpty(inCategory);
        Assert.All(inCategory, e => Assert.Equal(category, e.Category));
    }

    [Theory]
    [InlineData("heart", "❤️")]
    [InlineData("fire", "🔥")]
    [InlineData("rocket", "🚀")]
    [InlineData("star", "⭐")]
    [InlineData("folder", "📁")]
    [InlineData("kalp", "❤️")]
    [InlineData("ateş", "🔥")]
    [InlineData("yıldız", "⭐")]
    public void SearchFindsRelevantEmojiInEnglishAndTurkish(string query, string expectedGrapheme)
    {
        var results = EmojiCatalog.Search(query);
        Assert.NotEmpty(results);
        Assert.Contains(results, e => e.Grapheme == expectedGrapheme);
    }

    [Fact]
    public void MultiCodepointGraphemeClustersPreserved()
    {
        // Check keycap sequences (3 codepoints each)
        var zero = EmojiCatalog.All.FirstOrDefault(e => e.Grapheme == "0️⃣");
        Assert.NotNull(zero);
        Assert.True(zero.Grapheme.Length > 1);

        // Check skull and crossbones with variation selector
        var skull = EmojiCatalog.All.FirstOrDefault(e => e.Grapheme == "☠️");
        Assert.NotNull(skull);
    }
}
