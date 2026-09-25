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
                    updated_at TEXT NOT NULL
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

                CREATE INDEX IF NOT EXISTS idx_journal_state ON operation_journal(state);
            ";
            await command.ExecuteNonQueryAsync();

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
