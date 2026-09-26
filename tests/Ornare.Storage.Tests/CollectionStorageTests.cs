using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

namespace Ornare.Storage.Tests;

public sealed class CollectionStorageTests
{
    private static string CreateTempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"ornare_coll_test_{Guid.NewGuid():N}.db");

    private static void DeleteTempDb(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task ForwardMigrationV4PreservesExistingDataAndReachesVersion4()
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
            const string existingSha = "bbbb1111222233334444555566667777888899990000aaaabbbbccccddddeeee";
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
                        updated_at TEXT NOT NULL,
                        render_key TEXT
                    );

                    CREATE TABLE assets (
                        id TEXT PRIMARY KEY,
                        sha256 TEXT UNIQUE NOT NULL,
                        canonical_ico_path TEXT NOT NULL,
                        source_format TEXT NOT NULL,
                        created_at TEXT NOT NULL,
                        last_used_at TEXT NOT NULL
                    );

                    CREATE TABLE favorites (
                        asset_id TEXT PRIMARY KEY,
                        created_at TEXT NOT NULL
                    );

                    CREATE TABLE history (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        folder_path TEXT NOT NULL,
                        operation_id TEXT NOT NULL,
                        previous_asset_hash TEXT,
                        new_asset_hash TEXT NOT NULL,
                        operation_type TEXT NOT NULL,
                        timestamp TEXT NOT NULL,
                        render_key TEXT
                    );

                    CREATE TABLE render_cache (
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

                    CREATE TABLE tags (
                        id TEXT PRIMARY KEY,
                        display_name TEXT NOT NULL,
                        normalized_name TEXT NOT NULL UNIQUE,
                        created_utc INTEGER NOT NULL
                    );

                    CREATE TABLE asset_tags (
                        asset_hash TEXT NOT NULL REFERENCES assets(sha256) ON DELETE CASCADE,
                        tag_id TEXT NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
                        created_utc INTEGER NOT NULL,
                        PRIMARY KEY (asset_hash, tag_id)
                    );

                    INSERT INTO assets (id, sha256, canonical_ico_path, source_format, created_at, last_used_at)
                    VALUES ('asset-1', '" + existingSha + @"', 'C:\\dummy.ico', 'png', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');

                    INSERT INTO tags (id, display_name, normalized_name, created_utc)
                    VALUES ('tag-1', 'Initial Tag', 'INITIAL TAG', 1700000000);

                    INSERT INTO asset_tags (asset_hash, tag_id, created_utc)
                    VALUES ('" + existingSha + @"', 'tag-1', 1700000000);

                    PRAGMA user_version = 3;
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // Run migration by initializing storage service
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            int version = await storage.GetCurrentSchemaVersionAsync();
            Assert.Equal(4, version);

            // Existing assets and tags preserved
            var asset = await storage.GetAssetByHashAsync(existingSha);
            Assert.NotNull(asset);
            Assert.Equal(existingSha, asset.Sha256);

            var tags = await storage.GetTagsForAssetAsync(existingSha);
            Assert.Single(tags);
            Assert.Equal("Initial Tag", tags[0].DisplayName);

            // Collections table functional
            var coll = await storage.CreateCollectionAsync("Work Projects");
            Assert.Equal("Work Projects", coll.DisplayName);
            Assert.Equal(1024L, coll.SortKey);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task InjectedMigrationV4FailureRollsBackCleanlyToVersion3()
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
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE assets (
                        id TEXT PRIMARY KEY,
                        sha256 TEXT UNIQUE NOT NULL,
                        canonical_ico_path TEXT NOT NULL,
                        source_format TEXT NOT NULL,
                        created_at TEXT NOT NULL,
                        last_used_at TEXT NOT NULL
                    );

                    CREATE TABLE tags (
                        id TEXT PRIMARY KEY,
                        display_name TEXT NOT NULL,
                        normalized_name TEXT NOT NULL UNIQUE,
                        created_utc INTEGER NOT NULL
                    );

                    PRAGMA user_version = 3;
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            using var storage = new SqliteStorageService();
            // Test failure injection directly on V4
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    // Call the failure injection routine
                    await storage.ApplyForwardMigrationWithFailureInjectionAsync(4, injectFailure: true);
                });

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "PRAGMA user_version;";
                int ver = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(3, ver);

                checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='collections';";
                long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(0, count);
            }
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task NameNormalizationRulesAndDuplicateRejection()
    {
        string tempDb = CreateTempDbPath();
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            // Rejects null / whitespace
            await Assert.ThrowsAsync<ArgumentException>(() => storage.CreateCollectionAsync("   "));

            // Rejects control characters
            await Assert.ThrowsAsync<ArgumentException>(() => storage.CreateCollectionAsync("Test\u0000Collection"));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.CreateCollectionAsync("Line\nBreak"));

            // Rejects names longer than 80 scalars
            string longName = new string('A', 81);
            await Assert.ThrowsAsync<ArgumentException>(() => storage.CreateCollectionAsync(longName));

            // Accepts 80 scalars
            string valid80 = new string('B', 80);
            var coll80 = await storage.CreateCollectionAsync(valid80);
            Assert.Equal(valid80, coll80.DisplayName);

            // Case-insensitive duplicate rejection
            var coll1 = await storage.CreateCollectionAsync("Development Assets");
            Assert.Equal("Development Assets", coll1.DisplayName);
            Assert.Equal("DEVELOPMENT ASSETS", coll1.NormalizedName);

            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.CreateCollectionAsync("development assets"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.CreateCollectionAsync("  DEVELOPMENT ASSETS  "));

            // Rename validation
            await Assert.ThrowsAsync<ArgumentException>(() => storage.RenameCollectionAsync(coll1.Id, "   "));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.RenameCollectionAsync(coll1.Id, valid80)); // conflicts with coll80

            bool renamed = await storage.RenameCollectionAsync(coll1.Id, "Dev Assets");
            Assert.True(renamed);

            var updated = await storage.GetCollectionByIdAsync(coll1.Id);
            Assert.NotNull(updated);
            Assert.Equal("Dev Assets", updated.DisplayName);
            Assert.Equal("DEV ASSETS", updated.NormalizedName);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task TotalCollectionsHardCapOf500()
    {
        string tempDb = CreateTempDbPath();
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            // Fast insert 500 collections via SQL to verify cap check in CreateCollectionAsync
            string connStr = new SqliteConnectionStringBuilder
            {
                DataSource = tempDb,
                Mode = SqliteOpenMode.ReadWrite,
                ForeignKeys = true
            }.ToString();

            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();
                for (int i = 1; i <= 500; i++)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO collections (id, display_name, normalized_name, sort_key, created_utc, updated_utc)
                        VALUES (@id, @name, @norm, @sortKey, 1700000000, 1700000000);
                    ";
                    cmd.Parameters.AddWithValue("@id", $"c_{i:D4}");
                    cmd.Parameters.AddWithValue("@name", $"Collection {i}");
                    cmd.Parameters.AddWithValue("@norm", $"COLLECTION {i}");
                    cmd.Parameters.AddWithValue("@sortKey", (long)i * 1024L);
                    await cmd.ExecuteNonQueryAsync();
                }
                tx.Commit();
            }

            // Attempting to create the 501st collection must throw InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.CreateCollectionAsync("Overflow Collection"));
            Assert.Contains("500", ex.Message);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task MultiCollectionAssetMembershipAndCascadeIntegrity()
    {
        string tempDb = CreateTempDbPath();
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            const string shaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string shaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            await storage.AddOrUpdateAssetAsync(shaA, "C:\\test\\a.ico", "png");
            await storage.AddOrUpdateAssetAsync(shaB, "C:\\test\\b.ico", "svg");

            var col1 = await storage.CreateCollectionAsync("Favorites Archive");
            var col2 = await storage.CreateCollectionAsync("Dark Themes");

            // Attach shaA to both collections
            Assert.True(await storage.AddAssetToCollectionAsync(col1.Id, shaA));
            Assert.True(await storage.AddAssetToCollectionAsync(col2.Id, shaA));

            // Attach shaB to col2 only
            Assert.True(await storage.AddAssetToCollectionAsync(col2.Id, shaB));

            // Duplicate add is idempotent
            Assert.True(await storage.AddAssetToCollectionAsync(col1.Id, shaA));

            // Check memberships
            var col1Assets = await storage.GetAssetsInCollectionAsync(col1.Id);
            Assert.Single(col1Assets);
            Assert.Equal(shaA, col1Assets[0].Sha256);

            var col2Assets = await storage.GetAssetsInCollectionAsync(col2.Id);
            Assert.Equal(2, col2Assets.Count);

            var assetAColls = await storage.GetCollectionsForAssetAsync(shaA);
            Assert.Equal(2, assetAColls.Count);

            // Remove shaA from col1
            Assert.True(await storage.RemoveAssetFromCollectionAsync(col1.Id, shaA));
            col1Assets = await storage.GetAssetsInCollectionAsync(col1.Id);
            Assert.Empty(col1Assets);

            // shaA still in col2
            col2Assets = await storage.GetAssetsInCollectionAsync(col2.Id);
            Assert.Equal(2, col2Assets.Count);

            // Delete col2 -> junction rows deleted, canonical assets intact!
            Assert.True(await storage.DeleteCollectionAsync(col2.Id));

            var assetA = await storage.GetAssetByHashAsync(shaA);
            Assert.NotNull(assetA);
            var assetB = await storage.GetAssetByHashAsync(shaB);
            Assert.NotNull(assetB);

            // Asset memberships are empty now
            assetAColls = await storage.GetCollectionsForAssetAsync(shaA);
            Assert.Empty(assetAColls);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task SparseMidpointOrderingPrependAppendMiddleAndRebalance()
    {
        string tempDb = CreateTempDbPath();
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            // 1. Initial creation spaced by 1024
            var c1 = await storage.CreateCollectionAsync("Alpha");
            var c2 = await storage.CreateCollectionAsync("Beta");
            var c3 = await storage.CreateCollectionAsync("Gamma");

            Assert.Equal(1024L, c1.SortKey);
            Assert.Equal(2048L, c2.SortKey);
            Assert.Equal(3072L, c3.SortKey);

            // 2. Prepend: Reorder c3 before c1 (targetNeighbor = c1.Id, insertBefore = true)
            // c1 is first (1024), gap = 1024 - 0 = 1024. Midpoint = 512.
            Assert.True(await storage.ReorderCollectionAsync(c3.Id, c1.Id, insertBefore: true));
            var updatedC3 = await storage.GetCollectionByIdAsync(c3.Id);
            Assert.NotNull(updatedC3);
            Assert.Equal(512L, updatedC3.SortKey);

            // 3. Insert between: Reorder c3 between c1 and c2 (after c1)
            // c1 = 1024, c2 = 2048. Gap = 1024. Midpoint = 1024 + 512 = 1536.
            Assert.True(await storage.ReorderCollectionAsync(c3.Id, c1.Id, insertBefore: false));
            updatedC3 = await storage.GetCollectionByIdAsync(c3.Id);
            Assert.NotNull(updatedC3);
            Assert.Equal(1536L, updatedC3.SortKey);

            // 4. Repeated insertions collapsing gap until gap < 2 triggers local rebalance
            var all = await storage.GetAllCollectionsAsync();
            Assert.Equal(3, all.Count);
            // Current keys: c1=1024, c3=1536, c2=2048

            // Let's create two collections with gap=1 manually to test automatic rebalance
            var cA = await storage.CreateCollectionAsync("Key10");
            var cB = await storage.CreateCollectionAsync("Key11");

            string connStr = new SqliteConnectionStringBuilder { DataSource = tempDb, Mode = SqliteOpenMode.ReadWrite }.ToString();
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE collections SET sort_key = 10 WHERE id = @idA; UPDATE collections SET sort_key = 11 WHERE id = @idB;";
                cmd.Parameters.AddWithValue("@idA", cA.Id);
                cmd.Parameters.AddWithValue("@idB", cB.Id);
                await cmd.ExecuteNonQueryAsync();
            }

            // Gap between cA (10) and cB (11) is 1 (< 2).
            // Reordering c3 between cA and cB (before cB) must detect gap < 2, rebalance, and place cleanly!
            Assert.True(await storage.ReorderCollectionAsync(c3.Id, cB.Id, insertBefore: true));

            var c3After = await storage.GetCollectionByIdAsync(c3.Id);
            Assert.NotNull(c3After);

            // Verify strict ordering of all collections
            var reorderedList = await storage.GetAllCollectionsAsync();
            for (int i = 0; i < reorderedList.Count - 1; i++)
            {
                Assert.True(reorderedList[i].SortKey < reorderedList[i + 1].SortKey,
                    $"Expected {reorderedList[i].DisplayName} ({reorderedList[i].SortKey}) < {reorderedList[i + 1].DisplayName} ({reorderedList[i + 1].SortKey})");
            }
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task OverflowGuardRebalancesBeforeAppend()
    {
        string tempDb = CreateTempDbPath();
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            var c1 = await storage.CreateCollectionAsync("First");

            // Set sort_key near Int64.MaxValue
            string connStr = new SqliteConnectionStringBuilder { DataSource = tempDb, Mode = SqliteOpenMode.ReadWrite }.ToString();
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE collections SET sort_key = @maxVal WHERE id = @id;";
                cmd.Parameters.AddWithValue("@maxVal", long.MaxValue - 1000L); // above threshold
                cmd.Parameters.AddWithValue("@id", c1.Id);
                await cmd.ExecuteNonQueryAsync();
            }

            // Creating next collection must trigger rebalance, avoiding long overflow
            var c2 = await storage.CreateCollectionAsync("Second");
            Assert.NotNull(c2);

            var all = await storage.GetAllCollectionsAsync();
            Assert.Equal(2, all.Count);
            Assert.Equal(1024L, all[0].SortKey);
            Assert.Equal(2048L, all[1].SortKey);
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }

    [Fact]
    public async Task TenThousandAssetMembershipQueryBenchmarkUnder50Ms()
    {
        string tempDb = CreateTempDbPath();
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            var coll = await storage.CreateCollectionAsync("Huge Benchmark Collection");

            // Batch insert 10,000 assets and junction records
            string connStr = new SqliteConnectionStringBuilder { DataSource = tempDb, Mode = SqliteOpenMode.ReadWrite }.ToString();
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();
                for (int i = 0; i < 10000; i++)
                {
                    string sha = $"{i:D64}";
                    using var assetCmd = conn.CreateCommand();
                    assetCmd.Transaction = tx;
                    assetCmd.CommandText = @"
                        INSERT INTO assets (id, sha256, canonical_ico_path, source_format, created_at, last_used_at)
                        VALUES (@id, @sha, 'C:\\dummy.ico', 'ico', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
                    ";
                    assetCmd.Parameters.AddWithValue("@id", $"ast_{i}");
                    assetCmd.Parameters.AddWithValue("@sha", sha);
                    await assetCmd.ExecuteNonQueryAsync();

                    using var juncCmd = conn.CreateCommand();
                    juncCmd.Transaction = tx;
                    juncCmd.CommandText = @"
                        INSERT INTO collection_assets (collection_id, asset_hash, created_utc)
                        VALUES (@cid, @sha, @now);
                    ";
                    juncCmd.Parameters.AddWithValue("@cid", coll.Id);
                    juncCmd.Parameters.AddWithValue("@sha", sha);
                    juncCmd.Parameters.AddWithValue("@now", 1700000000 + i);
                    await juncCmd.ExecuteNonQueryAsync();
                }
                tx.Commit();
            }

            // JIT warm-up
            await storage.GetAssetsInCollectionAsync(coll.Id, limit: 1, offset: 0);

            // Benchmark paged query: fetching first page of 100 assets
            var sw = Stopwatch.StartNew();
            var page = await storage.GetAssetsInCollectionAsync(coll.Id, limit: 100, offset: 0);
            sw.Stop();

            Assert.Equal(100, page.Count);
            Assert.True(sw.ElapsedMilliseconds < 50, $"Indexed query took {sw.ElapsedMilliseconds} ms, expected < 50 ms");
        }
        finally
        {
            DeleteTempDb(tempDb);
        }
    }
}
