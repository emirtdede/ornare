using System.IO;

namespace Ornare.Core;

public static class TargetFolderValidator
{
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
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, $"Invalid path format: {ex.Message}");
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

        // Check max path length
        if (fullPath.Length > 260)
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, "Path exceeds maximum supported length (260 characters).");
        }

        // Protect Windows directory and Program Files
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if ((!string.IsNullOrEmpty(winDir) && fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(progFiles) && fullPath.StartsWith(progFiles, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(progFilesX86) && fullPath.StartsWith(progFilesX86, StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResult.Failure(ErrorCode.AccessDenied, "System and application program directories are protected.");
        }

        if (!Directory.Exists(fullPath))
        {
            return OperationResult.Failure(ErrorCode.TargetNotFound, "The specified directory does not exist.");
        }

        DirectoryInfo dirInfo = new(fullPath);

        // Reparse point check (symlink, junction)
        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return OperationResult.Failure(ErrorCode.TargetIsReparsePoint, "Symlinks, junctions, and reparse-point directories are not supported in V1.");
        }

        return OperationResult.Success();
    }
}
