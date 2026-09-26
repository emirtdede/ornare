using Ornare.Core;
using Ornare.Core.Presets;

namespace Ornare.Cli;

public enum RenderInputKind
{
    Source,
    Color,
    Preset
}

public sealed class RenderInput
{
    public string? Source { get; set; }
    public string? Color { get; set; }
    public string? Preset { get; set; }

    public string? Template { get; set; }
    public string? Mode { get; set; }
    public int? Padding { get; set; }
    public string? Background { get; set; }
    public string? Recolor { get; set; }

    public bool HasTemplate { get; set; }
    public bool HasMode { get; set; }
    public bool HasPadding { get; set; }
    public bool HasBackground { get; set; }
    public bool HasRecolor { get; set; }
}

public sealed class ValidatedRenderInput
{
    public RenderInputKind Kind { get; init; }
    public string? SourcePath { get; init; }
    public string? ColorHex { get; init; }
    public StatusPreset? Preset { get; init; }

    public string TemplateId { get; init; } = "ornare-classic";
    public ImagePlacementMode Mode { get; init; } = ImagePlacementMode.Contain;
    public int Padding { get; init; }
    public BackgroundMode BackgroundMode { get; init; } = BackgroundMode.Transparent;
    public string? BackgroundColorHex { get; init; }
    public string? RecolorHex { get; init; }
}

public sealed class RenderValidationResult
{
    public bool IsSuccess { get; init; }
    public int ExitCode { get; init; }
    public string Code { get; init; } = "InvalidArguments";
    public ErrorCode ErrorCode { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public ValidatedRenderInput? Value { get; init; }

    public static RenderValidationResult Success(ValidatedRenderInput value) => new()
    {
        IsSuccess = true,
        ExitCode = CliExitCodes.Success,
        Code = "Success",
        ErrorCode = ErrorCode.None,
        Value = value
    };

    public static RenderValidationResult Failure(int exitCode, string code, string message, ErrorCode errorCode = ErrorCode.InvalidTarget) => new()
    {
        IsSuccess = false,
        ExitCode = exitCode,
        Code = code,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}
