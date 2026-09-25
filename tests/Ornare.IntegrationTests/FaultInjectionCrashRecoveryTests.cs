using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Storage;
using Xunit;

namespace Ornare.IntegrationTests;

public class FaultInjectionCrashRecoveryTests
{
    [Fact]
    public async Task FaultInjectionAllCrashStagesReconcileDeterministically()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_fault_{Guid.NewGuid():N}.db");
        using var storage = new SqliteStorageService();

        try
        {
            await storage.InitializeAsync(tempDb);

            // Stage 1: Crash after ICO generation (AssetGenerated)
            Guid op1 = Guid.NewGuid();
            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = op1,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\Mock\Stage1",
                State = JournalState.AssetGenerated,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            // Stage 2: Crash after ICO write / staging (FilesystemStaged)
            Guid op2 = Guid.NewGuid();
            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = op2,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\Mock\Stage2",
                State = JournalState.FilesystemStaged,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            // Stage 3: Crash after desktop.ini commit (FilesystemCommitted)
            Guid op3 = Guid.NewGuid();
            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = op3,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\Mock\Stage3",
                State = JournalState.FilesystemCommitted,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            // Stage 4: Crash before shell refresh (FilesystemCommitted)
            Guid op4 = Guid.NewGuid();
            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = op4,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\Mock\Stage4",
                State = JournalState.FilesystemCommitted,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            // Stage 5: Crash before metadata DB commit (ShellRefreshRequested)
            Guid op5 = Guid.NewGuid();
            await storage.RecordJournalEntryAsync(new JournalEntry
            {
                OperationId = op5,
                Kind = OperationKind.Apply,
                TargetFolderPath = @"C:\Mock\Stage5",
                State = JournalState.ShellRefreshRequested,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            // Stage 6: Verify pending count is 5
            var pending = await storage.GetPendingJournalEntriesAsync();
            Assert.Equal(5, pending.Count);

            // Execute startup reconciliation (simulating app restart after crashes)
            int reconciled = await storage.ReconcileStartupAsync();
            Assert.Equal(5, reconciled);

            // Verify all pending operations are resolved
            var pendingAfter = await storage.GetPendingJournalEntriesAsync();
            Assert.Empty(pendingAfter);

            // Re-run to verify idempotency (INV-TX-002)
            int secondRun = await storage.ReconcileStartupAsync();
            Assert.Equal(0, secondRun);
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
    [InlineData("AssetGenerated", JournalState.RolledBack)]
    [InlineData("FilesystemStaged", JournalState.RolledBack)]
    [InlineData("FilesystemCommitted", JournalState.Completed)]
    [InlineData("MetadataPending", JournalState.Completed)]
    public async Task RealExternalProcessTerminationAcrossPhases(string barrier, JournalState expectedState)
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"ornare_crash_{barrier}_{Guid.NewGuid():N}.db");
        string testFolder = Path.Combine(Path.GetTempPath(), $"ornare_crash_dir_{barrier}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testFolder);

        string workerExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\tests\Ornare.CrashTestWorker\bin\Release\net10.0-windows10.0.26100.0\Ornare.CrashTestWorker.exe"));

        Assert.True(File.Exists(workerExe), $"CrashTestWorker must exist at {workerExe}");

        var psi = new ProcessStartInfo
        {
            FileName = workerExe,
            Arguments = $"--barrier {barrier} --folder \"{testFolder}\" --db \"{tempDb}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Wait until barrier is reached
        string? barrierLine = null;
        while (!process.HasExited)
        {
            string? line = await process.StandardOutput.ReadLineAsync();
            if (line != null && line.StartsWith("BARRIER_REACHED:", StringComparison.Ordinal))
            {
                barrierLine = line;
                break;
            }
        }

        Assert.Equal($"BARRIER_REACHED:{barrier}", barrierLine);

        // Terminate the process hard (simulating SIGKILL / TerminateProcess / sudden crash)
        process.Kill(entireProcessTree: true);
        process.WaitForExit(5000);
        Assert.True(process.HasExited);

        // Now start Ornare storage reconciliation (simulating app restart after crash)
        using var storage = new SqliteStorageService();
        await storage.InitializeAsync(tempDb);

        var pendingBefore = await storage.GetPendingJournalEntriesAsync();
        Assert.Single(pendingBefore);
        Guid opId = pendingBefore[0].OperationId;

        int reconciled = await storage.ReconcileStartupAsync();
        Assert.Equal(1, reconciled);

        // Verify pending queue is emptied
        var pendingAfter = await storage.GetPendingJournalEntriesAsync();
        Assert.Empty(pendingAfter);

        storage.Dispose();
        SqliteConnection.ClearAllPools();

        // Verify journal entry in database has the expected terminal state (RolledBack or Completed)
        using (var conn = new SqliteConnection($"Data Source={tempDb}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT state FROM operation_journal WHERE operation_id = @id;";
            cmd.Parameters.AddWithValue("@id", opId.ToString());
            var stateObj = await cmd.ExecuteScalarAsync();
            Assert.NotNull(stateObj);
            var actualState = (JournalState)Convert.ToInt32(stateObj, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expectedState, actualState);
        }

        // Cleanup
        SqliteConnection.ClearAllPools();
        if (File.Exists(tempDb)) File.Delete(tempDb);
        if (Directory.Exists(testFolder))
        {
            var dir = new DirectoryInfo(testFolder) { Attributes = FileAttributes.Normal };
            foreach (var f in dir.GetFiles("*", SearchOption.AllDirectories))
            {
                f.Attributes = FileAttributes.Normal;
            }
            Directory.Delete(testFolder, true);
        }
    }
}
