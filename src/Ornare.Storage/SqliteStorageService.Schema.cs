using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Ornare.Core;

namespace Ornare.Storage;

/// <summary>
/// Schema ownership: initial creation, forward migrations, connection opening and the
/// guards that decide whether the database may be touched at all.
/// <para>
/// Kept in its own file so a migration review never has to scroll through three thousand
/// lines of query code, and so the migration tests can point at one file (ENH-1).
/// </para>
/// </summary>
public sealed partial class SqliteStorageService
{
    private void EnsureWritable()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isReadOnly)
        {
            throw new InvalidOperationException("Cannot perform write operations on storage in read-only / DryRun mode.");
        }
    }

    public async Task InitializeReadOnlyAsync(string? dbPath = null)
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
            if (!File.Exists(databasePath))
            {
                _connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = ":memory:",
                    Mode = SqliteOpenMode.Memory,
                    ForeignKeys = true
                }.ToString();
            }
            else
            {
                _connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    ForeignKeys = true
                }.ToString();
            }

            _isReadOnly = true;
            _isInitialized = true;
        }
        finally
        {
            _dbLock.Release();
        }
    }

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

                // Safe column additions for an existing folder_assignments table. SQLite has no
                // "ADD COLUMN IF NOT EXISTS", so the live column set is read with PRAGMA
                // table_info first and only genuinely missing columns are added. Column names
                // and definitions are compile-time constants, never user input.
                HashSet<string> existingColumns = new(StringComparer.OrdinalIgnoreCase);
                using (SqliteCommand infoCmd = connection.CreateCommand())
                {
                    infoCmd.Transaction = tx;
                    infoCmd.CommandText = "PRAGMA table_info(folder_assignments);";
                    using SqliteDataReader infoReader = await infoCmd.ExecuteReaderAsync();
                    while (await infoReader.ReadAsync())
                    {
                        existingColumns.Add(infoReader.GetString(1));
                    }
                }

                (string Name, string Definition)[] migrationColumns =
                [
                    ("volume_serial", "volume_serial TEXT"),
                    ("file_id", "file_id TEXT"),
                    ("current_asset_hash", "current_asset_hash TEXT"),
                    ("conflict_state", "conflict_state TEXT")
                ];

                foreach ((string columnName, string columnDefinition) in migrationColumns)
                {
                    if (existingColumns.Contains(columnName))
                    {
                        continue;
                    }

                    // Defensive guard: the identifier is only ever a hard-coded constant above,
                    // but rejecting anything outside [a-z0-9_] keeps the interpolation safe if
                    // the array is edited by a future change.
                    if (!IsSafeSqlIdentifier(columnName))
                    {
                        throw new InvalidOperationException($"Unsafe migration column name '{columnName}'.");
                    }

                    using var alterCmd = connection.CreateCommand();
                    alterCmd.Transaction = tx;
                    alterCmd.CommandText = $"ALTER TABLE folder_assignments ADD COLUMN {columnDefinition};";
                    await alterCmd.ExecuteNonQueryAsync();
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
    private static string GetDefaultDbPath()
    {
        // OrnareConstants.AppDataRoot honours ORNARE_DATA_ROOT and stays the single definition
        // of the per-user data root. Recomputing LocalAppData here is what let the CLI and the
        // packaged app drift into two different databases for the same user.
        return Path.Combine(OrnareConstants.AppDataRoot, "data", "ornare.db");
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isInitialized)
        {
            throw new InvalidOperationException("SqliteStorageService must be initialized before use.");
        }
    }

    /// <summary>
    /// True when <paramref name="identifier"/> is a lowercase ASCII identifier. Used only to
    /// guard constant migration column names before they are interpolated into DDL.
    /// </summary>
    private static bool IsSafeSqlIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }

        foreach (char c in identifier)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
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
            catch (SqliteException ex)
            {
                // The column exists on an already-migrated database. Any other failure
                // still surfaces on the next schema-dependent query.
                System.Diagnostics.Trace.TraceWarning($"folder_assignments.render_key migration skipped: {ex.Message}");
            }

            try
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.Transaction = tx;
                alterCmd.CommandText = "ALTER TABLE history ADD COLUMN render_key TEXT;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch (SqliteException ex)
            {
                // Same idempotency contract as folder_assignments above.
                System.Diagnostics.Trace.TraceWarning($"history.render_key migration skipped: {ex.Message}");
            }

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
}
