using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Storage contract required by UndoService to orchestrate folder undo and reset
/// without coupling Ornare.Windows to concrete SQLite or storage implementation packages.
/// </summary>
public interface IUndoStorage : IJournalStorage
{
    Task<FolderAssignment?> FindAssignmentAsync(NtfsFolderIdentity? identity, string canonicalPath, CancellationToken ct = default);
    Task<FolderBaseline?> GetLatestBaselineForAssignmentAsync(string assignmentId);
    Task RemoveFolderAssignmentAsync(string folderPath);
    Task UpdateAssignmentPathAsync(string oldFolderPath, string newFolderPath);
    Task UpdateAssignmentHealthAsync(string folderPath, FolderCustomizationState status, string? conflictState = null);
}
