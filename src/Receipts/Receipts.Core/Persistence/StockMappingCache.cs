using Microsoft.Data.Sqlite;

namespace Receipts.Core.Persistence;

/// <summary>Cache of supplier product code → Pohoda stock code, so the pipeline does not ask mServer for the same card twice.</summary>
public sealed class StockMappingCache(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS stock_mapping (
            supplier_code TEXT PRIMARY KEY,
            pohoda_stock_code TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
        """;

    public async Task<string?> GetAsync(string supplierCode, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT pohoda_stock_code FROM stock_mapping WHERE supplier_code = $code";
        cmd.Parameters.AddWithValue("$code", supplierCode);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    public async Task SaveAsync(string supplierCode, string pohodaStockCode, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stock_mapping (supplier_code, pohoda_stock_code, created_at)
            VALUES ($code, $stockCode, $now)
            ON CONFLICT(supplier_code) DO UPDATE SET pohoda_stock_code = excluded.pohoda_stock_code
            """;
        cmd.Parameters.AddWithValue("$code", supplierCode);
        cmd.Parameters.AddWithValue("$stockCode", pohodaStockCode);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
