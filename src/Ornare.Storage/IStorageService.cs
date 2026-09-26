using Ornare.Core;

namespace Ornare.Storage;

public interface IStorageService : IFolderAssignmentLookup, IApplyLastStorage
{
    bool IsInitialized { get; }
    Task InitializeAsync(string? dbPath = null);

    // Journal (additional methods; RecordJournalEntryAsync and UpdateJournalStateAsync inherited from IApplyLastStorage)
    Task<IReadOnlyList<JournalEntry>> GetPendingJournalEntriesAsync();

    // Assignment & Baselines (SaveFolderAssignmentAsync, GetLatestBaselineAsync, and UpdateAssignmentIdentityAsync(string, string, string) inherited from IApplyLastStorage)
    Task RemoveFolderAssignmentAsync(string folderPath);
    Task<IReadOnlyList<FolderAssignment>> GetAllFolderAssignmentsAsync();
    Task UpdateAssignmentHealthAsync(string folderPath, FolderCustomizationState status, string? conflictState = null);
    Task UpdateAssignmentIdentityAsync(string folderPath, uint? volumeSerialNumber, ulong? fileId64);
    Task<FolderAssignment?> GetAssignmentByIdentityAsync(uint volumeSerialNumber, ulong fileId64);
    Task<FolderAssignment?> GetAssignmentByPathAsync(string folderPath);
    Task UpdateAssignmentPathAsync(string oldFolderPath, string newFolderPath);

    // Startup reconciliation
    Task<int> ReconcileStartupAsync(Func<JournalEntry, Task<bool>>? customReconciler = null);

    // Assets & Library (GetAssetByHashAsync inherited from IApplyLastStorage)
    Task<LibraryAsset> AddOrUpdateAssetAsync(string sha256, string canonicalIcoPath, string sourceFormat, DateTimeOffset? now = null);
    Task<IReadOnlyList<LibraryAsset>> GetAllAssetsAsync();
    Task<bool> DeleteAssetAsync(string sha256);

    // Favorites
    Task SetFavoriteAsync(string sha256, bool isFavorite);
    Task<IReadOnlyList<LibraryAsset>> GetFavoritesAsync();

    // Recent & History (RecordHistoryAsync inherited from IApplyLastStorage)
    Task<IReadOnlyList<LibraryAsset>> GetRecentAssetsAsync(int limit = 20);
    Task ClearRecentHistoryAsync();

    // Settings
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
    Task<UserSettings> GetUserSettingsAsync();
    Task SaveUserSettingsAsync(UserSettings settings);

    // F18: Render Cache & Reference-Safe Cleanup (GetRenderCacheEntryAsync and SaveRenderCacheEntryAsync inherited from IApplyLastStorage)
    Task InvalidateRenderCacheAsync(string renderKey);
    Task<bool> IsAssetOrRenderReferencedAsync(string hashOrKey);
    Task<IReadOnlyList<string>> GetUnreferencedAssetsAsync();
    Task<int> CleanupUnreferencedAssetsAsync();
    Task<int> GetCurrentSchemaVersionAsync();

    // F15: Library Tags
    Task<LibraryTag> CreateOrGetTagAsync(string tagName);
    Task<bool> AttachTagToAssetAsync(string assetHash, string tagId);
    Task<bool> DetachTagFromAssetAsync(string assetHash, string tagId);
    Task<bool> DeleteTagAsync(string tagId);
    Task<IReadOnlyList<LibraryTag>> GetTagsForAssetAsync(string assetHash);
    Task<IReadOnlyList<LibraryTag>> GetAllTagsAsync();
    Task<IReadOnlyList<LibraryAsset>> GetAssetsByTagAsync(string tagId);
    Task<Dictionary<string, List<LibraryTag>>> GetTagsForAssetsAsync(IEnumerable<string> assetHashes);
    Task<bool> VerifyForeignKeysEnabledAsync();

    // F16: Custom Collections
    Task<LibraryCollection> CreateCollectionAsync(string name);
    Task<LibraryCollection?> GetCollectionByIdAsync(string collectionId);
    Task<LibraryCollection?> GetCollectionByNormalizedNameAsync(string normalizedName);
    Task<IReadOnlyList<LibraryCollection>> GetAllCollectionsAsync();
    Task<bool> RenameCollectionAsync(string collectionId, string newName);
    Task<bool> DeleteCollectionAsync(string collectionId);
    Task<bool> AddAssetToCollectionAsync(string collectionId, string assetHash);
    Task<bool> RemoveAssetFromCollectionAsync(string collectionId, string assetHash);
    Task<IReadOnlyList<LibraryAsset>> GetAssetsInCollectionAsync(string collectionId, int limit = 100, int offset = 0);
    Task<IReadOnlyList<LibraryCollection>> GetCollectionsForAssetAsync(string assetHash);
    Task<bool> ReorderCollectionAsync(string collectionId, string? targetNeighborId, bool insertBefore);
    Task RebalanceCollectionSortKeysAsync();

    // F17: Library Search & Durable Index
    Task<SearchIndexState> GetSearchIndexStateAsync();
    Task<bool> StepSearchIndexBuildAsync(int batchSize = 500);
    Task EnsureSearchIndexReadyAsync(CancellationToken cancellationToken = default);
    Task RebuildSearchIndexAsync();
    Task<IReadOnlyList<LibraryAsset>> SearchLibraryAsync(string query, int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<bool> UpdateAssetDisplayNameAsync(string assetHash, string displayName);
    Task<bool> RenameTagAsync(string tagId, string newName);
}
