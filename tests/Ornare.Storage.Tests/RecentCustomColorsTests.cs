using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

namespace Ornare.Storage.Tests;

public sealed class RecentCustomColorsTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteStorageService _storage;

    public RecentCustomColorsTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ornare_test_recents_{Guid.NewGuid():N}.db");
        _storage = new SqliteStorageService();
        _storage.InitializeAsync(_tempDbPath).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _storage.Dispose();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public void WithRecentCustomColorDeduplicatesAndPreservesMruOrder()
    {
        var settings = new UserSettings();
        Assert.Empty(settings.RecentCustomColors);

        settings = settings.WithRecentCustomColor("#FF0000");
        Assert.Single(settings.RecentCustomColors);
        Assert.Equal("#FF0000", settings.RecentCustomColors[0]);

        settings = settings.WithRecentCustomColor("#00FF00");
        Assert.Equal(2, settings.RecentCustomColors.Count);
        Assert.Equal("#00FF00", settings.RecentCustomColors[0]);
        Assert.Equal("#FF0000", settings.RecentCustomColors[1]);

        // Re-add #FF0000 in lowercase without #
        settings = settings.WithRecentCustomColor("ff0000");
        Assert.Equal(2, settings.RecentCustomColors.Count);
        Assert.Equal("#FF0000", settings.RecentCustomColors[0]); // Promoted to MRU
        Assert.Equal("#00FF00", settings.RecentCustomColors[1]);
    }

    [Fact]
    public void WithRecentCustomColorCapsAtEightEntries()
    {
        var settings = new UserSettings();

        // Add 12 distinct colors
        for (int i = 1; i <= 12; i++)
        {
            string hex = $"#00{i:X2}00";
            settings = settings.WithRecentCustomColor(hex);
        }

        Assert.Equal(UserSettings.MaxRecentCustomColors, settings.RecentCustomColors.Count);
        Assert.Equal(8, settings.RecentCustomColors.Count);

        // Most recent should be #000C00 (12)
        Assert.Equal("#000C00", settings.RecentCustomColors[0]);
        // Oldest retained should be #000500 (5)
        Assert.Equal("#000500", settings.RecentCustomColors[7]);
    }

    [Fact]
    public async Task SqliteStorageServicePersistsAndLoadsRecentCustomColorsCorrectly()
    {
        var initial = await _storage.GetUserSettingsAsync();
        Assert.Empty(initial.RecentCustomColors);

        var updated = initial
            .WithRecentCustomColor("#112233")
            .WithRecentCustomColor("#AABBCC")
            .WithRecentCustomColor("#445566");

        await _storage.SaveUserSettingsAsync(updated);

        var reloaded = await _storage.GetUserSettingsAsync();
        Assert.Equal(3, reloaded.RecentCustomColors.Count);
        Assert.Equal("#445566", reloaded.RecentCustomColors[0]);
        Assert.Equal("#AABBCC", reloaded.RecentCustomColors[1]);
        Assert.Equal("#112233", reloaded.RecentCustomColors[2]);
    }
}
