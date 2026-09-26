using System.Text;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public sealed class LibrarySearchTokenizerTests
{
    private static readonly string[] ExpectedDelimited1 = ["BLUE", "FOLDER"];
    private static readonly string[] ExpectedDelimited2 = ["MY", "ICON", "V2", "SVG"];
    private static readonly string[] ExpectedDelimited3 = ["HELLO", "WORLD"];
    private static readonly string[] ExpectedDelimited4 = ["ONE", "TWO", "THREE", "FOUR"];
    private static readonly string[] ExpectedDelimited5 = ["ALPHA", "BETA", "GAMMA", "DELTA"];
    private static readonly string[] ExpectedCoalesced = ["BLUE", "FOLDER"];

    [Theory]
    [InlineData("blue folder", 1)]
    [InlineData("my-icon_v2.svg", 2)]
    [InlineData("  hello   world  ", 3)]
    [InlineData("one/two\\three:four", 4)]
    [InlineData("alpha(beta)[gamma]{delta}", 5)]
    public void TokenizeSplitsOnNonLetterOrDigitDelimiters(string input, int expectedCase)
    {
        var tokens = LibrarySearchTokenizer.Tokenize(input);
        string[] expected = expectedCase switch
        {
            1 => ExpectedDelimited1,
            2 => ExpectedDelimited2,
            3 => ExpectedDelimited3,
            4 => ExpectedDelimited4,
            5 => ExpectedDelimited5,
            _ => []
        };
        Assert.Equal(expected, tokens);
    }

    [Fact]
    public void TokenizeCoalescesDuplicateTokensPreservingOrder()
    {
        string input = "blue folder Blue FOLDER blue BLUE";
        var tokens = LibrarySearchTokenizer.Tokenize(input);
        Assert.Equal(ExpectedCoalesced, tokens);
    }

    [Fact]
    public void TokenizeClampsTokenAt64UnicodeScalars()
    {
        // 80 'A' characters followed by "-test"
        string longWord = new string('a', 80);
        string input = $"{longWord}-test";

        var tokens = LibrarySearchTokenizer.Tokenize(input);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(64, tokens[0].Length);
        Assert.Equal(new string('A', 64), tokens[0]);
        Assert.Equal("TEST", tokens[1]);
    }

    [Fact]
    public void TokenizeCapsEntityAt64Tokens()
    {
        // 80 distinct words
        var words = Enumerable.Range(1, 80).Select(i => $"word{i}").ToList();
        string input = string.Join(" ", words);

        var tokens = LibrarySearchTokenizer.Tokenize(input);
        Assert.Equal(64, tokens.Count);
        Assert.Equal("WORD1", tokens[0]);
        Assert.Equal("WORD64", tokens[63]);
    }

    [Fact]
    public void TokenizeNullEmptyOrPunctuationOnlyReturnsEmpty()
    {
        Assert.Empty(LibrarySearchTokenizer.Tokenize(null));
        Assert.Empty(LibrarySearchTokenizer.Tokenize(""));
        Assert.Empty(LibrarySearchTokenizer.Tokenize("   \t\r\n  "));
        Assert.Empty(LibrarySearchTokenizer.Tokenize("---___...///:::;;;!!!???"));
    }

    [Fact]
    public void TokenizeTurkishInvariantCasingBehavior()
    {
        // Invariant uppercase turns ASCII 'i' into 'I', preserves 'İ', and preserves 'ı' (U+0131)
        var tokens1 = LibrarySearchTokenizer.Tokenize("istanbul");
        Assert.Single(tokens1);
        Assert.Equal("ISTANBUL", tokens1[0]);

        var tokens2 = LibrarySearchTokenizer.Tokenize("İstanbul");
        Assert.Single(tokens2);
        Assert.Equal("İSTANBUL", tokens2[0]);

        // Explicitly documented invariant-normalizer behavior:
        // 'ı' (U+0131 dotless i) remains 'ı' in InvariantCulture, 'ş' becomes 'Ş', 'k' becomes 'K'.
        var tokens3 = LibrarySearchTokenizer.Tokenize("ışık");
        Assert.Single(tokens3);
        Assert.Equal("ıŞıK", tokens3[0]);
    }

    [Fact]
    public void TokenizeNfkcNormalizationProducesIdenticalTokens()
    {
        // Decomposed vs Composed 'é'
        string decomposed = "cafe\u0301";
        string composed = "caf\u00E9";

        var tokensDec = LibrarySearchTokenizer.Tokenize(decomposed);
        var tokensComp = LibrarySearchTokenizer.Tokenize(composed);

        Assert.Single(tokensDec);
        Assert.Single(tokensComp);
        Assert.Equal(tokensComp[0], tokensDec[0]);
        Assert.Equal("CAFÉ", tokensDec[0]);
    }

    [Theory]
    [InlineData("CAT", "CAU")]
    [InlineData("A", "B")]
    [InlineData("Z", "[")] // ASCII 90 'Z' -> 91 '['
    [InlineData("9", ":")] // ASCII 57 '9' -> 58 ':'
    [InlineData("TEST", "TESU")]
    [InlineData("FOO1", "FOO2")]
    public void ComputeLexicalNextTermProducesCorrectUpperBound(string term, string expectedNext)
    {
        string? next = LibrarySearchTokenizer.ComputeLexicalNextTerm(term);
        Assert.NotNull(next);
        Assert.Equal(expectedNext, next);

        // Verify string comparisons in Ordinal
        Assert.True(string.CompareOrdinal(term, next) < 0);
        Assert.True(string.CompareOrdinal($"{term}ZZZ", next) < 0);
    }

    [Fact]
    public void ComputeLexicalNextTermNullOrEmptyReturnsNull()
    {
        Assert.Null(LibrarySearchTokenizer.ComputeLexicalNextTerm(""));
        Assert.Null(LibrarySearchTokenizer.ComputeLexicalNextTerm(null!));
    }

    [Fact]
    public void ComputeLexicalNextTermTurkishScalarBound()
    {
        // 'İ' is U+0130. Next scalar is U+0131 ('ı')
        string term = "İ";
        string? next = LibrarySearchTokenizer.ComputeLexicalNextTerm(term);
        Assert.NotNull(next);
        Assert.Equal("\u0131", next);
        Assert.True(string.CompareOrdinal(term, next) < 0);
    }
}
