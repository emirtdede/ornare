using System;
using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Authoritative atomic exporter for Windows filesystem destinations (F13).
/// Guarantees that export writes to a uniquely named sibling temporary file (.ornare-export-{GUID}.tmp),
/// flushes to disk, validates frames, and commits using ReplaceFileW or MoveFileExW(MOVEFILE_WRITE_THROUGH).
/// Never overwrites or truncates an existing destination in place.
/// </summary>
public static class AtomicExportWriter
{
    private const uint MOVEFILE_REPLACE_EXISTING = 0x00000001;
    private const uint MOVEFILE_WRITE_THROUGH = 0x00000008;

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
    /// Exports an icon from a verified source path to a destination path using atomic replace/move semantics.
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

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.InvalidTarget, "Destination path cannot be empty.");
        }

        // Validate source ICO before attempting export
        bool sourceValid = customValidator != null ? customValidator(sourceIcoPath) : ValidateIcoBasic(sourceIcoPath);
        if (!sourceValid)
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.ImageCorrupted, "Source icon file is not a valid 10-frame ICO.");
        }

        string fullDestPath = Path.GetFullPath(destinationPath);
        string? destDir = Path.GetDirectoryName(fullDestPath);
        if (string.IsNullOrEmpty(destDir))
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.UnsupportedExportDestination, "Invalid destination directory.");
        }

        try
        {
            Directory.CreateDirectory(destDir);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.AccessDenied, $"Cannot create destination directory: {ex.Message}");
        }

        // 1. Same-directory stale-temp cleanup rule
        CleanupStaleSiblingTemps(destDir);

        // 2. Sibling temp named exactly .ornare-export-{GUID}.tmp with CREATE_NEW semantics
        Guid tempId = Guid.NewGuid();
        string tempPath = Path.Combine(destDir, $".ornare-export-{tempId:D}.tmp");

        try
        {
            using (var srcStream = new FileStream(sourceIcoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var dstStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                srcStream.CopyTo(dstStream);
                dstStream.Flush(flushToDisk: true);
            }
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.FilesystemError, $"Failed to write temporary export file: {ex.Message}");
        }

        // 3. Validate temp file size/header/frame set before committing
        bool tempValid = customValidator != null ? customValidator(tempPath) : ValidateIcoBasic(tempPath);
        if (!tempValid)
        {
            TryDeleteFile(tempPath);
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.ImageCorrupted, "Exported temporary file failed ICO verification.");
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
                return OperationResult.Failure<ExportIcoResult>(
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
                return OperationResult.Failure<ExportIcoResult>(
                    ErrorCode.FilesystemError,
                    $"Failed to publish export to destination (Win32 error: {err}).");
            }
        }

        // 5. Final validation: reopen destination and verify integrity
        bool finalValid = customValidator != null ? customValidator(fullDestPath) : ValidateIcoBasic(fullDestPath);
        if (!finalValid)
        {
            return OperationResult.Failure<ExportIcoResult>(
                ErrorCode.ImageCorrupted, "Destination verification failed after commit.");
        }

        FileInfo finalInfo = new(fullDestPath);
        return OperationResult.Success(new ExportIcoResult
        {
            DestinationPath = fullDestPath,
            FileSizeBytes = finalInfo.Length,
            FrameCount = 10,
            IsFromCache = isFromCache
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
