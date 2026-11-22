using System.Buffers;
using System.Globalization;
using System.Text;

namespace Ornare.Core;

public enum LibraryNameError
{
    None = 0,
    Empty = 1,
    TooShort = 2,
    TooLong = 3,
    ControlCharacter = 4,
}

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
    /// <remarks>
    /// <paramref name="errorMessage"/> is diagnostic English copy for logs only. User-facing text must be
    /// produced from <see cref="TryValidateAndNormalizeDetailed(string?, out string, out string, out LibraryNameError, int)"/>
    /// so every first-class locale is honoured.
    /// </remarks>
    public static bool TryValidateAndNormalize(
        string? input,
        out string displayName,
        out string normalizedName,
        out string? errorMessage,
        int maxScalars = MaxScalarsPerTag)
    {
        bool result = TryValidateAndNormalizeDetailed(
            input,
            out displayName,
            out normalizedName,
            out LibraryNameError error,
            maxScalars);

        errorMessage = error switch
        {
            LibraryNameError.Empty => "Name cannot be empty or only whitespace.",
            LibraryNameError.TooShort => $"Name must be at least {MinScalarsPerName} Unicode scalar values.",
            LibraryNameError.TooLong => $"Name must be at most {maxScalars} Unicode scalar values.",
            LibraryNameError.ControlCharacter => "Name cannot contain Unicode control characters.",
            _ => null,
        };

        return result;
    }

    /// <summary>
    /// Machine-readable overload used by the UI layer: the caller maps
    /// <see cref="LibraryNameError"/> to a localized resource instead of showing
    /// developer-facing English text to the user.
    /// </summary>
    public static bool TryValidateAndNormalizeDetailed(
        string? input,
        out string displayName,
        out string normalizedName,
        out LibraryNameError error,
        int maxScalars = MaxScalarsPerTag)
    {
        displayName = string.Empty;
        normalizedName = string.Empty;
        error = LibraryNameError.None;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = LibraryNameError.Empty;
            return false;
        }

        // 1. Trim leading and trailing Unicode whitespace by Rune semantics
        string trimmed = TrimUnicodeWhitespace(input);
        if (string.IsNullOrEmpty(trimmed))
        {
            error = LibraryNameError.Empty;
            return false;
        }

        // 2. Validate Unicode scalar values count and control characters
        int scalarCount = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
            {
                error = LibraryNameError.ControlCharacter;
                return false;
            }
            scalarCount++;
        }

        if (scalarCount < MinScalarsPerName)
        {
            error = LibraryNameError.TooShort;
            return false;
        }

        if (scalarCount > maxScalars)
        {
            error = LibraryNameError.TooLong;
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
            error = LibraryNameError.Empty;
            return false;
        }

        foreach (var rune in finalTrimmed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
            {
                error = LibraryNameError.ControlCharacter;
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
