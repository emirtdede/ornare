using System;
using System.Text.Json;

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
