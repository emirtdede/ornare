using System.Text.Json.Serialization;

namespace Ornare.Core;

[JsonSerializable(typeof(FolderIconSemantics))]
public sealed partial class FolderJsonContext : JsonSerializerContext
{
}
