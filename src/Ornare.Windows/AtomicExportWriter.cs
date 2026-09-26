using System;
using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Authoritative atomic exporter for Windows filesystem destinations (F13, F14).
/// Guarantees that export writes to a uniquely named sibling temporary file (.ornare-export-{GUID}.tmp),
/// flushes to disk, validates frames or dimensions, and commits using ReplaceFileW or MoveFileExW(MOVEFILE_WRITE_THROUGH).
/// Never overwrites or truncates an existing destination in place.
/// </summary>
public static class AtomicExportWriter
{
    private const uint MOVEFILE_REPLACE_EXISTING = 0x00000001;
    private const uint MOVEFILE_WRITE_THROUGH = 0x00000008;

    private static readonly byte[] PngHeaderMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ReplaceFileW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReplaceFileW(
        string lpReplacedFileName,
        string lpReplacementFileName,
        string? lpBackupFileName,
        uint dwReplaceFlags,
        IntPtr lpExclude,
        IntPtr lpReserved);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "MoveFileExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(
        string lpExistingFileName,
        string lpNewFileName,
        uint dwFlags);

    /// <summary>
    /// Scans the destination directory strictly for '.ornare-export-*.tmp' files older than 24 hours
    /// and deletes them safely. Skips files currently in use or locked.
    /// </summary>
    public static void CleanupStaleSiblingTemps(string destinationDirectory, TimeSpan? maxAge = null)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
        {
            return;
        }

        TimeSpan threshold = maxAge ?? TimeSpan.FromHours(24);
        DateTime utcNow = DateTime.UtcNow;

        try
        {
            DirectoryInfo dirInfo = new(destinationDirectory);
            foreach (FileInfo file in dirInfo.EnumerateFiles(".ornare-export-*.tmp"))
            {
                try
                {
                    if (utcNow - file.LastWriteTimeUtc > threshold)
                    {
                        file.Delete();
                    }
                }
                catch
                {
                    // In-use, locked, or access denied: skip safely
                }
            }
        }
        catch
        {
            // Directory enumeration failure: non-fatal
        }
    }

    /// <summary>
    /// Validates basic ICO header, minimum size (10 frames = at least 166 bytes), and container magic.
    /// </summary>
    public static bool ValidateIcoBasic(string filePath, int expectedMinFrames = 10)
    {
        try
        {
            FileInfo fi = new(filePath);
            if (fi.Length < 6 + (long)expectedMinFrames * 16)
            {
                return false;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[6];
            if (fs.Read(header, 0, 6) < 6)
            {
                return false;
            }

            if (header[0] != 0 || header[1] != 0 || header[2] != 1 || header[3] != 0)
            {
                return false;
            }

            int frameCount = header[4] | (header[5] << 8);
            return frameCount >= expectedMinFrames;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates basic PNG header, minimum size (at least 33 bytes), container signature, and chunk structure (F14).
    /// </summary>
    public static bool ValidatePngBasic(string filePath, int expectedSize = 0)
    {
        return TryGetPngInfo(filePath, expectedSize, out _, out _, out _);
    }

    /// <summary>
    /// Extracts width, height, and alpha presence directly from PNG IHDR chunk without decoding (F14).
    /// </summary>
    public static bool TryGetPngInfo(string filePath, int expectedSize, out int width, out int height, out bool hasAlpha)
    {
        width = 0;
        height = 0;
        hasAlpha = false;

        try
        {
            FileInfo fi = new(filePath);
            if (fi.Length < 33) return false;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[26];
            if (fs.Read(header, 0, 26) < 26) return false;

            for (int i = 0; i < 8; i++)
            {
                if (header[i] != PngHeaderMagic[i]) return false;
            }

            if (header[8] != 0 || header[9] != 0 || header[10] != 0 || header[11] != 13) return false;
            if (header[12] != 0x49 || header[13] != 0x48 || header[14] != 0x44 || header[15] != 0x52) return false;

            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];

            if (width <= 0 || height <= 0) return false;

            if (expectedSize > 0 && (width != expectedSize || height != expectedSize))
            {
                return false;
            }

            byte colorType = header[25];
            hasAlpha = colorType is 4 or 6;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Shared atomic commit core: writes payload to sibling temp, validates, and atomically replaces or moves.
    /// </summary>
    private static OperationResult<string> CommitAtomicPayload(
        string destinationPath,
        Action<string> writeAction,
        Func<string, bool> validator,
        string corruptErrorMessage)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return OperationResult.Failure<string>(
                ErrorCode.InvalidTarget, "Destination path cannot be empty.");
        }

        string fullDestPath = Path.GetFullPath(destinationPath);
        string? destDir = Path.GetDirectoryName(fullDestPath);
        if (string.IsNullOrEmpty(destDir))
        {
            return OperationResult.Failure<string>(
                ErrorCode.UnsupportedExportDestination, "Invalid destination directory.");
        }

        try
        {
            Directory.CreateDirectory(destDir);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<string>(
                ErrorCode.AccessDenied, $"Cannot create destination directory: {ex.Message}");
        }

        // 1. Same-directory stale-temp cleanup rule
        CleanupStaleSiblingTemps(destDir);

        // 2. Sibling temp named exactly .ornare-export-{GUID}.tmp with CREATE_NEW semantics
        Guid tempId = Guid.NewGuid();
        string tempPath = Path.Combine(destDir, $".ornare-export-{tempId:D}.tmp");

        try
        {
            writeAction(tempPath);
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            return OperationResult.Failure<string>(
                ErrorCode.FilesystemError, $"Failed to write temporary export file: {ex.Message}");
        }

        // 3. Validate temp file size/header before committing
        if (!validator(tempPath))
        {
            TryDeleteFile(tempPath);
            return OperationResult.Failure<string>(
                ErrorCode.ImageCorrupted, corruptErrorMessage);
        }

        // 4. Commit: ReplaceFileW if destination exists; MoveFileExW if destination does not exist
        bool destExisted = File.Exists(fullDestPath);
        if (destExisted)
        {
            bool replaced = ReplaceFileW(fullDestPath, tempPath, null, 0, IntPtr.Zero, IntPtr.Zero);
            if (!replaced)
            {
                int err = Marshal.GetLastWin32Error();
                TryDeleteFile(tempPath);
                return OperationResult.Failure<string>(
                    ErrorCode.UnsupportedExportDestination,
                    $"Atomic destination replacement failed (Win32 error: {err}). Existing file preserved.");
            }
        }
        else
        {
            bool moved = MoveFileExW(tempPath, fullDestPath, MOVEFILE_WRITE_THROUGH);
            if (!moved)
            {
                int err = Marshal.GetLastWin32Error();
                TryDeleteFile(tempPath);
                return OperationResult.Failure<string>(
                    ErrorCode.FilesystemError,
                    $"Failed to publish export to destination (Win32 error: {err}).");
            }
        }

        // 5. Final validation: reopen destination and verify integrity
        if (!validator(fullDestPath))
        {
            return OperationResult.Failure<string>(
                ErrorCode.ImageCorrupted, "Destination verification failed after commit.");
        }

        return OperationResult.Success(fullDestPath);
    }

    /// <summary>
    /// Exports an icon from a verified source path to a destination path using atomic replace/move semantics (F13).
    /// </summary>
    public static OperationResult<ExportIcoResult> ExportAtomically(
        string sourceIcoPath,
        string destinationPath,
        Func<string, bool>? customValidator = null,
        bool isFromCache = false)
    {
        if (string.IsNullOrWhiteSpace(sourceIcoPath) || !File.Exists(sourceIcoPath))
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.InvalidTarget, "Source icon path does not exist.");
        }

        // Validate source ICO before attempting export
        bool sourceValid = customValidator != null ? customValidator(sourceIcoPath) : ValidateIcoBasic(sourceIcoPath);
        if (!sourceValid)
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.ImageCorrupted, "Source icon file is not a valid 10-frame ICO.");
        }

        Func<string, bool> validator = customValidator ?? (p => ValidateIcoBasic(p));

        var commitResult = CommitAtomicPayload(
            destinationPath,
            tempPath =>
            {
                using var srcStream = new FileStream(sourceIcoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var dstStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                srcStream.CopyTo(dstStream);
                dstStream.Flush(flushToDisk: true);
            },
            validator,
            "Exported temporary file failed ICO verification.");

        if (!commitResult.IsSuccess || commitResult.Value == null)
        {
            return OperationResult.Failure<ExportIcoResult>(commitResult.ErrorCode, commitResult.ErrorMessage ?? "Export failed.");
        }

        string fullDestPath = commitResult.Value;
        FileInfo finalInfo = new(fullDestPath);
        return OperationResult.Success(new ExportIcoResult
        {
            DestinationPath = fullDestPath,
            FileSizeBytes = finalInfo.Length,
            FrameCount = 10,
            IsFromCache = isFromCache
        });
    }

    /// <summary>
    /// Exports PNG bytes atomically to a destination path using atomic replace/move semantics (F14).
    /// </summary>
    public static OperationResult<ExportPngResult> ExportPngAtomically(
        byte[] pngBytes,
        string destinationPath,
        int expectedSize = 0,
        bool isFromCache = false,
        string? renderKey = null)
    {
        if (pngBytes == null || pngBytes.Length == 0)
        {
            return OperationResult.Failure<ExportPngResult>(
                ErrorCode.InvalidTarget, "PNG byte payload cannot be empty.");
        }

        var commitResult = CommitAtomicPayload(
            destinationPath,
            tempPath =>
            {
                using var dstStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                dstStream.Write(pngBytes, 0, pngBytes.Length);
                dstStream.Flush(flushToDisk: true);
            },
            p => ValidatePngBasic(p, expectedSize),
            "Exported temporary file failed PNG verification.");

        if (!commitResult.IsSuccess || commitResult.Value == null)
        {
            return OperationResult.Failure<ExportPngResult>(commitResult.ErrorCode, commitResult.ErrorMessage ?? "Export failed.");
        }

        string fullDestPath = commitResult.Value;
        TryGetPngInfo(fullDestPath, expectedSize, out int width, out int height, out bool hasAlpha);
        FileInfo finalInfo = new(fullDestPath);

        return OperationResult.Success(new ExportPngResult
        {
            DestinationPath = fullDestPath,
            Width = width,
            Height = height,
            HasAlpha = hasAlpha,
            FileSizeBytes = finalInfo.Length,
            IsFromCache = isFromCache,
            RenderKey = renderKey
        });
    }

    /// <summary>
    /// Exports a PNG file from a verified source path to a destination path using atomic replace/move semantics (F14).
    /// </summary>
    public static OperationResult<ExportPngResult> ExportPngFileAtomically(
        string sourcePngPath,
        string destinationPath,
        int expectedSize = 0,
        bool isFromCache = false,
        string? renderKey = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePngPath) || !File.Exists(sourcePngPath))
        {
            return OperationResult.Failure<ExportPngResult>(
                ErrorCode.InvalidTarget, "Source PNG path does not exist.");
        }

        if (!ValidatePngBasic(sourcePngPath, expectedSize))
        {
            return OperationResult.Failure<ExportPngResult>(
                ErrorCode.ImageCorrupted, "Source PNG file failed verification.");
        }

        var commitResult = CommitAtomicPayload(
            destinationPath,
            tempPath =>
            {
                using var srcStream = new FileStream(sourcePngPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var dstStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                srcStream.CopyTo(dstStream);
                dstStream.Flush(flushToDisk: true);
            },
            p => ValidatePngBasic(p, expectedSize),
            "Exported temporary file failed PNG verification.");

        if (!commitResult.IsSuccess || commitResult.Value == null)
        {
            return OperationResult.Failure<ExportPngResult>(commitResult.ErrorCode, commitResult.ErrorMessage ?? "Export failed.");
        }

        string fullDestPath = commitResult.Value;
        TryGetPngInfo(fullDestPath, expectedSize, out int width, out int height, out bool hasAlpha);
        FileInfo finalInfo = new(fullDestPath);

        return OperationResult.Success(new ExportPngResult
        {
            DestinationPath = fullDestPath,
            Width = width,
            Height = height,
            HasAlpha = hasAlpha,
            FileSizeBytes = finalInfo.Length,
            IsFromCache = isFromCache,
            RenderKey = renderKey
        });
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
