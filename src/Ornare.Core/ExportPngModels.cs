using System;

namespace Ornare.Core;

/// <summary>
/// Constants defining permitted PNG export dimensions (F14).
/// </summary>
public static class ExportPngConstants
{
    public const int DefaultSize = 1024;
    public static readonly int[] AllowedSizes = [256, 512, 1024];

    public static bool IsAllowedSize(int size) => size is 256 or 512 or 1024;
}

/// <summary>
/// Represents a request to export an asset or recipe to a PNG file (F14).
/// </summary>
public sealed record ExportPngRequest
{
    public required string DestinationPath { get; init; }
    public int TargetSize { get; init; } = ExportPngConstants.DefaultSize;
    public RenderRecipe? Recipe { get; init; }
    public byte[]? SourceBytes { get; init; }
    public string? SourceFilePath { get; init; }
}

/// <summary>
/// Result of a rendered PNG image with metadata and cache information (F14).
/// </summary>
public sealed record RenderPngResult
{
    public required byte[] PngBytes { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required bool HasAlpha { get; init; }
    public required string RenderKey { get; init; }
    public required bool IsFromCache { get; init; }
    public string? CachedFilePath { get; init; }
}

/// <summary>
/// Represents the result of a verified atomic PNG export operation (F14).
/// </summary>
public sealed record ExportPngResult
{
    public required string DestinationPath { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required bool HasAlpha { get; init; }
    public required long FileSizeBytes { get; init; }
    public required bool IsFromCache { get; init; }
    public string? RenderKey { get; init; }
}
