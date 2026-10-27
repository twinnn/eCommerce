using Common.Data.Sqlite;
using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite.Tests;

public class SqliteDatabaseTests
{
    [Fact]
    public async Task OpenAsync_creates_the_database_file_and_runs_schema_statements()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-db-test-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = await SqliteDatabase.OpenAsync(dbPath,
            [
                "CREATE TABLE probe (id INTEGER PRIMARY KEY, value TEXT NOT NULL);"
            ]);

            using var insert = db.Connection.CreateCommand();
            insert.CommandText = "INSERT INTO probe (value) VALUES ('hello')";
            await insert.ExecuteNonQueryAsync();

            using var select = db.Connection.CreateCommand();
            select.CommandText = "SELECT value FROM probe WHERE id = 1";
            var value = await select.ExecuteScalarAsync();

            Assert.Equal("hello", value);
            Assert.True(File.Exists(dbPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task OpenAsync_is_idempotent_when_schema_uses_IF_NOT_EXISTS()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlite-db-test-{Guid.NewGuid():N}.db");
        try
        {
            string[] schema = ["CREATE TABLE IF NOT EXISTS probe (id INTEGER PRIMARY KEY);"];

            await using (var first = await SqliteDatabase.OpenAsync(dbPath, schema))
            {
            }

            // Opening again against the same file with the same schema must not throw.
            await using var second = await SqliteDatabase.OpenAsync(dbPath, schema);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
