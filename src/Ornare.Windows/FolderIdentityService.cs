namespace Ornare.Windows;

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Ornare.Core;

public static class FolderIdentityService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }

    private enum FILE_ID_TYPE : uint
    {
        FileIdType = 0,
        ObjectIdType = 1,
        ExtendedFileIdType = 2,
        MaximumFileIdType = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct FILE_ID_DESCRIPTOR
    {
        [FieldOffset(0)]
        public uint dwSize;

        [FieldOffset(4)]
        public FILE_ID_TYPE Type;

        [FieldOffset(8)]
        public ulong FileId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle OpenFileById(
        SafeFileHandle hVolumeHint,
        [In] ref FILE_ID_DESCRIPTOR lpFileId,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwFlagsAndAttributes);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle hFile,
        [Out] char[] lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationW(
        string lpRootPathName,
        [Out] char[]? lpVolumeNameBuffer,
        uint nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        [Out] char[] lpFileSystemNameBuffer,
        uint nFileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetDriveTypeW(string lpRootPathName);

    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    private const uint DRIVE_FIXED = 3;
    private const uint FILE_NAME_NORMALIZED = 0x0;
    private const uint VOLUME_NAME_DOS = 0x0;

    public static NtfsFolderIdentity? GetNtfsIdentity(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return null;
        }

        try
        {
            using SafeFileHandle handle = CreateFileW(
                folderPath,
                0, // 0 = query attributes only, zero read/write access required
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                return null;
            }

            if (GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION info))
            {
                ulong fileId64 = ((ulong)info.nFileIndexHigh << 32) | (ulong)info.nFileIndexLow;
                return new NtfsFolderIdentity(info.dwVolumeSerialNumber, fileId64);
            }
        }
        catch
        {
            // Graceful fallback if permission or volume doesn't support file index
        }

        return null;
    }

    public static (string? VolumeSerial, string? FileId) GetFolderIdentity(string folderPath)
    {
        var identity = GetNtfsIdentity(folderPath);
        if (identity.HasValue)
        {
            return (identity.Value.VolumeSerialHex, identity.Value.FileIdHex);
        }

        return (null, null);
    }

    public static bool IsSupportedFixedNtfsVolume(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string? root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!root.EndsWith('\\'))
        {
            root += '\\';
        }

        uint driveType = GetDriveTypeW(root);
        if (driveType != DRIVE_FIXED)
        {
            return false;
        }

        char[] fsNameBuffer = new char[64];
        if (!GetVolumeInformationW(root, null, 0, out _, out _, out _, fsNameBuffer, (uint)fsNameBuffer.Length))
        {
            return false;
        }

        int nullIdx = Array.IndexOf(fsNameBuffer, '\0');
        string name = nullIdx >= 0 ? new string(fsNameBuffer, 0, nullIdx) : new string(fsNameBuffer);
        return string.Equals(name, "NTFS", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryReopenAndReconcilePath(
        uint volumeSerialNumber,
        ulong fileId64,
        string? volumeHintPath,
        out string? canonicalPath)
    {
        canonicalPath = null;
        if (fileId64 == 0)
        {
            return false;
        }

        // Determine volume root
        string? volumeRoot = null;
        if (!string.IsNullOrWhiteSpace(volumeHintPath))
        {
            volumeRoot = Path.GetPathRoot(volumeHintPath);
        }

        if (string.IsNullOrEmpty(volumeRoot) || !Directory.Exists(volumeRoot))
        {
            volumeRoot = FindVolumeRootBySerialNumber(volumeSerialNumber);
        }

        if (string.IsNullOrEmpty(volumeRoot) || !Directory.Exists(volumeRoot))
        {
            return false;
        }

        if (!volumeRoot.EndsWith('\\'))
        {
            volumeRoot += '\\';
        }

        if (!IsSupportedFixedNtfsVolume(volumeRoot))
        {
            return false;
        }

        try
        {
            using SafeFileHandle hVolumeHint = CreateFileW(
                volumeRoot,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (hVolumeHint.IsInvalid)
            {
                return false;
            }

            FILE_ID_DESCRIPTOR desc = new()
            {
                dwSize = (uint)Marshal.SizeOf<FILE_ID_DESCRIPTOR>(),
                Type = FILE_ID_TYPE.FileIdType,
                FileId = fileId64
            };

            using SafeFileHandle reopenedHandle = OpenFileById(
                hVolumeHint,
                ref desc,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                FILE_FLAG_BACKUP_SEMANTICS);

            if (reopenedHandle.IsInvalid)
            {
                return false;
            }

            if (!GetFileInformationByHandle(reopenedHandle, out BY_HANDLE_FILE_INFORMATION info))
            {
                return false;
            }

            ulong reopenedFileId = ((ulong)info.nFileIndexHigh << 32) | (ulong)info.nFileIndexLow;
            if (info.dwVolumeSerialNumber != volumeSerialNumber || reopenedFileId != fileId64)
            {
                return false;
            }

            char[] pathBuffer = new char[1024];
            uint ret = GetFinalPathNameByHandleW(reopenedHandle, pathBuffer, (uint)pathBuffer.Length, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
            if (ret == 0)
            {
                return false;
            }

            if (ret > (uint)pathBuffer.Length)
            {
                pathBuffer = new char[ret + 1];
                ret = GetFinalPathNameByHandleW(reopenedHandle, pathBuffer, (uint)pathBuffer.Length, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
                if (ret == 0)
                {
                    return false;
                }
            }

            string rawPath = new string(pathBuffer, 0, (int)ret);
            canonicalPath = NormalizeDosPath(rawPath);
            return !string.IsNullOrEmpty(canonicalPath);
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeDosPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        string trimmed = rawPath.Trim();
        if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            if (trimmed.Length >= 6 && char.IsLetter(trimmed[4]) && trimmed[5] == ':')
            {
                trimmed = trimmed.Substring(4);
            }
        }

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    public static string? FindVolumeRootBySerialNumber(uint volumeSerialNumber)
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                string root = drive.RootDirectory.FullName;
                char[] fsNameBuffer = new char[64];
                if (GetVolumeInformationW(root, null, 0, out uint serial, out _, out _, fsNameBuffer, (uint)fsNameBuffer.Length))
                {
                    int nullIdx = Array.IndexOf(fsNameBuffer, '\0');
                    string name = nullIdx >= 0 ? new string(fsNameBuffer, 0, nullIdx) : new string(fsNameBuffer);
                    if (serial == volumeSerialNumber && string.Equals(name, "NTFS", StringComparison.OrdinalIgnoreCase))
                    {
                        return root;
                    }
                }
            }
        }
        catch
        {
            // Fail closed
        }

        return null;
    }
}
