namespace Ornare.Core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "LibraryCollection is the domain entity name for a user-created library collection.")]
public sealed record LibraryCollection(
    string Id,
    string DisplayName,
    string NormalizedName,
    long SortKey,
    long CreatedUtc,
    long UpdatedUtc
);
