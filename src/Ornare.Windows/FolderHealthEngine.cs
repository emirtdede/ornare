namespace Ornare.Windows;

using System;
using System.IO;
using Ornare.Core;

public sealed record FolderHealthCheckResult
{
    public required string FolderPath { get; init; }
    public required FolderCustomizationState State { get; init; }
    public string? ConflictDetails { get; init; }
    public bool CanAutoRepair { get; init; }
}

public static class FolderHealthEngine
{
    private const string RelativeIconSubPath = @".ornare\folder.ico";

    public static FolderHealthCheckResult CheckHealth(string folderPath, FolderAssignment? assignment = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return new FolderHealthCheckResult
            {
                FolderPath = folderPath,
                State = FolderCustomizationState.MissingTarget,
                ConflictDetails = "Folder path does not exist on disk.",
                CanAutoRepair = false
            };
        }

        string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
        string ownedIcoPath = Path.Combine(folderPath, RelativeIconSubPath);

        // If desktop.ini doesn't exist:
        if (!File.Exists(iniPath))
        {
            if (File.Exists(ownedIcoPath))
            {
                // .ornare\folder.ico exists, but desktop.ini missing -> NeedsAttention / NeedsReconciliation
                return new FolderHealthCheckResult
                {
                    FolderPath = folderPath,
                    State = FolderCustomizationState.NeedsReconciliation,
                    ConflictDetails = "Ornare icon exists but desktop.ini configuration is missing.",
                    CanAutoRepair = true
                };
            }

            return new FolderHealthCheckResult
            {
                FolderPath = folderPath,
                State = FolderCustomizationState.MissingOwnedIcon,
                ConflictDetails = "Folder is not customized or customization files were deleted.",
                CanAutoRepair = false
            };
        }

        FolderIconSemantics liveSemantics = DesktopIniParser.ReadSemantics(folderPath);

        // Check if IconResource points to Ornare
        bool pointsToOrnare = !string.IsNullOrEmpty(liveSemantics.IconResource) &&
                              liveSemantics.IconResource.Contains(OrnareConstants.CanonicalFolderName, StringComparison.OrdinalIgnoreCase);

        if (!pointsToOrnare)
        {
            // If desktop.ini has custom icon pointing elsewhere -> ExternalIconConflict!
            if (!string.IsNullOrEmpty(liveSemantics.IconResource))
            {
                return new FolderHealthCheckResult
                {
                    FolderPath = folderPath,
                    State = FolderCustomizationState.ExternalIconConflict,
                    ConflictDetails = $"IconResource points to external target: '{liveSemantics.IconResource}'.",
                    CanAutoRepair = false
                };
            }
        }

        // If it points to Ornare, check if owned ICO exists
        if (!File.Exists(ownedIcoPath))
        {
            return new FolderHealthCheckResult
            {
                FolderPath = folderPath,
                State = FolderCustomizationState.MissingOwnedIcon,
                ConflictDetails = "desktop.ini points to Ornare icon, but .ornare\\folder.ico is missing.",
                CanAutoRepair = true // Can repair if backup ICO or library asset available
            };
        }

        // Check attributes
        DirectoryInfo dirInfo = new(folderPath);
        bool hasReadOnly = (dirInfo.Attributes & (FileAttributes.ReadOnly | FileAttributes.System)) != 0;
        FileAttributes iniAttrs = File.GetAttributes(iniPath);
        bool iniHasHiddenSystem = (iniAttrs & FileAttributes.Hidden) != 0 && (iniAttrs & FileAttributes.System) != 0;

        if (!hasReadOnly || !iniHasHiddenSystem)
        {
            return new FolderHealthCheckResult
            {
                FolderPath = folderPath,
                State = FolderCustomizationState.NeedsReconciliation,
                ConflictDetails = $"Required filesystem attributes are missing (Folder +r: {hasReadOnly}, desktop.ini +h+s: {iniHasHiddenSystem}).",
                CanAutoRepair = true
            };
        }

        return new FolderHealthCheckResult
        {
            FolderPath = folderPath,
            State = FolderCustomizationState.Healthy,
            ConflictDetails = null,
            CanAutoRepair = false
        };
    }
}
