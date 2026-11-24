using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ornare.Core;

namespace Ornare.Cli;

/// <summary>
/// Deterministic output formatter for Ornare CLI (F24).
/// Ensures that under --json, stdout contains strictly one single JSON document and nothing else.
/// Diagnostics and human error messages are directed to Console.Error.
/// <para>
/// Every writer shares one code path: JSON mode serialises the envelope, human mode prints the
/// success lines the command owns or the error line, so the error contract is defined once.
/// </para>
/// </summary>
public static class CliResponseFormatter
{
    public static void WriteApply(bool isJson, CliEnvelope<ApplyData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeApplyData, data =>
        {
            List<string> lines = [$"Applied icon successfully to: {data?.TargetFolder}"];
            if (!string.IsNullOrEmpty(data?.CanonicalIcoPath))
            {
                lines.Add($"Canonical icon: {data.CanonicalIcoPath}");
            }

            return lines;
        });

    public static void WriteApplyLast(bool isJson, CliEnvelope<ApplyLastData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeApplyLastData,
            data => [$"Applied last icon successfully to: {data?.TargetFolder}"]);

    public static void WriteReset(bool isJson, CliEnvelope<ResetData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeResetData,
            data => [$"Reset folder customization successfully: {data?.TargetFolder}"]);

    public static void WriteUndo(bool isJson, CliEnvelope<UndoData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeUndoData,
            data => [$"Undid folder customization successfully: {data?.TargetFolder}"]);

    public static void WriteInspect(bool isJson, CliEnvelope<InspectData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeInspectData, data =>
        {
            if (data == null)
            {
                return [];
            }

            List<string> lines =
            [
                $"Folder: {data.TargetFolder}",
                $"Customized: {data.IsCustomized}",
                $"State: {data.State}",
                $"desktop.ini: {(data.DesktopIniExists ? "Present" : "None")}"
            ];

            if (!string.IsNullOrEmpty(data.IconResource))
            {
                lines.Add($"Icon Resource: {data.IconResource},{data.IconResourceIndex}");
            }

            lines.Add($"Tracked Assignment: {data.HasAssignment}");
            lines.Add($"Baseline Snapshot: {data.HasBaseline}");
            return lines;
        });

    public static void WriteExportIco(bool isJson, CliEnvelope<ExportIcoData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeExportIcoData,
            data => [$"Exported ICO successfully to: {data?.OutputPath} ({data?.FileSizeBytes} bytes)"]);

    public static void WriteExportPng(bool isJson, CliEnvelope<ExportPngData> envelope)
        => Write(isJson, envelope, CliJsonContext.Default.CliEnvelopeExportPngData,
            data => [$"Exported PNG successfully to: {data?.OutputPath} ({data?.Width}x{data?.Height}, {data?.FileSizeBytes} bytes)"]);

    public static void WritePlan(bool isJson, CliEnvelope<OperationPlan> envelope)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeOperationPlan));
            return;
        }

        OperationPlan? plan = envelope.Data;
        if (plan == null)
        {
            WriteErrorLine(envelope.Code, envelope.Message);
            return;
        }

        TextWriter writer = envelope.Success ? Console.Out : Console.Error;
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
            foreach (string condition in plan.IndeterminateConditions)
            {
                writer.WriteLine($"  - {condition}");
            }
        }
    }

    /// <summary>
    /// Writes a command failure. <paramref name="message"/> must be user-facing copy;
    /// <paramref name="diagnostic"/> is technical detail and is only ever written to stderr so
    /// the machine-readable stdout contract (one JSON document) is untouched.
    /// </summary>
    public static void WriteGeneralError(
        bool isJson,
        string command,
        string code,
        string message,
        string? operationId = null,
        string? diagnostic = null)
    {
        if (isJson)
        {
            var envelope = new CliEnvelope<object>
            {
                SchemaVersion = 1,
                Command = command,
                Success = false,
                Code = code,
                Message = message,
                OperationId = operationId,
                Data = null
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, CliJsonContext.Default.CliEnvelopeObject));
        }
        else
        {
            WriteErrorLine(code, message);
        }

        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            Console.Error.WriteLine($"diagnostic: {diagnostic}");
        }
    }

    private static void Write<T>(
        bool isJson,
        CliEnvelope<T> envelope,
        JsonTypeInfo<CliEnvelope<T>> typeInfo,
        Func<T?, IEnumerable<string>> successLines)
    {
        if (isJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(envelope, typeInfo));
            return;
        }

        if (!envelope.Success)
        {
            WriteErrorLine(envelope.Code, envelope.Message);
            return;
        }

        foreach (string line in successLines(envelope.Data))
        {
            Console.Out.WriteLine(line);
        }
    }

    private static void WriteErrorLine(string code, string message)
        => Console.Error.WriteLine($"Error [{code}]: {message}");
}
