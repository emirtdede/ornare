using Microsoft.Data.Sqlite;
using Xunit;

namespace Ornare.Storage.Tests;

public class SqliteBootstrapTests
{
    [Fact]
    public void Winsqlite3BundleInitializesAndExecutesQuery()
    {
        // Initialize SQLitePCLRaw bundle_winsqlite3 targeting Windows winsqlite3.dll
        SQLitePCL.Batteries_V2.Init();

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version();";
        var version = command.ExecuteScalar() as string;

        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }
}
