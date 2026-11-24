using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Ornare.Storage;

/// <summary>
/// Merges a pre-fix database that lives in the legacy unpackaged data root into the database
/// the packaged app actually uses.
/// <para>
/// Before audit finding 1.2, a raw <c>Ornare.Cli.exe</c> run wrote
/// <c>%LOCALAPPDATA%\Ornare\data\ornare.db</c> while the installed app used
/// <c>...\Packages\Ornare_&lt;publisher&gt;\LocalCache\Local\Ornare\data\ornare.db</c>. Any
/// folder the user customized through the CLI was therefore invisible to the app and could not
/// be repaired, undone or reset from the UI. The CLI now adopts the package root, and this class
/// preserves what was already written to the old one.
/// </para>
/// <para>
/// The merge is deliberately additive and idempotent: it inserts only rows that are absent by
/// primary key and never updates or deletes an existing row, so a newer assignment in the
/// packaged database always wins. Tables are merged in dependency order, and only columns that
/// exist on both sides are copied, so a legacy file written by an older schema still merges
/// instead of failing on a missing column.
/// </para>
/// </summary>
public static class LegacyDataRootReconciliation
{
    /// <summary>
    /// User-content tables, ordered so a referenced row exists before the row referencing it.
    /// <c>settings</c> and <c>operation_journal</c> are intentionally excluded: the first is
    /// per-device state that must not be transplanted, and the second describes in-flight work
    /// that is meaningless in another database.
    /// </summary>
    private static readonly string[] Tables =
    [
        "assets",
        "folder_assignments",
        "folder_baselines",
        "tags",
        "collections",
        "favorites",
        "history",
        "asset_tags",
        "collection_assets",
        "asset_search_tokens",
        "tag_search_tokens",
        "collection_search_tokens"
    ];

    /// <summary>
    /// Copies every row that the target database does not already hold.
    /// </summary>
    /// <param name="legacyDatabasePath">The old <c>%LOCALAPPDATA%\Ornare\data\ornare.db</c>.</param>
    /// <param name="targetDatabasePath">The packaged database to merge into.</param>
    /// <returns>The number of rows inserted, so the caller can log what actually happened.</returns>
    public static int Reconcile(string legacyDatabasePath, string targetDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);

        if (!File.Exists(legacyDatabasePath))
        {
            return 0;
        }

        string? targetDirectory = Path.GetDirectoryName(targetDatabasePath);
        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = targetDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,

            // Rows arrive in dependency order, but a partially imported legacy file must not be
            // able to abort the whole merge through an unsatisfied reference.
            ForeignKeys = false
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        if (!TableExists(connection, "main", "assets"))
        {
            // The target is not an Ornare database. Refuse to touch it.
            return 0;
        }

        using (var attach = connection.CreateCommand())
        {
            attach.CommandText = "ATTACH DATABASE $path AS legacy;";
            attach.Parameters.AddWithValue("$path", legacyDatabasePath);
            attach.ExecuteNonQuery();
        }

        int inserted = 0;
        using (SqliteTransaction transaction = connection.BeginTransaction())
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;

            foreach (string table in Tables)
            {
                if (!TableExists(connection, "main", table) || !TableExists(connection, "legacy", table))
                {
                    continue;
                }

                List<string> columns = SharedColumns(connection, transaction, table);
                if (columns.Count == 0)
                {
                    continue;
                }

                command.Parameters.Clear();
                string columnList = string.Join(", ", columns.ConvertAll(quote));
                command.CommandText =
                    $"INSERT OR IGNORE INTO main.\"{table}\" ({columnList}) " +
                    $"SELECT {columnList} FROM legacy.\"{table}\";";

                inserted += command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        using (var detach = connection.CreateCommand())
        {
            detach.CommandText = "DETACH DATABASE legacy;";
            detach.ExecuteNonQuery();
        }

        return inserted;

        static string quote(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
    }

    private static bool TableExists(SqliteConnection connection, string schema, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM " + schema + ".sqlite_master WHERE type IN ('table', 'view') AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }

    private static List<string> SharedColumns(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table)
    {
        HashSet<string> target = ColumnsOf(connection, transaction, "main", table);
        HashSet<string> legacy = ColumnsOf(connection, transaction, "legacy", table);

        var shared = new List<string>();
        foreach (string column in OrderedColumns(connection, transaction, "main", table))
        {
            if (target.Contains(column) && legacy.Contains(column))
            {
                shared.Add(column);
            }
        }

        return shared;
    }

    private static HashSet<string> ColumnsOf(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string schema,
        string table)
    {
        return new HashSet<string>(OrderedColumns(connection, transaction, schema, table), StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> OrderedColumns(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string schema,
        string table)
    {
        var columns = new List<string>();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name FROM pragma_table_info($table, $schema);";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$schema", schema);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    /// <summary>
    /// Moves a merged legacy database out of the way instead of deleting it, so a merge that
    /// turns out to be unwanted can still be inspected or restored by hand.
    /// </summary>
    public static void Retire(string legacyDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDatabasePath);

        if (!File.Exists(legacyDatabasePath))
        {
            return;
        }

        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string retired = legacyDatabasePath + ".merged-" + stamp;

        File.Move(legacyDatabasePath, retired);

        foreach (string suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(legacyDatabasePath + suffix))
            {
                File.Move(legacyDatabasePath + suffix, retired + suffix);
            }
        }
    }
}
