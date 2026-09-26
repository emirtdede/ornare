namespace Ornare.Core;

public sealed record SearchIndexState(
    int IndexVersion,
    string Phase,
    string? LastCursor,
    bool IsReady,
    long UpdatedUtc
);
