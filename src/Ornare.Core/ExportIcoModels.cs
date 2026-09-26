using System;

namespace Ornare.Core;

/// <summary>
/// Represents a request to export an icon to a user-selected filesystem path (F13).
/// </summary>
public sealed record ExportIcoRequest
{
    public required string SourceIcoPath { get; init; }
    public required string DestinationPath { get; init; }
    public string? RenderKey { get; init; }
}

/// <summary>
/// Represents the result of a verified atomic ICO export operation (F13).
/// </summary>
public sealed record ExportIcoResult
{
    public required string DestinationPath { get; init; }
    public required long FileSizeBytes { get; init; }
    public required int FrameCount { get; init; }
    public required bool IsFromCache { get; init; }
}
