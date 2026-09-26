using System;

namespace Ornare.Core;

public sealed class ApplyOperationResult
{
    public Guid OperationId { get; init; }
    public string CanonicalIcoPath { get; init; } = string.Empty;
    public string? RenderKey { get; init; }
    public string? ContentHash { get; init; }
}
