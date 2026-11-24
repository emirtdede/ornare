using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Storage.Cloud;
using Xunit;

namespace Ornare.Storage.Tests;

/// <summary>
/// Verifies the serverless cloud backup contract: atomic writes, previous-snapshot
/// retention, the 15-day rotation, the corruption recovery chain and the rule that an
/// empty local state may never overwrite a real backup.
/// </summary>
public sealed class CloudSyncServiceTests : IDisposable
{
    private readonly string _root;
    private readonly FixedTimeProvider _time;
    private readonly CloudSyncService _service;

    public CloudSyncServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ornare-cloud-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));
        _service = new CloudSyncService(_time, _root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temp cleanup is best effort and must not fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
            // A scanner can still hold a freshly written file open. Reporting that as a
            // failure would hide the test's real result.
        }
    }

    private static readonly string[] SecondCollections = { "Second" };

    private CloudSyncLocation Location() => new(CloudSyncProviderKind.Custom, _root, false);

    private static CloudSyncSnapshot Snapshot(bool empty = false) => new()
    {
        Provider = nameof(CloudSyncProviderKind.Custom),
        ExportedAt = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero),
        Settings = empty ? new UserSettings() : new UserSettings { Theme = "Dark", AccentColor = "#0F6CBD" },
        Folders = empty
            ? Array.Empty<CloudFolderSnapshot>()
            : new[] { new CloudFolderSnapshot(@"C:\Work", "library/abc.ico", "Healthy", DateTimeOffset.UtcNow) },
        Collections = empty ? Array.Empty<string>() : new[] { "Design" }
    };

    [Fact]
    public async Task FirstWriteCreatesCurrentAndDailyBackupWithoutPrevious()
    {
        CloudSyncWriteResult result = await _service.WriteSnapshotAsync(Location(), Snapshot());

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, CloudSyncFiles.CurrentName)));
        Assert.True(File.Exists(Path.Combine(_root, CloudSyncFiles.BackupFileName(_time.GetLocalNow()))));
        Assert.False(File.Exists(Path.Combine(_root, CloudSyncFiles.PreviousName)));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp-*"));
    }

    [Fact]
    public async Task SecondWriteKeepsPreviousSnapshot()
    {
        await _service.WriteSnapshotAsync(Location(), Snapshot());
        CloudSyncSnapshot updated = Snapshot() with { Collections = new[] { "Design", "Archive" } };
        CloudSyncWriteResult result = await _service.WriteSnapshotAsync(Location(), updated);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, CloudSyncFiles.PreviousName)));

        CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(Location());
        Assert.Equal(CloudSyncSource.Current, read.Source);
        Assert.Equal(2, read.Snapshot!.Collections.Count);
    }

    [Fact]
    public async Task EmptySnapshotCannotOverwriteRealBackup()
    {
        await _service.WriteSnapshotAsync(Location(), Snapshot());

        CloudSyncWriteResult blocked = await _service.WriteSnapshotAsync(Location(), Snapshot(empty: true));

        Assert.False(blocked.Success);
        Assert.Equal("EmptySnapshotGuard", blocked.ErrorCode);

        CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(Location());
        Assert.Single(read.Snapshot!.Folders);
    }

    [Fact]
    public async Task EmptySnapshotIsAllowedWhenNothingValuableExists()
    {
        CloudSyncWriteResult result = await _service.WriteSnapshotAsync(Location(), Snapshot(empty: true));

        Assert.True(result.Success);
        CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(Location());
        Assert.True(read.HasSnapshot);
        Assert.True(read.Snapshot!.IsEmpty);
    }

    [Fact]
    public async Task CorruptCurrentFallsBackToPrevious()
    {
        await _service.WriteSnapshotAsync(Location(), Snapshot());
        await _service.WriteSnapshotAsync(Location(), Snapshot() with { Collections = SecondCollections });
        await File.WriteAllTextAsync(Path.Combine(_root, CloudSyncFiles.CurrentName), "{ not json");

        CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(Location());

        Assert.True(read.HasSnapshot);
        Assert.Equal(CloudSyncSource.Previous, read.Source);
        Assert.Single(read.Snapshot!.Collections);
    }

    [Fact]
    public async Task CorruptCurrentAndPreviousFallBackToNewestDailyBackup()
    {
        await _service.WriteSnapshotAsync(Location(), Snapshot());
        await File.WriteAllTextAsync(Path.Combine(_root, CloudSyncFiles.CurrentName), "broken");
        await File.WriteAllTextAsync(Path.Combine(_root, CloudSyncFiles.PreviousName), "broken");

        CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(Location());

        Assert.True(read.HasSnapshot);
        Assert.Equal(CloudSyncSource.DailyBackup, read.Source);
    }

    [Fact]
    public void PruneKeepsExactlyFifteenNewestBackups()
    {
        for (int daysAgo = 0; daysAgo < 20; daysAgo++)
        {
            DateTimeOffset date = _time.GetLocalNow().AddDays(-daysAgo);
            File.WriteAllText(Path.Combine(_root, CloudSyncFiles.BackupFileName(date)), "{}");
        }

        int removed = _service.PruneBackups(Location());

        Assert.Equal(5, removed);
        Assert.Equal(CloudSyncFiles.RetainedBackupDays, CloudSyncService.ListBackupFiles(_root).Count);
    }

    [Fact]
    public async Task StatusReportsFolderAndCollectionCounts()
    {
        await _service.WriteSnapshotAsync(Location(), Snapshot());

        CloudSyncStatus status = await CloudSyncService.GetStatusAsync(Location());

        Assert.Equal(CloudSyncProviderKind.Custom, status.Provider);
        Assert.Equal(1, status.FolderCount);
        Assert.Equal(1, status.CollectionCount);
        Assert.Equal(1, status.BackupFileCount);
        Assert.NotNull(status.LastSyncUtc);
    }

    [Fact]
    public void UnsafePathsAreRejected()
    {
        Assert.False(CloudSyncService.IsSafeDirectoryPath(null));
        Assert.False(CloudSyncService.IsSafeDirectoryPath(string.Empty));
        Assert.False(CloudSyncService.IsSafeDirectoryPath("relative\\folder"));
        Assert.False(CloudSyncService.IsSafeDirectoryPath(Path.Combine(_root, "..", "..")));
        Assert.True(CloudSyncService.IsSafeDirectoryPath(_root));
    }

    [Fact]
    public void UnknownProviderResolvesToDetectedLocationOrFallback()
    {
        CloudSyncLocation location = _service.ResolveLocation("NotAProvider", null);

        Assert.Equal(CloudSyncProviderKind.None, location.Provider);
        Assert.True(location.IsFallback);
    }

    [Fact]
    public void CustomProviderUsesChoosenFolderWhenItExists()
    {
        CloudSyncLocation location = _service.ResolveLocation("Custom", _root);

        Assert.Equal(CloudSyncProviderKind.Custom, location.Provider);
        Assert.False(location.IsFallback);
        Assert.Equal(Path.GetFullPath(_root), location.RootPath);
    }

    [Fact]
    public void RedactFolderPathKeepsLeafButNotTheFullPath()
    {
        string redacted = CloudSyncService.RedactFolderPath(@"C:\Users\alice\Documents\Projects\Website");

        Assert.DoesNotContain("Users", redacted);
        Assert.DoesNotContain("alice", redacted);
        Assert.DoesNotContain(@"C:\", redacted);
        Assert.StartsWith("Website", redacted);
    }

    [Fact]
    public void RedactFolderPathIsStableForTheSamePathIgnoringCase()
    {
        string a = CloudSyncService.RedactFolderPath(@"C:\Work\Assets");
        string b = CloudSyncService.RedactFolderPath(@"c:\work\assets");

        // The fingerprint is the final 16 hex characters and is derived from the upper-cased
        // path, so it matches even when the leaf's casing differs.
        Assert.Equal(a[^16..], b[^16..]);
    }

    [Fact]
    public void RedactFolderPathHandlesVolumeRoot()
    {
        string redacted = CloudSyncService.RedactFolderPath(@"D:\");

        Assert.DoesNotContain(@"D:\", redacted);
        Assert.NotEmpty(redacted);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
