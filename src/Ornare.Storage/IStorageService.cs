using Ornare.Core;

namespace Ornare.Storage;

public interface IStorageService
{
    bool IsInitialized { get; }
    Task InitializeAsync(string? dbPath = null);

    // Journal
    Task RecordJournalEntryAsync(JournalEntry entry);
    Task UpdateJournalStateAsync(Guid operationId, JournalState state, string? errorDetails = null);
    Task<IReadOnlyList<JournalEntry>> GetPendingJournalEntriesAsync();

    // Assignment & Baselines
    Task SaveFolderAssignmentAsync(string folderPath, Guid epochId, FolderIconSemantics semantics, FolderBaseline baseline);
    Task<FolderBaseline?> GetLatestBaselineAsync(string folderPath);
    Task RemoveFolderAssignmentAsync(string folderPath);

    // Startup reconciliation
    Task<int> ReconcileStartupAsync(Func<JournalEntry, Task<bool>>? customReconciler = null);
}
