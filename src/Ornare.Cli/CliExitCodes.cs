using Ornare.Core;

namespace Ornare.Cli;

/// <summary>
/// Authoritative exit code contracts for Ornare CLI (F24).
/// Fixed mapping:
/// 0: Success
/// 2: Invalid arguments
/// 3: Unsupported target / input
/// 4: Conflict / not applicable
/// 5: Access / storage error
/// 6: Operation failed or recovery required
/// 10: Unexpected internal error
/// </summary>
public static class CliExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 2;
    public const int UnsupportedTarget = 3;
    public const int Conflict = 4;
    public const int StorageError = 5;
    public const int OperationFailed = 6;
    public const int UnexpectedError = 10;

    public static int FromErrorCode(ErrorCode errorCode) => errorCode switch
    {
        ErrorCode.None => Success,

        ErrorCode.InvalidTarget => UnsupportedTarget,
        ErrorCode.TargetNotFound => UnsupportedTarget,
        ErrorCode.TargetIsReparsePoint => UnsupportedTarget,
        ErrorCode.TargetIsVolumeRoot => UnsupportedTarget,
        ErrorCode.TargetNotLocal => UnsupportedTarget,
        ErrorCode.ItemIsFileNotFolder => UnsupportedTarget,
        ErrorCode.UnsupportedTarget => UnsupportedTarget,
        ErrorCode.UnsupportedImageFormat => UnsupportedTarget,
        ErrorCode.SvgNotThemeable => UnsupportedTarget,
        ErrorCode.SvgSecurityViolation => UnsupportedTarget,
        ErrorCode.ImageCorrupted => UnsupportedTarget,
        ErrorCode.ImageExceedsLimits => UnsupportedTarget,
        ErrorCode.ImageDecodeFailed => UnsupportedTarget,
        ErrorCode.IcoMalformed => UnsupportedTarget,
        ErrorCode.UnsupportedExportDestination => UnsupportedTarget,
        ErrorCode.DropLimitExceeded => UnsupportedTarget,

        ErrorCode.ExternalConflict => Conflict,
        ErrorCode.ConcurrencyConflict => Conflict,
        ErrorCode.TargetChanged => Conflict,
        ErrorCode.NoLastAppliedRecipe => Conflict,
        ErrorCode.NoUndoAvailable => Conflict,
        ErrorCode.NotCustomized => Conflict,

        ErrorCode.AccessDenied => StorageError,
        ErrorCode.FilesystemError => StorageError,

        ErrorCode.IcoGenerationFailed => OperationFailed,
        ErrorCode.JournalStateInvalid => OperationFailed,
        ErrorCode.ReconciliationFailed => OperationFailed,

        _ => UnexpectedError
    };
}
