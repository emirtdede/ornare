using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// The only place a file is committed with a Win32 atomic rename.
/// <para>
/// Two very different payloads need the same guarantee: a folder's <c>desktop.ini</c> and a
/// user's exported ICO/PNG. They previously carried separate <c>MoveFileExW</c> declarations,
/// which meant the extended-length path handling and the write-through flag had to be fixed
/// twice. Both now commit through this type.
/// </para>
/// <para>
/// Every call passes paths through <see cref="LongPath.ToExtended"/>: a 250-character folder
/// produces a 262-character <c>desktop.ini</c> path, and without the prefix the rename fails
/// with <c>ERROR_PATH_NOT_FOUND</c> for a file that exists.
/// </para>
/// </summary>
internal static class AtomicFileWriter
{
    private const uint MoveFileReplaceExisting = 0x00000001;
    private const uint MoveFileWriteThrough = 0x00000008;
    private const uint ReplaceFileIgnoreMergeErrors = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "MoveFileExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ReplaceFileW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReplaceFileW(
        string lpReplacedFileName,
        string lpReplacementFileName,
        string? lpBackupFileName,
        uint dwReplaceFlags,
        IntPtr lpExclude,
        IntPtr lpReserved);

    /// <summary>
    /// Commits <paramref name="tempPath"/> over an existing destination, or creates the
    /// destination when it does not exist yet. Write-through means the rename is durable when the
    /// call returns, so a power loss cannot leave an empty file behind.
    /// </summary>
    public static bool Commit(string tempPath, string destinationPath, out int win32Error)
    {
        win32Error = 0;

        if (MoveFileExW(
                LongPath.ToExtended(tempPath),
                LongPath.ToExtended(destinationPath),
                MoveFileReplaceExisting | MoveFileWriteThrough))
        {
            return true;
        }

        win32Error = Marshal.GetLastWin32Error();
        return false;
    }

    /// <summary>
    /// Replaces an existing destination while keeping its identity, extended attributes and ACLs
    /// (<c>ReplaceFileW</c>), which matters for an export overwriting a file the user already has
    /// open in another program. Falls back to a write-through rename when the destination does not
    /// exist or cannot be replaced.
    /// </summary>
    public static bool ReplaceOrCommit(string tempPath, string destinationPath, out int win32Error)
    {
        win32Error = 0;

        if (File.Exists(destinationPath))
        {
            if (ReplaceFileW(
                    LongPath.ToExtended(destinationPath),
                    LongPath.ToExtended(tempPath),
                    null,
                    ReplaceFileIgnoreMergeErrors,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                return true;
            }

            int replaceError = Marshal.GetLastWin32Error();

            // Some filesystems and destinations (for example a share that does not support
            // ReplaceFile) reject the replace but accept a rename. Only fall back when the
            // destination still exists, otherwise the rename would report a misleading error.
            if (!File.Exists(destinationPath))
            {
                win32Error = replaceError;
                return false;
            }
        }

        if (Commit(tempPath, destinationPath, out win32Error))
        {
            return true;
        }

        return false;
    }
}
