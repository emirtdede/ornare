namespace Ornare.Storage.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

public sealed class PortableFolderStorageTests
{
    private static string CreateTempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"ornare_portable_test_{Guid.NewGuid():N}.db");

    private static void DeleteTempDb(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task ForwardMigrationV6AddsIdentityColumnsAndReachesVersion6()
    {
        string tempDb = CreateTempDbPath();
        try
        {
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            int version = await storage.GetCurrentSchemaVersionAsync();
            Assert.Equal(6, version);

            // Verify folder_assignments has volume_serial_number and file_id_blob columns
            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = tempDb,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(folder_assignments);";
            using var reader = await cmd.ExecuteReaderAsync();

            bool hasVolumeSerial = false;
            bool hasFileIdBlob = false;

            while (await reader.ReadAsync())
            {
                string colName = reader.GetString(1);
                if (colName == "volume_serial_number")
                {
                    hasVolumeSerial = true;
                }
                else if (colName == "file_id_blob")
                {
                    hasFileIdBlob = true;
                }
            }

            Assert.True(hasVolumeSerial, "folder_assignments must have volume_serial_number column.");
            Assert.True(hasFileIdBlob, "folder_assignments must have file_id_blob column.");
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task ForwardMigrationV5ToV6PreservesExistingAssignmentsAndMigratesHex()
    {
        string tempDb = CreateTempDbPath();
        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = tempDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        try
        {
            // Seed database up to V5 with legacy hex columns
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();

                using var v1Cmd = conn.CreateCommand();
                v1Cmd.Transaction = tx;
                v1Cmd.CommandText = @"
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
                        updated_at TEXT NOT NULL
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
                        new_asset_hash TEXT NOT NULL,
                        operation_type TEXT NOT NULL,
                        timestamp TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS settings (
                        key TEXT PRIMARY KEY,
                        value TEXT NOT NULL
                    );

                    PRAGMA user_version = 5;
                ";
                await v1Cmd.ExecuteNonQueryAsync();

                // Insert a test assignment in V5 with hex identity
                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO folder_assignments (
                        id, folder_path, ownership_epoch_id, icon_relative_path, status,
                        created_at, updated_at, volume_serial, file_id
                    ) VALUES (
                        'test-uuid-1', 'C:\TestFolder', '00000000-0000-0000-0000-000000000001',
                        '.ornare\folder.ico,0', 0, '2026-09-26T10:00:00Z', '2026-09-26T10:00:00Z',
                        '3C4D5E6F', '0001000000002A3B'
                    );
                ";
                await insertCmd.ExecuteNonQueryAsync();

                tx.Commit();
            }

            // Initialize storage service to perform V5 -> V6 migration
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            int version = await storage.GetCurrentSchemaVersionAsync();
            Assert.Equal(6, version);

            // Verify existing assignment is retained and hex columns migrated to typed numbers
            var assignment = await storage.GetAssignmentByPathAsync(@"C:\TestFolder");
            Assert.NotNull(assignment);
            Assert.Equal(@"C:\TestFolder", assignment.FolderPath);
            Assert.Equal(0x3C4D5E6Fu, assignment.VolumeSerialNumber);
            Assert.Equal(0x0001000000002A3Bul, assignment.FileId64);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task UpdateAssignmentIdentityAndQueryByIdentityRoundtrip()
    {
        string tempDb = CreateTempDbPath();
        try
        {
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            string folderPath = @"C:\Users\emir\Projects\AppOne";
            Guid epochId = Guid.NewGuid();
            Guid assignmentId = Guid.NewGuid();

            var priorSemantics = new FolderIconSemantics
            {
                IconResource = "shell32.dll,4",
                AttributesIntroduced = false
            };

            var baseline = new FolderBaseline
            {
                FolderAssignmentId = assignmentId,
                OwnershipEpochId = epochId,
                PriorSemantics = priorSemantics,
                CapturedAt = DateTimeOffset.UtcNow
            };

            var semantics = new FolderIconSemantics
            {
                IconResource = @".ornare\folder.ico,0",
                AttributesIntroduced = true
            };

            await storage.SaveFolderAssignmentAsync(folderPath, epochId, semantics, baseline);

            // Update NTFS identity
            uint volumeSerial = 0x3C4D5E6F;
            ulong fileId = 0x0001000000002A3B;

            await storage.UpdateAssignmentIdentityAsync(folderPath, volumeSerial, fileId);

            // Retrieve by path
            var retrievedByPath = await storage.GetAssignmentByPathAsync(folderPath);
            Assert.NotNull(retrievedByPath);
            Assert.Equal(volumeSerial, retrievedByPath.VolumeSerialNumber);
            Assert.Equal(fileId, retrievedByPath.FileId64);
            Assert.NotNull(retrievedByPath.FileIdBlob);
            Assert.Equal(8, retrievedByPath.FileIdBlob.Length);

            // Retrieve by identity
            var retrievedByIdentity = await storage.GetAssignmentByIdentityAsync(volumeSerial, fileId);
            Assert.NotNull(retrievedByIdentity);
            Assert.Equal(assignmentId.ToString(), retrievedByIdentity.Id);
            Assert.Equal(folderPath, retrievedByIdentity.FolderPath);
            Assert.Equal(volumeSerial, retrievedByIdentity.VolumeSerialNumber);
            Assert.Equal(fileId, retrievedByIdentity.FileId64);

            // Query with non-matching serial or fileId should return null
            var notFound1 = await storage.GetAssignmentByIdentityAsync(0x99999999, fileId);
            Assert.Null(notFound1);

            var notFound2 = await storage.GetAssignmentByIdentityAsync(volumeSerial, 0x9999999999999999);
            Assert.Null(notFound2);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task UpdateAssignmentPathUpdatesPathAndPreservesIdentity()
    {
        string tempDb = CreateTempDbPath();
        try
        {
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            string oldPath = @"C:\OldLocation\Docs";
            string newPath = @"C:\NewLocation\RelocatedDocs";
            Guid epochId = Guid.NewGuid();
            Guid assignmentId = Guid.NewGuid();

            var baseline = new FolderBaseline
            {
                FolderAssignmentId = assignmentId,
                OwnershipEpochId = epochId,
                PriorSemantics = new FolderIconSemantics { IconResource = "shell32.dll,4" },
                CapturedAt = DateTimeOffset.UtcNow
            };

            var semantics = new FolderIconSemantics
            {
                IconResource = @".ornare\folder.ico,0",
                AttributesIntroduced = true
            };

            await storage.SaveFolderAssignmentAsync(oldPath, epochId, semantics, baseline);

            uint volumeSerial = 0xA1B2C3D4;
            ulong fileId = 0x1122334455667788;
            await storage.UpdateAssignmentIdentityAsync(oldPath, volumeSerial, fileId);

            // Relocate path
            await storage.UpdateAssignmentPathAsync(oldPath, newPath);

            // Old path should no longer exist
            var oldLookup = await storage.GetAssignmentByPathAsync(oldPath);
            Assert.Null(oldLookup);

            // New path should return the assignment with identity preserved
            var newLookup = await storage.GetAssignmentByPathAsync(newPath);
            Assert.NotNull(newLookup);
            Assert.Equal(assignmentId.ToString(), newLookup.Id);
            Assert.Equal(newPath, newLookup.FolderPath);
            Assert.Equal(volumeSerial, newLookup.VolumeSerialNumber);
            Assert.Equal(fileId, newLookup.FileId64);

            // Querying by identity should return the updated path
            var identityLookup = await storage.GetAssignmentByIdentityAsync(volumeSerial, fileId);
            Assert.NotNull(identityLookup);
            Assert.Equal(newPath, identityLookup.FolderPath);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }
}
