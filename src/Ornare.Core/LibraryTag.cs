namespace Ornare.Core;

public sealed record LibraryTag(
    string Id,
    string DisplayName,
    string NormalizedName,
    long CreatedUtc
);
