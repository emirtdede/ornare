using System.Text.Json;

namespace Ornare.Core;

public sealed record FolderIconSemantics
{
    public string? IconResource { get; init; }
    public string? IconFile { get; init; }
    public int? IconIndex { get; init; }
    public bool AttributesIntroduced { get; init; }
    public string? RawUnrelatedContent { get; init; }

    public bool HasCustomIcon =>
        !string.IsNullOrWhiteSpace(IconResource) || !string.IsNullOrWhiteSpace(IconFile);

    public string ToJson() =>
        JsonSerializer.Serialize(this, FolderJsonContext.Default.FolderIconSemantics);

    public static FolderIconSemantics FromJson(string json) =>
        JsonSerializer.Deserialize(json, FolderJsonContext.Default.FolderIconSemantics) ?? new FolderIconSemantics();
}
