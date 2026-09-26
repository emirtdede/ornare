namespace Ornare.Core;

public sealed class FolderInspectionResult
{
    public string TargetFolder { get; init; } = string.Empty;
    public bool IsCustomized { get; init; }
    public FolderCustomizationState State { get; init; }
    public bool DesktopIniExists { get; init; }
    public string? IconResource { get; init; }
    public int IconResourceIndex { get; init; }
    public bool HasBaseline { get; init; }
    public bool HasAssignment { get; init; }
    public string? CanonicalIcoPath { get; init; }
    public uint? VolumeSerialNumber { get; init; }
    public ulong? FileId { get; init; }
}
