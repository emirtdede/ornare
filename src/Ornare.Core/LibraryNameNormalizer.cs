using System.Buffers;
using System.Globalization;
using System.Text;

namespace Ornare.Core;

public static class LibraryNameNormalizer
{
    public const int MaxScalarsPerTag = 40;
    public const int MaxScalarsPerCollection = 80;
    public const int MaxScalarsPerName = MaxScalarsPerTag;
    public const int MinScalarsPerName = 1;
    public const int MaxTagsPerAsset = 32;
    public const int MaxTotalTags = 5000;
    public const int MaxTotalCollections = 500;

    /// <summary>
    /// Validates and normalizes a candidate tag or collection name according to Ornare's invariant rules.
    /// Preserves user display casing in <paramref name="displayName"/> while producing deterministic
    /// NFKC-invariant normalized string in <paramref name="normalizedName"/>.
    /// </summary>
    public static bool TryValidateAndNormalize(
        string? input,
        out string displayName,
        out string normalizedName,
        out string? errorMessage,
        int maxScalars = MaxScalarsPerTag)
    {
        displayName = string.Empty;
        normalizedName = string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = "Name cannot be empty or only whitespace.";
            return false;
        }

        // 1. Trim leading and trailing Unicode whitespace by Rune semantics
        string trimmed = TrimUnicodeWhitespace(input);
        if (string.IsNullOrEmpty(trimmed))
        {
            errorMessage = "Name cannot be empty or only whitespace.";
            return false;
        }

        // 2. Validate Unicode scalar values count and control characters
        int scalarCount = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
            {
                errorMessage = "Name cannot contain Unicode control characters.";
                return false;
            }
            scalarCount++;
        }

        if (scalarCount < MinScalarsPerName || scalarCount > maxScalars)
        {
            errorMessage = $"Name must be between {MinScalarsPerName} and {maxScalars} Unicode scalar values (got {scalarCount}).";
            return false;
        }

        displayName = trimmed;

        // 3. Exact tag normalization algorithm, in order:
        // - trim leading/trailing Unicode whitespace by Rune/Unicode whitespace semantics (already trimmed)
        // - normalize to Unicode NFKC (NormalizationForm.FormKC)
        // - apply .ToUpperInvariant()
        // - normalize to NFKC again
        // - reject empty result and any Unicode scalar whose category is Control
        // - use that resulting string as normalized_name
        string nfkc1 = displayName.Normalize(NormalizationForm.FormKC);
        string upper = nfkc1.ToUpperInvariant();
        string nfkc2 = upper.Normalize(NormalizationForm.FormKC);
        string finalTrimmed = TrimUnicodeWhitespace(nfkc2);

        if (string.IsNullOrEmpty(finalTrimmed))
        {
            errorMessage = "Normalized tag name cannot be empty.";
            return false;
        }

        foreach (var rune in finalTrimmed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
            {
                errorMessage = "Normalized tag name cannot contain Unicode control characters.";
                return false;
            }
        }

        normalizedName = finalTrimmed;
        return true;
    }

    /// <summary>
    /// Trims leading and trailing Unicode whitespace using System.Text.Rune semantics without intermediate string allocations.
    /// </summary>
    public static string TrimUnicodeWhitespace(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        ReadOnlySpan<char> span = input.AsSpan();

        // Trim leading whitespace
        while (!span.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(span, out Rune rune, out int charsConsumed);
            if (status == OperationStatus.Done && Rune.IsWhiteSpace(rune))
            {
                span = span.Slice(charsConsumed);
            }
            else
            {
                break;
            }
        }

        // Trim trailing whitespace
        while (!span.IsEmpty)
        {
            OperationStatus status = Rune.DecodeLastFromUtf16(span, out Rune rune, out int charsConsumed);
            if (status == OperationStatus.Done && Rune.IsWhiteSpace(rune))
            {
                span = span.Slice(0, span.Length - charsConsumed);
            }
            else
            {
                break;
            }
        }

        return span.ToString();
    }

    /// <summary>
    /// Counts the exact number of Unicode scalar values (Runes) in a string.
    /// </summary>
    public static int CountUnicodeScalars(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;
        int count = 0;
        foreach (var _ in input.EnumerateRunes())
        {
            count++;
        }
        return count;
    }
}
