using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Storage;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Audit finding 1.2. A raw <c>Ornare.Cli.exe</c> used to write its own database under
/// <c>%LOCALAPPDATA%\Ornare</c> while the installed app read
/// <c>...\Packages\Ornare_&lt;publisher&gt;\LocalCache\Local\Ornare</c>. Every folder the CLI
/// customized was invisible to the app and could not be undone or repaired from the UI.
/// <para>
/// These tests pin both halves of the fix: the merge preserves old work, and it can never
/// overwrite newer work.
/// </para>
/// </summary>
public sealed class LegacyDataRootReconciliationTests : IDisposable
{
    private readonly string _root;

    public LegacyDataRootReconciliationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"ornare_reconcile_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best effort.
        }
    }

    [Fact]
    public async Task LegacyAssignmentsSurviveTheMergeAndTheTargetStaysNewer()
    {
        string legacyPath = Path.Combine(_root, "legacy", "ornare.db");
        string targetPath = Path.Combine(_root, "packaged", "ornare.db");

        await SeedAsync(
            legacyPath,
            ("C:\\Legacy\\Only", Guid.NewGuid(), "2026-01-01T00:00:00Z"),
            ("C:\\Shared\\Path", Guid.NewGuid(), "2025-12-01T00:00:00Z"));
        await SeedAsync(
            targetPath,
            ("C:\\Shared\\Path", Guid.NewGuid(), "2026-01-01T00:00:00Z"),
            ("C:\\Packaged\\Newer", Guid.NewGuid(), "2026-06-01T00:00:00Z"));

        int merged = LegacyDataRootReconciliation.Reconcile(legacyPath, targetPath);

        // One legacy row is new, the shared path is already present and must not be touched.
        Assert.Equal(1, merged);

        var rows = await ReadAssignmentsAsync(targetPath);
        Assert.Equal(3, rows.Count);
        Assert.Contains("C:\\Legacy\\Only", rows.Keys);
        Assert.Contains("C:\\Packaged\\Newer", rows.Keys);

        // The packaged row for the shared path keeps its own timestamp: the merge is additive.
        Assert.Equal("2026-01-01T00:00:00Z", rows["C:\\Shared\\Path"]);
    }

    [Fact]
    public async Task MergingTwiceIsIdempotent()
    {
        string legacyPath = Path.Combine(_root, "legacy", "ornare.db");
        string targetPath = Path.Combine(_root, "packaged", "ornare.db");

        await SeedAsync(legacyPath, ("C:\\One", Guid.NewGuid(), "2026-01-01T00:00:00Z"));
        await SeedAsync(targetPath, ("C:\\Two", Guid.NewGuid(), "2026-02-01T00:00:00Z"));

        Assert.Equal(1, LegacyDataRootReconciliation.Reconcile(legacyPath, targetPath));
        Assert.Equal(0, LegacyDataRootReconciliation.Reconcile(legacyPath, targetPath));

        var rows = await ReadAssignmentsAsync(targetPath);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void NonOrnareTargetIsLeftAlone()
    {
        string legacyPath = Path.Combine(_root, "legacy", "ornare.db");
        string foreignPath = Path.Combine(_root, "foreign", "other.db");

        Directory.CreateDirectory(Path.GetDirectoryName(foreignPath)!);
        using (var connection = new SqliteConnection($"Data Source={foreignPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE unrelated (id INTEGER PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        using (var connection = new SqliteConnection($"Data Source={legacyPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE assets (id TEXT PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        // A database without Ornare's tables must not be written into.
        Assert.Equal(0, LegacyDataRootReconciliation.Reconcile(legacyPath, foreignPath));
        Assert.True(File.Exists(foreignPath));
    }

    [Fact]
    public void RetireRenamesInsteadOfDeleting()
    {
        string legacyPath = Path.Combine(_root, "legacy", "ornare.db");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, "placeholder");

        LegacyDataRootReconciliation.Retire(legacyPath);

        Assert.False(File.Exists(legacyPath));
        string[] retired = Directory.GetFiles(Path.GetDirectoryName(legacyPath)!, "ornare.db.merged-*");
        Assert.Single(retired);
        Assert.Equal("placeholder", File.ReadAllText(retired[0]));
    }

    private static async Task SeedAsync(
        string databasePath,
        params (string Path, Guid EpochId, string UpdatedAt)[] assignments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        // Build the real schema by opening the production storage service once, so the test
        // fails if a future migration changes the tables the merge depends on.
        using (var storage = new SqliteStorageService())
        {
            await storage.InitializeAsync(databasePath);
        }

        using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        foreach ((string path, Guid epochId, string updatedAt) in assignments)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO folder_assignments
                    (id, folder_path, ownership_epoch_id, icon_relative_path, status,
                     created_at, updated_at)
                VALUES ($id, $path, $epoch, '.ornare\folder.ico', 0, $now, $now);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$epoch", epochId.ToString("D"));
            command.Parameters.AddWithValue("$now", updatedAt);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<System.Collections.Generic.Dictionary<string, string>> ReadAssignmentsAsync(string databasePath)
    {
        var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT folder_path, updated_at FROM folder_assignments;";
        using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }
}
