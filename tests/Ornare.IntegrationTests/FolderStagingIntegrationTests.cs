namespace Ornare.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

public sealed class FolderStagingIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _tempDbPath;
    private SqliteStorageService? _storage;

    public FolderStagingIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"ornare_f20_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);

        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ornare_f20_db_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (_storage != null)
        {
            _storage.Dispose();
            _storage = null;
        }

        SqliteConnection.ClearAllPools();

        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }

        if (Directory.Exists(_testRoot))
        {
            try { Directory.Delete(_testRoot, true); } catch { }
        }
    }

    private async Task<SqliteStorageService> GetInitializedStorageAsync()
    {
        if (_storage == null)
        {
            _storage = new SqliteStorageService();
            await _storage.InitializeAsync(_tempDbPath);
        }
        return _storage;
    }

    [Fact]
    public async Task StageFoldersSingleValidFolderAcceptedWithIdentity()
    {
        string folder1 = Path.Combine(_testRoot, "TestFolder1");
        Directory.CreateDirectory(folder1);

        var result = await FolderStagingService.StageFoldersAsync(new[] { folder1 });

        Assert.False(result.IsLimitExceeded);
        Assert.False(result.IsCancelled);
        Assert.Equal(1, result.TotalRawCount);
        Assert.Equal(1, result.AcceptedCount);
        Assert.Equal(0, result.RejectedCount);

        var staged = Assert.Single(result.AcceptedItems);
        Assert.Equal(Path.GetFullPath(folder1), staged.CanonicalPath);
        Assert.Equal("TestFolder1", staged.FolderName);
        Assert.Null(staged.ExistingAssignmentId);
        Assert.False(staged.IsAlreadyCustomized);
    }

    [Fact]
    public async Task StageFoldersOneHundredFoldersAllAccepted()
    {
        var paths = new List<string>(100);
        for (int i = 0; i < 100; i++)
        {
            string dir = Path.Combine(_testRoot, $"Batch100_Folder_{i:D3}");
            Directory.CreateDirectory(dir);
            paths.Add(dir);
        }

        var result = await FolderStagingService.StageFoldersAsync(paths);

        Assert.False(result.IsLimitExceeded);
        Assert.False(result.IsCancelled);
        Assert.Equal(100, result.TotalRawCount);
        Assert.Equal(100, result.AcceptedCount);
        Assert.Equal(0, result.RejectedCount);
    }

    [Fact]
    public async Task StageFoldersOneThousandFoldersAcceptedAtBoundary()
    {
        var paths = new List<string>(1000);
        for (int i = 0; i < 1000; i++)
        {
            string dir = Path.Combine(_testRoot, $"Boundary1000_{i:D4}");
            Directory.CreateDirectory(dir);
            paths.Add(dir);
        }

        var result = await FolderStagingService.StageFoldersAsync(paths);

        Assert.False(result.IsLimitExceeded);
        Assert.Equal(1000, result.TotalRawCount);
        Assert.Equal(1000, result.AcceptedCount);
        Assert.Equal(0, result.RejectedCount);
    }

    [Fact]
    public async Task StageFoldersMoreThanOneThousandItemsRejectedImmediatelyWithoutFilesystemIo()
    {
        var paths = Enumerable.Range(1, 1001)
            .Select(i => Path.Combine(_testRoot, $"NonExistent_AboveLimit_{i}"))
            .ToList();

        var result = await FolderStagingService.StageFoldersAsync(paths);

        Assert.True(result.IsLimitExceeded);
        Assert.False(result.IsCancelled);
        Assert.Equal(1001, result.TotalRawCount);
        Assert.Equal(0, result.AcceptedCount);
        Assert.Equal(1001, result.RejectedCount);

        Assert.All(result.RejectedItems, r => Assert.Equal(ErrorCode.DropLimitExceeded, r.ErrorCode));
    }

    [Fact]
    public async Task StageFoldersItemIsFileNotFolderRejectedWithItemIsFileNotFolder()
    {
        string filePath = Path.Combine(_testRoot, "document.txt");
        File.WriteAllText(filePath, "sample file content");

        var result = await FolderStagingService.StageFoldersAsync(new[] { filePath });

        Assert.False(result.IsLimitExceeded);
        Assert.Equal(0, result.AcceptedCount);
        Assert.Equal(1, result.RejectedCount);

        var rejected = Assert.Single(result.RejectedItems);
        Assert.Equal(filePath, rejected.RawInputPath);
        Assert.Equal(ErrorCode.ItemIsFileNotFolder, rejected.ErrorCode);
    }

    [Fact]
    public async Task StageFoldersNonExistentPathRejectedWithTargetNotFound()
    {
        string missingPath = Path.Combine(_testRoot, "Does_Not_Exist_Dir_12345");

        var result = await FolderStagingService.StageFoldersAsync(new[] { missingPath });

        Assert.Equal(0, result.AcceptedCount);
        Assert.Equal(1, result.RejectedCount);

        var rejected = Assert.Single(result.RejectedItems);
        Assert.Equal(ErrorCode.TargetNotFound, rejected.ErrorCode);
    }

    [Fact]
    public async Task StageFoldersDuplicatePathAliasesCoalescesToSingleStagedItem()
    {
        string folder = Path.Combine(_testRoot, "CoalesceFolder");
        Directory.CreateDirectory(folder);

        string path1 = folder;
        string path2 = folder + @"\";
        string path3 = folder.ToLowerInvariant();
        string path4 = folder.ToUpperInvariant();

        var result = await FolderStagingService.StageFoldersAsync(new[] { path1, path2, path3, path4 });

        Assert.False(result.IsLimitExceeded);
        Assert.Equal(4, result.TotalRawCount);
        Assert.Equal(1, result.AcceptedCount);
        Assert.Equal(0, result.RejectedCount);

        var staged = Assert.Single(result.AcceptedItems);
        Assert.Equal(Path.GetFullPath(folder), staged.CanonicalPath, ignoreCase: true);
    }

    [Fact]
    public async Task StageFoldersExistingCustomizedFolderInSqliteAttachesExistingAssignmentMetadata()
    {
        var storage = await GetInitializedStorageAsync();

        string folder = Path.Combine(_testRoot, "CustomizedProject");
        Directory.CreateDirectory(folder);

        var ntfsId = FolderIdentityService.GetNtfsIdentity(folder);
        Assert.True(ntfsId.HasValue);

        Guid epochId = Guid.NewGuid();
        var semantics = new FolderIconSemantics
        {
            IconResource = @".ornare\folder.ico,0",
            AttributesIntroduced = true
        };
        var baseline = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = new FolderIconSemantics { IconResource = "", AttributesIntroduced = false },
            CapturedAt = DateTimeOffset.UtcNow
        };

        await storage.SaveFolderAssignmentAsync(folder, epochId, semantics, baseline);
        await storage.UpdateAssignmentIdentityAsync(folder, ntfsId.Value.VolumeSerialNumber, ntfsId.Value.FileId64);

        var existingAssignment = await storage.GetAssignmentByPathAsync(folder);
        Assert.NotNull(existingAssignment);

        var result = await FolderStagingService.StageFoldersAsync(new[] { folder }, storage);

        Assert.Equal(1, result.AcceptedCount);
        var staged = Assert.Single(result.AcceptedItems);
        Assert.True(staged.IsAlreadyCustomized);
        Assert.Equal(existingAssignment.Id, staged.ExistingAssignmentId);

        var found = await storage.FindAssignmentAsync(null, folder);
        Assert.NotNull(found);
        Assert.Equal(existingAssignment.Id, found.Id);
    }

    [Fact]
    public async Task StageFoldersZeroMutationInvariantDatabaseAndDiskRemainCompletelyIdentical()
    {
        var storage = await GetInitializedStorageAsync();

        string existingFolder = Path.Combine(_testRoot, "BaselineExistingFolder");
        Directory.CreateDirectory(existingFolder);

        var ntfsId = FolderIdentityService.GetNtfsIdentity(existingFolder);
        Guid epochId = Guid.NewGuid();
        var semantics = new FolderIconSemantics
        {
            IconResource = @".ornare\folder.ico,0",
            AttributesIntroduced = true
        };
        var baseline = new FolderBaseline
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = epochId,
            PriorSemantics = new FolderIconSemantics { IconResource = "", AttributesIntroduced = false },
            CapturedAt = DateTimeOffset.UtcNow
        };

        await storage.SaveFolderAssignmentAsync(existingFolder, epochId, semantics, baseline);
        if (ntfsId.HasValue)
        {
            await storage.UpdateAssignmentIdentityAsync(existingFolder, ntfsId.Value.VolumeSerialNumber, ntfsId.Value.FileId64);
        }

        async Task<(int userVersion, long foldersCount, long journalCount, long historyCount)> GetDbSnapshotAsync()
        {
            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _tempDbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            await conn.OpenAsync();

            using var cmdVersion = conn.CreateCommand();
            cmdVersion.CommandText = "PRAGMA user_version;";
            int version = Convert.ToInt32(await cmdVersion.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

            using var cmdFolders = conn.CreateCommand();
            cmdFolders.CommandText = "SELECT COUNT(*) FROM folder_assignments;";
            long folders = Convert.ToInt64(await cmdFolders.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

            using var cmdJournal = conn.CreateCommand();
            cmdJournal.CommandText = "SELECT COUNT(*) FROM operation_journal;";
            long journal = Convert.ToInt64(await cmdJournal.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

            using var cmdHistory = conn.CreateCommand();
            cmdHistory.CommandText = "SELECT COUNT(*) FROM history;";
            long history = Convert.ToInt64(await cmdHistory.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

            return (version, folders, journal, history);
        }

        var beforeDb = await GetDbSnapshotAsync();

        string dropFolder1 = Path.Combine(_testRoot, "FreshDrop1");
        string dropFolder2 = Path.Combine(_testRoot, "FreshDrop2");
        Directory.CreateDirectory(dropFolder1);
        Directory.CreateDirectory(dropFolder2);

        Assert.False(File.Exists(Path.Combine(dropFolder1, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(dropFolder1, ".ornare")));
        Assert.False(File.Exists(Path.Combine(dropFolder2, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(dropFolder2, ".ornare")));

        string filePath = Path.Combine(_testRoot, "ignored_file.txt");
        File.WriteAllText(filePath, "data");

        var dropPaths = new[]
        {
            dropFolder1,
            existingFolder,
            dropFolder2,
            filePath,
            Path.Combine(_testRoot, "missing_folder")
        };

        var stagingResult = await FolderStagingService.StageFoldersAsync(dropPaths, storage);

        Assert.Equal(3, stagingResult.AcceptedCount);
        Assert.Equal(2, stagingResult.RejectedCount);

        var afterDb = await GetDbSnapshotAsync();
        Assert.Equal(beforeDb.userVersion, afterDb.userVersion);
        Assert.Equal(beforeDb.foldersCount, afterDb.foldersCount);
        Assert.Equal(beforeDb.journalCount, afterDb.journalCount);
        Assert.Equal(beforeDb.historyCount, afterDb.historyCount);

        Assert.False(File.Exists(Path.Combine(dropFolder1, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(dropFolder1, ".ornare")));
        Assert.False(File.Exists(Path.Combine(dropFolder2, "desktop.ini")));
        Assert.False(Directory.Exists(Path.Combine(dropFolder2, ".ornare")));
    }

    [Fact]
    public async Task StageFoldersCancellationHaltsCleanlyAndProducesPartialResult()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        string folder = Path.Combine(_testRoot, "CancelFolder");
        Directory.CreateDirectory(folder);

        var result = await FolderStagingService.StageFoldersAsync(new[] { folder }, ct: cts.Token);

        Assert.True(result.IsCancelled);
        Assert.Equal(1, result.TotalRawCount);
    }
}
