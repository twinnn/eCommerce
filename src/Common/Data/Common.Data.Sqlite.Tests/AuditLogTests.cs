using Common.Data.Sqlite;

namespace Common.Data.Sqlite.Tests;

public class AuditLogTests
{
    private static async Task<(SqliteDatabase Db, AuditLog Log)> OpenAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"audit-log-test-{Guid.NewGuid():N}.db");
        var db = await SqliteDatabase.OpenAsync(dbPath, [AuditLog.Schema]);
        return (db, new AuditLog(db.Connection));
    }

    [Fact]
    public async Task ReadAllAsync_returns_empty_list_when_nothing_was_appended()
    {
        var (db, log) = await OpenAsync();
        await using var _ = db;

        var entries = await log.ReadAllAsync();

        Assert.Empty(entries);
    }

    [Fact]
    public async Task AppendAsync_then_ReadAllAsync_returns_the_entry()
    {
        var (db, log) = await OpenAsync();
        await using var _ = db;

        await log.AppendAsync("invoice-ft2-260220.pdf", "Created new prijemka 2026/0042");

        var entries = await log.ReadAllAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("invoice-ft2-260220.pdf", entry.Source);
        Assert.Equal("Created new prijemka 2026/0042", entry.Message);
    }

    [Fact]
    public async Task ReadAllAsync_returns_entries_in_the_order_they_were_appended()
    {
        var (db, log) = await OpenAsync();
        await using var _ = db;

        await log.AppendAsync("a.pdf", "first");
        await log.AppendAsync("b.pdf", "second");

        var entries = await log.ReadAllAsync();

        Assert.Equal(["first", "second"], entries.Select(e => e.Message));
    }
}
