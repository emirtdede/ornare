using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Inspects target folder customization state, live desktop.ini semantics,
/// stored baselines, and NTFS identity without mutating filesystem or storage (F24).
/// </summary>
public static class InspectService
{
    public static async Task<OperationResult<FolderInspectionResult>> InspectAsync(
        string folderPath,
        IInspectStorage storage,
        IFolderCustomizer customizer,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(customizer);

        var validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            return OperationResult.Failure<FolderInspectionResult>(validation.ErrorCode, validation.ErrorMessage ?? "Target folder is not supported.");
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(folderPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<FolderInspectionResult>(ErrorCode.InvalidTarget, $"Failed to resolve canonical path: {ex.Message}");
        }

        if (!Directory.Exists(canonicalPath))
        {
            return OperationResult.Failure<FolderInspectionResult>(ErrorCode.TargetNotFound, $"Target folder does not exist: '{canonicalPath}'.");
        }

        string iniPath = Path.Combine(canonicalPath, OrnareConstants.DesktopIniFileName);
        bool desktopIniExists = File.Exists(iniPath);
        FolderIconSemantics semantics = DesktopIniParser.ReadSemantics(canonicalPath);

        FolderBaseline? baseline = null;
        FolderAssignment? assignment = null;
        try
        {
            baseline = await storage.GetLatestBaselineAsync(canonicalPath);
            assignment = await storage.GetAssignmentByPathAsync(canonicalPath);
        }
        catch
        {
            // Non-fatal lookup fallback
        }

        FolderCustomizationState state = customizer.GetFolderState(canonicalPath, baseline);
        var ntfsId = FolderIdentityService.GetNtfsIdentity(canonicalPath);

        bool isCustomized = !string.IsNullOrWhiteSpace(semantics.IconResource);

        return OperationResult.Success(new FolderInspectionResult
        {
            TargetFolder = canonicalPath,
            IsCustomized = isCustomized,
            State = state,
            DesktopIniExists = desktopIniExists,
            IconResource = semantics.IconResource,
            IconResourceIndex = semantics.IconIndex ?? 0,
            HasBaseline = baseline != null,
            HasAssignment = assignment != null,
            CanonicalIcoPath = assignment != null && !string.IsNullOrEmpty(assignment.IconRelativePath)
                ? Path.Combine(canonicalPath, assignment.IconRelativePath.Split(',')[0].Trim())
                : null,
            VolumeSerialNumber = ntfsId?.VolumeSerialNumber,
            FileId = ntfsId?.FileId64
        });
    }
}
