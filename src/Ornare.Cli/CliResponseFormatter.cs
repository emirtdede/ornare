using System;
using System.Text.Json;
using Ornare.Core;

namespace Ornare.Cli;

/// <summary>
/// Deterministic output formatter for Ornare CLI (F24).
/// Ensures that under --json, stdout contains strictly one single JSON document and nothing else.
/// Diagnostics and human error messages are directed to Console.Error.
/// </summary>
public static class CliResponseFormatter
{
    public static void WriteApply(bool isJson, CliEnvelope<ApplyData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeApplyData));
        }
        else
        {
            if (envelope.Success)
            {
                Console.Out.WriteLine($"Applied icon successfully to: {envelope.Data?.TargetFolder}");
                if (!string.IsNullOrEmpty(envelope.Data?.CanonicalIcoPath))
                {
                    Console.Out.WriteLine($"Canonical icon: {envelope.Data.CanonicalIcoPath}");
                }
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WriteApplyLast(bool isJson, CliEnvelope<ApplyLastData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeApplyLastData));
        }
        else
        {
            if (envelope.Success)
            {
                Console.Out.WriteLine($"Applied last icon successfully to: {envelope.Data?.TargetFolder}");
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WriteReset(bool isJson, CliEnvelope<ResetData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeResetData));
        }
        else
        {
            if (envelope.Success)
            {
                Console.Out.WriteLine($"Reset folder customization successfully: {envelope.Data?.TargetFolder}");
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WriteUndo(bool isJson, CliEnvelope<UndoData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeUndoData));
        }
        else
        {
            if (envelope.Success)
            {
                Console.Out.WriteLine($"Undid folder customization successfully: {envelope.Data?.TargetFolder}");
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WriteInspect(bool isJson, CliEnvelope<InspectData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeInspectData));
        }
        else
        {
            if (envelope.Success && envelope.Data != null)
            {
                var d = envelope.Data;
                Console.Out.WriteLine($"Folder: {d.TargetFolder}");
                Console.Out.WriteLine($"Customized: {d.IsCustomized}");
                Console.Out.WriteLine($"State: {d.State}");
                Console.Out.WriteLine($"desktop.ini: {(d.DesktopIniExists ? "Present" : "None")}");
                if (!string.IsNullOrEmpty(d.IconResource))
                {
                    Console.Out.WriteLine($"Icon Resource: {d.IconResource},{d.IconResourceIndex}");
                }
                Console.Out.WriteLine($"Tracked Assignment: {d.HasAssignment}");
                Console.Out.WriteLine($"Baseline Snapshot: {d.HasBaseline}");
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WriteExportIco(bool isJson, CliEnvelope<ExportIcoData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeExportIcoData));
        }
        else
        {
            if (envelope.Success)
            {
                Console.Out.WriteLine($"Exported ICO successfully to: {envelope.Data?.OutputPath} ({envelope.Data?.FileSizeBytes} bytes)");
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WriteExportPng(bool isJson, CliEnvelope<ExportPngData> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeExportPngData));
        }
        else
        {
            if (envelope.Success)
            {
                Console.Out.WriteLine($"Exported PNG successfully to: {envelope.Data?.OutputPath} ({envelope.Data?.Width}x{envelope.Data?.Height}, {envelope.Data?.FileSizeBytes} bytes)");
            }
            else
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
            }
        }
    }

    public static void WritePlan(bool isJson, CliEnvelope<OperationPlan> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeOperationPlan));
        }
        else
        {
            var plan = envelope.Data;
            if (plan == null)
            {
                Console.Error.WriteLine($"Error [{envelope.Code}]: {envelope.MessageKey}");
                return;
            }

            var writer = envelope.Success ? Console.Out : Console.Error;
            writer.WriteLine($"[DRY-RUN] Operation Plan for '{envelope.Command}'");
            writer.WriteLine($"Status: {plan.Status}");
            writer.WriteLine($"Target: {plan.Target}");
            if (!string.IsNullOrEmpty(plan.TargetIdentity))
            {
                writer.WriteLine($"Target Identity: {plan.TargetIdentity}");
            }
            if (!string.IsNullOrEmpty(plan.SourceMetadata))
            {
                writer.WriteLine($"Source Metadata: {plan.SourceMetadata}");
            }
            if (!string.IsNullOrEmpty(plan.CacheExpectation))
            {
                writer.WriteLine($"Cache Expectation: {plan.CacheExpectation}");
            }
            if (!string.IsNullOrEmpty(plan.ExpectedArtifact))
            {
                writer.WriteLine($"Expected Artifact: {plan.ExpectedArtifact}");
            }
            if (plan.ExpectedArtifactSizeBytes.HasValue)
            {
                writer.WriteLine($"Expected Size: {plan.ExpectedArtifactSizeBytes.Value} bytes");
            }

            if (plan.PlannedActions.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine("Planned Actions:");
                for (int i = 0; i < plan.PlannedActions.Count; i++)
                {
                    writer.WriteLine($"  [{i + 1}] {plan.PlannedActions[i]}");
                }
            }

            if (plan.Blockers.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine("Blockers:");
                for (int i = 0; i < plan.Blockers.Count; i++)
                {
                    writer.WriteLine($"  [{i + 1}] {plan.Blockers[i].Code}: {plan.Blockers[i].Message}");
                }
            }

            if (plan.IndeterminateConditions.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine("Indeterminate Conditions:");
                foreach (var condition in plan.IndeterminateConditions)
                {
                    writer.WriteLine($"  - {condition}");
                }
            }
        }
    }

    public static void WriteGeneralError(bool isJson, string command, string code, string message, string? operationId = null)
    {
        if (isJson)
        {
            var envelope = new CliEnvelope<object>
            {
                SchemaVersion = 1,
                Command = command,
                Success = false,
                Code = code,
                MessageKey = message,
                OperationId = operationId,
                Data = null
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeObject));
        }
        else
        {
            Console.Error.WriteLine($"Error [{code}]: {message}");
        }
    }
}
