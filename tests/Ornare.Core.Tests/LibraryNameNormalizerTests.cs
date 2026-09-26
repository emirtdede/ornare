using System.Text;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public sealed class LibraryNameNormalizerTests
{
    [Theory]
    [InlineData("work", "work", "WORK")]
    [InlineData("  Projects  ", "Projects", "PROJECTS")]
    [InlineData("Design System", "Design System", "DESIGN SYSTEM")]
    [InlineData("C# Code", "C# Code", "C# CODE")]
    public void ValidTagNamesNormalizeCorrectly(string input, string expectedDisplay, string expectedNormalized)
    {
        bool success = LibraryNameNormalizer.TryValidateAndNormalize(
            input,
            out string displayName,
            out string normalizedName,
            out string? error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(expectedDisplay, displayName);
        Assert.Equal(expectedNormalized, normalizedName);
    }

    [Fact]
    public void UnicodeNfkcEquivalenceProducesIdenticalNormalizedName()
    {
        // Decomposed vs Composed 'é': 'e' + U+0301 (combining acute) vs U+00E9 ('é')
        string decomposed = "cafe\u0301";
        string composed = "caf\u00E9";

        bool decSuccess = LibraryNameNormalizer.TryValidateAndNormalize(
            decomposed,
            out _,
            out string decNorm,
            out _);
        bool compSuccess = LibraryNameNormalizer.TryValidateAndNormalize(
            composed,
            out _,
            out string compNorm,
            out _);

        Assert.True(decSuccess);
        Assert.True(compSuccess);
        Assert.Equal(compNorm, decNorm);
    }

    [Fact]
    public void TurkishInvariantCasingDoesNotUseTurkishLocaleI()
    {
        // "istanbul" in invariant upper is "ISTANBUL", not "İSTANBUL"
        string input = "istanbul";
        bool success = LibraryNameNormalizer.TryValidateAndNormalize(
            input,
            out string display,
            out string norm,
            out _);

        Assert.True(success);
        Assert.Equal("istanbul", display);
        Assert.Equal("ISTANBUL", norm);
    }

    [Fact]
    public void ScalarLengthCountCorrectlyForSurrogatePairs()
    {
        // An emoji like 🚀 (U+1F680) is 2 UTF-16 code units (chars), but exactly 1 Unicode scalar
        string emojiTag = "🚀 Rocket";
        int scalarCount = LibraryNameNormalizer.CountUnicodeScalars(emojiTag);
        Assert.Equal(8, scalarCount); // 1 emoji + 1 space + 6 letters

        bool success = LibraryNameNormalizer.TryValidateAndNormalize(
            emojiTag,
            out string display,
            out string norm,
            out _);

        Assert.True(success);
        Assert.Equal("🚀 Rocket", display);
        Assert.Equal("🚀 ROCKET", norm);
    }

    [Fact]
    public void ScalarLengthBoundariesExactly40Allowed41Rejected()
    {
        string exact40 = new string('a', 40);
        bool success40 = LibraryNameNormalizer.TryValidateAndNormalize(
            exact40,
            out string display40,
            out string norm40,
            out string? error40);

        Assert.True(success40);
        Assert.Null(error40);
        Assert.Equal(40, display40.Length);
        Assert.Equal(40, norm40.Length);

        string length41 = new string('a', 41);
        bool success41 = LibraryNameNormalizer.TryValidateAndNormalize(
            length41,
            out _,
            out _,
            out string? error41);

        Assert.False(success41);
        Assert.NotNull(error41);
        Assert.Contains("40", error41);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("\u3000")] // Ideographic space
    [InlineData("\u00A0")] // Non-breaking space only
    public void EmptyOrWhitespaceOnlyIsRejected(string input)
    {
        bool success = LibraryNameNormalizer.TryValidateAndNormalize(
            input,
            out _,
            out _,
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("bad\u0000tag")]
    [InlineData("line\ntag")]
    [InlineData("tab\ttag")]
    [InlineData("delete\u007Ftag")]
    [InlineData("control\u001Ftag")]
    public void ControlCharactersAreRejected(string input)
    {
        bool success = LibraryNameNormalizer.TryValidateAndNormalize(
            input,
            out _,
            out _,
            out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("control", error, StringComparison.OrdinalIgnoreCase);
    }
}
