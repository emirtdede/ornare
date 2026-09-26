using System.Text.Json.Serialization;

namespace Ornare.Core;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(FolderIconSemantics))]
[JsonSerializable(typeof(DiagnosticReport))]
[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(FolderAssignment))]
[JsonSerializable(typeof(LibraryAsset))]
[JsonSerializable(typeof(AssignmentHistoryEntry))]
[JsonSerializable(typeof(FolderBaseline))]
[JsonSerializable(typeof(JournalEntry))]
[JsonSerializable(typeof(JournalState))]
[JsonSerializable(typeof(OperationKind))]
[JsonSerializable(typeof(ImagePlacementMode))]
[JsonSerializable(typeof(FolderCustomizationState))]
public sealed partial class FolderJsonContext : JsonSerializerContext
{
}
