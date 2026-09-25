using System.IO;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

namespace Ornare.Storage.Tests;

public class SqliteStorageServiceTests
{
    [Fact]
    public async Task JournalLifecycleAndReconciliationIsIdempotent()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_test_db_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);
            Assert.True(storage.IsInitialized);

            Guid opId1 = Guid.NewGuid();
            var entry1 = new JournalEntry
            {
                OperationId = opId1,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\TestFolder\One",
                State = JournalState.Prepared,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            Guid opId2 = Guid.NewGuid();
            var entry2 = new JournalEntry
            {
                OperationId = opId2,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\TestFolder\Two",
                State = JournalState.FilesystemCommitted,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await storage.RecordJournalEntryAsync(entry1);
            await storage.RecordJournalEntryAsync(entry2);

            var pending = await storage.GetPendingJournalEntriesAsync();
            Assert.Equal(2, pending.Count);

            // Reconcile 1st time
            int count1 = await storage.ReconcileStartupAsync();
            Assert.Equal(2, count1);

            // Verify entry 1 was rolled back and entry 2 was completed
            var pendingAfter = await storage.GetPendingJournalEntriesAsync();
            Assert.Empty(pendingAfter);

            // Reconcile 2nd time to prove idempotency (INV-TX-002: Reconciliation is idempotent)
            int count2 = await storage.ReconcileStartupAsync();
            Assert.Equal(0, count2);
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
    public async Task SaveFolderAssignmentAndRetrieveBaselineRoundtrip()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_test_db_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            string folderPath = @"C:\MyFolder";
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

            var newSemantics = new FolderIconSemantics
            {
                IconResource = @".ornare\folder.ico,0",
                AttributesIntroduced = true
            };

            await storage.SaveFolderAssignmentAsync(folderPath, epochId, newSemantics, baseline);

            var retrieved = await storage.GetLatestBaselineAsync(folderPath);
            Assert.NotNull(retrieved);
            Assert.Equal(assignmentId, retrieved.FolderAssignmentId);
            Assert.Equal(epochId, retrieved.OwnershipEpochId);
            Assert.Equal(priorSemantics.IconResource, retrieved.PriorSemantics.IconResource);

            await storage.RemoveFolderAssignmentAsync(folderPath);
            var afterRemoval = await storage.GetLatestBaselineAsync(folderPath);
            Assert.Null(afterRemoval);
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
