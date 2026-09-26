using System.Collections.Generic;
using System.Text.Json.Serialization;
using Ornare.Core;

namespace Ornare.Cli;

/// <summary>
/// Source-generated JsonSerializerContext for Native AOT and zero reflection in Ornare CLI (F24/F25).
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CliEnvelope<ApplyData>))]
[JsonSerializable(typeof(CliEnvelope<ApplyLastData>))]
[JsonSerializable(typeof(CliEnvelope<ResetData>))]
[JsonSerializable(typeof(CliEnvelope<UndoData>))]
[JsonSerializable(typeof(CliEnvelope<InspectData>))]
[JsonSerializable(typeof(CliEnvelope<ExportIcoData>))]
[JsonSerializable(typeof(CliEnvelope<ExportPngData>))]
[JsonSerializable(typeof(CliEnvelope<OperationPlan>))]
[JsonSerializable(typeof(CliEnvelope<object>))]
[JsonSerializable(typeof(ApplyData))]
[JsonSerializable(typeof(ApplyLastData))]
[JsonSerializable(typeof(ResetData))]
[JsonSerializable(typeof(UndoData))]
[JsonSerializable(typeof(InspectData))]
[JsonSerializable(typeof(ExportIcoData))]
[JsonSerializable(typeof(ExportPngData))]
[JsonSerializable(typeof(OperationPlan))]
[JsonSerializable(typeof(PlanBlocker))]
[JsonSerializable(typeof(PlanStatus))]
[JsonSerializable(typeof(List<PlanBlocker>))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class CliJsonContext : JsonSerializerContext
{
}
