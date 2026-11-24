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

    /// <summary>
    /// A NeedsRepair entry whose folder no longer shows an Ornare icon is genuinely gone, so
    /// startup reconciliation may roll it back (INV-TX-002).
    /// </summary>
    [Fact]
    public async Task NeedsRepairEntryRollsBackWhenFolderNoLongerOwnedByOrnare()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_test_db_{Guid.NewGuid():N}.db");
        string folder = Path.Combine(Path.GetTempPath(), $"ornare_needsrepair_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = Guid.NewGuid(),
                Kind = OperationKind.Apply,
                TargetFolderPath = folder,
                State = JournalState.NeedsRepair,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            int reconciled = await storage.ReconcileStartupAsync();

            Assert.Equal(1, reconciled);
            Assert.Empty(await storage.GetPendingJournalEntriesAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// A NeedsRepair entry whose folder still shows an Ornare icon must stay pending so the
    /// user can run Repair; reconciliation must never guess it away (INV-FS-006).
    /// </summary>
    [Fact]
    public async Task NeedsRepairEntryStaysPendingWhileFolderStillOwnedByOrnare()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_test_db_{Guid.NewGuid():N}.db");
        string folder = Path.Combine(Path.GetTempPath(), $"ornare_needsrepair_{Guid.NewGuid():N}");
        string ornareDir = Path.Combine(folder, OrnareConstants.CanonicalFolderName);
        Directory.CreateDirectory(ornareDir);
        File.WriteAllBytes(Path.Combine(ornareDir, OrnareConstants.CustomIconFileName), [0x00, 0x00, 0x01, 0x00]);
        File.WriteAllText(
            Path.Combine(folder, OrnareConstants.DesktopIniFileName),
            "[.ShellClassInfo]\r\nIconResource=.ornare\\folder.ico,0\r\n");

        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = Guid.NewGuid(),
                Kind = OperationKind.Apply,
                TargetFolderPath = folder,
                State = JournalState.NeedsRepair,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            int reconciled = await storage.ReconcileStartupAsync();

            Assert.Equal(0, reconciled);
            IReadOnlyList<JournalEntry> pending = await storage.GetPendingJournalEntriesAsync();
            Assert.Single(pending);
            Assert.Equal(JournalState.NeedsRepair, pending[0].State);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// Settings are written in one transaction, so every value read back must match what was
    /// saved (INV-DATA-001).
    /// </summary>
    [Fact]
    public async Task SaveUserSettingsPersistsEveryField()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_test_db_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            var settings = new UserSettings
            {
                Theme = "Dark",
                Language = "tr-TR",
                AccentColor = "#123456",
                AppIconId = "ornare-ocean",
                UiScalePercent = 125,
                OnboardingCompleted = true,
                CloudSyncEnabled = true,
                CloudSyncProvider = "Custom",
                CloudSyncRoot = @"D:\Backup",
                CloudSyncLastSyncUtc = "2026-01-02T03:04:05Z",
                RecentCustomColors = ["#010203", "#0A0B0C"]
            };

            await storage.SaveUserSettingsAsync(settings);
            UserSettings read = await storage.GetUserSettingsAsync();

            Assert.Equal(settings.Theme, read.Theme);
            Assert.Equal(settings.Language, read.Language);
            Assert.Equal(settings.AccentColor, read.AccentColor);
            Assert.Equal(settings.AppIconId, read.AppIconId);
            Assert.Equal(settings.UiScalePercent, read.UiScalePercent);
            Assert.Equal(settings.OnboardingCompleted, read.OnboardingCompleted);
            Assert.Equal(settings.CloudSyncEnabled, read.CloudSyncEnabled);
            Assert.Equal(settings.CloudSyncProvider, read.CloudSyncProvider);
            Assert.Equal(settings.CloudSyncRoot, read.CloudSyncRoot);
            Assert.Equal(settings.CloudSyncLastSyncUtc, read.CloudSyncLastSyncUtc);
            Assert.Equal(settings.RecentCustomColors, read.RecentCustomColors);
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
