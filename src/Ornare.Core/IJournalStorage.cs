using System;
using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Common contract for operation journaling, history logging, and baseline retrieval
/// shared by Apply, Undo, and Reset workflows.
/// </summary>
public interface IJournalStorage
{
    Task RecordJournalEntryAsync(JournalEntry entry);
    Task UpdateJournalStateAsync(Guid operationId, JournalState state, string? errorDetails = null);
    Task RecordHistoryAsync(string folderPath, Guid operationId, string? prevHash, string newHash, string operationType);
    Task<FolderBaseline?> GetLatestBaselineAsync(string folderPath);
}
