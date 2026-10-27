# Gabel.PrijemkaSync Merge — PR1: Shared Libraries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three new, additive-only shared library projects to the eCommerce solution — `Common.Data.Sqlite`, `Common.Documents.PdfReader`, `Pohoda.MServer` — ported and generalized from `Gabel.PrijemkaSync`, with no changes to any existing project's behavior.

**Architecture:** Each library is a focused, single-purpose `net10.0` class library with its own xUnit test project. `Common.Data.Sqlite` and `Common.Documents.PdfReader` are generic, domain-agnostic helpers generalized out of Gabel's `SqliteStateStore` and `PdfRowReader`. `Pohoda.MServer` is the one shared low-level Pohoda mServer transport client (auth, encoding, error parsing, process launch, multi-instance support), replacing the transport code duplicated between `Gabel.PrijemkaSync.Pohoda` and eCommerce's existing `Pohoda.Server/MServer/MServer.cs` — but this PR only *adds* it; nothing is wired up to consume it yet (that starts in PR2/PR4 of the parent plan).

**Tech Stack:** .NET 10.0, `Microsoft.Data.Sqlite`, `UglyToad.PdfPig`, `Microsoft.Extensions.Http`/`Options`/`Hosting` abstractions, xUnit.

**Spec:** [docs/superpowers/specs/2026-08-24-gabel-prijemkasync-merge-design.md](../specs/2026-08-24-gabel-prijemkasync-merge-design.md) — this plan implements PR1 of that spec's §5 rollout sequencing. PR2–PR6 (Receipts logic, Receipts executables, migrating `Pohoda.Server`, re-verifying existing Jobs, retiring the old repo) are separate, later plans — each is written right before it starts, so it can be grounded in what PR1 actually produced rather than speculated now.

## Global Constraints

- Target framework: `net10.0` for every new project (matches every existing project in `eCommerce.sln`).
- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` on every new project (matches existing convention, e.g. `Pohoda.Server.csproj`).
- Pin `Microsoft.Extensions.*` package versions to `10.0.11` (matches `Pohoda.Server.csproj`, `Gabel.PrijemkaSync.Pohoda.csproj`).
- Pin `Microsoft.Data.Sqlite` to `10.0.11`, `UglyToad.PdfPig` to `1.7.0-custom-5` — exact versions already proven to restore and work, taken from `Gabel.PrijemkaSync`'s csproj files.
- Test projects pin `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.4`, `Microsoft.NET.Test.Sdk` `17.14.1`, `coverlet.collector` `6.0.4`, `<IsPackable>false</IsPackable>`, and a global `<Using Include="Xunit" />` — matches `Gabel.PrijemkaSync.Tests.csproj` exactly.
- No ORM — `Microsoft.Data.Sqlite` used directly with raw SQL (matches the rest of the portfolio's "stateless integration middleware" design and Gabel's own explicit choice).
- No EF Core, no test mocking framework beyond what's needed (a hand-written fake `HttpMessageHandler` is enough for HTTP tests — don't add Moq/NSubstitute for this PR).
- Every new project gets added to `src/eCommerce.sln` via `dotnet sln add` (never hand-edit the `.sln` GUIDs).
- Windows-1250 is the wire encoding for every Pohoda mServer request/response body — this is a hard protocol requirement, not a default to reconsider.
- mServer authentication is the `STW-Authorization` header (Basic base64 `username:password`), never the standard HTTP `Authorization` header — mServer silently 401s on the latter.
- This PR adds code; it does not delete or modify `src/Pohoda/Pohoda.Server/MServer/MServer.cs` or any existing job/import project. Existing `dotnet build`/`dotnet test` behavior for the current solution must be unaffected — verify this in Task 9.

---

## File Structure

```
src/Common/Data/
  Common.Data.Sqlite/
    Common.Data.Sqlite.csproj
    SqliteDatabase.cs          — connection open + schema bootstrap, domain-agnostic
    ProcessedFileTracker.cs    — sha256-based idempotency tracking (generalized from Gabel's processed_files table)
    AuditLog.cs                — append-only timestamped log (generalized from Gabel's audit_log table)
  Common.Data.Sqlite.Tests/
    Common.Data.Sqlite.Tests.csproj
    SqliteDatabaseTests.cs
    ProcessedFileTrackerTests.cs
    AuditLogTests.cs

src/Common/Documents/
  Common.Documents.PdfReader/
    Common.Documents.PdfReader.csproj
    PdfRowReader.cs            — public, generalized from Gabel's internal PdfRowReader
  Common.Documents.PdfReader.Tests/
    Common.Documents.PdfReader.Tests.csproj
    PdfRowReaderTests.cs

src/Pohoda/
  Pohoda.MServer/
    Pohoda.MServer.csproj
    MServerConfiguration.cs
    PohodaResponseException.cs
    PohodaResponseParser.cs
    MServerClient.cs
    MServerLauncher.cs
    ServiceCollectionExtensions.cs   — multi-instance (keyed DI) registration, new vs. Gabel's single-instance version
  Pohoda.MServer.Tests/
    Pohoda.MServer.Tests.csproj
    PohodaResponseParserTests.cs
    MServerClientTests.cs
    MServerLauncherTests.cs
    ServiceCollectionExtensionsTests.cs
```

Each library has exactly one responsibility (connection/schema bootstrap; idempotency tracking; audit logging; PDF row extraction; mServer transport). `SqliteDatabase` doesn't know about `processed_files` or `audit_log` — those tables are owned by the classes that use them, and `SqliteDatabase.OpenAsync` takes the schema DDL as a parameter so a future `Receipts.Core` can add its own tables (`stock_mapping`, `proposals`) onto the same connection without `Common.Data.Sqlite` ever needing to change.

---

## Task 1: `Common.Data.Sqlite` — project scaffold + `SqliteDatabase`

**Files:**
- Create: `src/Common/Data/Common.Data.Sqlite/Common.Data.Sqlite.csproj`
- Create: `src/Common/Data/Common.Data.Sqlite/SqliteDatabase.cs`
- Create: `src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj`
- Test: `src/Common/Data/Common.Data.Sqlite.Tests/SqliteDatabaseTests.cs`

**Interfaces:**
- Produces: `Common.Data.Sqlite.SqliteDatabase` — `static Task<SqliteDatabase> OpenAsync(string dbPath, IReadOnlyList<string> schemaStatements, CancellationToken ct = default)`, `SqliteConnection Connection { get; }`, implements `IAsyncDisposable`. Tasks 2 and 3 consume `SqliteDatabase.Connection`.

- [ ] **Step 1: Scaffold the project and add it to the solution**

```bash
cd C:\git\eCommerce
dotnet new classlib -n Common.Data.Sqlite -o src/Common/Data/Common.Data.Sqlite --framework net10.0
dotnet sln src/eCommerce.sln add src/Common/Data/Common.Data.Sqlite/Common.Data.Sqlite.csproj
rm src/Common/Data/Common.Data.Sqlite/Class1.cs
```

Edit `src/Common/Data/Common.Data.Sqlite/Common.Data.Sqlite.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.11" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Scaffold the test project and add it to the solution**

```bash
dotnet new xunit -n Common.Data.Sqlite.Tests -o src/Common/Data/Common.Data.Sqlite.Tests --framework net10.0
dotnet sln src/eCommerce.sln add src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
dotnet add src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj reference src/Common/Data/Common.Data.Sqlite/Common.Data.Sqlite.csproj
```

Edit `src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj` so the generated package versions match the rest of the solution and `IsPackable`/global usings are set:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Common.Data.Sqlite\Common.Data.Sqlite.csproj" />
  </ItemGroup>

</Project>
```

Delete the generated `src/Common/Data/Common.Data.Sqlite.Tests/UnitTest1.cs`.

- [ ] **Step 3: Write the failing test**

Create `src/Common/Data/Common.Data.Sqlite.Tests/SqliteDatabaseTests.cs`:

```csharp
using Common.Data.Sqlite;

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
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
```

Expected: build error — `SqliteDatabase` does not exist yet.

- [ ] **Step 5: Implement `SqliteDatabase`**

Create `src/Common/Data/Common.Data.Sqlite/SqliteDatabase.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite;

/// <summary>
/// Opens a SQLite connection and runs the given schema DDL statements. Deliberately domain-agnostic —
/// callers own their own table shapes (see <see cref="ProcessedFileTracker"/>, <see cref="AuditLog"/>
/// for the two generic tables this library ships, and any consumer-specific tables it doesn't know about).
/// </summary>
public sealed class SqliteDatabase : IAsyncDisposable
{
    public SqliteConnection Connection { get; }

    private SqliteDatabase(SqliteConnection connection) => Connection = connection;

    public static async Task<SqliteDatabase> OpenAsync(
        string dbPath, IReadOnlyList<string> schemaStatements, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);

        foreach (var statement in schemaStatements)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return new SqliteDatabase(connection);
    }

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
```

Expected: both tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/eCommerce.sln src/Common/Data/Common.Data.Sqlite src/Common/Data/Common.Data.Sqlite.Tests
git commit -m "feat: add Common.Data.Sqlite with SqliteDatabase bootstrap"
```

---

## Task 2: `Common.Data.Sqlite` — `ProcessedFileTracker`

**Files:**
- Create: `src/Common/Data/Common.Data.Sqlite/ProcessedFileTracker.cs`
- Test: `src/Common/Data/Common.Data.Sqlite.Tests/ProcessedFileTrackerTests.cs`

**Interfaces:**
- Consumes: `Common.Data.Sqlite.SqliteDatabase.Connection` (from Task 1).
- Produces: `Common.Data.Sqlite.ProcessedFileTracker` — `const string Schema`, constructor `ProcessedFileTracker(SqliteConnection connection)`, `Task<bool> IsProcessedAsync(string sha256, CancellationToken ct = default)`, `Task MarkProcessedAsync(string sha256, string filePath, string referenceId, CancellationToken ct = default)`, `Task ForgetAsync(string referenceId, CancellationToken ct = default)`.

- [ ] **Step 1: Write the failing test**

Create `src/Common/Data/Common.Data.Sqlite.Tests/ProcessedFileTrackerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
```

Expected: build error — `ProcessedFileTracker` does not exist yet.

- [ ] **Step 3: Implement `ProcessedFileTracker`**

Create `src/Common/Data/Common.Data.Sqlite/ProcessedFileTracker.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite;

/// <summary>
/// Sha256-based idempotency tracking: has a given file already been processed, and under which
/// reference id (e.g. a proposal id). Generalized from Gabel.PrijemkaSync's `processed_files` table —
/// `proposal_id` renamed `reference_id` since this is no longer receipt-proposal-specific.
/// </summary>
public sealed class ProcessedFileTracker(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS processed_files (
            sha256 TEXT PRIMARY KEY,
            file_path TEXT NOT NULL,
            reference_id TEXT NOT NULL,
            processed_at TEXT NOT NULL
        );
        """;

    public async Task<bool> IsProcessedAsync(string sha256, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM processed_files WHERE sha256 = $sha256";
        cmd.Parameters.AddWithValue("$sha256", sha256);
        var count = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        return count > 0;
    }

    public async Task MarkProcessedAsync(
        string sha256, string filePath, string referenceId, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO processed_files (sha256, file_path, reference_id, processed_at)
            VALUES ($sha256, $path, $referenceId, $now)
            ON CONFLICT(sha256) DO UPDATE SET reference_id = excluded.reference_id, processed_at = excluded.processed_at
            """;
        cmd.Parameters.AddWithValue("$sha256", sha256);
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.Parameters.AddWithValue("$referenceId", referenceId);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ForgetAsync(string referenceId, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM processed_files WHERE reference_id = $referenceId";
        cmd.Parameters.AddWithValue("$referenceId", referenceId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
```

Expected: all tests PASS (Task 1's tests plus these 4).

- [ ] **Step 5: Commit**

```bash
git add src/Common/Data/Common.Data.Sqlite/ProcessedFileTracker.cs src/Common/Data/Common.Data.Sqlite.Tests/ProcessedFileTrackerTests.cs
git commit -m "feat: add ProcessedFileTracker to Common.Data.Sqlite"
```

---

## Task 3: `Common.Data.Sqlite` — `AuditLog`

**Files:**
- Create: `src/Common/Data/Common.Data.Sqlite/AuditLog.cs`
- Test: `src/Common/Data/Common.Data.Sqlite.Tests/AuditLogTests.cs`

**Interfaces:**
- Consumes: `Common.Data.Sqlite.SqliteDatabase.Connection` (from Task 1).
- Produces: `Common.Data.Sqlite.AuditLog` — `const string Schema`, constructor `AuditLog(SqliteConnection connection)`, `Task AppendAsync(string source, string message, CancellationToken ct = default)`, `Task<IReadOnlyList<(DateTimeOffset Timestamp, string Source, string Message)>> ReadAllAsync(CancellationToken ct = default)`.

- [ ] **Step 1: Write the failing test**

Create `src/Common/Data/Common.Data.Sqlite.Tests/AuditLogTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
```

Expected: build error — `AuditLog` does not exist yet.

- [ ] **Step 3: Implement `AuditLog`**

Create `src/Common/Data/Common.Data.Sqlite/AuditLog.cs`:

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Common.Data.Sqlite;

/// <summary>
/// Append-only, chronological audit trail. Generalized from Gabel.PrijemkaSync's `audit_log` table —
/// `source_file` renamed `source` since this is no longer PDF-invoice-specific.
/// </summary>
public sealed class AuditLog(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS audit_log (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp TEXT NOT NULL,
            source TEXT NOT NULL,
            message TEXT NOT NULL
        );
        """;

    public async Task AppendAsync(string source, string message, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO audit_log (timestamp, source, message) VALUES ($ts, $source, $msg)";
        cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$source", source);
        cmd.Parameters.AddWithValue("$msg", message);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<(DateTimeOffset Timestamp, string Source, string Message)>> ReadAllAsync(
        CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT timestamp, source, message FROM audit_log ORDER BY id";

        var results = new List<(DateTimeOffset, string, string)>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return results;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Common/Data/Common.Data.Sqlite.Tests/Common.Data.Sqlite.Tests.csproj
```

Expected: all tests PASS (10 tests total across Tasks 1–3).

- [ ] **Step 5: Commit**

```bash
git add src/Common/Data/Common.Data.Sqlite/AuditLog.cs src/Common/Data/Common.Data.Sqlite.Tests/AuditLogTests.cs
git commit -m "feat: add AuditLog to Common.Data.Sqlite"
```

---

## Task 4: `Common.Documents.PdfReader` — `PdfRowReader`

**Files:**
- Create: `src/Common/Documents/Common.Documents.PdfReader/Common.Documents.PdfReader.csproj`
- Create: `src/Common/Documents/Common.Documents.PdfReader/PdfRowReader.cs`
- Create: `src/Common/Documents/Common.Documents.PdfReader.Tests/Common.Documents.PdfReader.Tests.csproj`
- Test: `src/Common/Documents/Common.Documents.PdfReader.Tests/PdfRowReaderTests.cs`

**Interfaces:**
- Produces: `Common.Documents.PdfReader.PdfRowReader` — `static IReadOnlyList<string> ReadRows(string pdfPath)`, `static IReadOnlyList<string> ReadRows(byte[] pdfBytes)`.

- [ ] **Step 1: Scaffold the project and add it to the solution**

```bash
cd C:\git\eCommerce
dotnet new classlib -n Common.Documents.PdfReader -o src/Common/Documents/Common.Documents.PdfReader --framework net10.0
dotnet sln src/eCommerce.sln add src/Common/Documents/Common.Documents.PdfReader/Common.Documents.PdfReader.csproj
rm src/Common/Documents/Common.Documents.PdfReader/Class1.cs
```

Edit `src/Common/Documents/Common.Documents.PdfReader/Common.Documents.PdfReader.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="UglyToad.PdfPig" Version="1.7.0-custom-5" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Scaffold the test project and add it to the solution**

```bash
dotnet new xunit -n Common.Documents.PdfReader.Tests -o src/Common/Documents/Common.Documents.PdfReader.Tests --framework net10.0
dotnet sln src/eCommerce.sln add src/Common/Documents/Common.Documents.PdfReader.Tests/Common.Documents.PdfReader.Tests.csproj
dotnet add src/Common/Documents/Common.Documents.PdfReader.Tests/Common.Documents.PdfReader.Tests.csproj reference src/Common/Documents/Common.Documents.PdfReader/Common.Documents.PdfReader.csproj
```

Edit `src/Common/Documents/Common.Documents.PdfReader.Tests/Common.Documents.PdfReader.Tests.csproj` to match the convention (same shape as Task 1 Step 2), and additionally add a `PackageReference` for `UglyToad.PdfPig` (the test builds a PDF fixture in-memory with PdfPig's writer, so it needs the package directly, not just transitively):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="UglyToad.PdfPig" Version="1.7.0-custom-5" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Common.Documents.PdfReader\Common.Documents.PdfReader.csproj" />
  </ItemGroup>

</Project>
```

Delete the generated `src/Common/Documents/Common.Documents.PdfReader.Tests/UnitTest1.cs`.

- [ ] **Step 3: Write the failing test**

Create `src/Common/Documents/Common.Documents.PdfReader.Tests/PdfRowReaderTests.cs`. This builds a small PDF in-memory with PdfPig's own writer (`PdfDocumentBuilder`) rather than shipping a binary fixture file — verified working against `UglyToad.PdfPig` `1.7.0-custom-5` directly before writing this plan:

```csharp
using Common.Documents.PdfReader;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Common.Documents.PdfReader.Tests;

public class PdfRowReaderTests
{
    private static byte[] BuildTwoRowPdf()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        // Same row: two words placed on the same horizontal line, left to right.
        page.AddText("Hello", 12, new PdfPoint(25, 700), font);
        page.AddText("World", 12, new PdfPoint(70, 700), font);

        // A second, lower row.
        page.AddText("Second", 12, new PdfPoint(25, 650), font);
        page.AddText("Row", 12, new PdfPoint(80, 650), font);

        return builder.Build();
    }

    [Fact]
    public void ReadRows_from_bytes_groups_words_on_the_same_line_into_one_row()
    {
        var rows = PdfRowReader.ReadRows(BuildTwoRowPdf());

        Assert.Equal(["Hello World", "Second Row"], rows);
    }

    [Fact]
    public void ReadRows_from_a_file_path_matches_ReadRows_from_bytes()
    {
        var bytes = BuildTwoRowPdf();
        var path = Path.Combine(Path.GetTempPath(), $"pdf-row-reader-test-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(path, bytes);

            var rowsFromPath = PdfRowReader.ReadRows(path);
            var rowsFromBytes = PdfRowReader.ReadRows(bytes);

            Assert.Equal(rowsFromBytes, rowsFromPath);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test src/Common/Documents/Common.Documents.PdfReader.Tests/Common.Documents.PdfReader.Tests.csproj
```

Expected: build error — `PdfRowReader` does not exist yet.

- [ ] **Step 5: Implement `PdfRowReader`**

Create `src/Common/Documents/Common.Documents.PdfReader/PdfRowReader.cs`:

```csharp
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Common.Documents.PdfReader;

/// <summary>
/// Reconstructs a PDF's text as visual rows (top-to-bottom, left-to-right) by clustering words with
/// PdfPig's per-word bounding boxes. More reliable than trusting the raw content-stream text order for
/// simple single-column tables (e.g. invoice line items), which does not always follow reading order.
/// Generalized from Gabel.PrijemkaSync.Extraction's (internal, Gabel-specific) PdfRowReader — this
/// version has no knowledge of any particular document layout.
/// </summary>
public static class PdfRowReader
{
    private const double SameRowTolerance = 2.5;

    public static IReadOnlyList<string> ReadRows(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return ReadRows(document);
    }

    public static IReadOnlyList<string> ReadRows(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        return ReadRows(document);
    }

    private static IReadOnlyList<string> ReadRows(PdfDocument document)
    {
        var rows = new List<string>();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0)
            {
                continue;
            }

            var sorted = words
                .OrderByDescending(w => w.BoundingBox.Top)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var currentRow = new List<Word> { sorted[0] };
            var currentTop = sorted[0].BoundingBox.Top;

            for (var i = 1; i < sorted.Count; i++)
            {
                var word = sorted[i];
                if (Math.Abs(word.BoundingBox.Top - currentTop) <= SameRowTolerance)
                {
                    currentRow.Add(word);
                }
                else
                {
                    rows.Add(BuildRowText(currentRow));
                    currentRow = [word];
                    currentTop = word.BoundingBox.Top;
                }
            }

            rows.Add(BuildRowText(currentRow));
        }

        return rows;
    }

    private static string BuildRowText(List<Word> row) =>
        string.Join(' ', row.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test src/Common/Documents/Common.Documents.PdfReader.Tests/Common.Documents.PdfReader.Tests.csproj
```

Expected: both tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/eCommerce.sln src/Common/Documents/Common.Documents.PdfReader src/Common/Documents/Common.Documents.PdfReader.Tests
git commit -m "feat: add Common.Documents.PdfReader with PdfRowReader"
```

---

## Task 5: `Pohoda.MServer` — project scaffold + configuration, exception, and response parser

**Files:**
- Create: `src/Pohoda/Pohoda.MServer/Pohoda.MServer.csproj`
- Create: `src/Pohoda/Pohoda.MServer/MServerConfiguration.cs`
- Create: `src/Pohoda/Pohoda.MServer/PohodaResponseException.cs`
- Create: `src/Pohoda/Pohoda.MServer/PohodaResponseParser.cs`
- Create: `src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj`
- Test: `src/Pohoda/Pohoda.MServer.Tests/PohodaResponseParserTests.cs`

**Interfaces:**
- Produces:
  - `Pohoda.MServer.MServerConfiguration` — `const string SectionName = "Pohoda"`, `required string BaseUrl`, `required int Port`, `required string ConfigName`, `string? PohodaExePath`, `required string Username`, `required string Password`, `required string Ico`, `required string DefaultStorageCode`, `required string DefaultTypePriceCode`, `Uri Endpoint { get; }`.
  - `Pohoda.MServer.PohodaResponseException : Exception` — `IReadOnlyList<string> Errors { get; }`, constructor `(string message, IReadOnlyList<string> errors)`.
  - `Pohoda.MServer.PohodaResult` — `record PohodaResult(bool Success, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, XDocument Document)`.
  - `Pohoda.MServer.PohodaResponseParser` — `static PohodaResult Parse(XDocument doc)`. Task 6's `MServerClient` consumes this directly.

- [ ] **Step 1: Scaffold the project and add it to the solution**

```bash
cd C:\git\eCommerce
dotnet new classlib -n Pohoda.MServer -o src/Pohoda/Pohoda.MServer --framework net10.0
dotnet sln src/eCommerce.sln add src/Pohoda/Pohoda.MServer/Pohoda.MServer.csproj
rm src/Pohoda/Pohoda.MServer/Class1.cs
```

Edit `src/Pohoda/Pohoda.MServer/Pohoda.MServer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.11" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.11" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.11" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Scaffold the test project and add it to the solution**

```bash
dotnet new xunit -n Pohoda.MServer.Tests -o src/Pohoda/Pohoda.MServer.Tests --framework net10.0
dotnet sln src/eCommerce.sln add src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
dotnet add src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj reference src/Pohoda/Pohoda.MServer/Pohoda.MServer.csproj
```

Edit `src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj` to match the standard test-project shape (same as Task 1 Step 2), plus a reference to `Microsoft.Extensions.Hosting` (needed by Task 8's DI test):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Pohoda.MServer\Pohoda.MServer.csproj" />
  </ItemGroup>

</Project>
```

Delete the generated `src/Pohoda/Pohoda.MServer.Tests/UnitTest1.cs`.

- [ ] **Step 3: Write the failing test**

Create `src/Pohoda/Pohoda.MServer.Tests/PohodaResponseParserTests.cs`:

```csharp
using System.Xml.Linq;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class PohodaResponseParserTests
{
    [Fact]
    public void Parse_returns_success_when_no_element_has_an_error_or_warning_state()
    {
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="ok" />
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_reports_failure_from_an_outer_error_state_with_a_note_attribute()
    {
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="error" note="Missing required element stk:stockType" />
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.False(result.Success);
        Assert.Contains("Missing required element stk:stockType", result.Errors);
    }

    [Fact]
    public void Parse_reports_failure_from_a_nested_error_state_even_when_the_outer_state_is_ok()
    {
        // Confirmed live against mServer "GoNoow" (see Gabel.PrijemkaSync docs/technicka-dokumentacia.md
        // section 3.2): the outer responsePackItem can say state="ok" while a nested element says
        // state="error" — this is the exact silent-failure gap the old eCommerce client never caught.
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="ok">
                <stk:stockItemResponse xmlns:stk="http://www.stormware.cz/schema/version_2/stock.xsd" state="error">
                  <rdc:importDetails xmlns:rdc="http://www.stormware.cz/schema/version_2/responseDoc.xsd">
                    <rdc:detail>
                      <rdc:errno>137</rdc:errno>
                      <rdc:note>Invalid column name 'Name'</rdc:note>
                      <rdc:XPath>/stk:stock/stk:stockHeader</rdc:XPath>
                    </rdc:detail>
                  </rdc:importDetails>
                </stk:stockItemResponse>
              </rsp:responsePackItem>
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("[137]") && e.Contains("Invalid column name 'Name'"));
    }

    [Fact]
    public void Parse_collects_warnings_without_failing_the_result()
    {
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem id="item1" state="warning" />
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.True(result.Success);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Parse_returns_failure_for_a_document_with_no_root_element_state()
    {
        var doc = new XDocument();

        var result = PohodaResponseParser.Parse(doc);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: build error — `MServerConfiguration`, `PohodaResponseException`, `PohodaResponseParser` don't exist yet.

- [ ] **Step 5: Implement `MServerConfiguration`**

Create `src/Pohoda/Pohoda.MServer/MServerConfiguration.cs`:

```csharp
namespace Pohoda.MServer;

/// <summary>Connection settings for one Pohoda mServer instance (e.g. "StwTest" or "GoNoow").</summary>
public sealed class MServerConfiguration
{
    public const string SectionName = "Pohoda";

    public required string BaseUrl { get; init; } = "http://127.0.0.1";
    public required int Port { get; init; }

    /// <summary>Name of the mServer configuration as set up in Pohoda (Účetní jednotky → Databáze → POHODA mServer). Used for the auto-start via `pohoda.exe /http start|stop "name"`.</summary>
    public required string ConfigName { get; init; }

    /// <summary>Full path to Pohoda.exe, used only to auto-start mServer when it is not already running.</summary>
    public string? PohodaExePath { get; init; }

    public required string Username { get; init; }
    public required string Password { get; init; }

    /// <summary>IČO of the accounting unit this mServer instance serves.</summary>
    public required string Ico { get; init; }

    /// <summary>Default "storage" (členění skladu) code used when creating new příjemky/skladové karty.</summary>
    public required string DefaultStorageCode { get; init; }

    /// <summary>Default price-group ("cenová skupina") code used when auto-creating a new skladová karta.</summary>
    public required string DefaultTypePriceCode { get; init; }

    public Uri Endpoint => new($"{BaseUrl}:{Port}/");
}
```

- [ ] **Step 6: Implement `PohodaResponseException`**

Create `src/Pohoda/Pohoda.MServer/PohodaResponseException.cs`:

```csharp
namespace Pohoda.MServer;

/// <summary>
/// Thrown when POHODA mServer responds with a transport-level failure (bad HTTP status, unparsable XML)
/// or with a `responsePack`/`responsePackItem` state of "error".
/// </summary>
public sealed class PohodaResponseException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public PohodaResponseException(string message, IReadOnlyList<string> errors)
        : base(message + (errors.Count > 0 ? " " + string.Join(" | ", errors) : string.Empty))
    {
        Errors = errors;
    }
}
```

- [ ] **Step 7: Implement `PohodaResponseParser`**

Create `src/Pohoda/Pohoda.MServer/PohodaResponseParser.cs`:

```csharp
using System.Xml.Linq;

namespace Pohoda.MServer;

public sealed record PohodaResult(bool Success, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, XDocument Document);

/// <summary>
/// Parses a POHODA mServer `responsePack` XML into a success/error result. Matches elements by
/// local name only (namespace-agnostic) since mServer error structures are not fully documented
/// publicly and prefixes are not guaranteed — this is intentionally defensive rather than strict.
///
/// mServer nests a `state` attribute at MULTIPLE levels, and an outer level being "ok" does NOT
/// guarantee the operation succeeded (confirmed live against mServer "GoNoow" — a `createStockRequest`
/// missing a required element came back with `responsePackItem state="ok"` while a nested
/// `stk:stockItemResponse state="error"` carried the real error). This parser therefore walks every
/// element in the document and checks every `state` attribute it finds, rather than only the top level.
/// </summary>
public static class PohodaResponseParser
{
    public static PohodaResult Parse(XDocument doc)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var root = doc.Root;

        if (root is null)
        {
            return new PohodaResult(false, ["Prázdna alebo neplatná odpoveď z mServer (chýba koreňový element)."], warnings, doc);
        }

        var success = true;

        foreach (var el in root.DescendantsAndSelf())
        {
            var stateAttr = el.Attribute("state");
            if (stateAttr is null)
            {
                continue;
            }

            if (IsErrorState(stateAttr))
            {
                success = false;
                errors.AddRange(ExtractErrorMessages(el));
            }
            else if (IsWarningState(stateAttr))
            {
                warnings.Add(DescribeItem(el));
            }
        }

        errors = errors.Distinct().ToList();
        if (errors.Count > 0)
        {
            success = false;
        }

        return new PohodaResult(success, errors, warnings, doc);
    }

    private static bool IsErrorState(XAttribute state) =>
        string.Equals(state.Value, "error", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningState(XAttribute state) =>
        string.Equals(state.Value, "warning", StringComparison.OrdinalIgnoreCase);

    private static string DescribeItem(XElement item)
    {
        var id = item.Attribute("id")?.Value ?? item.Name.LocalName;
        return $"{item.Name.LocalName} '{id}': warning";
    }

    /// <summary>
    /// Extracts human-readable error text from an element whose `state` attribute is "error".
    /// Handles the two shapes confirmed live: (1) the message directly in this element's own
    /// `note` attribute (schema-validation failures), and (2) a nested `rdc:importDetails/rdc:detail`
    /// with `errno`/`note`/`XPath` child elements (business-rule validation failures). Falls back to
    /// a dedicated `&lt;error&gt;` child (documented shape, not yet observed live) for robustness.
    /// </summary>
    private static IEnumerable<string> ExtractErrorMessages(XElement el)
    {
        var ownNote = el.Attribute("note")?.Value;
        if (!string.IsNullOrWhiteSpace(ownNote))
        {
            yield return ownNote;
        }

        foreach (var detail in el.Descendants().Where(d => d.Name.LocalName == "detail"))
        {
            var errno = detail.Elements().FirstOrDefault(c => c.Name.LocalName == "errno")?.Value;
            var note = detail.Elements().FirstOrDefault(c => c.Name.LocalName == "note")?.Value;
            var xPath = detail.Elements().FirstOrDefault(c => c.Name.LocalName == "XPath")?.Value;

            if (string.IsNullOrWhiteSpace(note))
            {
                continue;
            }

            var message = string.IsNullOrWhiteSpace(errno) ? note : $"[{errno}] {note}";
            yield return string.IsNullOrWhiteSpace(xPath) ? message : $"{message} ({xPath})";
        }

        foreach (var errorEl in el.Elements().Where(e => e.Name.LocalName == "error"))
        {
            var number = errorEl.Elements().FirstOrDefault(e => e.Name.LocalName == "errorNumber")?.Value;
            var note = errorEl.Elements().FirstOrDefault(e => e.Name.LocalName is "note" or "text")?.Value ?? errorEl.Value.Trim();
            yield return string.IsNullOrWhiteSpace(number) ? note : $"[{number}] {note}";
        }
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: all 5 tests PASS.

- [ ] **Step 9: Commit**

```bash
git add src/eCommerce.sln src/Pohoda/Pohoda.MServer src/Pohoda/Pohoda.MServer.Tests
git commit -m "feat: add Pohoda.MServer with config, error handling, and response parser"
```

---

## Task 6: `Pohoda.MServer` — `MServerClient`

**Files:**
- Create: `src/Pohoda/Pohoda.MServer/MServerClient.cs`
- Test: `src/Pohoda/Pohoda.MServer.Tests/MServerClientTests.cs`

**Interfaces:**
- Consumes: `Pohoda.MServer.PohodaResponseParser.Parse` and `Pohoda.MServer.PohodaResponseException` (Task 5).
- Produces: `Pohoda.MServer.MServerClient` — constructor `(HttpClient httpClient, ILogger<MServerClient> logger)`, `Task<bool> IsAvailableAsync(CancellationToken ct = default)`, `Task<PohodaResult> SendAsync(XDocument requestDataPack, CancellationToken ct = default)`, `Task<PohodaResult> SendOrThrowAsync(XDocument requestDataPack, CancellationToken ct = default)`. Task 7's `MServerLauncher` consumes `IsAvailableAsync`.

- [ ] **Step 1: Write the failing test**

Create `src/Pohoda/Pohoda.MServer.Tests/MServerClientTests.cs`. This uses a hand-written fake `HttpMessageHandler` (no live mServer, no mocking library needed) so `SendAsync`/`IsAvailableAsync` can be tested against canned Windows-1250-encoded responses:

```csharp
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class MServerClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private static Encoding Windows1250()
    {
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
    }

    private static HttpResponseMessage Windows1250Response(HttpStatusCode status, string xmlBody)
    {
        var bytes = Windows1250().GetBytes(xmlBody);
        return new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) };
    }

    private static MServerClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new FakeHandler(respond)) { BaseAddress = new Uri("http://127.0.0.1:444/") };
        return new MServerClient(httpClient, NullLogger<MServerClient>.Instance);
    }

    [Fact]
    public async Task IsAvailableAsync_returns_true_when_status_endpoint_reports_mServer()
    {
        var client = CreateClient(req =>
        {
            Assert.Equal("status", req.RequestUri!.AbsolutePath.TrimStart('/'));
            return Windows1250Response(HttpStatusCode.OK, "<status><name>mServer</name></status>");
        });

        Assert.True(await client.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_returns_false_on_a_non_success_status_code()
    {
        var client = CreateClient(_ => Windows1250Response(HttpStatusCode.Unauthorized, ""));

        Assert.False(await client.IsAvailableAsync());
    }

    [Fact]
    public async Task SendAsync_returns_a_successful_result_for_an_ok_response()
    {
        var client = CreateClient(req =>
        {
            Assert.Equal("xml", req.RequestUri!.AbsolutePath.TrimStart('/'));
            return Windows1250Response(HttpStatusCode.OK, """
                <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
                  <rsp:responsePackItem state="ok" />
                </rsp:responsePack>
                """);
        });

        var result = await client.SendAsync(new XDocument(new XElement("dummy")));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SendAsync_throws_PohodaResponseException_on_a_non_success_http_status()
    {
        var client = CreateClient(_ => Windows1250Response(HttpStatusCode.Unauthorized, ""));

        await Assert.ThrowsAsync<PohodaResponseException>(() => client.SendAsync(new XDocument(new XElement("dummy"))));
    }

    [Fact]
    public async Task SendOrThrowAsync_throws_when_the_parsed_result_is_not_successful()
    {
        var client = CreateClient(_ => Windows1250Response(HttpStatusCode.OK, """
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="error" note="boom" />
            </rsp:responsePack>
            """));

        var ex = await Assert.ThrowsAsync<PohodaResponseException>(
            () => client.SendOrThrowAsync(new XDocument(new XElement("dummy"))));
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task SendAsync_round_trips_windows_1250_bytes_correctly()
    {
        // "Skladová karta" — the accented characters only round-trip correctly if the request/response
        // bytes are handled as Windows-1250, not UTF-8. This is the exact class of bug the previous
        // eCommerce integration had.
        var client = CreateClient(req => Windows1250Response(HttpStatusCode.OK, """
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="ok" note="Skladová karta zapísaná" />
            </rsp:responsePack>
            """));

        var result = await client.SendAsync(new XDocument(new XElement("dummy", "Skladová karta")));

        Assert.True(result.Success);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: build error — `MServerClient` does not exist yet.

- [ ] **Step 3: Implement `MServerClient`**

Create `src/Pohoda/Pohoda.MServer/MServerClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Pohoda.MServer;

/// <summary>
/// Thin HTTP client for POHODA mServer. Auth header and base address are configured by the caller
/// (see <see cref="ServiceCollectionExtensions.AddPohodaMServer"/>) via the standard
/// <c>IHttpClientFactory</c> named-client pattern. Every response is parsed for `state`/`error`
/// (via <see cref="PohodaResponseParser"/>) before being trusted.
/// </summary>
public sealed class MServerClient
{
    private static readonly Encoding Windows1250;
    private readonly HttpClient _http;
    private readonly ILogger<MServerClient> _logger;

    static MServerClient()
    {
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        Windows1250 = Encoding.GetEncoding(1250);
    }

    public MServerClient(HttpClient httpClient, ILogger<MServerClient> logger)
    {
        _http = httpClient;
        _logger = logger;
    }

    /// <summary>Non-throwing health-check against `GET /status`.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("status", ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await ReadWindows1250Async(response, ct);
            return body.Contains("mServer", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "mServer /status health-check zlyhal.");
            return false;
        }
    }

    /// <summary>Sends a dataPack request and returns the parsed result without throwing on a Pohoda-level error.</summary>
    public async Task<PohodaResult> SendAsync(XDocument requestDataPack, CancellationToken ct = default)
    {
        var bodyBytes = Windows1250.GetBytes(BuildXmlWithDeclaration(requestDataPack));
        using var content = new ByteArrayContent(bodyBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "windows-1250" };

        using var response = await _http.PostAsync("xml", content, ct);
        var responseText = await ReadWindows1250Async(response, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new PohodaResponseException(
                $"mServer HTTP {(int)response.StatusCode} ({ExplainStatusCode(response.StatusCode)}).",
                [responseText]);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(responseText);
        }
        catch (Exception ex)
        {
            throw new PohodaResponseException("Odpoveď mServer sa nedala naparsovať ako XML.", [ex.Message]);
        }

        var result = PohodaResponseParser.Parse(doc);
        foreach (var warning in result.Warnings)
        {
            _logger.LogWarning("mServer warning: {Warning}", warning);
        }

        if (!result.Success)
        {
            _logger.LogWarning("mServer odpoveď so stavom error, raw XML: {RawResponse}", responseText);
        }
        else
        {
            _logger.LogDebug("mServer raw response: {RawResponse}", responseText);
        }

        return result;
    }

    /// <summary>Same as <see cref="SendAsync"/> but throws <see cref="PohodaResponseException"/> if the result is not successful.</summary>
    public async Task<PohodaResult> SendOrThrowAsync(XDocument requestDataPack, CancellationToken ct = default)
    {
        var result = await SendAsync(requestDataPack, ct);
        if (!result.Success)
        {
            throw new PohodaResponseException("mServer vrátil chybový stav.", result.Errors);
        }

        return result;
    }

    private static string BuildXmlWithDeclaration(XDocument doc)
    {
        var body = doc.Root!.ToString(SaveOptions.DisableFormatting);
        return "<?xml version=\"1.0\" encoding=\"Windows-1250\"?>" + body;
    }

    private static async Task<string> ReadWindows1250Async(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return Windows1250.GetString(bytes);
    }

    private static string ExplainStatusCode(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "chýbajú alebo sú neplatné prihlasovacie údaje (STW-Authorization)",
        HttpStatusCode.Forbidden => "užívateľ nemá právo otvoriť účtovnú jednotku / POHODA mServer",
        HttpStatusCode.NotFound => "nesprávna URL cesta (chýba /xml)",
        HttpStatusCode.RequestTimeout => "vypršal čas na spracovanie požiadavky",
        _ => code.ToString()
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: all tests PASS (5 from Task 5, 6 from this task).

- [ ] **Step 5: Commit**

```bash
git add src/Pohoda/Pohoda.MServer/MServerClient.cs src/Pohoda/Pohoda.MServer.Tests/MServerClientTests.cs
git commit -m "feat: add MServerClient to Pohoda.MServer"
```

---

## Task 7: `Pohoda.MServer` — `MServerLauncher`

**Files:**
- Create: `src/Pohoda/Pohoda.MServer/MServerLauncher.cs`
- Test: `src/Pohoda/Pohoda.MServer.Tests/MServerLauncherTests.cs`

**Interfaces:**
- Consumes: `Pohoda.MServer.MServerClient.IsAvailableAsync` (Task 6), `Pohoda.MServer.MServerConfiguration` (Task 5).
- Produces: `Pohoda.MServer.MServerAvailability` — `readonly record struct MServerAvailability(bool IsAvailable, bool StartedByUs)`. `Pohoda.MServer.MServerLauncher` — constructor `(MServerClient client, MServerConfiguration config, ILogger<MServerLauncher> logger)`, `Task<MServerAvailability> EnsureRunningAsync(CancellationToken ct = default)`, `Task StopAsync(CancellationToken ct = default)`.

Note: the constructor takes a plain `MServerConfiguration` rather than `IOptions<MServerConfiguration>` (unlike Gabel's original) — with the multi-instance keyed DI registration in Task 8, the *specific* instance's already-resolved config is what gets passed in, not the single unnamed default `IOptions<T>` only supports.

- [ ] **Step 1: Write the failing test**

Create `src/Pohoda/Pohoda.MServer.Tests/MServerLauncherTests.cs`. Live process-spawn + retry-until-mServer-responds is out of scope for a unit test (it needs a real `Pohoda.exe`, matching how Gabel.PrijemkaSync itself only verified this live, never in an automated test) — these tests cover the decision logic that doesn't require a real process:

```csharp
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class MServerLauncherTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private static MServerConfiguration Config(string? pohodaExePath) => new()
    {
        BaseUrl = "http://127.0.0.1",
        Port = 444,
        ConfigName = "GoNoow",
        PohodaExePath = pohodaExePath,
        Username = "user",
        Password = "pass",
        Ico = "53870441",
        DefaultStorageCode = "GoNoow",
        DefaultTypePriceCode = "0"
    };

    private static MServerClient AlwaysUnavailableClient()
    {
        var httpClient = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))
        {
            BaseAddress = new Uri("http://127.0.0.1:444/")
        };
        return new MServerClient(httpClient, NullLogger<MServerClient>.Instance);
    }

    private static MServerClient AlwaysAvailableClient()
    {
        var httpClient = new HttpClient(new FakeHandler(_ =>
        {
            var bytes = Encoding.UTF8.GetBytes("<status><name>mServer</name></status>");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }))
        { BaseAddress = new Uri("http://127.0.0.1:444/") };
        return new MServerClient(httpClient, NullLogger<MServerClient>.Instance);
    }

    [Fact]
    public async Task EnsureRunningAsync_returns_available_without_starting_anything_when_mServer_already_responds()
    {
        var launcher = new MServerLauncher(AlwaysAvailableClient(), Config(pohodaExePath: null), NullLogger<MServerLauncher>.Instance);

        var result = await launcher.EnsureRunningAsync();

        Assert.True(result.IsAvailable);
        Assert.False(result.StartedByUs);
    }

    [Fact]
    public async Task EnsureRunningAsync_skips_auto_start_and_returns_unavailable_when_no_exe_path_is_configured()
    {
        var launcher = new MServerLauncher(AlwaysUnavailableClient(), Config(pohodaExePath: null), NullLogger<MServerLauncher>.Instance);

        var result = await launcher.EnsureRunningAsync();

        Assert.False(result.IsAvailable);
        Assert.False(result.StartedByUs);
    }

    [Fact]
    public async Task EnsureRunningAsync_skips_auto_start_and_returns_unavailable_when_the_configured_exe_path_does_not_exist()
    {
        var launcher = new MServerLauncher(
            AlwaysUnavailableClient(), Config(pohodaExePath: @"C:\does\not\exist\Pohoda.exe"), NullLogger<MServerLauncher>.Instance);

        var result = await launcher.EnsureRunningAsync();

        Assert.False(result.IsAvailable);
        Assert.False(result.StartedByUs);
    }

    [Fact]
    public async Task StopAsync_does_nothing_when_mServer_is_already_unavailable()
    {
        var launcher = new MServerLauncher(AlwaysUnavailableClient(), Config(pohodaExePath: null), NullLogger<MServerLauncher>.Instance);

        // Must not throw even with no exe path configured and nothing running.
        await launcher.StopAsync();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: build error — `MServerLauncher`/`MServerAvailability` don't exist yet.

- [ ] **Step 3: Implement `MServerLauncher`**

Create `src/Pohoda/Pohoda.MServer/MServerLauncher.cs`:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Pohoda.MServer;

/// <summary>Result of <see cref="MServerLauncher.EnsureRunningAsync"/>.</summary>
public readonly record struct MServerAvailability(bool IsAvailable, bool StartedByUs);

/// <summary>
/// Ensures POHODA mServer is running before the client makes a request, starting it via
/// `pohoda.exe /http start "&lt;ConfigName&gt;"` if it isn't — and can stop it again afterwards via
/// `pohoda.exe /http stop "&lt;ConfigName&gt;"`. Invokes Pohoda.exe directly via
/// <see cref="ProcessStartInfo.ArgumentList"/> (no `cmd.exe` in between).
///
/// Callers should only call <see cref="StopAsync"/> when <see cref="MServerAvailability.StartedByUs"/>
/// was true — i.e. only stop an mServer instance this application itself started, never one that was
/// already running (it may be serving other clients, e.g. Pohoda E1 itself or another integration).
/// </summary>
public sealed class MServerLauncher
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)
    ];

    private readonly MServerClient _client;
    private readonly MServerConfiguration _config;
    private readonly ILogger<MServerLauncher> _logger;

    public MServerLauncher(MServerClient client, MServerConfiguration config, ILogger<MServerLauncher> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    /// <summary>Checks `/status` and starts mServer if needed. <see cref="MServerAvailability.StartedByUs"/> tells the caller whether it should later call <see cref="StopAsync"/>.</summary>
    public async Task<MServerAvailability> EnsureRunningAsync(CancellationToken ct = default)
    {
        if (await _client.IsAvailableAsync(ct))
        {
            return new MServerAvailability(true, StartedByUs: false);
        }

        if (!TryGetExePath(out var exePath))
        {
            _logger.LogWarning(
                "mServer neodpovedá a 'Pohoda:PohodaExePath' nie je nastavená na existujúci Pohoda.exe — automatický štart preskočený.");
            return new MServerAvailability(false, StartedByUs: false);
        }

        _logger.LogInformation("mServer neodpovedá, spúšťam konfiguráciu '{ConfigName}' cez {ExePath}.", _config.ConfigName, exePath);

        if (!RunPohodaHttpCommand(exePath, "start"))
        {
            return new MServerAvailability(false, StartedByUs: false);
        }

        foreach (var delay in RetryDelays)
        {
            await Task.Delay(delay, ct);
            if (await _client.IsAvailableAsync(ct))
            {
                _logger.LogInformation("mServer '{ConfigName}' beží.", _config.ConfigName);
                return new MServerAvailability(true, StartedByUs: true);
            }
        }

        _logger.LogError("mServer '{ConfigName}' neodpovedal ani po pokuse o automatický štart.", _config.ConfigName);
        return new MServerAvailability(false, StartedByUs: false);
    }

    /// <summary>Stops the mServer configuration via `pohoda.exe /http stop`. Only call this for an instance this application started itself.</summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!TryGetExePath(out var exePath))
        {
            return;
        }

        if (!await _client.IsAvailableAsync(ct))
        {
            return;
        }

        _logger.LogInformation("Zastavujem mServer '{ConfigName}'.", _config.ConfigName);
        RunPohodaHttpCommand(exePath, "stop");

        foreach (var delay in RetryDelays)
        {
            await Task.Delay(delay, ct);
            if (!await _client.IsAvailableAsync(ct))
            {
                _logger.LogInformation("mServer '{ConfigName}' zastavený.", _config.ConfigName);
                return;
            }
        }

        _logger.LogWarning("mServer '{ConfigName}' po pokuse o zastavenie stále odpovedá.", _config.ConfigName);
    }

    private bool TryGetExePath(out string exePath)
    {
        exePath = _config.PohodaExePath ?? string.Empty;
        return !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);
    }

    private bool RunPohodaHttpCommand(string exePath, string action)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = exePath;
            process.StartInfo.ArgumentList.Add("/http");
            process.StartInfo.ArgumentList.Add(action);
            process.StartInfo.ArgumentList.Add(_config.ConfigName);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pohoda.exe /http {Action} '{ConfigName}' zlyhalo.", action, _config.ConfigName);
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: all tests PASS (11 from Tasks 5–6, 4 from this task).

- [ ] **Step 5: Commit**

```bash
git add src/Pohoda/Pohoda.MServer/MServerLauncher.cs src/Pohoda/Pohoda.MServer.Tests/MServerLauncherTests.cs
git commit -m "feat: add MServerLauncher to Pohoda.MServer"
```

---

## Task 8: `Pohoda.MServer` — multi-instance `ServiceCollectionExtensions`

**Files:**
- Create: `src/Pohoda/Pohoda.MServer/ServiceCollectionExtensions.cs`
- Test: `src/Pohoda/Pohoda.MServer.Tests/ServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `Pohoda.MServer.MServerConfiguration`, `MServerClient`, `MServerLauncher` (Tasks 5–7).
- Produces: `Pohoda.MServer.ServiceCollectionExtensions.AddPohodaMServer(this IServiceCollection services, IConfiguration configuration, string instanceKey)`. Registers, keyed by `instanceKey`: bound `MServerConfiguration` (named options), a named `HttpClient`, a keyed `MServerClient` singleton, a keyed `MServerLauncher` singleton. This is what PR2/PR3/PR4 of the parent plan will call once per mServer instance (`"StwTest"`, `"GoNoow"`).

- [ ] **Step 1: Write the failing test**

Create `src/Pohoda/Pohoda.MServer.Tests/ServiceCollectionExtensionsTests.cs`. This mirrors the keyed-DI + named-HttpClient + named-options pattern verified directly against `Microsoft.Extensions.Hosting`/`Microsoft.Extensions.Http` before writing this plan:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class ServiceCollectionExtensionsTests
{
    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pohoda:StwTest:BaseUrl"] = "http://127.0.0.1",
            ["Pohoda:StwTest:Port"] = "5336",
            ["Pohoda:StwTest:ConfigName"] = "StwTest",
            ["Pohoda:StwTest:Username"] = "stwtest-user",
            ["Pohoda:StwTest:Password"] = "stwtest-pass",
            ["Pohoda:StwTest:Ico"] = "53870441",
            ["Pohoda:StwTest:DefaultStorageCode"] = "AzetCool",
            ["Pohoda:StwTest:DefaultTypePriceCode"] = "0",

            ["Pohoda:GoNoow:BaseUrl"] = "http://127.0.0.1",
            ["Pohoda:GoNoow:Port"] = "444",
            ["Pohoda:GoNoow:ConfigName"] = "GoNoow",
            ["Pohoda:GoNoow:Username"] = "gonoow-user",
            ["Pohoda:GoNoow:Password"] = "gonoow-pass",
            ["Pohoda:GoNoow:Ico"] = "53870441",
            ["Pohoda:GoNoow:DefaultStorageCode"] = "GoNoow",
            ["Pohoda:GoNoow:DefaultTypePriceCode"] = "0"
        });

        builder.Services.AddPohodaMServer(builder.Configuration, "StwTest");
        builder.Services.AddPohodaMServer(builder.Configuration, "GoNoow");

        return builder.Build();
    }

    [Fact]
    public void AddPohodaMServer_registers_a_keyed_MServerClient_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<MServerClient>("StwTest");
        var goNoow = host.Services.GetKeyedService<MServerClient>("GoNoow");

        Assert.NotNull(stwTest);
        Assert.NotNull(goNoow);
        Assert.NotSame(stwTest, goNoow);
    }

    [Fact]
    public void AddPohodaMServer_registers_a_keyed_MServerLauncher_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<MServerLauncher>("StwTest");
        var goNoow = host.Services.GetKeyedService<MServerLauncher>("GoNoow");

        Assert.NotNull(stwTest);
        Assert.NotNull(goNoow);
        Assert.NotSame(stwTest, goNoow);
    }

    [Fact]
    public void AddPohodaMServer_configures_a_distinct_named_HttpClient_per_instance()
    {
        using var host = BuildHost();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();

        var stwTestClient = factory.CreateClient("StwTest");
        var goNoowClient = factory.CreateClient("GoNoow");

        Assert.Equal(new Uri("http://127.0.0.1:5336/"), stwTestClient.BaseAddress);
        Assert.Equal(new Uri("http://127.0.0.1:444/"), goNoowClient.BaseAddress);
    }

    [Fact]
    public void AddPohodaMServer_sets_the_STW_Authorization_header_not_the_standard_Authorization_header()
    {
        using var host = BuildHost();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient("GoNoow");

        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.True(client.DefaultRequestHeaders.Contains("STW-Authorization"));
        var expected = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("gonoow-user:gonoow-pass"));
        Assert.Equal(expected, client.DefaultRequestHeaders.GetValues("STW-Authorization").Single());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: build error — `AddPohodaMServer` does not exist yet.

- [ ] **Step 3: Implement `ServiceCollectionExtensions`**

Create `src/Pohoda/Pohoda.MServer/ServiceCollectionExtensions.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pohoda.MServer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers one Pohoda mServer instance under <paramref name="instanceKey"/>: bound
    /// <see cref="MServerConfiguration"/> (from config section <c>Pohoda:&lt;instanceKey&gt;</c>), a
    /// named <see cref="HttpClient"/> (base address + STW-Authorization header), and keyed
    /// <see cref="MServerClient"/>/<see cref="MServerLauncher"/> singletons. Call once per mServer
    /// instance eCommerce needs to talk to (e.g. "StwTest" for the existing Jobs, "GoNoow" for Receipts)
    /// — each instance is independently resolvable via keyed DI.
    /// </summary>
    public static IServiceCollection AddPohodaMServer(
        this IServiceCollection services, IConfiguration configuration, string instanceKey)
    {
        services.AddOptions<MServerConfiguration>(instanceKey)
            .Bind(configuration.GetSection($"{MServerConfiguration.SectionName}:{instanceKey}"));

        services.AddHttpClient(instanceKey, (sp, http) =>
        {
            var config = sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get(instanceKey);

            http.BaseAddress = config.Endpoint;

            // POHODA mServer does NOT use the standard HTTP `Authorization` header — it requires a
            // custom `STW-Authorization` header. Setting `http.DefaultRequestHeaders.Authorization`
            // instead is silently wrong: .NET sends it as `Authorization`, which mServer ignores, and
            // the request fails with HTTP 401.
            http.DefaultRequestHeaders.Add(
                "STW-Authorization",
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}")));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        });

        services.AddKeyedSingleton<MServerClient>(instanceKey, (sp, key) =>
            new MServerClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient((string)key!),
                sp.GetRequiredService<ILogger<MServerClient>>()));

        services.AddKeyedSingleton<MServerLauncher>(instanceKey, (sp, key) =>
            new MServerLauncher(
                sp.GetRequiredKeyedService<MServerClient>(key),
                sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get((string)key!),
                sp.GetRequiredService<ILogger<MServerLauncher>>()));

        return services;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Pohoda/Pohoda.MServer.Tests/Pohoda.MServer.Tests.csproj
```

Expected: all tests PASS (15 from Tasks 5–7, 4 from this task — 19 total).

- [ ] **Step 5: Commit**

```bash
git add src/Pohoda/Pohoda.MServer/ServiceCollectionExtensions.cs src/Pohoda/Pohoda.MServer.Tests/ServiceCollectionExtensionsTests.cs
git commit -m "feat: add multi-instance DI registration to Pohoda.MServer"
```

---

## Task 9: Full-solution verification

**Files:** none (verification only).

**Interfaces:** none — this task checks that Tasks 1–8 didn't disturb anything already in `eCommerce.sln`.

- [ ] **Step 1: Build the entire solution**

```bash
dotnet build src/eCommerce.sln
```

Expected: succeeds, including every pre-existing project (`Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`, `Import.Hrackys`, `Pohoda.Server`, `Nop.ApiConsumer`, `Expando.Connector`, etc.) alongside the 3 new library projects and their 3 test projects.

- [ ] **Step 2: Run every test project in the solution**

```bash
dotnet test src/eCommerce.sln
```

Expected: all tests pass — 10 (`Common.Data.Sqlite.Tests`) + 2 (`Common.Documents.PdfReader.Tests`) + 19 (`Pohoda.MServer.Tests`) = 31 tests, 0 failures. (There were no test projects in the solution before this PR, so this is also the first time `dotnet test src/eCommerce.sln` does anything.)

- [ ] **Step 3: Confirm no existing file was modified**

```bash
git diff --stat main -- src/Pohoda/Pohoda.Server src/Pohoda/Pohoda.Contract src/Jobs src/Imports src/Nop src/Expando src/Common/Contract src/Common/Caching src/Common/Email src/Common/Readers
```

Expected: empty output — this PR only adds new files under `src/Common/Data/`, `src/Common/Documents/`, `src/Pohoda/Pohoda.MServer/`, `src/Pohoda/Pohoda.MServer.Tests/`, and modifies `src/eCommerce.sln` (to register the new projects).

- [ ] **Step 4: Update the solution README to mention the new shared libraries**

Add a short note to `README.md`'s "Štruktúra riešenia" and "Moduly a ich funkcionalita" sections listing the three new projects under `Common/` and `Pohoda/`, one sentence each, matching the existing README's style and language (Slovak). Keep it purely descriptive — no mention of Gabel.PrijemkaSync yet (that lands with the modules that actually consume these libraries, in PR2+).

- [ ] **Step 5: Commit the README update**

```bash
git add README.md
git commit -m "docs: document new Common.Data.Sqlite, Common.Documents.PdfReader, Pohoda.MServer projects"
```

---

## Self-Review

**Spec coverage** (against `docs/superpowers/specs/2026-08-24-gabel-prijemkasync-merge-design.md` §3, §5 PR1, §7):
- `Common.Documents.PdfReader` generalized from Gabel's `PdfRowReader` — Task 4. ✅
- `Common.Data.Sqlite` generalized state-store helper — Tasks 1–3 (`SqliteDatabase`, `ProcessedFileTracker`, `AuditLog`). ✅
- `Pohoda.MServer` shared low-level client: error/warning parsing, process launcher without `cmd.exe`, Windows-1250 encoding, multi-instance keyed DI — Tasks 5–8. ✅
- PR1 additive-only, no existing consumer touched — Task 9 verifies this explicitly. ✅
- Config section shape `Pohoda:<instanceKey>:*` per spec §7 — implemented in Task 8, tested in `ServiceCollectionExtensionsTests`. ✅
- `IReceiptService`/`IStockService` extensions, `Pohoda.Server` migration, `Receipts.*` projects, `Jobs.Receipts.*` — correctly out of scope for PR1 (spec §5 PR2–PR4), not included here.

**Placeholder scan:** no TBD/TODO, no "add appropriate error handling"-style steps — every step has literal, complete code. `PohodaResponseParser`'s doc-comment references "confirmed live" facts from Gabel's own technical documentation (§3.2 of `Gabel.PrijemkaSync/docs/technicka-dokumentacia.md`), not a placeholder.

**Type consistency:** `MServerClient(HttpClient, ILogger<MServerClient>)` (Task 6) matches the construction call in `ServiceCollectionExtensions` (Task 8). `MServerLauncher(MServerClient, MServerConfiguration, ILogger<MServerLauncher>)` (Task 7) matches its construction call in Task 8. `SqliteDatabase.Connection` (Task 1, type `SqliteConnection`) matches the constructor parameter type used by `ProcessedFileTracker` (Task 2) and `AuditLog` (Task 3). `ProcessedFileTracker.Schema`/`AuditLog.Schema` (both `const string`) match how they're passed to `SqliteDatabase.OpenAsync(string, IReadOnlyList<string>, ...)` in their respective tests.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-08-24-gabel-prijemkasync-merge-pr1-shared-libraries.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
