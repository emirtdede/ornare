using System;
using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Storage contract required by ApplyLastService to orchestrate apply-last mutations
/// without coupling Ornare.Windows to concrete SQLite or storage implementation packages.
/// </summary>
public interface IApplyLastStorage
{
    Task<string?> GetLastAppliedRenderKeyAsync();
    Task SetLastAppliedRenderKeyAsync(string renderKey);
    Task ClearLastAppliedRenderKeyAsync();

    Task<RenderCacheEntry?> GetRenderCacheEntryAsync(string renderKey);
    Task SaveRenderCacheEntryAsync(RenderCacheEntry entry);

    Task RecordJournalEntryAsync(JournalEntry entry);
    Task UpdateJournalStateAsync(Guid operationId, JournalState state, string? errorDetails = null);

    Task SaveFolderAssignmentAsync(string folderPath, Guid epochId, FolderIconSemantics semantics, FolderBaseline baseline);
    Task<FolderBaseline?> GetLatestBaselineAsync(string folderPath);

    Task RecordHistoryAsync(string folderPath, Guid operationId, string? prevHash, string newHash, string operationType);
    Task UpdateAssignmentIdentityAsync(string folderPath, string? volumeSerial, string? fileId);
    Task<LibraryAsset?> GetAssetByHashAsync(string sha256);
}
