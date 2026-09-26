using System;
using System.Threading.Tasks;

namespace Ornare.Core;

/// <summary>
/// Storage contract required by ApplyLastService to orchestrate apply-last mutations
/// without coupling Ornare.Windows to concrete SQLite or storage implementation packages.
/// </summary>
public interface IApplyLastStorage : IJournalStorage
{
    Task<string?> GetLastAppliedRenderKeyAsync();
    Task SetLastAppliedRenderKeyAsync(string renderKey);
    Task ClearLastAppliedRenderKeyAsync();

    Task<RenderCacheEntry?> GetRenderCacheEntryAsync(string renderKey);
    Task SaveRenderCacheEntryAsync(RenderCacheEntry entry);

    Task SaveFolderAssignmentAsync(string folderPath, Guid epochId, FolderIconSemantics semantics, FolderBaseline baseline);
    Task UpdateAssignmentIdentityAsync(string folderPath, string? volumeSerial, string? fileId);
    Task<LibraryAsset?> GetAssetByHashAsync(string sha256);
}
