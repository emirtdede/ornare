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
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true
            }.ToString();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using (var pragmaCmd = connection.CreateCommand())
            {
                pragmaCmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA foreign_keys = ON;
                ";
                await pragmaCmd.ExecuteNonQueryAsync();
            }

            int currentVersion = 0;
            using (var versionCheckCmd = connection.CreateCommand())
            {
                versionCheckCmd.CommandText = "PRAGMA user_version;";
                object? vObj = await versionCheckCmd.ExecuteScalarAsync();
                currentVersion = vObj is long l ? (int)l : Convert.ToInt32(vObj, CultureInfo.InvariantCulture);
            }

            if (currentVersion == 0)
            {
                using var tx = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = tx;
                command.CommandText = @"
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
                        conflict_state TEXT,
                        volume_serial_number INTEGER,
                        file_id_blob BLOB
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
                        alterCmd.Transaction = tx;
                        alterCmd.CommandText = $"ALTER TABLE folder_assignments ADD COLUMN {col};";
                        await alterCmd.ExecuteNonQueryAsync();
                    }
                    catch (SqliteException)
                    {
                        // Column already exists, safe to ignore
                    }
                }

                using var setV1Cmd = connection.CreateCommand();
                setV1Cmd.Transaction = tx;
                setV1Cmd.CommandText = "PRAGMA user_version = 1;";
                await setV1Cmd.ExecuteNonQueryAsync();

                tx.Commit();
                currentVersion = 1;
            }

            if (currentVersion < 2)
            {
                await ApplyMigrationV2InternalAsync(connection, injectFailure: false);
            }

            if (currentVersion < 3)
            {
                await ApplyMigrationV3InternalAsync(connection, injectFailure: false);
            }

            if (currentVersion < 4)
            {
                await ApplyMigrationV4InternalAsync(connection, injectFailure: false);
            }

            if (currentVersion < 5)
            {
                await ApplyMigrationV5InternalAsync(connection, injectFailure: false);
            }

            if (currentVersion < 6)
            {
                await ApplyMigrationV6InternalAsync(connection, injectFailure: false);
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
                       created_at, updated_at, volume_serial_number, file_id_blob
                FROM folder_assignments
                ORDER BY updated_at DESC;
            ";

            List<FolderAssignment> list = [];
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapFolderAssignment(reader));
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
        uint? volNum = null;
        ulong? fileId64 = null;

        if (!string.IsNullOrWhiteSpace(volumeSerial) &&
            uint.TryParse(volumeSerial, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedVol))
        {
            volNum = parsedVol;
        }

        if (!string.IsNullOrWhiteSpace(fileId) &&
            ulong.TryParse(fileId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsedFid))
        {
            fileId64 = parsedFid;
        }

        await UpdateAssignmentIdentityAsync(folderPath, volNum, fileId64);
    }

    public async Task UpdateAssignmentIdentityAsync(string folderPath, uint? volumeSerialNumber, ulong? fileId64)
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
                SET volume_serial = @volumeSerial,
                    file_id = @fileId,
                    volume_serial_number = @volNum,
                    file_id_blob = @fileBlob,
                    updated_at = @now
                WHERE folder_path = @path;
            ";
            string? volHex = volumeSerialNumber?.ToString("X8", CultureInfo.InvariantCulture);
            string? fidHex = fileId64?.ToString("X16", CultureInfo.InvariantCulture);
            byte[]? fileBlob = fileId64.HasValue ? BitConverter.GetBytes(fileId64.Value) : null;

            command.Parameters.AddWithValue("@volumeSerial", (object?)volHex ?? DBNull.Value);
            command.Parameters.AddWithValue("@fileId", (object?)fidHex ?? DBNull.Value);
            command.Parameters.AddWithValue("@volNum", (object?)volumeSerialNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@fileBlob", (object?)fileBlob ?? DBNull.Value);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@path", folderPath);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<FolderAssignment?> GetAssignmentByIdentityAsync(uint volumeSerialNumber, ulong fileId64)
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
                       created_at, updated_at, volume_serial_number, file_id_blob
                FROM folder_assignments
                WHERE volume_serial_number = @volNum AND file_id_blob = @fileBlob
                LIMIT 1;
            ";
            byte[] fileBlob = BitConverter.GetBytes(fileId64);
            command.Parameters.AddWithValue("@volNum", (long)volumeSerialNumber);
            command.Parameters.AddWithValue("@fileBlob", fileBlob);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFolderAssignment(reader);
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<FolderAssignment?> GetAssignmentByPathAsync(string folderPath)
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
                       created_at, updated_at, volume_serial_number, file_id_blob
                FROM folder_assignments
                WHERE folder_path = @path
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("@path", folderPath);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapFolderAssignment(reader);
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpdateAssignmentPathAsync(string oldFolderPath, string newFolderPath)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(oldFolderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newFolderPath);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE folder_assignments
                SET folder_path = @newPath, updated_at = @now
                WHERE folder_path = @oldPath;
            ";
            command.Parameters.AddWithValue("@newPath", newFolderPath);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@oldPath", oldFolderPath);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static FolderAssignment MapFolderAssignment(SqliteDataReader reader)
    {
        string? volHex = reader.IsDBNull(2) ? null : reader.GetString(2);
        string? fidHex = reader.IsDBNull(3) ? null : reader.GetString(3);
        uint? volNum = reader.IsDBNull(11) ? null : (uint)reader.GetInt64(11);
        byte[]? blob = reader.IsDBNull(12) ? null : (byte[])reader.GetValue(12);
        ulong? fileId64 = (blob != null && blob.Length == 8) ? BitConverter.ToUInt64(blob) : null;

        if (!volNum.HasValue && !string.IsNullOrWhiteSpace(volHex) &&
            uint.TryParse(volHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedVol))
        {
            volNum = parsedVol;
        }

        if (!fileId64.HasValue && !string.IsNullOrWhiteSpace(fidHex) &&
            ulong.TryParse(fidHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsedFid))
        {
            fileId64 = parsedFid;
            blob = BitConverter.GetBytes(parsedFid);
        }

        return new FolderAssignment
        {
            Id = reader.GetString(0),
            FolderPath = reader.GetString(1),
            VolumeSerial = volHex,
            FileId = fidHex,
            CurrentAssetHash = reader.IsDBNull(4) ? null : reader.GetString(4),
            OwnershipEpochId = Guid.Parse(reader.GetString(5)),
            IconRelativePath = reader.GetString(6),
            Status = (FolderCustomizationState)reader.GetInt32(7),
            ConflictState = reader.IsDBNull(8) ? null : reader.GetString(8),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture),
            VolumeSerialNumber = volNum,
            FileId64 = fileId64,
            FileIdBlob = blob
        };
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

            LibraryAsset? asset = null;
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    asset = new LibraryAsset
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
            }

            if (asset == null)
            {
                throw new InvalidOperationException($"Failed to insert or query asset with sha256 '{sha256}'.");
            }

            try
            {
                string fn = Path.GetFileNameWithoutExtension(canonicalIcoPath);
                var tokens = LibrarySearchTokenizer.Tokenize(fn);
                foreach (var t in tokens)
                {
                    using var tokenCmd = connection.CreateCommand();
                    tokenCmd.CommandText = "INSERT OR IGNORE INTO asset_search_tokens (asset_hash, token) VALUES (@hash, @token);";
                    tokenCmd.Parameters.AddWithValue("@hash", sha256);
                    tokenCmd.Parameters.AddWithValue("@token", t);
                    await tokenCmd.ExecuteNonQueryAsync();
                }
            }
            catch (SqliteException)
            {
                // Pre-V5 migration compatibility
            }

            return asset;
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
        string? recentColorsRaw = await GetSettingAsync("RecentCustomColors");

        IReadOnlyList<string> recentColors = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(recentColorsRaw))
        {
            recentColors = recentColorsRaw
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(c => c.StartsWith('#') && c.Length == 7)
                .Take(UserSettings.MaxRecentCustomColors)
                .ToList();
        }

        return new UserSettings
        {
            Theme = theme,
            Language = language,
            RecentCustomColors = recentColors
        };
    }

    public async Task SaveUserSettingsAsync(UserSettings settings)
    {
        await SetSettingAsync("Theme", settings.Theme);
        await SetSettingAsync("Language", settings.Language);
        string recentColorsRaw = string.Join(";", settings.RecentCustomColors.Take(UserSettings.MaxRecentCustomColors));
        await SetSettingAsync("RecentCustomColors", recentColorsRaw);
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

    public async Task<RenderCacheEntry?> GetRenderCacheEntryAsync(string renderKey)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(renderKey))
        {
            return null;
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT render_key, recipe_version, recipe_json, source_asset_hash,
                       canonical_ico_path, frame_sizes, file_size_bytes, created_at, last_used_at
                FROM render_cache
                WHERE render_key = @key;
            ";
            command.Parameters.AddWithValue("@key", renderKey);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var entry = new RenderCacheEntry
                {
                    RenderKey = reader.GetString(0),
                    RecipeVersion = reader.GetInt32(1),
                    RecipeJson = reader.GetString(2),
                    SourceAssetHash = reader.GetString(3),
                    CanonicalIcoPath = reader.GetString(4),
                    FrameSizes = reader.GetString(5),
                    FileSizeBytes = reader.GetInt64(6),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                    LastUsedAt = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture)
                };

                using var touchCmd = connection.CreateCommand();
                touchCmd.CommandText = "UPDATE render_cache SET last_used_at = @now WHERE render_key = @key;";
                touchCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                touchCmd.Parameters.AddWithValue("@key", renderKey);
                await touchCmd.ExecuteNonQueryAsync();

                return entry;
            }

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SaveRenderCacheEntryAsync(RenderCacheEntry entry)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(entry);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO render_cache (
                    render_key, recipe_version, recipe_json, source_asset_hash,
                    canonical_ico_path, frame_sizes, file_size_bytes, created_at, last_used_at
                ) VALUES (
                    @render_key, @recipe_version, @recipe_json, @source_asset_hash,
                    @canonical_ico_path, @frame_sizes, @file_size_bytes, @created_at, @last_used_at
                )
                ON CONFLICT(render_key) DO UPDATE SET
                    canonical_ico_path = @canonical_ico_path,
                    file_size_bytes = @file_size_bytes,
                    last_used_at = @last_used_at;
            ";
            command.Parameters.AddWithValue("@render_key", entry.RenderKey);
            command.Parameters.AddWithValue("@recipe_version", entry.RecipeVersion);
            command.Parameters.AddWithValue("@recipe_json", entry.RecipeJson);
            command.Parameters.AddWithValue("@source_asset_hash", entry.SourceAssetHash);
            command.Parameters.AddWithValue("@canonical_ico_path", entry.CanonicalIcoPath);
            command.Parameters.AddWithValue("@frame_sizes", entry.FrameSizes);
            command.Parameters.AddWithValue("@file_size_bytes", entry.FileSizeBytes);
            command.Parameters.AddWithValue("@created_at", entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@last_used_at", entry.LastUsedAt.ToString("o", CultureInfo.InvariantCulture));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task InvalidateRenderCacheAsync(string renderKey)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(renderKey))
        {
            return;
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM render_cache WHERE render_key = @key;";
            command.Parameters.AddWithValue("@key", renderKey);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> IsAssetOrRenderReferencedAsync(string hashOrKey)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(hashOrKey))
        {
            return false;
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    (SELECT COUNT(1) FROM folder_assignments WHERE current_asset_hash = @id OR render_key = @id) +
                    (SELECT COUNT(1) FROM history WHERE previous_asset_hash = @id OR new_asset_hash = @id OR render_key = @id) +
                    (SELECT COUNT(1) FROM favorites WHERE asset_id = @id) +
                    (SELECT COUNT(1) FROM settings WHERE key IN ('LastAppliedRenderKey', 'LastAppliedAssetHash') AND value = @id) +
                    (SELECT COUNT(1) FROM render_cache rc
                     WHERE rc.source_asset_hash = @id
                       AND (
                           EXISTS (SELECT 1 FROM folder_assignments fa WHERE fa.render_key = rc.render_key)
                           OR EXISTS (SELECT 1 FROM history h WHERE h.render_key = rc.render_key)
                           OR EXISTS (SELECT 1 FROM settings s WHERE s.key = 'LastAppliedRenderKey' AND s.value = rc.render_key)
                       )
                    );
            ";
            command.Parameters.AddWithValue("@id", hashOrKey);

            object? result = await command.ExecuteScalarAsync();
            long count = result is long l ? l : Convert.ToInt64(result, CultureInfo.InvariantCulture);
            return count > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetUnreferencedAssetsAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.sha256
                FROM assets a
                WHERE NOT EXISTS (
                    SELECT 1 FROM folder_assignments fa WHERE fa.current_asset_hash = a.sha256
                )
                AND NOT EXISTS (
                    SELECT 1 FROM history h WHERE h.previous_asset_hash = a.sha256 OR h.new_asset_hash = a.sha256
                )
                AND NOT EXISTS (
                    SELECT 1 FROM favorites f WHERE f.asset_id = a.sha256
                )
                AND NOT EXISTS (
                    SELECT 1 FROM settings s WHERE s.key = 'LastAppliedAssetHash' AND s.value = a.sha256
                )
                AND NOT EXISTS (
                    SELECT 1 FROM render_cache rc
                    WHERE rc.source_asset_hash = a.sha256
                    AND (
                        EXISTS (SELECT 1 FROM folder_assignments fa WHERE fa.render_key = rc.render_key)
                        OR EXISTS (SELECT 1 FROM history h WHERE h.render_key = rc.render_key)
                        OR EXISTS (SELECT 1 FROM settings s WHERE s.key = 'LastAppliedRenderKey' AND s.value = rc.render_key)
                    )
                );
            ";

            var list = new List<string>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(reader.GetString(0));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> CleanupUnreferencedAssetsAsync()
    {
        EnsureInitialized();
        var unreferencedHashes = await GetUnreferencedAssetsAsync();
        if (unreferencedHashes.Count == 0)
        {
            return 0;
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            int cleanedCount = 0;
            foreach (var hash in unreferencedHashes)
            {
                string? assetIcoPath = null;
                using (var getCmd = connection.CreateCommand())
                {
                    getCmd.Transaction = tx;
                    getCmd.CommandText = "SELECT canonical_ico_path FROM assets WHERE sha256 = @hash;";
                    getCmd.Parameters.AddWithValue("@hash", hash);
                    object? res = await getCmd.ExecuteScalarAsync();
                    if (res is string s) assetIcoPath = s;
                }

                var renderPaths = new List<string>();
                using (var getRenderCmd = connection.CreateCommand())
                {
                    getRenderCmd.Transaction = tx;
                    getRenderCmd.CommandText = "SELECT canonical_ico_path FROM render_cache WHERE source_asset_hash = @hash;";
                    getRenderCmd.Parameters.AddWithValue("@hash", hash);
                    using var rReader = await getRenderCmd.ExecuteReaderAsync();
                    while (await rReader.ReadAsync())
                    {
                        renderPaths.Add(rReader.GetString(0));
                    }
                }

                using (var delRenderCmd = connection.CreateCommand())
                {
                    delRenderCmd.Transaction = tx;
                    delRenderCmd.CommandText = "DELETE FROM render_cache WHERE source_asset_hash = @hash;";
                    delRenderCmd.Parameters.AddWithValue("@hash", hash);
                    await delRenderCmd.ExecuteNonQueryAsync();
                }

                using (var delAssetCmd = connection.CreateCommand())
                {
                    delAssetCmd.Transaction = tx;
                    delAssetCmd.CommandText = "DELETE FROM assets WHERE sha256 = @hash;";
                    delAssetCmd.Parameters.AddWithValue("@hash", hash);
                    cleanedCount += await delAssetCmd.ExecuteNonQueryAsync();
                }

                if (!string.IsNullOrEmpty(assetIcoPath) && File.Exists(assetIcoPath))
                {
                    try { File.Delete(assetIcoPath); } catch { }
                }
                foreach (var rp in renderPaths)
                {
                    if (!string.IsNullOrEmpty(rp) && File.Exists(rp))
                    {
                        try { File.Delete(rp); } catch { }
                    }
                }
            }

            tx.Commit();
            return cleanedCount;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<int> GetCurrentSchemaVersionAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            object? result = await cmd.ExecuteScalarAsync();
            return result is long l ? (int)l : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task ApplyForwardMigrationWithFailureInjectionAsync(int targetVersion, bool injectFailure)
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            await ApplyMigrationForTestingAsync(_connectionString, targetVersion, injectFailure);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public static async Task ApplyMigrationForTestingAsync(string connectionString, int targetVersion, bool injectFailure)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        if (targetVersion == 2)
        {
            await ApplyMigrationV2InternalAsync(connection, injectFailure);
        }
        else if (targetVersion == 3)
        {
            await ApplyMigrationV3InternalAsync(connection, injectFailure);
        }
        else if (targetVersion == 4)
        {
            await ApplyMigrationV4InternalAsync(connection, injectFailure);
        }
        else if (targetVersion == 5)
        {
            await ApplyMigrationV5InternalAsync(connection, injectFailure);
        }
        else if (targetVersion == 6)
        {
            await ApplyMigrationV6InternalAsync(connection, injectFailure);
        }
    }

    private static async Task ApplyMigrationV2InternalAsync(SqliteConnection connection, bool injectFailure)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS render_cache (
                    render_key TEXT PRIMARY KEY,
                    recipe_version INTEGER NOT NULL,
                    recipe_json TEXT NOT NULL,
                    source_asset_hash TEXT NOT NULL,
                    canonical_ico_path TEXT NOT NULL,
                    frame_sizes TEXT NOT NULL,
                    file_size_bytes INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    last_used_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_render_cache_source ON render_cache(source_asset_hash);
                CREATE INDEX IF NOT EXISTS idx_render_cache_last_used ON render_cache(last_used_at DESC);
            ";
            await cmd.ExecuteNonQueryAsync();

            try
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.Transaction = tx;
                alterCmd.CommandText = "ALTER TABLE folder_assignments ADD COLUMN render_key TEXT;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch (SqliteException) { }

            try
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.Transaction = tx;
                alterCmd.CommandText = "ALTER TABLE history ADD COLUMN render_key TEXT;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch (SqliteException) { }

            if (injectFailure)
            {
                throw new InvalidOperationException("Injected forward migration failure for rollback verification.");
            }

            using var setVersionCmd = connection.CreateCommand();
            setVersionCmd.Transaction = tx;
            setVersionCmd.CommandText = "PRAGMA user_version = 2;";
            await setVersionCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task ApplyMigrationV3InternalAsync(SqliteConnection connection, bool injectFailure)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS tags (
                    id TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    normalized_name TEXT NOT NULL UNIQUE,
                    created_utc INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS asset_tags (
                    asset_hash TEXT NOT NULL REFERENCES assets(sha256) ON DELETE CASCADE,
                    tag_id TEXT NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
                    created_utc INTEGER NOT NULL,
                    PRIMARY KEY (asset_hash, tag_id)
                );

                CREATE INDEX IF NOT EXISTS idx_tags_normalized_name ON tags(normalized_name);
                CREATE INDEX IF NOT EXISTS idx_asset_tags_tag_id ON asset_tags(tag_id);
                CREATE INDEX IF NOT EXISTS idx_asset_tags_asset_hash ON asset_tags(asset_hash);
            ";
            await cmd.ExecuteNonQueryAsync();

            if (injectFailure)
            {
                throw new InvalidOperationException("Injected forward migration failure for rollback verification.");
            }

            using var setVersionCmd = connection.CreateCommand();
            setVersionCmd.Transaction = tx;
            setVersionCmd.CommandText = "PRAGMA user_version = 3;";
            await setVersionCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task ApplyMigrationV4InternalAsync(SqliteConnection connection, bool injectFailure)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS collections (
                    id TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    normalized_name TEXT NOT NULL UNIQUE,
                    sort_key INTEGER NOT NULL,
                    created_utc INTEGER NOT NULL,
                    updated_utc INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS collection_assets (
                    collection_id TEXT NOT NULL REFERENCES collections(id) ON DELETE CASCADE,
                    asset_hash TEXT NOT NULL REFERENCES assets(sha256) ON DELETE CASCADE,
                    created_utc INTEGER NOT NULL,
                    PRIMARY KEY (collection_id, asset_hash)
                );

                CREATE INDEX IF NOT EXISTS idx_collections_sort_key ON collections(sort_key ASC);
                CREATE INDEX IF NOT EXISTS idx_collections_normalized_name ON collections(normalized_name);
                CREATE INDEX IF NOT EXISTS idx_collection_assets_asset_hash ON collection_assets(asset_hash);
                CREATE INDEX IF NOT EXISTS idx_collection_assets_collection_id ON collection_assets(collection_id);
                CREATE INDEX IF NOT EXISTS idx_collection_assets_col_created ON collection_assets(collection_id, created_utc DESC);
            ";
            await cmd.ExecuteNonQueryAsync();

            if (injectFailure)
            {
                throw new InvalidOperationException("Injected forward migration failure for rollback verification.");
            }

            using var setVersionCmd = connection.CreateCommand();
            setVersionCmd.Transaction = tx;
            setVersionCmd.CommandText = "PRAGMA user_version = 4;";
            await setVersionCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task ApplyMigrationV5InternalAsync(SqliteConnection connection, bool injectFailure)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS asset_search_tokens (
                    asset_hash TEXT NOT NULL REFERENCES assets(sha256) ON DELETE CASCADE,
                    token TEXT NOT NULL,
                    PRIMARY KEY (asset_hash, token)
                );

                CREATE TABLE IF NOT EXISTS tag_search_tokens (
                    tag_id TEXT NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
                    token TEXT NOT NULL,
                    PRIMARY KEY (tag_id, token)
                );

                CREATE TABLE IF NOT EXISTS collection_search_tokens (
                    collection_id TEXT NOT NULL REFERENCES collections(id) ON DELETE CASCADE,
                    token TEXT NOT NULL,
                    PRIMARY KEY (collection_id, token)
                );

                CREATE TABLE IF NOT EXISTS search_index_state (
                    index_version INTEGER PRIMARY KEY,
                    phase TEXT NOT NULL,
                    last_cursor TEXT,
                    is_ready INTEGER NOT NULL,
                    updated_utc INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_asset_search_tokens_token ON asset_search_tokens(token ASC, asset_hash);
                CREATE INDEX IF NOT EXISTS idx_tag_search_tokens_token ON tag_search_tokens(token ASC, tag_id);
                CREATE INDEX IF NOT EXISTS idx_collection_search_tokens_token ON collection_search_tokens(token ASC, collection_id);
            ";
            await cmd.ExecuteNonQueryAsync();

            try
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.Transaction = tx;
                alterCmd.CommandText = "ALTER TABLE assets ADD COLUMN display_name TEXT;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Column may already exist
            }

            if (injectFailure)
            {
                throw new InvalidOperationException("Injected forward migration failure for rollback verification.");
            }

            using var setVersionCmd = connection.CreateCommand();
            setVersionCmd.Transaction = tx;
            setVersionCmd.CommandText = "PRAGMA user_version = 5;";
            await setVersionCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task ApplyMigrationV6InternalAsync(SqliteConnection connection, bool injectFailure)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            // 1. Add volume_serial_number (INTEGER) and file_id_blob (BLOB) to folder_assignments
            try
            {
                using var alterCmd1 = connection.CreateCommand();
                alterCmd1.Transaction = tx;
                alterCmd1.CommandText = "ALTER TABLE folder_assignments ADD COLUMN volume_serial_number INTEGER;";
                await alterCmd1.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Column may already exist
            }

            try
            {
                using var alterCmd2 = connection.CreateCommand();
                alterCmd2.Transaction = tx;
                alterCmd2.CommandText = "ALTER TABLE folder_assignments ADD COLUMN file_id_blob BLOB;";
                await alterCmd2.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Column may already exist
            }

            // 2. Convert any existing legacy hex strings (volume_serial and file_id) to typed columns
            bool hasLegacyVolSerial = false;
            bool hasLegacyFileId = false;

            using (var infoCmd = connection.CreateCommand())
            {
                infoCmd.Transaction = tx;
                infoCmd.CommandText = "PRAGMA table_info(folder_assignments);";
                using var infoReader = await infoCmd.ExecuteReaderAsync();
                while (await infoReader.ReadAsync())
                {
                    string colName = infoReader.GetString(1);
                    if (string.Equals(colName, "volume_serial", StringComparison.OrdinalIgnoreCase))
                    {
                        hasLegacyVolSerial = true;
                    }
                    else if (string.Equals(colName, "file_id", StringComparison.OrdinalIgnoreCase))
                    {
                        hasLegacyFileId = true;
                    }
                }
            }

            var existingRows = new List<(string id, string? volHex, string? fidHex)>();
            if (hasLegacyVolSerial || hasLegacyFileId)
            {
                string volCol = hasLegacyVolSerial ? "volume_serial" : "NULL";
                string fidCol = hasLegacyFileId ? "file_id" : "NULL";
                using var selectCmd = connection.CreateCommand();
                selectCmd.Transaction = tx;
                selectCmd.CommandText = $"SELECT id, {volCol}, {fidCol} FROM folder_assignments WHERE {volCol} IS NOT NULL OR {fidCol} IS NOT NULL;";
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingRows.Add((
                        reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2)
                    ));
                }
            }

            foreach (var row in existingRows)
            {
                uint? volNum = null;
                byte[]? fidBlob = null;

                if (!string.IsNullOrWhiteSpace(row.volHex) &&
                    uint.TryParse(row.volHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedVol))
                {
                    volNum = parsedVol;
                }

                if (!string.IsNullOrWhiteSpace(row.fidHex) &&
                    ulong.TryParse(row.fidHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsedFid))
                {
                    fidBlob = BitConverter.GetBytes(parsedFid);
                }

                if (volNum.HasValue || fidBlob != null)
                {
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.Transaction = tx;
                    updateCmd.CommandText = @"
                        UPDATE folder_assignments
                        SET volume_serial_number = COALESCE(@volNum, volume_serial_number),
                            file_id_blob = COALESCE(@blob, file_id_blob)
                        WHERE id = @id;
                    ";
                    updateCmd.Parameters.AddWithValue("@volNum", (object?)volNum ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@blob", (object?)fidBlob ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@id", row.id);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            }

            // 3. Create compound index on (volume_serial_number, file_id_blob)
            using (var indexCmd = connection.CreateCommand())
            {
                indexCmd.Transaction = tx;
                indexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_folder_assignments_identity ON folder_assignments(volume_serial_number, file_id_blob);";
                await indexCmd.ExecuteNonQueryAsync();
            }

            if (injectFailure)
            {
                throw new InvalidOperationException("Injected forward migration failure for rollback verification.");
            }

            using var setVersionCmd = connection.CreateCommand();
            setVersionCmd.Transaction = tx;
            setVersionCmd.CommandText = "PRAGMA user_version = 6;";
            await setVersionCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    #region F15: Library Tags Implementation

    private async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    public async Task<bool> VerifyForeignKeysEnabledAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys;";
            object? result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<LibraryTag> CreateOrGetTagAsync(string tagName)
    {
        EnsureInitialized();
        if (!LibraryNameNormalizer.TryValidateAndNormalize(tagName, out string displayName, out string normalizedName, out string? error))
        {
            throw new ArgumentException(error ?? "Invalid tag name.", nameof(tagName));
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.Transaction = tx;
                selectCmd.CommandText = "SELECT id, display_name, normalized_name, created_utc FROM tags WHERE normalized_name = @norm;";
                selectCmd.Parameters.AddWithValue("@norm", normalizedName);
                using var reader = await selectCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new LibraryTag(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt64(3)
                    );
                }
            }

            using (var countCmd = connection.CreateCommand())
            {
                countCmd.Transaction = tx;
                countCmd.CommandText = "SELECT COUNT(*) FROM tags;";
                long count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (count >= LibraryNameNormalizer.MaxTotalTags)
                {
                    throw new InvalidOperationException($"Total tag limit of {LibraryNameNormalizer.MaxTotalTags} has been reached.");
                }
            }

            string newId = Guid.NewGuid().ToString("N");
            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO tags (id, display_name, normalized_name, created_utc)
                    VALUES (@id, @display, @norm, @created);
                ";
                insertCmd.Parameters.AddWithValue("@id", newId);
                insertCmd.Parameters.AddWithValue("@display", displayName);
                insertCmd.Parameters.AddWithValue("@norm", normalizedName);
                insertCmd.Parameters.AddWithValue("@created", nowUtc);
                await insertCmd.ExecuteNonQueryAsync();
            }

            try
            {
                var tagTokens = LibrarySearchTokenizer.Tokenize(displayName);
                foreach (var t in tagTokens)
                {
                    using var tCmd = connection.CreateCommand();
                    tCmd.Transaction = tx;
                    tCmd.CommandText = "INSERT OR IGNORE INTO tag_search_tokens (tag_id, token) VALUES (@id, @token);";
                    tCmd.Parameters.AddWithValue("@id", newId);
                    tCmd.Parameters.AddWithValue("@token", t);
                    await tCmd.ExecuteNonQueryAsync();
                }
            }
            catch (SqliteException)
            {
                // Pre-V5 migration compatibility
            }

            tx.Commit();
            return new LibraryTag(newId, displayName, normalizedName, nowUtc);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> AttachTagToAssetAsync(string assetHash, string tagId)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT 1 FROM asset_tags WHERE asset_hash = @hash AND tag_id = @tagId;";
                checkCmd.Parameters.AddWithValue("@hash", assetHash);
                checkCmd.Parameters.AddWithValue("@tagId", tagId);
                object? exists = await checkCmd.ExecuteScalarAsync();
                if (exists != null)
                {
                    return true;
                }
            }

            using (var countCmd = connection.CreateCommand())
            {
                countCmd.Transaction = tx;
                countCmd.CommandText = "SELECT COUNT(*) FROM asset_tags WHERE asset_hash = @hash;";
                countCmd.Parameters.AddWithValue("@hash", assetHash);
                long count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (count >= LibraryNameNormalizer.MaxTagsPerAsset)
                {
                    throw new InvalidOperationException($"Asset tag limit of {LibraryNameNormalizer.MaxTagsPerAsset} has been reached for this asset.");
                }
            }

            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO asset_tags (asset_hash, tag_id, created_utc)
                    VALUES (@hash, @tagId, @created);
                ";
                insertCmd.Parameters.AddWithValue("@hash", assetHash);
                insertCmd.Parameters.AddWithValue("@tagId", tagId);
                insertCmd.Parameters.AddWithValue("@created", nowUtc);
                await insertCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> DetachTagFromAssetAsync(string assetHash, string tagId)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM asset_tags WHERE asset_hash = @hash AND tag_id = @tagId;";
            cmd.Parameters.AddWithValue("@hash", assetHash);
            cmd.Parameters.AddWithValue("@tagId", tagId);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> DeleteTagAsync(string tagId)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM tags WHERE id = @tagId;";
            cmd.Parameters.AddWithValue("@tagId", tagId);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryTag>> GetTagsForAssetAsync(string assetHash)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT t.id, t.display_name, t.normalized_name, t.created_utc
                FROM tags t
                INNER JOIN asset_tags at ON t.id = at.tag_id
                WHERE at.asset_hash = @hash
                ORDER BY t.display_name COLLATE NOCASE ASC;
            ";
            cmd.Parameters.AddWithValue("@hash", assetHash);

            var list = new List<LibraryTag>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryTag(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3)
                ));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryTag>> GetAllTagsAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, display_name, normalized_name, created_utc FROM tags ORDER BY display_name COLLATE NOCASE ASC;";

            var list = new List<LibraryTag>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryTag(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3)
                ));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryAsset>> GetAssetsByTagAsync(string tagId)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       (CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END) AS is_favorite
                FROM assets a
                INNER JOIN asset_tags at ON a.sha256 = at.asset_hash
                LEFT JOIN favorites f ON a.id = f.asset_id
                WHERE at.tag_id = @tagId
                ORDER BY a.last_used_at DESC;
            ";
            cmd.Parameters.AddWithValue("@tagId", tagId);

            var list = new List<LibraryAsset>();
            using var reader = await cmd.ExecuteReaderAsync();
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

    public async Task<Dictionary<string, List<LibraryTag>>> GetTagsForAssetsAsync(IEnumerable<string> assetHashes)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(assetHashes);

        var hashes = assetHashes.Distinct().Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        var result = new Dictionary<string, List<LibraryTag>>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in hashes)
        {
            result[h] = new List<LibraryTag>();
        }

        if (hashes.Count == 0)
        {
            return result;
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();

            foreach (var chunk in hashes.Chunk(100))
            {
                using var cmd = connection.CreateCommand();
                var paramNames = new List<string>();
                for (int i = 0; i < chunk.Length; i++)
                {
                    string p = $"@h{i}";
                    paramNames.Add(p);
                    cmd.Parameters.AddWithValue(p, chunk[i]);
                }

                cmd.CommandText = $@"
                    SELECT at.asset_hash, t.id, t.display_name, t.normalized_name, t.created_utc
                    FROM tags t
                    INNER JOIN asset_tags at ON t.id = at.tag_id
                    WHERE at.asset_hash IN ({string.Join(",", paramNames)})
                    ORDER BY t.display_name COLLATE NOCASE ASC;
                ";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string hash = reader.GetString(0);
                    var tag = new LibraryTag(
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt64(4)
                    );
                    if (result.TryGetValue(hash, out var list))
                    {
                        list.Add(tag);
                    }
                }
            }

            return result;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    #endregion

    #region F16: Custom Collections Implementation

    public const long CollectionSortKeyStep = 1024L;
    public const long CollectionSortKeyOverflowThreshold = long.MaxValue - 2048L;

    public async Task<LibraryCollection> CreateCollectionAsync(string name)
    {
        EnsureInitialized();
        if (!LibraryNameNormalizer.TryValidateAndNormalize(
            name,
            out string displayName,
            out string normalizedName,
            out string? error,
            maxScalars: LibraryNameNormalizer.MaxScalarsPerCollection))
        {
            throw new ArgumentException(error ?? "Invalid collection name.", nameof(name));
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var countCmd = connection.CreateCommand())
            {
                countCmd.Transaction = tx;
                countCmd.CommandText = "SELECT COUNT(*) FROM collections;";
                long count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (count >= LibraryNameNormalizer.MaxTotalCollections)
                {
                    throw new InvalidOperationException($"Maximum limit of {LibraryNameNormalizer.MaxTotalCollections} collections reached.");
                }
            }

            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT COUNT(*) FROM collections WHERE normalized_name = @norm;";
                checkCmd.Parameters.AddWithValue("@norm", normalizedName);
                long dupCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (dupCount > 0)
                {
                    throw new InvalidOperationException($"A collection with the name '{displayName}' already exists.");
                }
            }

            long sortKey = CollectionSortKeyStep;
            using (var maxCmd = connection.CreateCommand())
            {
                maxCmd.Transaction = tx;
                maxCmd.CommandText = "SELECT MAX(sort_key) FROM collections;";
                object? maxVal = await maxCmd.ExecuteScalarAsync();
                if (maxVal != null && maxVal != DBNull.Value)
                {
                    long currentMax = Convert.ToInt64(maxVal, CultureInfo.InvariantCulture);
                    if (currentMax >= CollectionSortKeyOverflowThreshold)
                    {
                        await RebalanceInternalAsync(connection, tx, excludeId: null);
                        using var remaxCmd = connection.CreateCommand();
                        remaxCmd.Transaction = tx;
                        remaxCmd.CommandText = "SELECT MAX(sort_key) FROM collections;";
                        object? remaxVal = await remaxCmd.ExecuteScalarAsync();
                        currentMax = (remaxVal != null && remaxVal != DBNull.Value)
                            ? Convert.ToInt64(remaxVal, CultureInfo.InvariantCulture)
                            : 0L;
                    }
                    sortKey = currentMax + CollectionSortKeyStep;
                }
            }

            string id = Guid.NewGuid().ToString("N");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO collections (id, display_name, normalized_name, sort_key, created_utc, updated_utc)
                    VALUES (@id, @displayName, @norm, @sortKey, @now, @now);
                ";
                insertCmd.Parameters.AddWithValue("@id", id);
                insertCmd.Parameters.AddWithValue("@displayName", displayName);
                insertCmd.Parameters.AddWithValue("@norm", normalizedName);
                insertCmd.Parameters.AddWithValue("@sortKey", sortKey);
                insertCmd.Parameters.AddWithValue("@now", now);
                await insertCmd.ExecuteNonQueryAsync();
            }

            try
            {
                var colTokens = LibrarySearchTokenizer.Tokenize(displayName);
                foreach (var t in colTokens)
                {
                    using var tCmd = connection.CreateCommand();
                    tCmd.Transaction = tx;
                    tCmd.CommandText = "INSERT OR IGNORE INTO collection_search_tokens (collection_id, token) VALUES (@id, @token);";
                    tCmd.Parameters.AddWithValue("@id", id);
                    tCmd.Parameters.AddWithValue("@token", t);
                    await tCmd.ExecuteNonQueryAsync();
                }
            }
            catch (SqliteException)
            {
                // Pre-V5 migration compatibility
            }

            tx.Commit();
            return new LibraryCollection(id, displayName, normalizedName, sortKey, now, now);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<LibraryCollection?> GetCollectionByIdAsync(string collectionId)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, display_name, normalized_name, sort_key, created_utc, updated_utc FROM collections WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", collectionId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LibraryCollection(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5)
                );
            }
            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<LibraryCollection?> GetCollectionByNormalizedNameAsync(string normalizedName)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, display_name, normalized_name, sort_key, created_utc, updated_utc FROM collections WHERE normalized_name = @norm;";
            cmd.Parameters.AddWithValue("@norm", normalizedName);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LibraryCollection(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5)
                );
            }
            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryCollection>> GetAllCollectionsAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, display_name, normalized_name, sort_key, created_utc, updated_utc FROM collections ORDER BY sort_key ASC, display_name COLLATE NOCASE ASC;";
            var list = new List<LibraryCollection>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryCollection(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5)
                ));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> RenameCollectionAsync(string collectionId, string newName)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        if (!LibraryNameNormalizer.TryValidateAndNormalize(
            newName,
            out string displayName,
            out string normalizedName,
            out string? error,
            maxScalars: LibraryNameNormalizer.MaxScalarsPerCollection))
        {
            throw new ArgumentException(error ?? "Invalid collection name.", nameof(newName));
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT COUNT(*) FROM collections WHERE normalized_name = @norm AND id != @id;";
                checkCmd.Parameters.AddWithValue("@norm", normalizedName);
                checkCmd.Parameters.AddWithValue("@id", collectionId);
                long dupCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (dupCount > 0)
                {
                    throw new InvalidOperationException($"A collection with the name '{displayName}' already exists.");
                }
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int rows;
            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
                    UPDATE collections
                    SET display_name = @displayName, normalized_name = @norm, updated_utc = @now
                    WHERE id = @id;
                ";
                updateCmd.Parameters.AddWithValue("@displayName", displayName);
                updateCmd.Parameters.AddWithValue("@norm", normalizedName);
                updateCmd.Parameters.AddWithValue("@now", now);
                updateCmd.Parameters.AddWithValue("@id", collectionId);
                rows = await updateCmd.ExecuteNonQueryAsync();
            }

            try
            {
                using var delCmd = connection.CreateCommand();
                delCmd.Transaction = tx;
                delCmd.CommandText = "DELETE FROM collection_search_tokens WHERE collection_id = @id;";
                delCmd.Parameters.AddWithValue("@id", collectionId);
                await delCmd.ExecuteNonQueryAsync();

                var colTokens = LibrarySearchTokenizer.Tokenize(displayName);
                foreach (var t in colTokens)
                {
                    using var tCmd = connection.CreateCommand();
                    tCmd.Transaction = tx;
                    tCmd.CommandText = "INSERT OR IGNORE INTO collection_search_tokens (collection_id, token) VALUES (@id, @token);";
                    tCmd.Parameters.AddWithValue("@id", collectionId);
                    tCmd.Parameters.AddWithValue("@token", t);
                    await tCmd.ExecuteNonQueryAsync();
                }
            }
            catch (SqliteException)
            {
                // Pre-V5 migration compatibility
            }

            tx.Commit();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> DeleteCollectionAsync(string collectionId)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM collections WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", collectionId);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> AddAssetToCollectionAsync(string collectionId, string assetHash)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM collection_assets WHERE collection_id = @cid AND asset_hash = @hash;";
            checkCmd.Parameters.AddWithValue("@cid", collectionId);
            checkCmd.Parameters.AddWithValue("@hash", assetHash);
            if (await checkCmd.ExecuteScalarAsync() != null)
            {
                return true;
            }

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO collection_assets (collection_id, asset_hash, created_utc)
                VALUES (@cid, @hash, @now);
            ";
            insertCmd.Parameters.AddWithValue("@cid", collectionId);
            insertCmd.Parameters.AddWithValue("@hash", assetHash);
            insertCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            int rows = await insertCmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> RemoveAssetFromCollectionAsync(string collectionId, string assetHash)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM collection_assets WHERE collection_id = @cid AND asset_hash = @hash;";
            cmd.Parameters.AddWithValue("@cid", collectionId);
            cmd.Parameters.AddWithValue("@hash", assetHash);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryAsset>> GetAssetsInCollectionAsync(string collectionId, int limit = 100, int offset = 0)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       (CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END) AS is_favorite
                FROM assets a
                INNER JOIN collection_assets ca ON a.sha256 = ca.asset_hash
                LEFT JOIN favorites f ON a.id = f.asset_id
                WHERE ca.collection_id = @cid
                ORDER BY ca.created_utc DESC, a.last_used_at DESC
                LIMIT @limit OFFSET @offset;
            ";
            cmd.Parameters.AddWithValue("@cid", collectionId);
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            var list = new List<LibraryAsset>();
            using var reader = await cmd.ExecuteReaderAsync();
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

    public async Task<IReadOnlyList<LibraryCollection>> GetCollectionsForAssetAsync(string assetHash)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT c.id, c.display_name, c.normalized_name, c.sort_key, c.created_utc, c.updated_utc
                FROM collections c
                INNER JOIN collection_assets ca ON c.id = ca.collection_id
                WHERE ca.asset_hash = @hash
                ORDER BY c.sort_key ASC;
            ";
            cmd.Parameters.AddWithValue("@hash", assetHash);
            var list = new List<LibraryCollection>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new LibraryCollection(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5)
                ));
            }
            return list;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> ReorderCollectionAsync(string collectionId, string? targetNeighborId, bool insertBefore)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        if (targetNeighborId == collectionId)
        {
            return true;
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.Transaction = tx;
                verifyCmd.CommandText = "SELECT 1 FROM collections WHERE id = @id;";
                verifyCmd.Parameters.AddWithValue("@id", collectionId);
                if (await verifyCmd.ExecuteScalarAsync() == null)
                {
                    return false;
                }
            }

            long newKey;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (targetNeighborId == null)
            {
                var other = await LoadCollectionsInternalAsync(connection, tx, excludeId: collectionId);
                if (other.Count == 0)
                {
                    newKey = CollectionSortKeyStep;
                }
                else if (insertBefore)
                {
                    long firstKey = other[0].SortKey;
                    if (firstKey > 1)
                    {
                        newKey = firstKey / 2;
                    }
                    else
                    {
                        await RebalanceInternalAsync(connection, tx, excludeId: collectionId);
                        newKey = 512L;
                    }
                }
                else
                {
                    long lastKey = other[^1].SortKey;
                    if (lastKey >= CollectionSortKeyOverflowThreshold)
                    {
                        await RebalanceInternalAsync(connection, tx, excludeId: collectionId);
                        other = await LoadCollectionsInternalAsync(connection, tx, excludeId: collectionId);
                        lastKey = other[^1].SortKey;
                    }
                    newKey = lastKey + CollectionSortKeyStep;
                }
            }
            else
            {
                var other = await LoadCollectionsInternalAsync(connection, tx, excludeId: collectionId);
                int targetIdx = other.FindIndex(c => c.Id == targetNeighborId);
                if (targetIdx < 0)
                {
                    return false;
                }

                if (insertBefore)
                {
                    long prevKey = targetIdx > 0 ? other[targetIdx - 1].SortKey : 0L;
                    long nextKey = other[targetIdx].SortKey;
                    long gap = nextKey - prevKey;
                    if (gap >= 2)
                    {
                        newKey = prevKey + (gap / 2);
                    }
                    else
                    {
                        await RebalanceInternalAsync(connection, tx, excludeId: collectionId);
                        other = await LoadCollectionsInternalAsync(connection, tx, excludeId: collectionId);
                        prevKey = targetIdx > 0 ? other[targetIdx - 1].SortKey : 0L;
                        nextKey = other[targetIdx].SortKey;
                        newKey = prevKey + ((nextKey - prevKey) / 2);
                    }
                }
                else
                {
                    long prevKey = other[targetIdx].SortKey;
                    if (targetIdx < other.Count - 1)
                    {
                        long nextKey = other[targetIdx + 1].SortKey;
                        long gap = nextKey - prevKey;
                        if (gap >= 2)
                        {
                            newKey = prevKey + (gap / 2);
                        }
                        else
                        {
                            await RebalanceInternalAsync(connection, tx, excludeId: collectionId);
                            other = await LoadCollectionsInternalAsync(connection, tx, excludeId: collectionId);
                            prevKey = other[targetIdx].SortKey;
                            nextKey = other[targetIdx + 1].SortKey;
                            newKey = prevKey + ((nextKey - prevKey) / 2);
                        }
                    }
                    else
                    {
                        if (prevKey >= CollectionSortKeyOverflowThreshold)
                        {
                            await RebalanceInternalAsync(connection, tx, excludeId: collectionId);
                            other = await LoadCollectionsInternalAsync(connection, tx, excludeId: collectionId);
                            prevKey = other[targetIdx].SortKey;
                        }
                        newKey = prevKey + CollectionSortKeyStep;
                    }
                }
            }

            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE collections SET sort_key = @key, updated_utc = @now WHERE id = @id;";
                updateCmd.Parameters.AddWithValue("@key", newKey);
                updateCmd.Parameters.AddWithValue("@now", now);
                updateCmd.Parameters.AddWithValue("@id", collectionId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task RebalanceCollectionSortKeysAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();
            await RebalanceInternalAsync(connection, tx, excludeId: null);
            tx.Commit();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static async Task<List<LibraryCollection>> LoadCollectionsInternalAsync(SqliteConnection connection, SqliteTransaction tx, string? excludeId = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        if (excludeId != null)
        {
            cmd.CommandText = "SELECT id, display_name, normalized_name, sort_key, created_utc, updated_utc FROM collections WHERE id != @exId ORDER BY sort_key ASC, id ASC;";
            cmd.Parameters.AddWithValue("@exId", excludeId);
        }
        else
        {
            cmd.CommandText = "SELECT id, display_name, normalized_name, sort_key, created_utc, updated_utc FROM collections ORDER BY sort_key ASC, id ASC;";
        }

        var list = new List<LibraryCollection>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LibraryCollection(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5)
            ));
        }
        return list;
    }

    private static async Task RebalanceInternalAsync(SqliteConnection connection, SqliteTransaction tx, string? excludeId = null)
    {
        var ids = new List<string>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            if (excludeId != null)
            {
                cmd.CommandText = "SELECT id FROM collections WHERE id != @exId ORDER BY sort_key ASC, id ASC;";
                cmd.Parameters.AddWithValue("@exId", excludeId);
            }
            else
            {
                cmd.CommandText = "SELECT id FROM collections ORDER BY sort_key ASC, id ASC;";
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetString(0));
            }
        }

        long key = CollectionSortKeyStep;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var id in ids)
        {
            using var updateCmd = connection.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE collections SET sort_key = @key, updated_utc = @now WHERE id = @id;";
            updateCmd.Parameters.AddWithValue("@key", key);
            updateCmd.Parameters.AddWithValue("@now", now);
            updateCmd.Parameters.AddWithValue("@id", id);
            await updateCmd.ExecuteNonQueryAsync();
            key += CollectionSortKeyStep;
        }
    }

    #endregion

    #region F17: Library Search & Durable Index Implementation

    public async Task<SearchIndexState> GetSearchIndexStateAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT index_version, phase, last_cursor, is_ready, updated_utc FROM search_index_state WHERE index_version = 1;";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new SearchIndexState(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetInt32(3) == 1,
                    reader.GetInt64(4)
                );
            }

            return new SearchIndexState(1, "Assets", null, false, 0);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> StepSearchIndexBuildAsync(int batchSize = 500)
    {
        EnsureInitialized();
        if (batchSize <= 0) batchSize = 500;

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            SearchIndexState state;
            using (var stateCmd = connection.CreateCommand())
            {
                stateCmd.Transaction = tx;
                stateCmd.CommandText = "SELECT index_version, phase, last_cursor, is_ready, updated_utc FROM search_index_state WHERE index_version = 1;";
                using var reader = await stateCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    state = new SearchIndexState(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetInt32(3) == 1,
                        reader.GetInt64(4)
                    );
                }
                else
                {
                    state = new SearchIndexState(1, "Assets", null, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    using var insertStateCmd = connection.CreateCommand();
                    insertStateCmd.Transaction = tx;
                    insertStateCmd.CommandText = "INSERT INTO search_index_state (index_version, phase, last_cursor, is_ready, updated_utc) VALUES (1, 'Assets', NULL, 0, @now);";
                    insertStateCmd.Parameters.AddWithValue("@now", state.UpdatedUtc);
                    await insertStateCmd.ExecuteNonQueryAsync();
                }
            }

            if (state.IsReady)
            {
                tx.Commit();
                return true;
            }

            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (string.Equals(state.Phase, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                var batch = new List<(string sha256, string? displayName, string icoPath)>();
                using (var fetchCmd = connection.CreateCommand())
                {
                    fetchCmd.Transaction = tx;
                    if (state.LastCursor == null)
                    {
                        fetchCmd.CommandText = "SELECT sha256, display_name, canonical_ico_path FROM assets ORDER BY sha256 ASC LIMIT @limit;";
                    }
                    else
                    {
                        fetchCmd.CommandText = "SELECT sha256, display_name, canonical_ico_path FROM assets WHERE sha256 > @cursor ORDER BY sha256 ASC LIMIT @limit;";
                        fetchCmd.Parameters.AddWithValue("@cursor", state.LastCursor);
                    }
                    fetchCmd.Parameters.AddWithValue("@limit", batchSize);

                    using var reader = await fetchCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        batch.Add((
                            reader.GetString(0),
                            reader.IsDBNull(1) ? null : reader.GetString(1),
                            reader.GetString(2)
                        ));
                    }
                }

                foreach (var item in batch)
                {
                    string textToTokenize = !string.IsNullOrWhiteSpace(item.displayName)
                        ? item.displayName
                        : (Path.GetFileNameWithoutExtension(item.icoPath) ?? item.sha256);

                    var tokens = LibrarySearchTokenizer.Tokenize(textToTokenize);
                    foreach (var t in tokens)
                    {
                        using var insCmd = connection.CreateCommand();
                        insCmd.Transaction = tx;
                        insCmd.CommandText = "INSERT OR IGNORE INTO asset_search_tokens (asset_hash, token) VALUES (@hash, @token);";
                        insCmd.Parameters.AddWithValue("@hash", item.sha256);
                        insCmd.Parameters.AddWithValue("@token", t);
                        await insCmd.ExecuteNonQueryAsync();
                    }
                }

                using (var updateStateCmd = connection.CreateCommand())
                {
                    updateStateCmd.Transaction = tx;
                    if (batch.Count == batchSize)
                    {
                        string nextCursor = batch[^1].sha256;
                        updateStateCmd.CommandText = "UPDATE search_index_state SET last_cursor = @cursor, updated_utc = @now WHERE index_version = 1;";
                        updateStateCmd.Parameters.AddWithValue("@cursor", nextCursor);
                        updateStateCmd.Parameters.AddWithValue("@now", nowUtc);
                    }
                    else
                    {
                        updateStateCmd.CommandText = "UPDATE search_index_state SET phase = 'Tags', last_cursor = NULL, updated_utc = @now WHERE index_version = 1;";
                        updateStateCmd.Parameters.AddWithValue("@now", nowUtc);
                    }
                    await updateStateCmd.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return false;
            }
            else if (string.Equals(state.Phase, "Tags", StringComparison.OrdinalIgnoreCase))
            {
                var batch = new List<(string id, string displayName)>();
                using (var fetchCmd = connection.CreateCommand())
                {
                    fetchCmd.Transaction = tx;
                    if (state.LastCursor == null)
                    {
                        fetchCmd.CommandText = "SELECT id, display_name FROM tags ORDER BY id ASC LIMIT @limit;";
                    }
                    else
                    {
                        fetchCmd.CommandText = "SELECT id, display_name FROM tags WHERE id > @cursor ORDER BY id ASC LIMIT @limit;";
                        fetchCmd.Parameters.AddWithValue("@cursor", state.LastCursor);
                    }
                    fetchCmd.Parameters.AddWithValue("@limit", batchSize);

                    using var reader = await fetchCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        batch.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                foreach (var item in batch)
                {
                    var tokens = LibrarySearchTokenizer.Tokenize(item.displayName);
                    foreach (var t in tokens)
                    {
                        using var insCmd = connection.CreateCommand();
                        insCmd.Transaction = tx;
                        insCmd.CommandText = "INSERT OR IGNORE INTO tag_search_tokens (tag_id, token) VALUES (@id, @token);";
                        insCmd.Parameters.AddWithValue("@id", item.id);
                        insCmd.Parameters.AddWithValue("@token", t);
                        await insCmd.ExecuteNonQueryAsync();
                    }
                }

                using (var updateStateCmd = connection.CreateCommand())
                {
                    updateStateCmd.Transaction = tx;
                    if (batch.Count == batchSize)
                    {
                        string nextCursor = batch[^1].id;
                        updateStateCmd.CommandText = "UPDATE search_index_state SET last_cursor = @cursor, updated_utc = @now WHERE index_version = 1;";
                        updateStateCmd.Parameters.AddWithValue("@cursor", nextCursor);
                        updateStateCmd.Parameters.AddWithValue("@now", nowUtc);
                    }
                    else
                    {
                        updateStateCmd.CommandText = "UPDATE search_index_state SET phase = 'Collections', last_cursor = NULL, updated_utc = @now WHERE index_version = 1;";
                        updateStateCmd.Parameters.AddWithValue("@now", nowUtc);
                    }
                    await updateStateCmd.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return false;
            }
            else if (string.Equals(state.Phase, "Collections", StringComparison.OrdinalIgnoreCase))
            {
                var batch = new List<(string id, string displayName)>();
                using (var fetchCmd = connection.CreateCommand())
                {
                    fetchCmd.Transaction = tx;
                    if (state.LastCursor == null)
                    {
                        fetchCmd.CommandText = "SELECT id, display_name FROM collections ORDER BY id ASC LIMIT @limit;";
                    }
                    else
                    {
                        fetchCmd.CommandText = "SELECT id, display_name FROM collections WHERE id > @cursor ORDER BY id ASC LIMIT @limit;";
                        fetchCmd.Parameters.AddWithValue("@cursor", state.LastCursor);
                    }
                    fetchCmd.Parameters.AddWithValue("@limit", batchSize);

                    using var reader = await fetchCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        batch.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                foreach (var item in batch)
                {
                    var tokens = LibrarySearchTokenizer.Tokenize(item.displayName);
                    foreach (var t in tokens)
                    {
                        using var insCmd = connection.CreateCommand();
                        insCmd.Transaction = tx;
                        insCmd.CommandText = "INSERT OR IGNORE INTO collection_search_tokens (collection_id, token) VALUES (@id, @token);";
                        insCmd.Parameters.AddWithValue("@id", item.id);
                        insCmd.Parameters.AddWithValue("@token", t);
                        await insCmd.ExecuteNonQueryAsync();
                    }
                }

                using (var updateStateCmd = connection.CreateCommand())
                {
                    updateStateCmd.Transaction = tx;
                    if (batch.Count == batchSize)
                    {
                        string nextCursor = batch[^1].id;
                        updateStateCmd.CommandText = "UPDATE search_index_state SET last_cursor = @cursor, updated_utc = @now WHERE index_version = 1;";
                        updateStateCmd.Parameters.AddWithValue("@cursor", nextCursor);
                        updateStateCmd.Parameters.AddWithValue("@now", nowUtc);
                        await updateStateCmd.ExecuteNonQueryAsync();
                        tx.Commit();
                        return false;
                    }
                    else
                    {
                        updateStateCmd.CommandText = "UPDATE search_index_state SET phase = 'Ready', last_cursor = NULL, is_ready = 1, updated_utc = @now WHERE index_version = 1;";
                        updateStateCmd.Parameters.AddWithValue("@now", nowUtc);
                        await updateStateCmd.ExecuteNonQueryAsync();
                        tx.Commit();
                        return true;
                    }
                }
            }
            else
            {
                using var markReadyCmd = connection.CreateCommand();
                markReadyCmd.Transaction = tx;
                markReadyCmd.CommandText = "UPDATE search_index_state SET phase = 'Ready', is_ready = 1, updated_utc = @now WHERE index_version = 1;";
                markReadyCmd.Parameters.AddWithValue("@now", nowUtc);
                await markReadyCmd.ExecuteNonQueryAsync();
                tx.Commit();
                return true;
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task EnsureSearchIndexReadyAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        while (!cancellationToken.IsCancellationRequested)
        {
            bool isDone = await StepSearchIndexBuildAsync(500);
            if (isDone)
            {
                break;
            }
        }
    }

    public async Task RebuildSearchIndexAsync()
    {
        EnsureInitialized();
        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    DELETE FROM asset_search_tokens;
                    DELETE FROM tag_search_tokens;
                    DELETE FROM collection_search_tokens;
                    DELETE FROM search_index_state;
                    INSERT INTO search_index_state (index_version, phase, last_cursor, is_ready, updated_utc)
                    VALUES (1, 'Assets', NULL, 0, @now);
                ";
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> UpdateAssetDisplayNameAsync(string assetHash, string displayName)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(assetHash);
        ArgumentNullException.ThrowIfNull(displayName);

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE assets SET display_name = @name WHERE sha256 = @hash;";
                updateCmd.Parameters.AddWithValue("@name", displayName);
                updateCmd.Parameters.AddWithValue("@hash", assetHash);
                int rows = await updateCmd.ExecuteNonQueryAsync();
                if (rows == 0) return false;
            }

            using (var delCmd = connection.CreateCommand())
            {
                delCmd.Transaction = tx;
                delCmd.CommandText = "DELETE FROM asset_search_tokens WHERE asset_hash = @hash;";
                delCmd.Parameters.AddWithValue("@hash", assetHash);
                await delCmd.ExecuteNonQueryAsync();
            }

            var tokens = LibrarySearchTokenizer.Tokenize(displayName);
            foreach (var t in tokens)
            {
                using var insCmd = connection.CreateCommand();
                insCmd.Transaction = tx;
                insCmd.CommandText = "INSERT OR IGNORE INTO asset_search_tokens (asset_hash, token) VALUES (@hash, @token);";
                insCmd.Parameters.AddWithValue("@hash", assetHash);
                insCmd.Parameters.AddWithValue("@token", t);
                await insCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<bool> RenameTagAsync(string tagId, string newName)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);
        if (!LibraryNameNormalizer.TryValidateAndNormalize(newName, out string displayName, out string normalizedName, out string? error))
        {
            throw new ArgumentException(error ?? "Invalid tag name.", nameof(newName));
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            using var tx = connection.BeginTransaction();

            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.Transaction = tx;
                checkCmd.CommandText = "SELECT COUNT(*) FROM tags WHERE normalized_name = @norm AND id != @id;";
                checkCmd.Parameters.AddWithValue("@norm", normalizedName);
                checkCmd.Parameters.AddWithValue("@id", tagId);
                long dup = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                if (dup > 0)
                {
                    throw new InvalidOperationException($"A tag with the name '{displayName}' already exists.");
                }
            }

            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE tags SET display_name = @display, normalized_name = @norm WHERE id = @id;";
                updateCmd.Parameters.AddWithValue("@display", displayName);
                updateCmd.Parameters.AddWithValue("@norm", normalizedName);
                updateCmd.Parameters.AddWithValue("@id", tagId);
                int rows = await updateCmd.ExecuteNonQueryAsync();
                if (rows == 0) return false;
            }

            using (var delCmd = connection.CreateCommand())
            {
                delCmd.Transaction = tx;
                delCmd.CommandText = "DELETE FROM tag_search_tokens WHERE tag_id = @id;";
                delCmd.Parameters.AddWithValue("@id", tagId);
                await delCmd.ExecuteNonQueryAsync();
            }

            var tokens = LibrarySearchTokenizer.Tokenize(displayName);
            foreach (var t in tokens)
            {
                using var insCmd = connection.CreateCommand();
                insCmd.Transaction = tx;
                insCmd.CommandText = "INSERT OR IGNORE INTO tag_search_tokens (tag_id, token) VALUES (@id, @token);";
                insCmd.Parameters.AddWithValue("@id", tagId);
                insCmd.Parameters.AddWithValue("@token", t);
                await insCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryAsset>> SearchLibraryAsync(
        string query,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        if (limit <= 0) limit = 100;
        if (offset < 0) offset = 0;

        var queryTokens = LibrarySearchTokenizer.Tokenize(query);
        if (queryTokens.Count == 0)
        {
            return await GetPagedAssetsInternalAsync(limit, offset, cancellationToken);
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = await CreateOpenConnectionAsync(cancellationToken);

            // Check if search index is ready; return no partial results if incomplete
            using (var checkReadyCmd = connection.CreateCommand())
            {
                checkReadyCmd.CommandText = "SELECT is_ready FROM search_index_state WHERE index_version = 1;";
                object? isReadyVal = await checkReadyCmd.ExecuteScalarAsync(cancellationToken);
                bool isReady = isReadyVal != null && isReadyVal != DBNull.Value && Convert.ToInt32(isReadyVal, CultureInfo.InvariantCulture) == 1;
                if (!isReady)
                {
                    return Array.Empty<LibraryAsset>();
                }
            }

            // Cap query tokens at 16 to protect query plan budget
            var effectiveTokens = queryTokens.Take(16).ToList();

            using var cmd = connection.CreateCommand();
            var branches = new List<string>();

            for (int i = 0; i < effectiveTokens.Count; i++)
            {
                string token = effectiveTokens[i];
                string? nextToken = LibrarySearchTokenizer.ComputeLexicalNextTerm(token);
                string pMin = $"@tmin_{i}";
                string pNext = $"@tnext_{i}";
                string pOrd = $"@ord_{i}";

                cmd.Parameters.AddWithValue(pMin, token);
                cmd.Parameters.AddWithValue(pOrd, i);

                if (nextToken != null)
                {
                    cmd.Parameters.AddWithValue(pNext, nextToken);
                    branches.Add($@"
                        SELECT asset_hash, {pOrd} AS term_ord FROM asset_search_tokens WHERE token >= {pMin} AND token < {pNext}
                        UNION
                        SELECT at.asset_hash, {pOrd} AS term_ord FROM tag_search_tokens tst INNER JOIN asset_tags at ON at.tag_id = tst.tag_id WHERE tst.token >= {pMin} AND tst.token < {pNext}
                        UNION
                        SELECT ca.asset_hash, {pOrd} AS term_ord FROM collection_search_tokens cst INNER JOIN collection_assets ca ON ca.collection_id = cst.collection_id WHERE cst.token >= {pMin} AND cst.token < {pNext}
                    ");
                }
                else
                {
                    branches.Add($@"
                        SELECT asset_hash, {pOrd} AS term_ord FROM asset_search_tokens WHERE token = {pMin}
                        UNION
                        SELECT at.asset_hash, {pOrd} AS term_ord FROM tag_search_tokens tst INNER JOIN asset_tags at ON at.tag_id = tst.tag_id WHERE tst.token = {pMin}
                        UNION
                        SELECT ca.asset_hash, {pOrd} AS term_ord FROM collection_search_tokens cst INNER JOIN collection_assets ca ON ca.collection_id = cst.collection_id WHERE cst.token = {pMin}
                    ");
                }
            }

            string unionSql = string.Join("\nUNION\n", branches);
            cmd.CommandText = $@"
                WITH matches AS (
                    {unionSql}
                ),
                filtered_assets AS (
                    SELECT asset_hash
                    FROM matches
                    GROUP BY asset_hash
                    HAVING COUNT(DISTINCT term_ord) = @tokenCount
                )
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       (CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END) AS is_favorite
                FROM filtered_assets fa
                INNER JOIN assets a ON a.sha256 = fa.asset_hash
                LEFT JOIN favorites f ON f.asset_id = a.id
                ORDER BY a.last_used_at DESC
                LIMIT @limit OFFSET @offset;
            ";
            cmd.Parameters.AddWithValue("@tokenCount", effectiveTokens.Count);
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            var list = new List<LibraryAsset>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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

    public async Task<IReadOnlyList<string>> ExplainSearchQueryPlanAsync(string query)
    {
        EnsureInitialized();
        var queryTokens = LibrarySearchTokenizer.Tokenize(query);
        if (queryTokens.Count == 0)
        {
            return Array.Empty<string>();
        }

        await _dbLock.WaitAsync();
        try
        {
            using var connection = await CreateOpenConnectionAsync();
            var effectiveTokens = queryTokens.Take(16).ToList();

            using var cmd = connection.CreateCommand();
            var branches = new List<string>();

            for (int i = 0; i < effectiveTokens.Count; i++)
            {
                string token = effectiveTokens[i];
                string? nextToken = LibrarySearchTokenizer.ComputeLexicalNextTerm(token);
                string pMin = $"@tmin_{i}";
                string pNext = $"@tnext_{i}";
                string pOrd = $"@ord_{i}";

                cmd.Parameters.AddWithValue(pMin, token);
                cmd.Parameters.AddWithValue(pOrd, i);

                if (nextToken != null)
                {
                    cmd.Parameters.AddWithValue(pNext, nextToken);
                    branches.Add($@"
                        SELECT asset_hash, {pOrd} AS term_ord FROM asset_search_tokens WHERE token >= {pMin} AND token < {pNext}
                        UNION
                        SELECT at.asset_hash, {pOrd} AS term_ord FROM tag_search_tokens tst INNER JOIN asset_tags at ON at.tag_id = tst.tag_id WHERE tst.token >= {pMin} AND tst.token < {pNext}
                        UNION
                        SELECT ca.asset_hash, {pOrd} AS term_ord FROM collection_search_tokens cst INNER JOIN collection_assets ca ON ca.collection_id = cst.collection_id WHERE cst.token >= {pMin} AND cst.token < {pNext}
                    ");
                }
                else
                {
                    branches.Add($@"
                        SELECT asset_hash, {pOrd} AS term_ord FROM asset_search_tokens WHERE token = {pMin}
                        UNION
                        SELECT at.asset_hash, {pOrd} AS term_ord FROM tag_search_tokens tst INNER JOIN asset_tags at ON at.tag_id = tst.tag_id WHERE tst.token = {pMin}
                        UNION
                        SELECT ca.asset_hash, {pOrd} AS term_ord FROM collection_search_tokens cst INNER JOIN collection_assets ca ON ca.collection_id = cst.collection_id WHERE cst.token = {pMin}
                    ");
                }
            }

            string unionSql = string.Join("\nUNION\n", branches);
            cmd.CommandText = $@"
                EXPLAIN QUERY PLAN
                WITH matches AS (
                    {unionSql}
                ),
                filtered_assets AS (
                    SELECT asset_hash
                    FROM matches
                    GROUP BY asset_hash
                    HAVING COUNT(DISTINCT term_ord) = @tokenCount
                )
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       (CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END) AS is_favorite
                FROM filtered_assets fa
                INNER JOIN assets a ON a.sha256 = fa.asset_hash
                LEFT JOIN favorites f ON f.asset_id = a.id
                ORDER BY a.last_used_at DESC
                LIMIT @limit OFFSET @offset;
            ";
            cmd.Parameters.AddWithValue("@tokenCount", effectiveTokens.Count);
            cmd.Parameters.AddWithValue("@limit", 100);
            cmd.Parameters.AddWithValue("@offset", 0);

            var lines = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // In SQLite EXPLAIN QUERY PLAN, column 3 (detail) contains the plan description
                lines.Add(reader.GetString(3));
            }

            return lines;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<IReadOnlyList<LibraryAsset>> GetPagedAssetsInternalAsync(int limit, int offset, CancellationToken cancellationToken)
    {
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = await CreateOpenConnectionAsync(cancellationToken);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT a.id, a.sha256, a.canonical_ico_path, a.source_format, a.created_at, a.last_used_at,
                       (CASE WHEN f.asset_id IS NOT NULL THEN 1 ELSE 0 END) AS is_favorite
                FROM assets a
                LEFT JOIN favorites f ON a.id = f.asset_id
                ORDER BY a.last_used_at DESC
                LIMIT @limit OFFSET @offset;
            ";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            var list = new List<LibraryAsset>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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

    #endregion

    public void Dispose()
    {
        _dbLock.Dispose();
    }
}
