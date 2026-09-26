namespace Ornare.Windows;

using System;
using System.IO;
using Ornare.Core;

public enum ReconciliationOutcome
{
    Healthy = 0,
    Relocated = 1,
    LocationUnknown = 2,
    MissingTarget = 3,
    IdentityMismatch = 4
}

public sealed record ReconciliationDecision
{
    public required ReconciliationOutcome Outcome { get; init; }
    public required string OriginalPath { get; init; }
    public string? RelocatedPath { get; init; }
    public FolderCustomizationState RecommendedState { get; init; }
    public string? Reason { get; init; }

    public static ReconciliationDecision Healthy(string path) =>
        new()
        {
            Outcome = ReconciliationOutcome.Healthy,
            OriginalPath = path,
            RelocatedPath = path,
            RecommendedState = FolderCustomizationState.Healthy
        };

    public static ReconciliationDecision Relocated(string oldPath, string newPath, FolderCustomizationState state = FolderCustomizationState.Healthy) =>
        new()
        {
            Outcome = ReconciliationOutcome.Relocated,
            OriginalPath = oldPath,
            RelocatedPath = newPath,
            RecommendedState = state,
            Reason = $"Folder relocated from '{oldPath}' to '{newPath}'."
        };

    public static ReconciliationDecision LocationUnknown(string path, string? reason = null) =>
        new()
        {
            Outcome = ReconciliationOutcome.LocationUnknown,
            OriginalPath = path,
            RecommendedState = FolderCustomizationState.LocationUnknown,
            Reason = reason ?? "Folder location could not be resolved by file ID."
        };

    public static ReconciliationDecision MissingTarget(string path) =>
        new()
        {
            Outcome = ReconciliationOutcome.MissingTarget,
            OriginalPath = path,
            RecommendedState = FolderCustomizationState.MissingTarget,
            Reason = "Target folder does not exist on disk."
        };
}

public static class FolderReconciliationService
{
    public static ReconciliationDecision EvaluateAssignment(FolderAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        string currentPath = assignment.FolderPath;

        if (Directory.Exists(currentPath))
        {
            var liveIdentity = FolderIdentityService.GetNtfsIdentity(currentPath);
            if (liveIdentity.HasValue && assignment.VolumeSerialNumber.HasValue && assignment.FileId64.HasValue)
            {
                if (liveIdentity.Value.VolumeSerialNumber == assignment.VolumeSerialNumber.Value &&
                    liveIdentity.Value.FileId64 == assignment.FileId64.Value)
                {
                    return ReconciliationDecision.Healthy(currentPath);
                }

                // Path exists, but the identity differs! Another folder was created at this path.
                // Do not misassociate ownership. Try to find where the original folder was moved.
                bool recovered = FolderIdentityService.TryReopenAndReconcilePath(
                    assignment.VolumeSerialNumber.Value,
                    assignment.FileId64.Value,
                    currentPath,
                    out string? newPath);

                if (recovered && !string.IsNullOrEmpty(newPath) && Directory.Exists(newPath))
                {
                    var health = FolderHealthEngine.CheckHealth(newPath, assignment);
                    return ReconciliationDecision.Relocated(currentPath, newPath, health.State);
                }

                return ReconciliationDecision.LocationUnknown(currentPath, "Identity mismatch at path; original folder location unknown.");
            }

            return ReconciliationDecision.Healthy(currentPath);
        }

        // Path does not exist on disk
        if (assignment.VolumeSerialNumber.HasValue && assignment.FileId64.HasValue)
        {
            bool recovered = FolderIdentityService.TryReopenAndReconcilePath(
                assignment.VolumeSerialNumber.Value,
                assignment.FileId64.Value,
                currentPath,
                out string? newPath);

            if (recovered && !string.IsNullOrEmpty(newPath) && Directory.Exists(newPath))
            {
                var health = FolderHealthEngine.CheckHealth(newPath, assignment);
                return ReconciliationDecision.Relocated(currentPath, newPath, health.State);
            }

            return ReconciliationDecision.LocationUnknown(currentPath, "Folder not found at path and could not be reopened by file ID.");
        }

        return ReconciliationDecision.MissingTarget(currentPath);
    }
}
