using System;

namespace Ornare.Core;

/// <summary>
/// Specifies the import classification applied to an incoming ICO file (F12).
/// </summary>
public enum IcoImportKind
{
    /// <summary>
    /// The incoming ICO satisfies all fast-path compliance rules (all 10 Ornare sizes present, square, bounds respected).
    /// Stored directly without re-encoding.
    /// </summary>
    IcoCompliant = 1,

    /// <summary>
    /// The incoming ICO was missing required sizes or contained non-standard frames, and was normalized
    /// using deterministic frame selection.
    /// </summary>
    IcoNormalized = 2
}

/// <summary>
/// Represents the result of an ICO import operation (F12).
/// </summary>
public sealed record IcoImportResult
{
    public required string CanonicalHash { get; init; }
    public required string CanonicalPath { get; init; }
    public required IcoImportKind ImportKind { get; init; }
    public required int[] AvailableSizes { get; init; }
    public required string RenderKey { get; init; }
    public required bool IsFastPath { get; init; }
}
