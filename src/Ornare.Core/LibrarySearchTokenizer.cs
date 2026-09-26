using System.Buffers;
using System.Globalization;
using System.Text;

namespace Ornare.Core;

public static class LibrarySearchTokenizer
{
    public const int MaxScalarsPerToken = 64;
    public const int MaxTokensPerEntity = 64;

    /// <summary>
    /// Normalizes an input string according to Ornare's F15/F17 search specification:
    /// Unicode NFKC normalization -> Invariant uppercase -> Unicode NFKC normalization.
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string nfkc1 = input.Normalize(NormalizationForm.FormKC);
        string upper = nfkc1.ToUpperInvariant();
        return upper.Normalize(NormalizationForm.FormKC);
    }

    /// <summary>
    /// Determines whether a Unicode scalar (Rune) is a letter or decimal digit.
    /// Any scalar not satisfying this is treated as a token boundary.
    /// </summary>
    public static bool IsLetterOrDecimalDigit(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category == UnicodeCategory.UppercaseLetter ||
               category == UnicodeCategory.LowercaseLetter ||
               category == UnicodeCategory.TitlecaseLetter ||
               category == UnicodeCategory.ModifierLetter ||
               category == UnicodeCategory.OtherLetter ||
               category == UnicodeCategory.DecimalDigitNumber;
    }

    /// <summary>
    /// Splits an input string into distinct, normalized search tokens according to Ornare's invariant rules:
    /// - Reuses NFKC + invariant uppercase normalizer.
    /// - Splits on any Unicode scalar that is not a letter or decimal digit.
    /// - Clamps each token at <see cref="MaxScalarsPerToken"/> Unicode scalars.
    /// - Caps total extracted tokens at <paramref name="maxTokens"/> (default <see cref="MaxTokensPerEntity"/>).
    /// - Coalesces duplicate tokens preserving first-seen order.
    /// - Drops empty tokens.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string? input, int maxTokens = MaxTokensPerEntity)
    {
        if (string.IsNullOrWhiteSpace(input) || maxTokens <= 0)
        {
            return Array.Empty<string>();
        }

        string normalized = Normalize(input);
        if (string.IsNullOrEmpty(normalized))
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var currentTokenRunes = new List<Rune>();

        void FlushCurrentToken()
        {
            if (currentTokenRunes.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder(currentTokenRunes.Count);
            foreach (var r in currentTokenRunes)
            {
                sb.Append(r.ToString());
            }
            string token = sb.ToString();
            currentTokenRunes.Clear();

            if (token.Length > 0 && seen.Add(token))
            {
                results.Add(token);
            }
        }

        foreach (var rune in normalized.EnumerateRunes())
        {
            if (IsLetterOrDecimalDigit(rune))
            {
                if (currentTokenRunes.Count < MaxScalarsPerToken)
                {
                    currentTokenRunes.Add(rune);
                }
            }
            else
            {
                FlushCurrentToken();
                if (results.Count >= maxTokens)
                {
                    break;
                }
            }
        }

        if (results.Count < maxTokens)
        {
            FlushCurrentToken();
        }

        return results;
    }

    /// <summary>
    /// Computes the immediate lexical upper-bound term for index-backed prefix matching:
    /// WHERE token >= @term AND token < @nextTerm.
    /// Scans backwards from the last scalar, increments the scalar, and truncates.
    /// Returns null if all scalars are at maximum Unicode scalar value (0x10FFFF).
    /// </summary>
    public static string? ComputeLexicalNextTerm(string term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return null;
        }

        var runes = new List<Rune>();
        foreach (var r in term.EnumerateRunes())
        {
            runes.Add(r);
        }

        if (runes.Count == 0)
        {
            return null;
        }

        for (int i = runes.Count - 1; i >= 0; i--)
        {
            int val = runes[i].Value;
            int nextVal = val + 1;
            if (nextVal >= 0xD800 && nextVal <= 0xDFFF)
            {
                nextVal = 0xE000;
            }

            if (nextVal <= 0x10FFFF)
            {
                var sb = new StringBuilder();
                for (int j = 0; j < i; j++)
                {
                    sb.Append(runes[j].ToString());
                }
                sb.Append(new Rune(nextVal).ToString());
                return sb.ToString();
            }
        }

        return null;
    }
}
