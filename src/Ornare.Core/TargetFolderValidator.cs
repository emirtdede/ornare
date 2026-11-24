using System.IO;

namespace Ornare.Core;

public static class TargetFolderValidator
{
    /// <summary>
    /// Windows <c>MAX_PATH</c> is 260 characters including the terminating null, so the
    /// longest representable path is 259 characters. Shell folder customization is
    /// documented as <c>MAX_PATH</c>-bounded (see docs/LONG_PATH_COMPATIBILITY.md), so a
    /// 260-character path must be rejected rather than silently truncated.
    /// </summary>
    private const int MaxSupportedPathLength = 259;

    /// <summary>
    /// Attributes that mark a cloud placeholder (OneDrive/Dropbox Files On-Demand) whose
    /// content is not locally present. Writing a folder icon into such a target would
    /// trigger an unexpected hydration or fail with a sharing violation.
    /// </summary>
    private const FileAttributes CloudPlaceholderMask =
        FileAttributes.Offline |
        (FileAttributes)0x00040000 | // FILE_ATTRIBUTE_RECALL_ON_OPEN
        (FileAttributes)0x00400000;  // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS

    public static OperationResult ValidateTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, "Folder path cannot be empty.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, "The folder path is not valid.", ex.ToString());
        }

        // Must be local drive format, not UNC
        if (fullPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Failure(ErrorCode.TargetNotLocal, "Network shares (UNC) are not supported in V1.");
        }

        // Check if path is volume root
        string? root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && string.Equals(fullPath.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Failure(ErrorCode.TargetIsVolumeRoot, "Volume roots cannot be customized directly.");
        }

        // Check max path length. MAX_PATH includes the null terminator, so 259 is the limit.
        if (fullPath.Length > MaxSupportedPathLength)
        {
            return OperationResult.Failure(
                ErrorCode.InvalidTarget,
                $"Path exceeds maximum supported length ({MaxSupportedPathLength} characters).");
        }

        // Protect Windows directory and Program Files. The separator matters: a plain
        // prefix test would also reject unrelated siblings such as "C:\Windows.old".
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (IsUnderDirectory(fullPath, winDir) ||
            IsUnderDirectory(fullPath, progFiles) ||
            IsUnderDirectory(fullPath, progFilesX86))
        {
            return OperationResult.Failure(ErrorCode.AccessDenied, "System and application program directories are protected.");
        }

        if (!Directory.Exists(fullPath))
        {
            return OperationResult.Failure(ErrorCode.TargetNotFound, "The specified directory does not exist.");
        }

        DirectoryInfo dirInfo = new(fullPath);

        // Reparse point check (symlink, junction, mount point)
        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return OperationResult.Failure(ErrorCode.TargetIsReparsePoint, "Symlinks, junctions, and reparse-point directories are not supported in V1.");
        }

        // Cloud placeholder check (OneDrive/Dropbox Files On-Demand)
        if ((dirInfo.Attributes & CloudPlaceholderMask) != 0)
        {
            return OperationResult.Failure(
                ErrorCode.TargetIsCloudPlaceholder,
                "Cloud placeholder folders must be fully downloaded before they can be customized.");
        }

        return OperationResult.Success();
    }

    /// <summary>
    /// Returns true when <paramref name="path"/> is the directory itself or a descendant
    /// of <paramref name="directory"/>, using a separator boundary so siblings with a
    /// common prefix are not misclassified.
    /// </summary>
    private static bool IsUnderDirectory(string path, string? directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        string normalizedPath = path.TrimEnd('\\', '/');
        string normalizedDirectory = directory.TrimEnd('\\', '/');

        if (string.Equals(normalizedPath, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedPath.StartsWith(normalizedDirectory + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
