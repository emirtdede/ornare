using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

namespace Ornare.Storage.Tests;

public class LibraryStorageAndScaleTests
{
    [Fact]
    public async Task AssetDeduplicationWorksDeterministically()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_asset_test_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            string hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
            string path1 = @"C:\AppData\Ornare\icons\first.ico";
            string path2 = @"C:\AppData\Ornare\icons\second.ico";

            DateTimeOffset t1 = DateTimeOffset.UtcNow.AddMinutes(-10);
            DateTimeOffset t2 = DateTimeOffset.UtcNow;

            var asset1 = await storage.AddOrUpdateAssetAsync(hash, path1, "PNG", t1);
            Assert.Equal(hash, asset1.Sha256);
            Assert.Equal(path1, asset1.CanonicalIcoPath);

            // Adding same hash again should update canonical path and last_used_at, not create second record
            var asset2 = await storage.AddOrUpdateAssetAsync(hash, path2, "PNG", t2);
            Assert.Equal(hash, asset2.Sha256);
            Assert.Equal(path2, asset2.CanonicalIcoPath);

            var all = await storage.GetAllAssetsAsync();
            Assert.Single(all);
            Assert.Equal(hash, all[0].Sha256);
            Assert.Equal(path2, all[0].CanonicalIcoPath);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }

    [Fact]
    public async Task FavoritesAndRecentHistoryAreCorrectlyManaged()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_fav_rec_test_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            string hashA = "aaaa1111222233334444555566667777888899990000aaaabbbbccccddddeeee";
            string hashB = "bbbb1111222233334444555566667777888899990000aaaabbbbccccddddeeee";

            await storage.AddOrUpdateAssetAsync(hashA, @"C:\Icons\a.ico", "PNG");
            await storage.AddOrUpdateAssetAsync(hashB, @"C:\Icons\b.ico", "PNG");

            // Favorite hashA
            await storage.SetFavoriteAsync(hashA, true);
            var favs = await storage.GetFavoritesAsync();
            Assert.Single(favs);
            Assert.Equal(hashA, favs[0].Sha256);
            Assert.True(favs[0].IsFavorite);

            // Verify GetAllAssets reflects favorite status
            var all = await storage.GetAllAssetsAsync();
            var assetA = all.First(x => x.Sha256 == hashA);
            var assetB = all.First(x => x.Sha256 == hashB);
            Assert.True(assetA.IsFavorite);
            Assert.False(assetB.IsFavorite);

            // Record history
            await storage.RecordHistoryAsync(@"C:\Folder1", Guid.NewGuid(), null, hashA, "Apply");
            await storage.RecordHistoryAsync(@"C:\Folder2", Guid.NewGuid(), hashA, hashB, "Apply");

            var recent = await storage.GetRecentAssetsAsync();
            Assert.Equal(2, recent.Count);
            // hashB was used most recently, should be first
            Assert.Equal(hashB, recent[0].Sha256);
            Assert.Equal(hashA, recent[1].Sha256);

            // Clear recent history
            await storage.ClearRecentHistoryAsync();
            var recentAfterClear = await storage.GetRecentAssetsAsync();
            Assert.Empty(recentAfterClear);

            // Clearing recent history MUST NOT delete the canonical assets
            var allAfterClear = await storage.GetAllAssetsAsync();
            Assert.Equal(2, allAfterClear.Count);

            // Unfavorite hashA
            await storage.SetFavoriteAsync(hashA, false);
            var favsAfterUnfav = await storage.GetFavoritesAsync();
            Assert.Empty(favsAfterUnfav);

            // Delete assetB
            bool deleted = await storage.DeleteAssetAsync(hashB);
            Assert.True(deleted);
            var allFinal = await storage.GetAllAssetsAsync();
            Assert.Single(allFinal);
            Assert.Equal(hashA, allFinal[0].Sha256);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }

    [Fact]
    public async Task SettingsStoragePersistsThemeAndLanguage()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_settings_test_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            var defaultSettings = await storage.GetUserSettingsAsync();
            Assert.Equal("System", defaultSettings.Theme);
            Assert.Equal("System", defaultSettings.Language);

            var customSettings = new UserSettings
            {
                Theme = "Dark",
                Language = "tr-TR"
            };

            await storage.SaveUserSettingsAsync(customSettings);

            var loaded = await storage.GetUserSettingsAsync();
            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal("tr-TR", loaded.Language);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task LibraryScaleTests(int assetCount)
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_scale_assets_{assetCount}_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            for (int i = 0; i < assetCount; i++)
            {
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"asset_{i}"))).ToLowerInvariant();
                await storage.AddOrUpdateAssetAsync(hash, $@"C:\Icons\asset_{i}.ico", "PNG");
            }

            var all = await storage.GetAllAssetsAsync();
            Assert.Equal(assetCount, all.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task FolderAssignmentScaleTests(int count)
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_scale_assign_{count}_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            for (int i = 0; i < count; i++)
            {
                string path = $@"C:\TestFolders\Folder_{i}";
                var semantics = new FolderIconSemantics
                {
                    IconResource = @".ornare\folder.ico,0"
                };
                var baseline = new FolderBaseline
                {
                    FolderAssignmentId = Guid.NewGuid(),
                    OwnershipEpochId = Guid.NewGuid(),
                    PriorSemantics = new FolderIconSemantics(),
                    CapturedAt = DateTimeOffset.UtcNow
                };

                await storage.SaveFolderAssignmentAsync(path, baseline.OwnershipEpochId, semantics, baseline);
            }

            var assignments = await storage.GetAllFolderAssignmentsAsync();
            Assert.Equal(count, assignments.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
        }
    }
}
