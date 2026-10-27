using Common.Data.Sqlite;

namespace Common.Data.Sqlite.Tests;

public class ProcessedFileTrackerTests
{
    private static async Task<(SqliteDatabase Db, ProcessedFileTracker Tracker)> OpenAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"processed-files-test-{Guid.NewGuid():N}.db");
        var db = await SqliteDatabase.OpenAsync(dbPath, [ProcessedFileTracker.Schema]);
        return (db, new ProcessedFileTracker(db.Connection));
    }

    [Fact]
    public async Task IsProcessedAsync_returns_false_for_an_unknown_sha256()
    {
        var (db, tracker) = await OpenAsync();
        await using var _ = db;

        Assert.False(await tracker.IsProcessedAsync("unknown-sha"));
    }

    [Fact]
    public async Task MarkProcessedAsync_then_IsProcessedAsync_returns_true()
    {
        var (db, tracker) = await OpenAsync();
        await using var _ = db;

        await tracker.MarkProcessedAsync("sha-1", "C:\\invoices\\a.pdf", "proposal-1");

        Assert.True(await tracker.IsProcessedAsync("sha-1"));
    }

    [Fact]
    public async Task MarkProcessedAsync_upserts_on_the_same_sha256()
    {
        var (db, tracker) = await OpenAsync();
        await using var _ = db;

        await tracker.MarkProcessedAsync("sha-1", "C:\\invoices\\a.pdf", "proposal-1");
        await tracker.MarkProcessedAsync("sha-1", "C:\\invoices\\a.pdf", "proposal-2");

        Assert.True(await tracker.IsProcessedAsync("sha-1"));
    }

    [Fact]
    public async Task ForgetAsync_removes_tracking_for_the_given_reference_id()
    {
        var (db, tracker) = await OpenAsync();
        await using var _ = db;

        await tracker.MarkProcessedAsync("sha-1", "C:\\invoices\\a.pdf", "proposal-1");
        await tracker.ForgetAsync("proposal-1");

        Assert.False(await tracker.IsProcessedAsync("sha-1"));
    }
}
