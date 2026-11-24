namespace Ornare.Core;

public enum ErrorCode
{
    None = 0,
    InvalidTarget,
    TargetNotFound,
    TargetIsReparsePoint,
    TargetIsVolumeRoot,
    TargetNotLocal,
    AccessDenied,
    ImageDecodeFailed,
    ImageCorrupted,
    ImageExceedsLimits,
    UnsupportedImageFormat,
    IcoGenerationFailed,
    FilesystemError,
    ExternalConflict,
    JournalStateInvalid,
    ReconciliationFailed,
    ConcurrencyConflict,
    SvgNotThemeable,
    SvgSecurityViolation,
    IcoMalformed,
    UnsupportedExportDestination,
    DropLimitExceeded,
    ItemIsFileNotFolder,
    NoLastAppliedRecipe,
    NoUndoAvailable,
    TargetChanged,
    NotCustomized,
    UnsupportedTarget,
    TargetIsCloudPlaceholder,
    OperationBusy
}

/// <summary>
/// Result of a service operation.
/// <para>
/// <see cref="ErrorMessage"/> is user-facing copy: it must never contain file paths, SQL
/// schema names, HRESULTs or raw exception text (INV-UI-004). Anything diagnostic belongs in
/// <see cref="DiagnosticDetail"/>, which is written to the diagnostic log and is never
/// rendered.
/// </para>
/// </summary>
public sealed class OperationResult
{
    public bool IsSuccess { get; }
    public ErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }

    /// <summary>Machine/exception detail for logs only. Never render this.</summary>
    public string? DiagnosticDetail { get; }

    /// <summary>What a diagnostic logger should record: user copy plus technical detail.</summary>
    public string? DiagnosticDescription => DiagnosticDetail is null
        ? ErrorMessage
        : $"{ErrorMessage} | {DiagnosticDetail}";

    internal OperationResult(bool isSuccess, ErrorCode errorCode, string? errorMessage, string? diagnosticDetail = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        DiagnosticDetail = diagnosticDetail;
    }

    public static OperationResult Success() => new(true, ErrorCode.None, null);

    public static OperationResult Failure(ErrorCode code, string message, string? diagnosticDetail = null)
        => new(false, code, message, diagnosticDetail);

    public static OperationResult<T> Success<T>(T value) => new(true, value, ErrorCode.None, null);

    public static OperationResult<T> Failure<T>(ErrorCode code, string message, string? diagnosticDetail = null)
        => new(false, default, code, message, diagnosticDetail);
}

public sealed class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }

    /// <summary>Machine/exception detail for logs only. Never render this.</summary>
    public string? DiagnosticDetail { get; }

    /// <summary>What a diagnostic logger should record: user copy plus technical detail.</summary>
    public string? DiagnosticDescription => DiagnosticDetail is null
        ? ErrorMessage
        : $"{ErrorMessage} | {DiagnosticDetail}";

    internal OperationResult(bool isSuccess, T? value, ErrorCode errorCode, string? errorMessage, string? diagnosticDetail = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        DiagnosticDetail = diagnosticDetail;
    }
}
