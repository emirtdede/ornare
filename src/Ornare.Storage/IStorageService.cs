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
    Task<IReadOnlyList<FolderAssignment>> GetAllFolderAssignmentsAsync();
    Task UpdateAssignmentHealthAsync(string folderPath, FolderCustomizationState status, string? conflictState = null);
    Task UpdateAssignmentIdentityAsync(string folderPath, string? volumeSerial, string? fileId);

    // Startup reconciliation
    Task<int> ReconcileStartupAsync(Func<JournalEntry, Task<bool>>? customReconciler = null);

    // Assets & Library
    Task<LibraryAsset> AddOrUpdateAssetAsync(string sha256, string canonicalIcoPath, string sourceFormat, DateTimeOffset? now = null);
    Task<LibraryAsset?> GetAssetByHashAsync(string sha256);
    Task<IReadOnlyList<LibraryAsset>> GetAllAssetsAsync();
    Task<bool> DeleteAssetAsync(string sha256);

    // Favorites
    Task SetFavoriteAsync(string sha256, bool isFavorite);
    Task<IReadOnlyList<LibraryAsset>> GetFavoritesAsync();

    // Recent & History
    Task RecordHistoryAsync(string folderPath, Guid operationId, string? prevHash, string newHash, string operationType);
    Task<IReadOnlyList<LibraryAsset>> GetRecentAssetsAsync(int limit = 20);
    Task ClearRecentHistoryAsync();

    // Settings
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
    Task<UserSettings> GetUserSettingsAsync();
    Task SaveUserSettingsAsync(UserSettings settings);
}
