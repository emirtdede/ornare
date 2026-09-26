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
    IcoMalformed
}

public sealed class OperationResult
{
    public bool IsSuccess { get; }
    public ErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }

    internal OperationResult(bool isSuccess, ErrorCode errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static OperationResult Success() => new(true, ErrorCode.None, null);
    public static OperationResult Failure(ErrorCode code, string message) => new(false, code, message);

    public static OperationResult<T> Success<T>(T value) => new(true, value, ErrorCode.None, null);
    public static OperationResult<T> Failure<T>(ErrorCode code, string message) => new(false, default, code, message);
}

public sealed class OperationResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }

    internal OperationResult(bool isSuccess, T? value, ErrorCode errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
