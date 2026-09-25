using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Ornare.Core;

namespace Ornare.Storage;

public sealed class SqliteStorageService : IStorageService, IDisposable
{
    private string _connectionString = string.Empty;
    private bool _isInitialized;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public bool IsInitialized => _isInitialized;

    public async Task InitializeAsync(string? dbPath = null)
    {
        if (_isInitialized)
        {
            return;
        }

        await _dbLock.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();

            string databasePath = dbPath ?? GetDefaultDbPath();
            string? dir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE IF NOT EXISTS folder_assignments (
                    id TEXT PRIMARY KEY,
                    folder_path TEXT NOT NULL UNIQUE,
                    ownership_epoch_id TEXT NOT NULL,
                    icon_relative_path TEXT NOT NULL,
                    status INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    volume_serial TEXT,
                    file_id TEXT,
                    current_asset_hash TEXT,
                    conflict_state TEXT
                );

                CREATE TABLE IF NOT EXISTS folder_baselines (
                    id TEXT PRIMARY KEY,
                    folder_assignment_id TEXT NOT NULL,
                    ownership_epoch_id TEXT NOT NULL,
                    prior_semantics_json TEXT NOT NULL,
                    captured_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS operation_journal (
                    operation_id TEXT PRIMARY KEY,
                    operation_kind INTEGER NOT NULL,
                    target_folder_path TEXT NOT NULL,
                    state INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    target_asset_path TEXT,
                    prior_semantics_json TEXT,
                    new_semantics_json TEXT,
                    error_details TEXT
                );

                CREATE TABLE IF NOT EXISTS assets (
                    id TEXT PRIMARY KEY,
                    sha256 TEXT UNIQUE NOT NULL,
                    canonical_ico_path TEXT NOT NULL,
                    source_format TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    last_used_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS favorites (
                    asset_id TEXT PRIMARY KEY,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    folder_path TEXT NOT NULL,
                    operation_id TEXT NOT NULL,
                    previous_asset_hash TEXT,
                    new_asset_hash TEXT NOT NULL,
                    operation_type TEXT NOT NULL,
                    timestamp TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_journal_state ON operation_journal(state);
                CREATE INDEX IF NOT EXISTS idx_assets_sha256 ON assets(sha256);
                CREATE INDEX IF NOT EXISTS idx_assets_last_used ON assets(last_used_at DESC);
                CREATE INDEX IF NOT EXISTS idx_history_timestamp ON history(timestamp DESC);
            ";
            await command.ExecuteNonQueryAsync();

            // Safe column additions for existing folder_assignments table
            string[] migrationColumns = ["volume_serial TEXT", "file_id TEXT", "current_asset_hash TEXT", "conflict_state TEXT"];
            foreach (var col in migrationColumns)
            {
                try
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE folder_assignments ADD COLUMN {col};";
                    await alterCmd.ExecuteNonQueryAsync();
                }
                catch (SqliteException)
                {
                    // Column already exists, safe to ignore
                }
            }

            _isInitialized = true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task RecordJournalEntryAsync(JournalEntry entry)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO operation_journal (
                    operation_id, operation_kind, target_folder_path, state,
                    created_at, updated_at, target_asset_path,
                    prior_semantics_json, new_semantics_json, error_details
                ) VALUES (
                    @opId, @kind, @path, @state,
                    @createdAt, @updatedAt, @assetPath,
                    @priorSemantics, @newSemantics, @error
                );
            ";
            command.Parameters.AddWithValue("@opId", entry.OperationId.ToString());
            command.Parameters.AddWithValue("@kind", (int)entry.Kind);
            command.Parameters.AddWithValue("@path", entry.TargetFolderPath);
            command.Parameters.AddWithValue("@state", (int)entry.State);
            command.Parameters.AddWithValue("@createdAt", entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@assetPath", (object?)entry.TargetAssetPath ?? DBNull.Value);
            command.Parameters.AddWithValue("@priorSemantics", (object?)entry.PriorSemanticsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("@newSemantics", (object?)entry.NewSemanticsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("@error", (object?)entry.ErrorDetails ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateJournalStateAsync(Guid operationId, JournalState state, string? errorDetails = null)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE operation_journal
                SET state = @state, updated_at = @updatedAt, error_details = COALESCE(@error, error_details)
                WHERE operation_id = @opId;
            ";
            command.Parameters.AddWithValue("@opId", operationId.ToString());
            command.Parameters.AddWithValue("@state", (int)state);
            command.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@error", (object?)errorDetails ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<JournalEntry>> GetPendingJournalEntriesAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT operation_id, operation_kind, target_folder_path, state,
                       created_at, updated_at, target_asset_path,
                       prior_semantics_json, new_semantics_json, error_details
                FROM operation_journal
                WHERE state NOT IN (@completed, @aborted, @rolledBack);
            ";
            command.Parameters.AddWithValue("@completed", (int)JournalState.Completed);
            command.Parameters.AddWithValue("@aborted", (int)JournalState.Aborted);
            command.Parameters.AddWithValue("@rolledBack", (int)JournalState.RolledBack);

            List<JournalEntry> results = [];
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new JournalEntry
                {
                    OperationId = Guid.Parse(reader.GetString(0)),
                    Kind = (OperationKind)reader.GetInt32(1),
                    TargetFolderPath = reader.GetString(2),
                    State = (JournalState)reader.GetInt32(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    UpdatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    TargetAssetPath = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PriorSemanticsJson = reader.IsDBNull(7) ? null : reader.GetString(7),
                    NewSemanticsJson = reader.IsDBNull(8) ? null : reader.GetString(8),
                    ErrorDetails = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return results;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SaveFolderAssignmentAsync(
        string folderPath,
        Guid epochId,
        FolderIconSemantics semantics,
        FolderBaseline baseline)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            Guid assignmentId = baseline.FolderAssignmentId;
            string now = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            // 1. Insert or update assignment
            using var assignCmd = connection.CreateCommand();
            assignCmd.Transaction = transaction;
            assignCmd.CommandText = @"
                INSERT INTO folder_assignments (
                    id, folder_path, ownership_epoch_id, icon_relative_path, status, created_at, updated_at
                ) VALUES (
                    @id, @path, @epochId, @iconPath, @status, @now, @now
                ) ON CONFLICT(folder_path) DO UPDATE SET
                    ownership_epoch_id = @epochId,
                    icon_relative_path = @iconPath,
                    status = @status,
                    updated_at = @now;
            ";
            assignCmd.Parameters.AddWithValue("@id", assignmentId.ToString());
            assignCmd.Parameters.AddWithValue("@path", folderPath);
            assignCmd.Parameters.AddWithValue("@epochId", epochId.ToString());
            assignCmd.Parameters.AddWithValue("@iconPath", semantics.IconResource ?? string.Empty);
            assignCmd.Parameters.AddWithValue("@status", (int)FolderCustomizationState.Healthy);
            assignCmd.Parameters.AddWithValue("@now", now);
            await assignCmd.ExecuteNonQueryAsync();

            // 2. Insert baseline
            using var baseCmd = connection.CreateCommand();
            baseCmd.Transaction = transaction;
            baseCmd.CommandText = @"
                INSERT INTO folder_baselines (
                    id, folder_assignment_id, ownership_epoch_id, prior_semantics_json, captured_at
                ) VALUES (
                    @id, @assignmentId, @epochId, @priorSemantics, @capturedAt
                );
            ";
            baseCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            baseCmd.Parameters.AddWithValue("@assignmentId", assignmentId.ToString());
            baseCmd.Parameters.AddWithValue("@epochId", epochId.ToString());
            baseCmd.Parameters.AddWithValue("@priorSemantics", baseline.PriorSemantics.ToJson());
            baseCmd.Parameters.AddWithValue("@capturedAt", baseline.CapturedAt.ToString("o", CultureInfo.InvariantCulture));
            await baseCmd.ExecuteNonQueryAsync();

            transaction.Commit();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<FolderBaseline?> GetLatestBaselineAsync(string folderPath)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.id, b.ownership_epoch_id, b.prior_semantics_json, b.captured_at
                FROM folder_assignments a
                JOIN folder_baselines b ON a.id = b.folder_assignment_id AND a.ownership_epoch_id = b.ownership_epoch_id
                WHERE a.folder_path = @path
                ORDER BY b.captured_at DESC
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("@path", folderPath);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new FolderBaseline
                {
                    FolderAssignmentId = Guid.Parse(reader.GetString(0)),
                    OwnershipEpochId = Guid.Parse(reader.GetString(1)),
                    PriorSemantics = FolderIconSemantics.FromJson(reader.GetString(2)),
                    CapturedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)
                };
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task RemoveFolderAssignmentAsync(string folderPath)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM folder_assignments WHERE folder_path = @path;";
            command.Parameters.AddWithValue("@path", folderPath);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> ReconcileStartupAsync(Func<JournalEntry, Task<bool>>? customReconciler = null)
    {
        var pending = await GetPendingJournalEntriesAsync();
        int reconciledCount = 0;

        foreach (var entry in pending)
        {
            if (customReconciler != null)
            {
                bool success = await customReconciler(entry);
                if (success)
                {
                    await UpdateJournalStateAsync(entry.OperationId, JournalState.Completed);
                }
                else
                {
                    await UpdateJournalStateAsync(entry.OperationId, JournalState.NeedsRepair, "Custom reconciliation failed");
                }
                reconciledCount++;
                continue;
            }

            // Default deterministic reconciliation logic (INV-TX-001, INV-TX-002)
            switch (entry.State)
            {
                case JournalState.Prepared:
                case JournalState.AssetGenerated:
                case JournalState.FilesystemStaged:
                    // Filesystem mutation had not yet committed; safe rollback
                    await UpdateJournalStateAsync(entry.OperationId, JournalState.RolledBack, "Reconciled at startup: incomplete before commit");
                    reconciledCount++;
                    break;

                case JournalState.FilesystemCommitted:
                case JournalState.ShellRefreshRequested:
                case JournalState.MetadataCommitted:
                    // Filesystem committed, mark completed
                    await UpdateJournalStateAsync(entry.OperationId, JournalState.Completed, "Reconciled at startup: filesystem was committed");
                    reconciledCount++;
                    break;

                default:
                    break;
            }
        }

        return reconciledCount;
    }

    public async Task<IReadOnlyList<FolderAssignment>> GetAllFolderAssignmentsAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, folder_path, volume_serial, file_id, current_asset_hash,
                       ownership_epoch_id, icon_relative_path, status, conflict_state,
                       created_at, updated_at
                FROM folder_assignments
                ORDER BY updated_at DESC;
            ";

            List<FolderAssignment> list = [];
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FolderAssignment
                {
                    Id = reader.GetString(0),
                    FolderPath = reader.GetString(1),
                    VolumeSerial = reader.IsDBNull(2) ? null : reader.GetString(2),
                    FileId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CurrentAssetHash = reader.IsDBNull(4) ? null : reader.GetString(4),
                    OwnershipEpochId = Guid.Parse(reader.GetString(5)),
                    IconRelativePath = reader.GetString(6),
                    Status = (FolderCustomizationState)reader.GetInt32(7),
                    ConflictState = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
                    UpdatedAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture)
                });
            }

            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateAssignmentHealthAsync(string folderPath, FolderCustomizationState status, string? conflictState = null)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE folder_assignments
                SET status = @status, conflict_state = @conflictState, updated_at = @now
                WHERE folder_path = @path;
            ";
            command.Parameters.AddWithValue("@status", (int)status);
            command.Parameters.AddWithValue("@conflictState", (object?)conflictState ?? DBNull.Value);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@path", folderPath);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateAssignmentIdentityAsync(string folderPath, string? volumeSerial, string? fileId)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE folder_assignments
                SET volume_serial = @volumeSerial, file_id = @fileId, updated_at = @now
                WHERE folder_path = @path;
            ";
            command.Parameters.AddWithValue("@volumeSerial", (object?)volumeSerial ?? DBNull.Value);
            command.Parameters.AddWithValue("@fileId", (object?)fileId ?? DBNull.Value);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@path", folderPath);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<LibraryAsset> AddOrUpdateAssetAsync(string sha256, string canonicalIcoPath, string sourceFormat, DateTimeOffset? now = null)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            DateTimeOffset timestamp = now ?? DateTimeOffset.UtcNow;
            string nowStr = timestamp.ToString("o", CultureInfo.InvariantCulture);
            string assetId = Guid.NewGuid().ToString("N");

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO assets (id, sha256, canonical_ico_path, source_format, created_at, last_used_at)
                VALUES (@id, @sha256, @icoPath, @format, @now, @now)
                ON CONFLICT(sha256) DO UPDATE SET
                    canonical_ico_path = @icoPath,
                    source_format = @format,
                    last_used_at = @now;

                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END AS is_favorite
                FROM assets a
                LEFT JOIN favorites f ON a.sha256 = f.asset_id
                WHERE a.sha256 = @sha256;
            ";
            command.Parameters.AddWithValue("@id", assetId);
            command.Parameters.AddWithValue("@sha256", sha256);
            command.Parameters.AddWithValue("@icoPath", canonicalIcoPath);
            command.Parameters.AddWithValue("@format", sourceFormat);
            command.Parameters.AddWithValue("@now", nowStr);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LibraryAsset
                {
                    Id = reader.GetString(0),
                    Sha256 = reader.GetString(1),
                    CanonicalIcoPath = reader.GetString(2),
                    SourceFormat = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    LastUsedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    IsFavorite = reader.GetInt32(6) == 1
                };
            }

            throw new InvalidOperationException($"Failed to insert or query asset with sha256 '{sha256}'.");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<LibraryAsset?> GetAssetByHashAsync(string sha256)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END AS is_favorite
                FROM assets a
                LEFT JOIN favorites f ON a.sha256 = f.asset_id
                WHERE a.sha256 = @sha256;
            ";
            command.Parameters.AddWithValue("@sha256", sha256);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LibraryAsset
                {
                    Id = reader.GetString(0),
                    Sha256 = reader.GetString(1),
                    CanonicalIcoPath = reader.GetString(2),
                    SourceFormat = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    LastUsedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    IsFavorite = reader.GetInt32(6) == 1
                };
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryAsset>> GetAllAssetsAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END AS is_favorite
                FROM assets a
                LEFT JOIN favorites f ON a.sha256 = f.asset_id
                ORDER BY a.last_used_at DESC;
            ";

            List<LibraryAsset> list = [];
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryAsset
                {
                    Id = reader.GetString(0),
                    Sha256 = reader.GetString(1),
                    CanonicalIcoPath = reader.GetString(2),
                    SourceFormat = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    LastUsedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    IsFavorite = reader.GetInt32(6) == 1
                });
            }

            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> DeleteAssetAsync(string sha256)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            using var favCmd = connection.CreateCommand();
            favCmd.Transaction = tx;
            favCmd.CommandText = "DELETE FROM favorites WHERE asset_id = @sha256;";
            favCmd.Parameters.AddWithValue("@sha256", sha256);
            await favCmd.ExecuteNonQueryAsync();

            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM assets WHERE sha256 = @sha256;";
            cmd.Parameters.AddWithValue("@sha256", sha256);
            int rows = await cmd.ExecuteNonQueryAsync();

            tx.Commit();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SetFavoriteAsync(string sha256, bool isFavorite)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            if (isFavorite)
            {
                command.CommandText = @"
                    INSERT OR REPLACE INTO favorites (asset_id, created_at)
                    VALUES (@sha256, @now);
                ";
                command.Parameters.AddWithValue("@sha256", sha256);
                command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            }
            else
            {
                command.CommandText = "DELETE FROM favorites WHERE asset_id = @sha256;";
                command.Parameters.AddWithValue("@sha256", sha256);
            }

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryAsset>> GetFavoritesAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at, 1 AS is_favorite
                FROM assets a
                INNER JOIN favorites f ON a.sha256 = f.asset_id
                ORDER BY f.created_at DESC;
            ";

            List<LibraryAsset> list = [];
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryAsset
                {
                    Id = reader.GetString(0),
                    Sha256 = reader.GetString(1),
                    CanonicalIcoPath = reader.GetString(2),
                    SourceFormat = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    LastUsedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    IsFavorite = true
                });
            }

            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task RecordHistoryAsync(string folderPath, Guid operationId, string? prevHash, string newHash, string operationType)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            string nowStr = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            using var histCmd = connection.CreateCommand();
            histCmd.Transaction = tx;
            histCmd.CommandText = @"
                INSERT INTO history (folder_path, operation_id, previous_asset_hash, new_asset_hash, operation_type, timestamp)
                VALUES (@path, @opId, @prevHash, @newHash, @opType, @now);
            ";
            histCmd.Parameters.AddWithValue("@path", folderPath);
            histCmd.Parameters.AddWithValue("@opId", operationId.ToString());
            histCmd.Parameters.AddWithValue("@prevHash", (object?)prevHash ?? DBNull.Value);
            histCmd.Parameters.AddWithValue("@newHash", newHash);
            histCmd.Parameters.AddWithValue("@opType", operationType);
            histCmd.Parameters.AddWithValue("@now", nowStr);
            await histCmd.ExecuteNonQueryAsync();

            using var assetCmd = connection.CreateCommand();
            assetCmd.Transaction = tx;
            assetCmd.CommandText = "UPDATE assets SET last_used_at = @now WHERE sha256 = @newHash;";
            assetCmd.Parameters.AddWithValue("@now", nowStr);
            assetCmd.Parameters.AddWithValue("@newHash", newHash);
            await assetCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryAsset>> GetRecentAssetsAsync(int limit = 20)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, MAX(h.timestamp) AS recent_ts,
                       CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END AS is_favorite
                FROM history h
                INNER JOIN assets a ON h.new_asset_hash = a.sha256
                LEFT JOIN favorites f ON a.sha256 = f.asset_id
                GROUP BY a.sha256
                ORDER BY recent_ts DESC
                LIMIT @limit;
            ";
            command.Parameters.AddWithValue("@limit", limit);

            List<LibraryAsset> list = [];
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryAsset
                {
                    Id = reader.GetString(0),
                    Sha256 = reader.GetString(1),
                    CanonicalIcoPath = reader.GetString(2),
                    SourceFormat = reader.GetString(3),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    LastUsedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                    IsFavorite = reader.GetInt32(6) == 1
                });
            }

            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task ClearRecentHistoryAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM history;";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = @key;";
            command.Parameters.AddWithValue("@key", key);

            object? val = await command.ExecuteScalarAsync();
            return val is string s ? s : null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SetSettingAsync(string key, string value)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO settings (key, value) VALUES (@key, @value)
                ON CONFLICT(key) DO UPDATE SET value = @value;
            ";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<UserSettings> GetUserSettingsAsync()
    {
        string theme = await GetSettingAsync("Theme") ?? "System";
        string language = await GetSettingAsync("Language") ?? "System";
        return new UserSettings { Theme = theme, Language = language };
    }

    public async Task SaveUserSettingsAsync(UserSettings settings)
    {
        await SetSettingAsync("Theme", settings.Theme);
        await SetSettingAsync("Language", settings.Language);
    }

    private static string GetDefaultDbPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, OrnareConstants.ApplicationName, "data", "ornare.db");
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SqliteStorageService must be initialized before use.");
        }
    }

    public void Dispose()
    {
        _dbLock.Dispose();
    }
}
