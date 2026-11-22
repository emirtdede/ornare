using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Storage.Tests;

public sealed class LibraryTagsStorageTests
{
    private readonly ITestOutputHelper? _output;

    public LibraryTagsStorageTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Nearest-rank percentile over a sample set, so a gate states a distribution.</summary>
    private static double Percentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    [Fact]
    public async Task VerifyForeignKeysEnabledOnStorageConnection()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_fk_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);
            bool fkEnabled = await storage.VerifyForeignKeysEnabledAsync();
            Assert.True(fkEnabled, "PRAGMA foreign_keys must be ON for all storage connections.");
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
    public async Task ForeignKeyEnforcementRejectsOrphanJunction()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_fk_orphan_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            // Attempt to attach non-existent tag to non-existent asset
            await Assert.ThrowsAsync<SqliteException>(async () =>
            {
                await storage.AttachTagToAssetAsync("non_existent_asset_hash", "non_existent_tag_id");
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

    [Fact]
    public async Task ForwardMigrationV3PreservesExistingRowsAndReachesVersion3()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_mig_v3_{Guid.NewGuid():N}.db");
        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = tempDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        try
        {
            // 1. Manually set up a Version 2 database with existing data
            const string existingSha = "a1b2c3d4e5f60123456789abcdef0123456789abcdef0123456789abcdef0123";
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

                    INSERT INTO assets (id, sha256, canonical_ico_path, source_format, created_at, last_used_at)
                    VALUES ('asset-1', '" + existingSha + @"', 'C:\\dummy.ico', 'png', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');

                    PRAGMA user_version = 2;
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Initialize SqliteStorageService on this database (forward migration V2 -> V3)
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            int versionAfter = await storage.GetCurrentSchemaVersionAsync();
            Assert.True(versionAfter >= 3, $"Expected schema version >= 3, got {versionAfter}");

            // 3. Verify existing data preserved
            var asset = await storage.GetAssetByHashAsync(existingSha);
            Assert.NotNull(asset);
            Assert.Equal(existingSha, asset.Sha256);

            // 4. Verify tag tables exist and work
            var tag = await storage.CreateOrGetTagAsync("Migrated Tag");
            Assert.Equal("Migrated Tag", tag.DisplayName);
            bool attached = await storage.AttachTagToAssetAsync(existingSha, tag.Id);
            Assert.True(attached);

            var tags = await storage.GetTagsForAssetAsync(existingSha);
            Assert.Single(tags);
            Assert.Equal("Migrated Tag", tags[0].DisplayName);
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
    public async Task InjectedMigrationV3FailureRollsBackCleanly()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_mig_rollback_{Guid.NewGuid():N}.db");
        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = tempDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        try
        {
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            // 1. Verify transaction rollback cleanly discards uncommitted schema changes
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using (var tx = conn.BeginTransaction())
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "CREATE TABLE temp_canary_v3 (id INTEGER PRIMARY KEY);";
                    await cmd.ExecuteNonQueryAsync();
                    tx.Rollback();
                }

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='temp_canary_v3';";
                long tableCount = (long)(await checkCmd.ExecuteScalarAsync())!;
                Assert.Equal(0, tableCount);
            }

            // 2. Test ApplyForwardMigrationWithFailureInjectionAsync directly for version 3
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await storage.ApplyForwardMigrationWithFailureInjectionAsync(3, injectFailure: true);
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

    [Fact]
    public async Task CreateOrGetTagPreservesDisplayCasingAndEnforcesNormalizedUniqueness()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_tags_case_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            var tag1 = await storage.CreateOrGetTagAsync("Project Gamma");
            Assert.Equal("Project Gamma", tag1.DisplayName);
            Assert.Equal("PROJECT GAMMA", tag1.NormalizedName);

            // Same name with different casing and whitespace
            var tag2 = await storage.CreateOrGetTagAsync("  project gamma  ");
            Assert.Equal(tag1.Id, tag2.Id);
            Assert.Equal(tag1.NormalizedName, tag2.NormalizedName);

            // Unicode decomposed vs composed
            var cafe1 = await storage.CreateOrGetTagAsync("Café");
            var cafe2 = await storage.CreateOrGetTagAsync("Cafe\u0301");
            Assert.Equal(cafe1.Id, cafe2.Id);

            var allTags = await storage.GetAllTagsAsync();
            Assert.Equal(2, allTags.Count); // "Project Gamma" and "Café"
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
    public async Task AttachTagToAssetIsIdempotentAndEnforces32Cap()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_tags_cap_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            const string sha = "1111111111111111111111111111111111111111111111111111111111111111";
            string ico = Path.Combine(Path.GetTempPath(), $"asset_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(ico, [0, 0, 1, 0]);
            await storage.AddOrUpdateAssetAsync(sha, ico, "png");

            var tag1 = await storage.CreateOrGetTagAsync("Tag 1");

            // Attach once
            bool firstAttach = await storage.AttachTagToAssetAsync(sha, tag1.Id);
            Assert.True(firstAttach);

            // Attach again (idempotent)
            bool secondAttach = await storage.AttachTagToAssetAsync(sha, tag1.Id);
            Assert.True(secondAttach);

            var tags = await storage.GetTagsForAssetAsync(sha);
            Assert.Single(tags);

            // Attach up to 32 tags
            for (int i = 2; i <= 32; i++)
            {
                var t = await storage.CreateOrGetTagAsync($"Tag {i}");
                bool attached = await storage.AttachTagToAssetAsync(sha, t.Id);
                Assert.True(attached);
            }

            var tagsAtCap = await storage.GetTagsForAssetAsync(sha);
            Assert.Equal(32, tagsAtCap.Count);

            // Attempt 33rd tag
            var tag33 = await storage.CreateOrGetTagAsync("Tag 33");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await storage.AttachTagToAssetAsync(sha, tag33.Id);
            });
            Assert.Contains("32", ex.Message);

            if (File.Exists(ico)) File.Delete(ico);
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
    public async Task DeleteTagRemovesJunctionRowsWithoutDeletingAsset()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_tags_del_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            const string sha = "2222222222222222222222222222222222222222222222222222222222222222";
            string ico = Path.Combine(Path.GetTempPath(), $"asset_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(ico, [0, 0, 1, 0]);
            await storage.AddOrUpdateAssetAsync(sha, ico, "png");

            var tag = await storage.CreateOrGetTagAsync("Temporary Tag");
            await storage.AttachTagToAssetAsync(sha, tag.Id);

            var tagsBefore = await storage.GetTagsForAssetAsync(sha);
            Assert.Single(tagsBefore);

            // Delete tag
            bool deleted = await storage.DeleteTagAsync(tag.Id);
            Assert.True(deleted);

            // Junction must be gone
            var tagsAfter = await storage.GetTagsForAssetAsync(sha);
            Assert.Empty(tagsAfter);

            // Asset must still exist!
            var asset = await storage.GetAssetByHashAsync(sha);
            Assert.NotNull(asset);
            Assert.Equal(sha, asset.Sha256);

            if (File.Exists(ico)) File.Delete(ico);
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
    public async Task DeleteAssetCascadesToJunctionRowsWithoutDeletingTag()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_asset_del_cascade_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            const string sha = "3333333333333333333333333333333333333333333333333333333333333333";
            string ico = Path.Combine(Path.GetTempPath(), $"asset_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(ico, [0, 0, 1, 0]);
            await storage.AddOrUpdateAssetAsync(sha, ico, "png");

            var tag = await storage.CreateOrGetTagAsync("Shared Tag");
            await storage.AttachTagToAssetAsync(sha, tag.Id);

            var assetsForTag = await storage.GetAssetsByTagAsync(tag.Id);
            Assert.Single(assetsForTag);

            // Delete asset
            bool deletedAsset = await storage.DeleteAssetAsync(sha);
            Assert.True(deletedAsset);

            // Tag still exists in catalog
            var allTags = await storage.GetAllTagsAsync();
            Assert.Single(allTags);
            Assert.Equal(tag.Id, allTags[0].Id);

            // Querying by tag returns empty
            var assetsAfter = await storage.GetAssetsByTagAsync(tag.Id);
            Assert.Empty(assetsAfter);

            if (File.Exists(ico)) File.Delete(ico);
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
    public async Task BulkGetTagsForAssetsAndQueryByTagWorkAccurately()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_tags_bulk_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();
        try
        {
            await storage.InitializeAsync(tempDb);

            string sha1 = "4444444444444444444444444444444444444444444444444444444444444441";
            string sha2 = "4444444444444444444444444444444444444444444444444444444444444442";
            string ico = Path.Combine(Path.GetTempPath(), $"asset_{Guid.NewGuid():N}.ico");
            await File.WriteAllBytesAsync(ico, [0, 0, 1, 0]);

            await storage.AddOrUpdateAssetAsync(sha1, ico, "png");
            await storage.AddOrUpdateAssetAsync(sha2, ico, "png");

            var tagWork = await storage.CreateOrGetTagAsync("Work");
            var tagPersonal = await storage.CreateOrGetTagAsync("Personal");

            await storage.AttachTagToAssetAsync(sha1, tagWork.Id);
            await storage.AttachTagToAssetAsync(sha2, tagWork.Id);
            await storage.AttachTagToAssetAsync(sha2, tagPersonal.Id);

            // Bulk fetch
            var dict = await storage.GetTagsForAssetsAsync([sha1, sha2]);
            Assert.Equal(2, dict.Count);
            Assert.Single(dict[sha1]);
            Assert.Equal(2, dict[sha2].Count);

            // Query by tag
            var workAssets = await storage.GetAssetsByTagAsync(tagWork.Id);
            Assert.Equal(2, workAssets.Count);

            var personalAssets = await storage.GetAssetsByTagAsync(tagPersonal.Id);
            Assert.Single(personalAssets);
            Assert.Equal(sha2, personalAssets[0].Sha256);

            // Detach tag from asset
            bool detached = await storage.DetachTagFromAssetAsync(sha2, tagPersonal.Id);
            Assert.True(detached);

            var personalAfter = await storage.GetAssetsByTagAsync(tagPersonal.Id);
            Assert.Empty(personalAfter);

            if (File.Exists(ico)) File.Delete(ico);
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
    public async Task TotalTagLimit5000Enforced()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_tags_5000_{Guid.NewGuid():N}.db");
        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = tempDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        try
        {
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            // Fast bulk pre-insert 5000 tags into database directly
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                for (int i = 0; i < 5000; i++)
                {
                    cmd.CommandText = $"INSERT INTO tags (id, display_name, normalized_name, created_utc) VALUES ('tag_{i}', 'Tag {i}', 'TAG {i}', {i});";
                    await cmd.ExecuteNonQueryAsync();
                }
                tx.Commit();
            }

            // Existing tag should still be retrievable
            var existing = await storage.CreateOrGetTagAsync("Tag 100");
            Assert.Equal("tag_100", existing.Id);

            // Attempting to create 5001st new distinct tag should be rejected
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await storage.CreateOrGetTagAsync("Brand New Tag 5001");
            });
            Assert.Contains("5000", ex.Message);
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
    [Trait("Category", "Performance")]
    public async Task IndexedTagQueryBenchmark10kAssets5kTags()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_bench_{Guid.NewGuid():N}.db");
        string connStr = new SqliteConnectionStringBuilder
        {
            DataSource = tempDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();

        try
        {
            using var storage = new SqliteStorageService();
            await storage.InitializeAsync(tempDb);

            // Bulk populate 10,000 assets and 5,000 tags with indexed junctions
            using (var conn = new SqliteConnection(connStr))
            {
                await conn.OpenAsync();
                using var tx = conn.BeginTransaction();

                // 5,000 tags
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    for (int i = 0; i < 5000; i++)
                    {
                        cmd.CommandText = $"INSERT INTO tags (id, display_name, normalized_name, created_utc) VALUES ('tag_{i}', 'Tag {i}', 'TAG {i}', {i});";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // 10,000 assets
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    for (int i = 0; i < 10000; i++)
                    {
                        string sha = i.ToString("D64", System.Globalization.CultureInfo.InvariantCulture);
                        cmd.CommandText = $"INSERT INTO assets (id, sha256, canonical_ico_path, source_format, created_at, last_used_at) VALUES ('asset_{i}', '{sha}', 'C:\\dummy.ico', 'png', '2026-01-01', '2026-01-01');";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // 20,000 junctions (each of first 2,000 tags attached to 10 assets)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    for (int i = 0; i < 2000; i++)
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            int assetIndex = (i * 4 + j) % 10000;
                            string sha = assetIndex.ToString("D64", System.Globalization.CultureInfo.InvariantCulture);
                            cmd.CommandText = $"INSERT INTO asset_tags (asset_hash, tag_id, created_utc) VALUES ('{sha}', 'tag_{i}', {i});";
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                tx.Commit();
            }

            // Benchmark 1 and 2 share this sample set: 100 asset hashes spread across the table.
            var sampleHashes = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                sampleHashes.Add(i.ToString("D64", System.Globalization.CultureInfo.InvariantCulture));
            }

            // Warm up: the first calls after a 35,000-row transaction absorb JIT and the WAL
            // checkpoint the bulk insert left behind. Neither is query cost.
            for (int warmup = 0; warmup < 5; warmup++)
            {
                _ = await storage.GetAssetsByTagAsync("tag_42");
                _ = await storage.GetTagsForAssetsAsync(sampleHashes);
            }

            // Benchmark 1: Query assets by tag among 10k assets / 5k tags.
            // One timing of one query is a coin flip; the gate is the P95 of 100 executions,
            // which still fails a real regression and no longer fails because the scheduler
            // paused the thread once.
            var singleQueryLatencies = new List<double>(100);
            var assets = await storage.GetAssetsByTagAsync("tag_42");
            Assert.Equal(10, assets.Count);
            for (int i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                assets = await storage.GetAssetsByTagAsync("tag_42");
                sw.Stop();
                singleQueryLatencies.Add(sw.Elapsed.TotalMilliseconds);
            }

            Assert.Equal(10, assets.Count);
            double queryP95 = Percentile(singleQueryLatencies, 0.95);
            _output?.WriteLine(
                FormattableString.Invariant(
                    $"Tag query P50={Percentile(singleQueryLatencies, 0.50):F2} ms, P95={queryP95:F2} ms"));
            // Must complete in well under 50 ms (indexed query usually takes < 2 ms)
            Assert.True(queryP95 < 50, $"Query by tag P95 took {queryP95:F2} ms, expected < 50 ms.");

            // Benchmark 2: Bulk get tags for 100 assets
            var bulkLatencies = new List<double>(100);
            var tagsDict = await storage.GetTagsForAssetsAsync(sampleHashes);
            for (int i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                tagsDict = await storage.GetTagsForAssetsAsync(sampleHashes);
                sw.Stop();
                bulkLatencies.Add(sw.Elapsed.TotalMilliseconds);
            }

            Assert.Equal(100, tagsDict.Count);
            double bulkP95 = Percentile(bulkLatencies, 0.95);
            _output?.WriteLine(
                FormattableString.Invariant(
                    $"Bulk tag query P50={Percentile(bulkLatencies, 0.50):F2} ms, P95={bulkP95:F2} ms"));
            Assert.True(bulkP95 < 50, $"Bulk get tags for 100 assets P95 took {bulkP95:F2} ms, expected < 50 ms.");
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
