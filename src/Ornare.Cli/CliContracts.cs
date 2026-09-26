using System;

namespace Ornare.Cli;

/// <summary>
/// Stable JSON envelope for Ornare CLI machine-readable output (F24).
/// Schema version 1 is frozen for this release.
/// </summary>
public sealed record CliEnvelope<T>
{
    public int SchemaVersion { get; init; } = 1;
    public string Command { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string MessageKey { get; init; } = string.Empty;
    public string? OperationId { get; init; }
    public T? Data { get; init; }
}

public static class CliEnvelope
{
    public static CliEnvelope<T> CreateSuccess<T>(string command, T data, string? operationId = null) => new()
    {
        SchemaVersion = 1,
        Command = command,
        Success = true,
        Code = "Success",
        MessageKey = "Success",
        OperationId = operationId,
        Data = data
    };

    public static CliEnvelope<T> CreateFailure<T>(string command, string code, string messageKey, string? operationId = null, T? data = default) => new()
    {
        SchemaVersion = 1,
        Command = command,
        Success = false,
        Code = code,
        MessageKey = messageKey,
        OperationId = operationId,
        Data = data
    };
}

public sealed record ApplyData
{
    public string TargetFolder { get; init; } = string.Empty;
    public string CanonicalIcoPath { get; init; } = string.Empty;
    public string? RenderKey { get; init; }
    public string? ContentHash { get; init; }
}

public sealed record ApplyLastData
{
    public string TargetFolder { get; init; } = string.Empty;
    public string CanonicalIcoPath { get; init; } = string.Empty;
    public string? RenderKey { get; init; }
}

public sealed record ResetData
{
    public string TargetFolder { get; init; } = string.Empty;
    public string? PriorState { get; init; }
}

public sealed record UndoData
{
    public string TargetFolder { get; init; } = string.Empty;
    public string? RestoredSemantics { get; init; }
}

public sealed record InspectData
{
    public string TargetFolder { get; init; } = string.Empty;
    public bool IsCustomized { get; init; }
    public string State { get; init; } = string.Empty;
    public bool DesktopIniExists { get; init; }
    public string? IconResource { get; init; }
    public int IconResourceIndex { get; init; }
    public bool HasBaseline { get; init; }
    public bool HasAssignment { get; init; }
    public string? CanonicalIcoPath { get; init; }
    public uint? VolumeSerialNumber { get; init; }
    public ulong? FileId { get; init; }
}

public sealed record ExportIcoData
{
    public string OutputPath { get; init; } = string.Empty;
    public string? RenderKey { get; init; }
    public long FileSizeBytes { get; init; }
    public int[] FrameSizes { get; init; } = [];
}

public sealed record ExportPngData
{
    public string OutputPath { get; init; } = string.Empty;
    public string? RenderKey { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long FileSizeBytes { get; init; }
}
