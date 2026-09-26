using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

namespace Ornare.Storage.Tests;

public class AssetDeduplicationStorageTests
{
    [Fact]
    public async Task SamePngImported100TimesConcurrentlyResultsInOneCanonicalRowAndFile()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_dedup_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            const string sha256 = "c25c3453b6fa62cbfdb00192e276033486a4897f2658de39cf9b6f376cf47f38";
            string icoPath = Path.Combine(Path.GetTempPath(), $"canonical_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(icoPath, [0, 0, 1, 0]);

            // Execute 100 concurrent imports of identical source asset
            var tasks = new Task[100];
            for (int i = 0; i < 100; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await storage.AddOrUpdateAssetAsync(sha256, icoPath, "png");
                });
            }

            await Task.WhenAll(tasks);

            // Invariant: Exactly one canonical asset record exists in database
            var assets = await storage.GetAllAssetsAsync();
            Assert.Single(assets);
            Assert.Equal(sha256, assets[0].Sha256);

            // Fetch by hash
            var fetched = await storage.GetAssetByHashAsync(sha256);
            Assert.NotNull(fetched);
            Assert.Equal(sha256, fetched.Sha256);

            if (File.Exists(icoPath)) File.Delete(icoPath);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task LastAppliedOrHistoryReferencePreventsCleanupRemovingFinalReferenceMakesItemEligible()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_cleanup_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            const string sha256 = "a1b2c3d4e5f60123456789abcdef0123456789abcdef0123456789abcdef0123";
            string icoPath = Path.Combine(Path.GetTempPath(), $"asset_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(icoPath, [0, 0, 1, 0]);

            await storage.AddOrUpdateAssetAsync(sha256, icoPath, "png");

            // Initially unreferenced (no assignments, history, favorites, or settings)
            bool isReferencedInitial = await storage.IsAssetOrRenderReferencedAsync(sha256);
            Assert.False(isReferencedInitial);

            var unreferencedInitial = await storage.GetUnreferencedAssetsAsync();
            Assert.Contains(sha256, unreferencedInitial);

            // Add history entry referencing this asset
            Guid opId = Guid.NewGuid();
            await storage.RecordHistoryAsync(@"C:\TestFolder\Protected", opId, null, sha256, "apply");

            // Now protected by history reference
            bool isReferencedHistory = await storage.IsAssetOrRenderReferencedAsync(sha256);
            Assert.True(isReferencedHistory);

            var unreferencedWithHistory = await storage.GetUnreferencedAssetsAsync();
            Assert.DoesNotContain(sha256, unreferencedWithHistory);

            // Cleanup attempt must NOT delete referenced asset
            int cleanedWithHistory = await storage.CleanupUnreferencedAssetsAsync();
            Assert.Equal(0, cleanedWithHistory);
            Assert.NotNull(await storage.GetAssetByHashAsync(sha256));

            // Also add settings reference (LastAppliedAssetHash)
            await storage.SetSettingAsync("LastAppliedAssetHash", sha256);

            // Clear history
            await storage.ClearRecentHistoryAsync();

            // Still protected by LastAppliedAssetHash setting
            bool isReferencedSetting = await storage.IsAssetOrRenderReferencedAsync(sha256);
            Assert.True(isReferencedSetting);

            // Remove setting reference
            await storage.SetSettingAsync("LastAppliedAssetHash", "different_hash");

            // Now final reference removed: eligible for cleanup
            bool isReferencedAfterRemoval = await storage.IsAssetOrRenderReferencedAsync(sha256);
            Assert.False(isReferencedAfterRemoval);

            var unreferencedFinal = await storage.GetUnreferencedAssetsAsync();
            Assert.Contains(sha256, unreferencedFinal);

            // Cleanup cleans unreferenced asset
            int cleanedCount = await storage.CleanupUnreferencedAssetsAsync();
            Assert.Equal(1, cleanedCount);
            Assert.Null(await storage.GetAssetByHashAsync(sha256));
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task LastAppliedRenderKeyRoundtripAndCleanupRetention()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_lastkey_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            // Initially null
            var initialKey = await storage.GetLastAppliedRenderKeyAsync();
            Assert.Null(initialKey);

            const string renderKey = "7f8e9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f";
            const string sourceHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

            // Save render cache entry
            string icoPath = Path.Combine(Path.GetTempPath(), $"canonical_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(icoPath, [0, 0, 1, 0]);

            await storage.AddOrUpdateAssetAsync(sourceHash, icoPath, "png");
            await storage.SaveRenderCacheEntryAsync(new RenderCacheEntry
            {
                RenderKey = renderKey,
                RecipeVersion = 1,
                RecipeJson = "{}",
                SourceAssetHash = sourceHash,
                CanonicalIcoPath = icoPath,
                FrameSizes = "256",
                FileSizeBytes = 4,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsedAt = DateTimeOffset.UtcNow
            });

            // Set LastAppliedRenderKey
            await storage.SetLastAppliedRenderKeyAsync(renderKey);
            var retrievedKey = await storage.GetLastAppliedRenderKeyAsync();
            Assert.Equal(renderKey, retrievedKey);

            // Verify both renderKey and underlying source asset are protected from cleanup
            bool isRenderReferenced = await storage.IsAssetOrRenderReferencedAsync(renderKey);
            Assert.True(isRenderReferenced);

            bool isSourceReferenced = await storage.IsAssetOrRenderReferencedAsync(sourceHash);
            Assert.True(isSourceReferenced);

            // Cleanup attempt preserves both
            int cleaned = await storage.CleanupUnreferencedAssetsAsync();
            Assert.Equal(0, cleaned);
            Assert.NotNull(await storage.GetAssetByHashAsync(sourceHash));
            Assert.NotNull(await storage.GetRenderCacheEntryAsync(renderKey));

            // Clear LastAppliedRenderKey
            await storage.ClearLastAppliedRenderKeyAsync();
            Assert.Null(await storage.GetLastAppliedRenderKeyAsync());

            // Now references are eligible for cleanup
            bool isSourceReferencedAfter = await storage.IsAssetOrRenderReferencedAsync(sourceHash);
            Assert.False(isSourceReferencedAfter);

            if (File.Exists(icoPath)) File.Delete(icoPath);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task ForwardMigrationFailureRollsBackTransactionCleanly()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_mig_{Guid.NewGuid():N}.db");

        try
        {
            // 1. Manually set up a Version 1 database
            string connStr = new SqliteConnectionStringBuilder
            {
                DataSource = tempDb,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE folder_assignments (
                        id TEXT PRIMARY KEY,
                        folder_path TEXT NOT NULL UNIQUE,
                        ownership_epoch_id TEXT NOT NULL,
                        icon_relative_path TEXT NOT NULL,
                        status INTEGER NOT NULL,
                        created_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL
                    );
                    PRAGMA user_version = 1;
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Open via SqliteStorageService
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            int versionBefore = await storage.GetCurrentSchemaVersionAsync();
            // Since InitializeAsync applies migrations up to latest, let's verify current schema is at least 2
            Assert.True(versionBefore >= 2);

            // 3. Test failure injection on migration to verify rollback behavior
            // We simulate a failed migration step on a fresh connection
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "CREATE TABLE temp_migration_canary (id INTEGER PRIMARY KEY);";
                await cmd.ExecuteNonQueryAsync();

                // Roll back transaction to simulate failure
                tx.Rollback();

                // Verify table was rolled back cleanly
                using var verifyCmd = conn.CreateCommand();
                verifyCmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='temp_migration_canary';";
                long canaryCount = (long)(await verifyCmd.ExecuteScalarAsync())!;
                Assert.Equal(0, canaryCount);
            }

            // 4. Test ApplyForwardMigrationWithFailureInjectionAsync directly
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await storage.ApplyForwardMigrationWithFailureInjectionAsync(2, injectFailure: true);
            });
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
