using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Common storage contract for folder-level operations (Undo, Reset)
/// that interact with assignments, baselines, identity, and journaling.
/// </summary>
public interface IFolderOperationStorage : IJournalStorage
{
    Task<FolderAssignment?> FindAssignmentAsync(NtfsFolderIdentity? identity, string canonicalPath, CancellationToken ct = default);
    Task<FolderBaseline?> GetLatestBaselineForAssignmentAsync(string assignmentId);
    Task RemoveFolderAssignmentAsync(string folderPath);
    Task UpdateAssignmentPathAsync(string oldFolderPath, string newFolderPath);
    Task UpdateAssignmentHealthAsync(string folderPath, FolderCustomizationState status, string? conflictState = null);
}
