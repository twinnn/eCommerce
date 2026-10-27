# Gabel.PrijemkaSync Merge — PR2: Receipts Libraries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the supplier-agnostic receipt-sync libraries (`Receipts.Contract`, `Receipts.Core`) plus the first supplier plugin (`Receipts.Gabel`) to `eCommerce.sln`, ported from `C:\git\Gabel.PrijemkaSync` onto the PR1 shared libraries, together with the Pohoda-side contracts and mServer-backed services they need (`IReceiptService`, `IStockCardService` in `Pohoda.Contract`; `ReceiptService`, `StockCardService` in `Pohoda.Server`) — without changing the behavior of any existing Job.

**Architecture:** `Receipts.Contract` holds the domain models (`ParsedInvoice`, `ReceiptProposal`, …) and the two supplier plugin interfaces (`IInvoiceParser`, `IVariantDetector`). `Receipts.Core` holds the supplier-agnostic pipeline (`ProposalBuilder`, `ProposalApplier`, `PreviewWriter`) and its SQLite state (`ReceiptsStateStore`, composed from `Common.Data.Sqlite`'s `ProcessedFileTracker`/`AuditLog` plus two receipt-specific tables) and depends only on `Receipts.Contract`, `Pohoda.Contract`, `Common.Data.Sqlite`. `Receipts.Gabel` is the Gabel plugin (`GabelInvoiceParser`, `GabelVariantDetector`) on top of `Common.Documents.PdfReader`. Pohoda agenda access is behind `Pohoda.Contract` interfaces whose implementations live in `Pohoda.Server` and talk through PR1's `Pohoda.MServer.MServerClient` — the existing `Pohoda.Server` services and their `IStorageSystem` transport are not touched (that is PR4).

**Tech Stack:** .NET 10.0, `Microsoft.Data.Sqlite` (via `Common.Data.Sqlite`), `UglyToad.PdfPig` (via `Common.Documents.PdfReader`), `System.Xml.Linq`, `Microsoft.Extensions.Logging.Abstractions`/`Options`, xUnit.

**Spec:** [docs/superpowers/specs/2026-08-24-gabel-prijemkasync-merge-design.md](../specs/2026-08-24-gabel-prijemkasync-merge-design.md) — this plan implements PR2 of that spec's §5 rollout sequencing. PR1 (shared libraries) is complete on this branch (commits `72b28d4`..`7a05581`). PR3 (`Jobs.Receipts.Scan`/`Jobs.Receipts.Apply`), PR4 (migrate `Pohoda.Server` onto `Pohoda.MServer`), PR5, PR6 are later plans.

**Source being ported:** `C:\git\Gabel.PrijemkaSync\src\` — `Gabel.PrijemkaSync.Domain` (models), `Gabel.PrijemkaSync.Extraction` (`GabelInvoiceParser`, `GabelVariantDetector`), `Gabel.PrijemkaSync.Pohoda` (`PrijemkaClient`, `StockClient`, `Xml/*`), `Gabel.PrijemkaSync.App` (`ProposalBuilder`, `ProposalApplier`, `PreviewWriter`, `AppOptions`), `Gabel.PrijemkaSync.Data` (`SqliteStateStore`), `Gabel.PrijemkaSync.Tests` (parser tests + `TestData/*.pdf`). Every code block in this plan is the complete ported content — the implementer does not need to open the old repo, except to copy the PDF fixtures in Task 4.

## Global Constraints

- Target framework: `net10.0` for every new project; `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` on every **new** project.
- `Pohoda.Contract` and `Pohoda.Server` are **existing** projects **without** `ImplicitUsings` — every new file added to them needs explicit `using System; using System.Collections.Generic; using System.Linq; using System.Threading; using System.Threading.Tasks;` as applicable. Use file-scoped namespaces in new files (C# 14 on net10.0).
- Inside any `namespace Pohoda.Server.*` file, never write `MServer.Something` — the simple name `MServer` resolves to the legacy namespace `Pohoda.Server.MServer`, not to `Pohoda.MServer`. Always import with `using Pohoda.MServer;` and use the simple type names `MServerClient` / `MServerConfiguration`.
- Pin `Microsoft.Extensions.*` package versions to `10.0.11`. Test projects pin `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.4`, `Microsoft.NET.Test.Sdk` `17.14.1`, `coverlet.collector` `6.0.4`, `<IsPackable>false</IsPackable>`, global `<Using Include="Xunit" />` — identical to `Common.Data.Sqlite.Tests.csproj`.
- No ORM, no mocking framework — hand-written fakes (`FakeMServer`, `FakeStockCardService`, `FakeReceiptService`, `FakeVariantDetector`) are the test doubles.
- Every new project is added to `src/eCommerce.sln` via `dotnet sln add --solution-folder <Folder>` (never hand-edit `.sln` GUIDs). Solution folders: `Pohoda` for `Pohoda.Server.Tests`, `Receipts` for the four `Receipts.*` projects.
- Windows-1250 is the wire encoding and `STW-Authorization` the auth header — both are already handled inside `Pohoda.MServer.MServerClient`; the new services never touch HTTP directly.
- **Only three pre-existing files may change in this PR:** `src/eCommerce.sln` (project registration), `src/Pohoda/Pohoda.Server/Pohoda.Server.csproj` (one added `<ProjectReference>` to `Pohoda.MServer`), `README.md` (Task 9). No existing `.cs` file is modified. `IStockService`, `StockService`, `MServer.cs`, and every Jobs/Imports project stay byte-identical — Task 9 verifies this with `git diff`.
- `Receipts.Core` must not reference `Receipts.Gabel` or any other supplier project, and must not contain the string `Gabel` outside of doc comments. Supplier-specific behavior enters only through `IInvoiceParser`, `IVariantDetector`, and `ParsedInvoice.SupplierName`.
- C# identifiers use `Receipt` where Gabel used `Prijemka` (`PrijemkaProposal` → `ReceiptProposal`, `ExistingPrijemka` → `ExistingReceipt`, `PrijemkaLineToWrite` → `ReceiptLineToWrite`, `ExistingPrijemkaId` → `ExistingReceiptId`, `ResultPrijemkaNumber` → `ResultReceiptNumber`, `PrijemkaListLimit` → `ReceiptListLimit`). User-facing Slovak strings (warnings, audit lines, preview text) are carried over **verbatim** — they are part of the proven behavior and the user's documentation quotes them. Two deliberate exceptions: the preview table's first column is labeled `Kód dodávateľa` (was `Kód (Gabel)`), and the freight accessory-charge note is `Doprava (vedľajšie náklady)` (was `Doprava (SPESE TRASP.)`) — both because the code that emits them is supplier-agnostic.
- Business rules carry over unchanged (spec §3): only `FT n/nnnnnn` invoices parse (proforma skipped with the existing reason text); receipt matching is `Text.Contains(invoiceNumber, OrdinalIgnoreCase)` over the locally listed receipts; `AppendLines` never resends a line whose Pohoda stock code already exists in the receipt, differences are only warnings; freight always becomes `prijemkaAccessoryChargesItem`; master cards are suggested only when exactly one non-variant candidate remains, never linked.
- The XML wire shapes in `ReceiptService`/`StockCardService` (double-nested `stockItem`, `limit/count`, `requestPrijemka`/`requestStock` element namespaces, `Nazev LIKE`, `rateVAT="none"`, `stockType="card"`) are copied exactly from Gabel's live-verified code. They are asserted by tests in Tasks 2–3 and must not be "corrected" from schema reading.

---

## File Structure

```
src/Pohoda/
  Pohoda.Contract/                          (existing — additive)
    Models/ReceiptModels.cs                 — ExistingReceipt, ExistingReceiptLine, ReceiptLineToWrite records
    Models/StockNameMatch.cs                — StockNameMatch record
    Services/IReceiptService.cs             — příjemka agenda: ListRecentAsync / CreateAsync / UpdateAsync
    Services/IStockCardService.cs           — stock card ops Receipts needs: FindBySupplierCodeAsync / SearchByNameContainingAsync / CreateAsync
  Pohoda.Server/                            (existing — additive; csproj gains a reference to Pohoda.MServer)
    Xml/PohodaNamespaces.cs                 — Stormware XSD namespace constants
    Xml/DataPackBuilder.cs                  — dat:dataPack / dat:dataPackItem envelope
    Services/StockCardService.cs            — IStockCardService over MServerClient (from Gabel's StockClient)
    Services/ReceiptService.cs              — IReceiptService over MServerClient (from Gabel's PrijemkaClient)
  Pohoda.Server.Tests/                      NEW
    Pohoda.Server.Tests.csproj
    FakeMServer.cs                          — captures request dataPacks, replays canned responsePacks
    DataPackBuilderTests.cs
    StockCardServiceTests.cs
    ReceiptServiceTests.cs

src/Receipts/
  Receipts.Contract/                        NEW
    Receipts.Contract.csproj
    ParsedInvoice.cs                        — InvoiceLine, ParsedInvoice
    ReceiptProposal.cs                      — ReceiptProposalType, ProposalStatus, ReceiptLineProposal, ReceiptProposal
    IInvoiceParser.cs                       — supplier plugin: PDF → ParsedInvoice
    IVariantDetector.cs                     — supplier plugin: is this stock name a size variant?
  Receipts.Gabel/                           NEW
    Receipts.Gabel.csproj
    GabelVariantDetector.cs                 — IVariantDetector + static Detect(text)
    GabelInvoiceParser.cs                   — IInvoiceParser over Common.Documents.PdfReader
  Receipts.Core/                            NEW
    Receipts.Core.csproj
    Persistence/StockMappingCache.cs        — stock_mapping table (supplier code → Pohoda stock code)
    Persistence/ProposalRepository.cs       — proposals table (JSON line/warning columns)
    Persistence/ReceiptsStateStore.cs       — opens one SQLite DB with all four tables; exposes ProcessedFiles, Audit, StockMappings, Proposals
    MatchingOptions.cs                      — LookbackMonths, ReceiptListLimit
    ProposalBuilder.cs                      — ParsedInvoice + recent receipts → ReceiptProposal (read-only against Pohoda)
    ProposalApplier.cs                      — ReceiptProposal → Pohoda writes (the only writer)
    PreviewWriter.cs                        — Markdown preview for human approval
  Receipts.Tests/                           NEW
    Receipts.Tests.csproj
    TestData/*.pdf                          — 6 real Gabel PDFs copied from the old repo
    GabelInvoiceParserTests.cs              — moved unchanged (+ one SupplierName assertion)
    GabelVariantDetectorTests.cs
    Fakes/FakeStockCardService.cs
    Fakes/FakeReceiptService.cs
    Fakes/FakeVariantDetector.cs
    Fakes/Make.cs                           — small builders for ParsedInvoice / InvoiceLine / ReceiptProposal
    ReceiptsStateStoreTests.cs
    ProposalBuilderTests.cs
    ProposalApplierTests.cs
    PreviewWriterTests.cs
```

Dependency graph (arrows = ProjectReference): `Receipts.Gabel → Receipts.Contract, Common.Documents.PdfReader`; `Receipts.Core → Receipts.Contract, Pohoda.Contract, Common.Data.Sqlite`; `Pohoda.Server → Pohoda.MServer` (new edge, additive); `Receipts.Tests → Receipts.Core, Receipts.Gabel, Receipts.Contract, Pohoda.Contract, Common.Data.Sqlite`; `Pohoda.Server.Tests → Pohoda.Server, Pohoda.MServer, Pohoda.Contract`.

---

## Task 1: `Pohoda.Contract` receipt/stock-card contracts + `Pohoda.Server` XML envelope helpers + `Pohoda.Server.Tests` scaffold

**Files:**
- Create: `src/Pohoda/Pohoda.Contract/Models/ReceiptModels.cs`
- Create: `src/Pohoda/Pohoda.Contract/Models/StockNameMatch.cs`
- Create: `src/Pohoda/Pohoda.Contract/Services/IReceiptService.cs`
- Create: `src/Pohoda/Pohoda.Contract/Services/IStockCardService.cs`
- Modify: `src/Pohoda/Pohoda.Server/Pohoda.Server.csproj` (add one `<ProjectReference>`)
- Create: `src/Pohoda/Pohoda.Server/Xml/PohodaNamespaces.cs`
- Create: `src/Pohoda/Pohoda.Server/Xml/DataPackBuilder.cs`
- Create: `src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj`
- Test: `src/Pohoda/Pohoda.Server.Tests/DataPackBuilderTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces (used by Tasks 2, 3, 6, 7):
  - `Pohoda.Contract.Models.ExistingReceiptLine(string PohodaStockCode, string Text, string Unit, decimal Quantity, decimal UnitPrice, decimal DiscountPercent)`
  - `Pohoda.Contract.Models.ExistingReceipt(string PohodaId, string Number, string Text, IReadOnlyList<ExistingReceiptLine> Lines)`
  - `Pohoda.Contract.Models.ReceiptLineToWrite(string PohodaStockCode, string Text, decimal Quantity, string Unit, decimal UnitPrice, decimal DiscountPercent)`
  - `Pohoda.Contract.Models.StockNameMatch(string Code, string Name)`
  - `Pohoda.Contract.Services.IReceiptService` — `Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default)`, `Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)`, `Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)`
  - `Pohoda.Contract.Services.IStockCardService` — `Task<string?> FindBySupplierCodeAsync(string supplierCode, CancellationToken ct = default)`, `Task<IReadOnlyList<StockNameMatch>> SearchByNameContainingAsync(string nameContains, CancellationToken ct = default)`, `Task<string> CreateAsync(string code, string name, string? orderName, CancellationToken ct = default)`
  - `Pohoda.Server.Xml.PohodaNamespaces` — `public static readonly XNamespace Dat, Stk, ListStk, Prj, Lst, Ftr, Typ, Rsp`
  - `Pohoda.Server.Xml.DataPackBuilder.Wrap(string ico, string note, params XElement[] items) : XDocument`, `DataPackBuilder.ApplicationName = "eCommerce.Pohoda.Server"`

- [ ] **Step 1: Add the contract models and interfaces to `Pohoda.Contract`**

Create `src/Pohoda/Pohoda.Contract/Models/ReceiptModels.cs`:

```csharp
using System.Collections.Generic;

namespace Pohoda.Contract.Models;

/// <summary>
/// One line item read back from an existing příjemka in Pohoda. Carries price/discount/unit (not
/// just quantity) so that an <see cref="Services.IReceiptService.UpdateAsync"/> full-document resend
/// can reproduce existing lines byte-for-byte instead of risking overwriting them with zeros.
/// </summary>
public sealed record ExistingReceiptLine(string PohodaStockCode, string Text, string Unit, decimal Quantity, decimal UnitPrice, decimal DiscountPercent);

/// <summary>An existing příjemka document as returned by <see cref="Services.IReceiptService.ListRecentAsync"/>.</summary>
public sealed record ExistingReceipt(string PohodaId, string Number, string Text, IReadOnlyList<ExistingReceiptLine> Lines);

/// <summary>One line to write into a new or updated příjemka.</summary>
public sealed record ReceiptLineToWrite(string PohodaStockCode, string Text, decimal Quantity, string Unit, decimal UnitPrice, decimal DiscountPercent);
```

Create `src/Pohoda/Pohoda.Contract/Models/StockNameMatch.cs`:

```csharp
namespace Pohoda.Contract.Models;

/// <summary>One skladová karta match returned by a name search.</summary>
public sealed record StockNameMatch(string Code, string Name);
```

Create `src/Pohoda/Pohoda.Contract/Services/IReceiptService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pohoda.Contract.Models;

namespace Pohoda.Contract.Services;

/// <summary>Příjemka (goods receipt) agenda: list recent documents, create a new one, or update an existing one.</summary>
public interface IReceiptService
{
    /// <summary>
    /// Lists příjemky since <paramref name="since"/> (bounded by <paramref name="limit"/>) so the caller
    /// can locally search their <c>Text</c> for a substring (e.g. "FT 2/260220") — the Pohoda XML agenda
    /// has no server-side free-text filter for this document type.
    /// </summary>
    Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default);

    /// <summary>Creates a new příjemka and returns its Pohoda document number.</summary>
    Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing příjemka. Callers must pass the FULL desired line set (existing lines +
    /// newly discovered ones) — resending the complete list is the safe default until incremental
    /// append semantics are confirmed live.
    /// </summary>
    Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default);
}
```

Create `src/Pohoda/Pohoda.Contract/Services/IStockCardService.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pohoda.Contract.Models;

namespace Pohoda.Contract.Services;

/// <summary>
/// Skladová karta operations the receipt-sync pipeline needs: look a card up by the supplier's own
/// product code (stored verbatim in <c>stk:code</c>), search by partial name, and create a new card.
/// Transitional: PR4 of the merge folds these members into <see cref="IStockService"/> when the
/// existing <c>Pohoda.Server.StockService</c> moves onto <c>Pohoda.MServer</c>.
/// </summary>
public interface IStockCardService
{
    /// <summary>Returns the Pohoda stock code of the card whose <c>stk:code</c> equals <paramref name="supplierCode"/>, or null if none exists.</summary>
    Task<string?> FindBySupplierCodeAsync(string supplierCode, CancellationToken ct = default);

    /// <summary>Returns every card whose <c>stk:name</c> contains <paramref name="nameContains"/> (callers filter sibling variants themselves).</summary>
    Task<IReadOnlyList<StockNameMatch>> SearchByNameContainingAsync(string nameContains, CancellationToken ct = default);

    /// <summary>Creates a new card with the given code/name; <paramref name="orderName"/> (e.g. "120 cm") is written to <c>stk:orderName</c> when supplied. Returns the code.</summary>
    Task<string> CreateAsync(string code, string name, string? orderName, CancellationToken ct = default);
}
```

- [ ] **Step 2: Reference `Pohoda.MServer` from `Pohoda.Server` and add the XML helpers**

Edit `src/Pohoda/Pohoda.Server/Pohoda.Server.csproj` — inside the existing `<ItemGroup>` that holds the three `<ProjectReference>` lines, add one more line so the group reads:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\Common\Caching\Common.Caching\Common.Caching.csproj" />
    <ProjectReference Include="..\..\Common\Contract\Common.Contract\Common.Contract.csproj" />
    <ProjectReference Include="..\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\Pohoda.MServer\Pohoda.MServer.csproj" />
  </ItemGroup>
```

Nothing else in the csproj changes.

Create `src/Pohoda/Pohoda.Server/Xml/PohodaNamespaces.cs`:

```csharp
using System.Xml.Linq;

namespace Pohoda.Server.Xml;

/// <summary>Stormware XSD namespaces used across dataPack requests, confirmed against the published schemas at stormware.cz/xml/schema/version_2/.</summary>
public static class PohodaNamespaces
{
    public static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";
    public static readonly XNamespace Stk = "http://www.stormware.cz/schema/version_2/stock.xsd";
    public static readonly XNamespace ListStk = "http://www.stormware.cz/schema/version_2/list_stock.xsd";
    public static readonly XNamespace Prj = "http://www.stormware.cz/schema/version_2/prijemka.xsd";
    public static readonly XNamespace Lst = "http://www.stormware.cz/schema/version_2/list.xsd";
    public static readonly XNamespace Ftr = "http://www.stormware.cz/schema/version_2/filter.xsd";
    public static readonly XNamespace Typ = "http://www.stormware.cz/schema/version_2/type.xsd";
    public static readonly XNamespace Rsp = "http://www.stormware.cz/schema/version_2/response.xsd";
}
```

Create `src/Pohoda/Pohoda.Server/Xml/DataPackBuilder.cs`:

```csharp
using System;
using System.Linq;
using System.Xml.Linq;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Xml;

/// <summary>Builds the outer `dat:dataPack` / `dat:dataPackItem` envelope shared by every mServer request.</summary>
public static class DataPackBuilder
{
    /// <summary>Value of the dataPack `application` attribute — informational only, shown in Pohoda's XML log.</summary>
    public const string ApplicationName = "eCommerce.Pohoda.Server";

    public static XDocument Wrap(string ico, string note, params XElement[] items)
    {
        var root = new XElement(Dat + "dataPack",
            new XAttribute("id", DateTime.UtcNow.Ticks.ToString("x")),
            new XAttribute("ico", ico),
            new XAttribute("application", ApplicationName),
            new XAttribute("version", "2.0"),
            new XAttribute("note", note),
            items.Select((item, idx) => WrapItem(item, idx)));

        return new XDocument(root);
    }

    private static XElement WrapItem(XElement content, int index) =>
        new(Dat + "dataPackItem",
            new XAttribute("id", $"item{index + 1}"),
            new XAttribute("version", "2.0"),
            content);
}
```

- [ ] **Step 3: Scaffold `Pohoda.Server.Tests` and add it to the solution**

Create `src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj`:

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
    <ProjectReference Include="..\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\Pohoda.MServer\Pohoda.MServer.csproj" />
    <ProjectReference Include="..\Pohoda.Server\Pohoda.Server.csproj" />
  </ItemGroup>

</Project>
```

Run from the repo root:

```bash
dotnet sln src/eCommerce.sln add --solution-folder Pohoda src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj
```

- [ ] **Step 4: Write the failing test**

Create `src/Pohoda/Pohoda.Server.Tests/DataPackBuilderTests.cs`:

```csharp
using System.Xml.Linq;
using Pohoda.Server.Xml;

namespace Pohoda.Server.Tests;

public class DataPackBuilderTests
{
    private static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";

    [Fact]
    public void Wrap_builds_a_dataPack_envelope_with_one_numbered_item_per_element()
    {
        var doc = DataPackBuilder.Wrap("53870441", "test note", new XElement("first"), new XElement("second"));

        var root = doc.Root!;
        Assert.Equal(Dat + "dataPack", root.Name);
        Assert.Equal("53870441", root.Attribute("ico")!.Value);
        Assert.Equal("2.0", root.Attribute("version")!.Value);
        Assert.Equal("test note", root.Attribute("note")!.Value);
        Assert.Equal(DataPackBuilder.ApplicationName, root.Attribute("application")!.Value);
        Assert.False(string.IsNullOrEmpty(root.Attribute("id")?.Value));

        var items = root.Elements(Dat + "dataPackItem").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("item1", items[0].Attribute("id")!.Value);
        Assert.Equal("item2", items[1].Attribute("id")!.Value);
        Assert.Equal("2.0", items[0].Attribute("version")!.Value);
        Assert.Equal("first", items[0].Elements().Single().Name.LocalName);
        Assert.Equal("second", items[1].Elements().Single().Name.LocalName);
    }
}
```

- [ ] **Step 5: Build and run the tests**

Because Steps 1–2 already wrote the implementation (interfaces and helpers are declarative — there is no meaningful "red" state for a namespace constant table), the verification here is: build the whole solution to prove the contract additions and the new `Pohoda.Server → Pohoda.MServer` reference compile against every existing consumer, then run the new test project.

```bash
dotnet build src/eCommerce.sln
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: build succeeds with 0 errors (warnings from legacy projects are pre-existing); `Passed! - Failed: 0, Passed: 1`.

- [ ] **Step 6: Commit**

```bash
git add src/eCommerce.sln src/Pohoda/Pohoda.Contract/Models src/Pohoda/Pohoda.Contract/Services/IReceiptService.cs src/Pohoda/Pohoda.Contract/Services/IStockCardService.cs src/Pohoda/Pohoda.Server/Pohoda.Server.csproj src/Pohoda/Pohoda.Server/Xml src/Pohoda/Pohoda.Server.Tests
git commit -m "feat: add IReceiptService/IStockCardService contracts and Pohoda.Server XML envelope helpers"
```

---

## Task 2: `Pohoda.Server.StockCardService` over `MServerClient`

**Files:**
- Create: `src/Pohoda/Pohoda.Server/Services/StockCardService.cs`
- Create: `src/Pohoda/Pohoda.Server.Tests/FakeMServer.cs`
- Test: `src/Pohoda/Pohoda.Server.Tests/StockCardServiceTests.cs`

**Interfaces:**
- Consumes: `IStockCardService`, `StockNameMatch` (Task 1); `PohodaNamespaces`, `DataPackBuilder` (Task 1); `Pohoda.MServer.MServerClient.SendOrThrowAsync(XDocument, CancellationToken) : Task<PohodaResult>` and `PohodaResult.Document : XDocument` (PR1); `Pohoda.MServer.MServerConfiguration { Ico, DefaultStorageCode, DefaultTypePriceCode, … }` (PR1).
- Produces: `Pohoda.Server.Services.StockCardService(MServerClient client, MServerConfiguration config) : IStockCardService`; test helper `Pohoda.Server.Tests.FakeMServer` (reused by Task 3) with `Client`, `Config`, `Requests : List<XDocument>`, `RequestPayload(int index = 0) : XElement`, `Enqueue(string responseXml)`, `static OkResponse(string innerXml = "")`, `static ErrorResponse(string note)`.

- [ ] **Step 1: Write the shared fake mServer test helper**

Create `src/Pohoda/Pohoda.Server.Tests/FakeMServer.cs`:

```csharp
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.MServer;

namespace Pohoda.Server.Tests;

/// <summary>
/// Stands in for POHODA mServer: records every dataPack the service under test sends (decoded from
/// the Windows-1250 wire bytes) and replies with canned responsePack XML, in FIFO order — an OK
/// response with an empty item when nothing was enqueued.
/// </summary>
internal sealed class FakeMServer
{
    private static readonly Encoding Windows1250 = GetWindows1250();
    private readonly Queue<string> _responses = new();

    public List<XDocument> Requests { get; } = [];

    public MServerClient Client { get; }

    public MServerConfiguration Config { get; } = new()
    {
        BaseUrl = "http://127.0.0.1",
        Port = 444,
        ConfigName = "GoNoow",
        Username = "user",
        Password = "secret",
        Ico = "53870441",
        DefaultStorageCode = "GoNoow",
        DefaultTypePriceCode = "Cenova skupina 1"
    };

    public FakeMServer()
    {
        var http = new HttpClient(new Handler(this)) { BaseAddress = Config.Endpoint };
        Client = new MServerClient(http, NullLogger<MServerClient>.Instance);
    }

    public FakeMServer Enqueue(string responseXml)
    {
        _responses.Enqueue(responseXml);
        return this;
    }

    /// <summary>The single dataPackItem payload of the N-th captured request (default: the first).</summary>
    public XElement RequestPayload(int index = 0) =>
        Requests[index].Root!.Elements().Single().Elements().Single();

    public static string OkResponse(string innerXml = "") => $"""
        <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd"
                          xmlns:lStk="http://www.stormware.cz/schema/version_2/list_stock.xsd"
                          xmlns:stk="http://www.stormware.cz/schema/version_2/stock.xsd"
                          xmlns:lst="http://www.stormware.cz/schema/version_2/list.xsd"
                          xmlns:pri="http://www.stormware.cz/schema/version_2/prijemka.xsd"
                          xmlns:typ="http://www.stormware.cz/schema/version_2/type.xsd"
                          xmlns:rdc="http://www.stormware.cz/schema/version_2/documentresponse.xsd"
                          version="2.0" state="ok">
          <rsp:responsePackItem version="2.0" id="item1" state="ok">{innerXml}</rsp:responsePackItem>
        </rsp:responsePack>
        """;

    public static string ErrorResponse(string note) => $"""
        <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" version="2.0" state="ok">
          <rsp:responsePackItem version="2.0" id="item1" state="error" note="{note}" />
        </rsp:responsePack>
        """;

    private static Encoding GetWindows1250()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
    }

    private sealed class Handler(FakeMServer owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            owner.Requests.Add(XDocument.Parse(Windows1250.GetString(bytes)));

            var body = owner._responses.Count > 0 ? owner._responses.Dequeue() : OkResponse();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Windows1250.GetBytes(body)) };
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `src/Pohoda/Pohoda.Server.Tests/StockCardServiceTests.cs`:

```csharp
using Pohoda.MServer;
using Pohoda.Server.Services;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Tests;

public class StockCardServiceTests
{
    private static (FakeMServer Server, StockCardService Service) Create()
    {
        var server = new FakeMServer();
        return (server, new StockCardService(server.Client, server.Config));
    }

    private const string OneStockListing = """
        <lStk:listStock version="2.0">
          <lStk:stock version="2.0">
            <stk:stockHeader>
              <stk:id>1</stk:id>
              <stk:code>7009190321200</stk:code>
              <stk:EAN>8030515107460</stk:EAN>
              <stk:name>Gabel DH PLATINUM SERIES 120 cm</stk:name>
            </stk:stockHeader>
          </lStk:stock>
        </lStk:listStock>
        """;

    [Fact]
    public async Task FindBySupplierCodeAsync_sends_a_listStockRequest_filtered_by_code_and_returns_the_matching_code()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse(OneStockListing));

        var code = await service.FindBySupplierCodeAsync("7009190321200");

        Assert.Equal("7009190321200", code);

        var request = server.RequestPayload();
        Assert.Equal(ListStk + "listStockRequest", request.Name);
        Assert.Equal("2.0", request.Attribute("version")!.Value);
        Assert.Equal("2.0", request.Attribute("stockVersion")!.Value);

        // requestStock is declared in list_stock.xsd (lStk), its filter children in filter.xsd (ftr) — live-verified.
        var filter = request.Element(ListStk + "requestStock")!.Element(Ftr + "filter")!;
        Assert.Equal("7009190321200", filter.Element(Ftr + "code")!.Value);

        Assert.Equal("53870441", server.Requests[0].Root!.Attribute("ico")!.Value);
    }

    [Fact]
    public async Task FindBySupplierCodeAsync_returns_null_when_no_card_matches()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""<lStk:listStock version="2.0" />"""));

        Assert.Null(await service.FindBySupplierCodeAsync("0000000000000"));
    }

    [Fact]
    public async Task SearchByNameContainingAsync_uses_a_queryFilter_LIKE_condition_with_escaped_quotes()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""
            <lStk:listStock version="2.0">
              <lStk:stock version="2.0"><stk:stockHeader><stk:code>7009181280000</stk:code><stk:name>Gabel SLK-R - pretekárska palica</stk:name></stk:stockHeader></lStk:stock>
              <lStk:stock version="2.0"><stk:stockHeader><stk:code>7009181281250</stk:code><stk:name>Gabel SLK-R - pretekárska palica 125 cm</stk:name></stk:stockHeader></lStk:stock>
              <lStk:stock version="2.0"><stk:stockHeader><stk:name>bez kódu — musí byť vynechaná</stk:name></stk:stockHeader></lStk:stock>
            </lStk:listStock>
            """));

        var matches = await service.SearchByNameContainingAsync("SLK'R");

        Assert.Equal(2, matches.Count);
        Assert.Equal("7009181280000", matches[0].Code);
        Assert.Equal("Gabel SLK-R - pretekárska palica", matches[0].Name);
        Assert.Equal("7009181281250", matches[1].Code);

        var request = server.RequestPayload();
        Assert.Equal(ListStk + "listStockRequest", request.Name);
        var queryFilter = request.Element(ListStk + "requestStock")!.Element(Ftr + "queryFilter")!;
        Assert.Equal("Nazev LIKE '%SLK''R%'", queryFilter.Element(Ftr + "filter")!.Value);
    }

    [Fact]
    public async Task CreateAsync_sends_a_card_with_stockType_code_name_storage_typePrice_and_orderName()
    {
        var (server, service) = Create();

        var code = await service.CreateAsync("7009190321200", "DH PLATINUM SERIES CM.120", "120 cm");

        Assert.Equal("7009190321200", code);

        var stock = server.RequestPayload();
        Assert.Equal(Stk + "stock", stock.Name);
        Assert.Equal("2.0", stock.Attribute("version")!.Value);

        var header = stock.Element(Stk + "stockHeader")!;
        Assert.Equal("card", header.Element(Stk + "stockType")!.Value);
        Assert.Equal("7009190321200", header.Element(Stk + "code")!.Value);
        Assert.Equal("DH PLATINUM SERIES CM.120", header.Element(Stk + "name")!.Value);
        Assert.Equal("GoNoow", header.Element(Stk + "storage")!.Element(Typ + "ids")!.Value);
        Assert.Equal("Cenova skupina 1", header.Element(Stk + "typePrice")!.Element(Typ + "ids")!.Value);
        Assert.Equal("120 cm", header.Element(Stk + "orderName")!.Value);

        // The supplier code is NOT a real EAN — it must never be written to stk:EAN.
        Assert.Null(header.Element(Stk + "EAN"));
    }

    [Fact]
    public async Task CreateAsync_omits_orderName_when_none_is_supplied()
    {
        var (server, service) = Create();

        await service.CreateAsync("7008352650000", "STRETCH LITE CARBON LILAC", orderName: null);

        var header = server.RequestPayload().Element(Stk + "stockHeader")!;
        Assert.Null(header.Element(Stk + "orderName"));
    }

    [Fact]
    public async Task CreateAsync_throws_PohodaResponseException_when_mServer_reports_an_error()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.ErrorResponse("Typ zásoby. Tento element je vyžadovaný pri vytvorení dokladu."));

        var ex = await Assert.ThrowsAsync<PohodaResponseException>(() => service.CreateAsync("1", "x", null));

        Assert.Contains("Typ zásoby", ex.Message);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: build error — `StockCardService` does not exist.

- [ ] **Step 4: Implement `StockCardService`**

Create `src/Pohoda/Pohoda.Server/Services/StockCardService.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Pohoda.Server.Xml;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Services;

/// <summary>
/// Stock (Zásoby) agenda operations over <see cref="MServerClient"/>: find a skladová karta by the
/// supplier's own product code, search by partial name, or create a new card. Ported from
/// Gabel.PrijemkaSync's live-verified <c>StockClient</c>.
/// </summary>
public sealed class StockCardService : IStockCardService
{
    private readonly MServerClient _client;
    private readonly MServerConfiguration _config;

    public StockCardService(MServerClient client, MServerConfiguration config)
    {
        _client = client;
        _config = config;
    }

    /// <summary>
    /// Looks up an existing skladová karta by <c>stk:code</c>. Returns its Pohoda stock code (i.e.
    /// <paramref name="supplierCode"/> itself, confirming it exists), or null if not found.
    ///
    /// Confirmed against a live mServer response: the 13-digit Gabel "ARTICOLO" code from the
    /// invoice is stored verbatim in existing cards' <c>stk:code</c> — NOT <c>stk:EAN</c>, which
    /// holds a separate, genuine EAN-13 barcode we have no way to derive from the invoice.
    /// </summary>
    public async Task<string?> FindBySupplierCodeAsync(string supplierCode, CancellationToken ct = default)
    {
        var request = new XElement(ListStk + "listStockRequest",
            new XAttribute("version", "2.0"),
            new XAttribute("stockVersion", "2.0"),
            // The element name "requestStock" belongs to the list_stock.xsd (lStk) namespace even
            // though its *type* comes from filter.xsd — confirmed live via a schema-validation error.
            new XElement(ListStk + "requestStock",
                new XElement(Ftr + "filter",
                    new XElement(Ftr + "code", supplierCode))));

        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Vyhľadanie skladovej karty podľa kódu {supplierCode}", request);
        var result = await _client.SendOrThrowAsync(dataPack, ct);

        var codeElement = result.Document.Descendants()
            .Where(e => e.Name.LocalName == "stockHeader")
            .Select(h => h.Elements().FirstOrDefault(c => c.Name.LocalName == "code"))
            .FirstOrDefault(c => c is not null);

        return codeElement?.Value;
    }

    /// <summary>
    /// Searches skladové karty whose <c>stk:name</c> CONTAINS <paramref name="nameContains"/> — used
    /// to look for a possible "master card" of a variant product family. Pohoda's plain
    /// <c>filter/name</c> is an EXACT match (confirmed live), so this uses <c>queryFilter/filter</c>
    /// with a SQL <c>Nazev LIKE '%text%'</c> condition. The column name <c>Nazev</c> is not part of
    /// the public XML schema — <c>Name</c> failed live with mServer error 137 "Invalid column name";
    /// <c>Nazev</c> was confirmed against real data. Single quotes are doubled for the SQL text.
    ///
    /// Because a variant's own name also contains the base name, this returns ALL matches — callers
    /// exclude sibling variants themselves to find the actual master card.
    /// </summary>
    public async Task<IReadOnlyList<StockNameMatch>> SearchByNameContainingAsync(string nameContains, CancellationToken ct = default)
    {
        var escaped = nameContains.Replace("'", "''");
        var request = new XElement(ListStk + "listStockRequest",
            new XAttribute("version", "2.0"),
            new XAttribute("stockVersion", "2.0"),
            new XElement(ListStk + "requestStock",
                new XElement(Ftr + "queryFilter",
                    new XElement(Ftr + "filter", $"Nazev LIKE '%{escaped}%'"))));

        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Vyhľadanie materskej karty podľa časti názvu '{nameContains}'", request);
        var result = await _client.SendOrThrowAsync(dataPack, ct);

        return result.Document.Descendants()
            .Where(e => e.Name.LocalName == "stockHeader")
            .Select(h => new StockNameMatch(
                h.Elements().FirstOrDefault(c => c.Name.LocalName == "code")?.Value ?? string.Empty,
                h.Elements().FirstOrDefault(c => c.Name.LocalName == "name")?.Value ?? string.Empty))
            .Where(m => m.Code.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Creates a new skladová karta using <paramref name="code"/> as-is (existing supplier cards use
    /// the supplier code verbatim as <c>stk:code</c>, no prefix) and <paramref name="name"/>.
    /// Deliberately does NOT set <c>stk:EAN</c>: the supplier code is not a real barcode.
    ///
    /// Variant products: <c>stock.xsd</c> has NO field linking a card to a master/parent card. When
    /// <paramref name="orderName"/> is supplied (a detected size like "120 cm") it is written to
    /// <c>stk:orderName</c> — the one field real size-variant cards carry — but this does NOT place
    /// the card under any master card; that remains a manual step in the Pohoda desktop UI.
    /// </summary>
    public async Task<string> CreateAsync(string code, string name, string? orderName, CancellationToken ct = default)
    {
        var stockHeaderChildren = new List<XElement>
        {
            // stk:stockType is required — confirmed live via mServer error 108 on an attempt that omitted it.
            new(Stk + "stockType", "card"),
            new(Stk + "code", code),
            new(Stk + "name", name),
            new(Stk + "storage", new XElement(Typ + "ids", _config.DefaultStorageCode)),
            new(Stk + "typePrice", new XElement(Typ + "ids", _config.DefaultTypePriceCode))
        };

        if (!string.IsNullOrWhiteSpace(orderName))
        {
            stockHeaderChildren.Add(new XElement(Stk + "orderName", orderName));
        }

        var request = new XElement(Stk + "stock",
            new XAttribute("version", "2.0"),
            new XElement(Stk + "stockHeader", stockHeaderChildren));

        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Založenie novej skladovej karty {code}", request);
        await _client.SendOrThrowAsync(dataPack, ct);

        return code;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: `Passed! - Failed: 0, Passed: 7` (1 from Task 1 + 6 new).

- [ ] **Step 6: Commit**

```bash
git add src/Pohoda/Pohoda.Server/Services/StockCardService.cs src/Pohoda/Pohoda.Server.Tests/FakeMServer.cs src/Pohoda/Pohoda.Server.Tests/StockCardServiceTests.cs
git commit -m "feat: add StockCardService (IStockCardService over Pohoda.MServer) to Pohoda.Server"
```

---

## Task 3: `Pohoda.Server.ReceiptService` over `MServerClient`

**Files:**
- Create: `src/Pohoda/Pohoda.Server/Services/ReceiptService.cs`
- Test: `src/Pohoda/Pohoda.Server.Tests/ReceiptServiceTests.cs`

**Interfaces:**
- Consumes: `IReceiptService`, `ExistingReceipt`, `ExistingReceiptLine`, `ReceiptLineToWrite` (Task 1); `PohodaNamespaces`, `DataPackBuilder` (Task 1); `FakeMServer` (Task 2); `MServerClient.SendOrThrowAsync`, `MServerConfiguration.Ico` (PR1).
- Produces: `Pohoda.Server.Services.ReceiptService(MServerClient client, MServerConfiguration config) : IReceiptService`; `ReceiptService.FreightNote = "Doprava (vedľajšie náklady)"` (public const, used by tests).

**Known open item carried over (spec §8, not changed here):** `UpdateAsync` writes today's date into `prijemkaHeader/date` (Gabel's `BuildPrijemkaElement(date: null)` behavior) and resends the full line set. Whether Pohoda accepts this without side effects is unverified live — it will be exercised for the first time in PR3's gated `apply`. Port as-is; do not "fix" it here.

- [ ] **Step 1: Write the failing tests**

Create `src/Pohoda/Pohoda.Server.Tests/ReceiptServiceTests.cs`:

```csharp
using Pohoda.Contract.Models;
using Pohoda.Server.Services;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Tests;

public class ReceiptServiceTests
{
    private static (FakeMServer Server, ReceiptService Service) Create()
    {
        var server = new FakeMServer();
        return (server, new ReceiptService(server.Client, server.Config));
    }

    private const string OneReceiptListing = """
        <lst:listPrijemka version="2.0" prijemkaVersion="2.0">
          <lst:prijemka version="2.0">
            <pri:prijemkaHeader>
              <pri:id>42</pri:id>
              <pri:number><typ:numberRequested>26PR00012</typ:numberRequested></pri:number>
              <pri:date>2025-11-20</pri:date>
              <pri:text>Gabel FT 2/250334</pri:text>
            </pri:prijemkaHeader>
            <pri:prijemkaDetail>
              <pri:prijemkaItem>
                <pri:text>DH PLATINUM SERIES CM.120</pri:text>
                <pri:quantity>1</pri:quantity>
                <pri:unit>PA</pri:unit>
                <pri:discountPercentage>35</pri:discountPercentage>
                <pri:homeCurrency><typ:unitPrice>94.5</typ:unitPrice></pri:homeCurrency>
                <pri:stockItem><typ:stockItem><typ:ids>7009190321200</typ:ids></typ:stockItem></pri:stockItem>
              </pri:prijemkaItem>
              <pri:prijemkaItem>
                <pri:text>STRETCH LITE CARBON LILAC</pri:text>
                <pri:quantity>20</pri:quantity>
                <pri:unit>PA</pri:unit>
                <pri:discountPercentage>35</pri:discountPercentage>
                <pri:homeCurrency><typ:unitPrice>35</typ:unitPrice></pri:homeCurrency>
                <pri:stockItem><typ:stockItem><typ:ids>7008352650000</typ:ids></typ:stockItem></pri:stockItem>
              </pri:prijemkaItem>
            </pri:prijemkaDetail>
          </lst:prijemka>
        </lst:listPrijemka>
        """;

    private static readonly ReceiptLineToWrite LineA = new("7009190321200", "DH PLATINUM SERIES CM.120", 2m, "PA", 94.5m, 35m);
    private static readonly ReceiptLineToWrite LineB = new("7008352650000", "STRETCH LITE CARBON LILAC", 20m, "PA", 35m, 35m);

    [Fact]
    public async Task ListRecentAsync_sends_a_listPrijemkaRequest_with_a_count_limit_and_a_dateFrom_filter()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""<lst:listPrijemka version="2.0" prijemkaVersion="2.0" />"""));

        var receipts = await service.ListRecentAsync(new DateOnly(2025, 9, 11), 500);

        Assert.Empty(receipts);

        var request = server.RequestPayload();
        Assert.Equal(Lst + "listPrijemkaRequest", request.Name);
        Assert.Equal("2.0", request.Attribute("version")!.Value);
        Assert.Equal("2.0", request.Attribute("prijemkaVersion")!.Value);

        // ftr:limitType is a complex type (idFrom/count), not plain text — live-verified.
        Assert.Equal("500", request.Element(Lst + "limit")!.Element(Ftr + "count")!.Value);

        // "requestPrijemka" is declared in list.xsd (lst) even though its type is from filter.xsd — live-verified.
        var filter = request.Element(Lst + "requestPrijemka")!.Element(Ftr + "filter")!;
        Assert.Equal("2025-09-11", filter.Element(Ftr + "dateFrom")!.Value);
    }

    [Fact]
    public async Task ListRecentAsync_parses_each_receipt_with_its_header_and_lines()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse(OneReceiptListing));

        var receipts = await service.ListRecentAsync(new DateOnly(2025, 1, 1), 100);

        var receipt = Assert.Single(receipts);
        Assert.Equal("42", receipt.PohodaId);
        Assert.Equal("26PR00012", receipt.Number);
        Assert.Equal("Gabel FT 2/250334", receipt.Text);

        Assert.Equal(2, receipt.Lines.Count);
        Assert.Equal(new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 1m, 94.5m, 35m), receipt.Lines[0]);
        Assert.Equal(new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m), receipt.Lines[1]);
    }

    [Fact]
    public async Task CreateAsync_builds_a_prijemka_with_double_nested_stock_references_and_a_freight_charge()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""
            <pri:prijemkaResponse version="2.0" state="ok">
              <pri:producedDetails><pri:id>77</pri:id><pri:number>26PR00013</pri:number></pri:producedDetails>
            </pri:prijemkaResponse>
            """));

        var number = await service.CreateAsync("Gabel FT 2/260220", new DateOnly(2026, 6, 23), [LineA, LineB], freight: 180.5m);

        Assert.Equal("26PR00013", number);

        var prijemka = server.RequestPayload();
        Assert.Equal(Prj + "prijemka", prijemka.Name);
        Assert.Equal("2.0", prijemka.Attribute("version")!.Value);

        var header = prijemka.Element(Prj + "prijemkaHeader")!;
        Assert.Null(header.Element(Prj + "id"));
        Assert.Equal("2026-06-23", header.Element(Prj + "date")!.Value);
        Assert.Equal("Gabel FT 2/260220", header.Element(Prj + "text")!.Value);

        var items = prijemka.Element(Prj + "prijemkaDetail")!.Elements(Prj + "prijemkaItem").ToList();
        Assert.Equal(2, items.Count);

        var first = items[0];
        Assert.Equal("DH PLATINUM SERIES CM.120", first.Element(Prj + "text")!.Value);
        Assert.Equal("2", first.Element(Prj + "quantity")!.Value);
        Assert.Equal("PA", first.Element(Prj + "unit")!.Value);
        Assert.Equal("false", first.Element(Prj + "payVAT")!.Value);
        Assert.Equal("none", first.Element(Prj + "rateVAT")!.Value);
        Assert.Equal("35", first.Element(Prj + "discountPercentage")!.Value);
        Assert.Equal("94.5", first.Element(Prj + "homeCurrency")!.Element(Typ + "unitPrice")!.Value);

        // Outer stockItem is prijemka.xsd's field; it wraps type.xsd's stockItem holding the typ:ids reference — live-verified.
        var stockRef = first.Element(Prj + "stockItem")!.Element(Typ + "stockItem")!.Element(Typ + "ids")!;
        Assert.Equal("7009190321200", stockRef.Value);

        var freight = Assert.Single(prijemka.Element(Prj + "prijemkaDetail")!.Elements(Prj + "prijemkaAccessoryChargesItem"));
        Assert.Equal(ReceiptService.FreightNote, freight.Element(Prj + "note")!.Value);
        Assert.Equal("1", freight.Element(Prj + "quantity")!.Value);
        Assert.Equal("false", freight.Element(Prj + "payVAT")!.Value);
        Assert.Equal("none", freight.Element(Prj + "rateVAT")!.Value);
        Assert.Equal("180.5", freight.Element(Prj + "homeCurrency")!.Element(Typ + "unitPrice")!.Value);
        // Accessory charges have no text/unit/stockItem — a different XSD type than prijemkaItem (live-verified).
        Assert.Null(freight.Element(Prj + "text"));
        Assert.Null(freight.Element(Prj + "stockItem"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task CreateAsync_omits_the_freight_charge_when_freight_is_absent_or_zero(int? freightWhole)
    {
        var (server, service) = Create();
        decimal? freight = freightWhole is null ? null : (decimal)freightWhole.Value;

        await service.CreateAsync("Gabel FT 2/260220", new DateOnly(2026, 6, 23), [LineA], freight);

        var detail = server.RequestPayload().Element(Prj + "prijemkaDetail")!;
        Assert.Single(detail.Elements(Prj + "prijemkaItem"));
        Assert.Empty(detail.Elements(Prj + "prijemkaAccessoryChargesItem"));
    }

    [Fact]
    public async Task CreateAsync_falls_back_to_the_header_text_when_the_response_carries_no_number()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse());

        var number = await service.CreateAsync("Gabel FT 2/260220", new DateOnly(2026, 6, 23), [LineA], null);

        Assert.Equal("Gabel FT 2/260220", number);
    }

    [Fact]
    public async Task UpdateAsync_puts_the_existing_id_in_the_header_and_resends_every_line()
    {
        var (server, service) = Create();

        await service.UpdateAsync("42", "Gabel FT 2/250334", [LineA, LineB], freight: null);

        var prijemka = server.RequestPayload();
        var header = prijemka.Element(Prj + "prijemkaHeader")!;
        Assert.Equal("42", header.Element(Prj + "id")!.Value);
        Assert.Equal("Gabel FT 2/250334", header.Element(Prj + "text")!.Value);

        var items = prijemka.Element(Prj + "prijemkaDetail")!.Elements(Prj + "prijemkaItem").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("7009190321200", items[0].Element(Prj + "stockItem")!.Element(Typ + "stockItem")!.Element(Typ + "ids")!.Value);
        Assert.Equal("7008352650000", items[1].Element(Prj + "stockItem")!.Element(Typ + "stockItem")!.Element(Typ + "ids")!.Value);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: build error — `ReceiptService` does not exist.

- [ ] **Step 3: Implement `ReceiptService`**

Create `src/Pohoda/Pohoda.Server/Services/ReceiptService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Pohoda.Server.Xml;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Services;

/// <summary>
/// Příjemka (goods receipt) agenda operations over <see cref="MServerClient"/>. Ported from
/// Gabel.PrijemkaSync's <c>PrijemkaClient</c>.
///
/// IMPORTANT — residual risk: <see cref="ListRecentAsync"/> is live-verified against production data;
/// <see cref="CreateAsync"/>/<see cref="UpdateAsync"/> wire shapes were assembled from the published
/// XSDs plus live schema-validation errors, but no write has been executed against a live mServer
/// yet. The first real write stays gated behind explicit human approval (spec §6).
/// </summary>
public sealed class ReceiptService : IReceiptService
{
    /// <summary>Note written on the accessory-charge line that carries the invoice's freight amount.</summary>
    public const string FreightNote = "Doprava (vedľajšie náklady)";

    private readonly MServerClient _client;
    private readonly MServerConfiguration _config;

    public ReceiptService(MServerClient client, MServerConfiguration config)
    {
        _client = client;
        _config = config;
    }

    public async Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default)
    {
        var request = new XElement(Lst + "listPrijemkaRequest",
            new XAttribute("version", "2.0"),
            new XAttribute("prijemkaVersion", "2.0"),
            // ftr:limitType is a complex type (idFrom/count), not plain text — confirmed against a
            // live mServer validation error ("Text is not allowed... element 'limit'").
            new XElement(Lst + "limit", new XElement(Ftr + "count", limit)),
            // The element name "requestPrijemka" is declared in list.xsd (lst) even though its
            // *type* is ftr:requestPrijemkaType — confirmed against a live mServer schema-validation
            // error ("Expecting: {list.xsd}requestPrijemka").
            new XElement(Lst + "requestPrijemka",
                new XElement(Ftr + "filter",
                    new XElement(Ftr + "dateFrom", since.ToString("yyyy-MM-dd")))));

        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Zoznam príjemiek od {since:yyyy-MM-dd}", request);
        var result = await _client.SendOrThrowAsync(dataPack, ct);

        return result.Document.Descendants()
            .Where(e => e.Name.LocalName == "prijemka")
            .Select(ParseExistingReceipt)
            .ToList();
    }

    private static ExistingReceipt ParseExistingReceipt(XElement prijemkaEl)
    {
        var header = prijemkaEl.Elements().FirstOrDefault(e => e.Name.LocalName == "prijemkaHeader");
        var id = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value ?? string.Empty;
        var number = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "number")?.Value ?? string.Empty;
        var text = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? string.Empty;

        var detail = prijemkaEl.Elements().FirstOrDefault(e => e.Name.LocalName == "prijemkaDetail");
        var lines = (detail?.Elements().Where(e => e.Name.LocalName == "prijemkaItem") ?? [])
            .Select(item =>
            {
                var stockCode = item.Descendants()
                    .Where(e => e.Name.LocalName is "code" or "ids")
                    .Select(e => e.Value)
                    .FirstOrDefault() ?? string.Empty;
                var itemText = item.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? string.Empty;
                var unit = item.Elements().FirstOrDefault(e => e.Name.LocalName == "unit")?.Value ?? string.Empty;

                var qtyText = item.Elements().FirstOrDefault(e => e.Name.LocalName == "quantity")?.Value;
                decimal.TryParse(qtyText, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);

                var discountText = item.Elements().FirstOrDefault(e => e.Name.LocalName == "discountPercentage")?.Value;
                decimal.TryParse(discountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var discount);

                var priceText = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "unitPrice")?.Value;
                decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var unitPrice);

                return new ExistingReceiptLine(stockCode, itemText, unit, qty, unitPrice, discount);
            })
            .ToList();

        return new ExistingReceipt(id, number, text, lines);
    }

    public async Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)
    {
        var prijemka = BuildPrijemkaElement(headerId: null, headerText, date, lines, freight);
        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Nová príjemka: {headerText}", prijemka);
        var result = await _client.SendOrThrowAsync(dataPack, ct);

        return result.Document.Descendants()
            .Where(e => e.Name.LocalName == "number")
            .Select(e => e.Value)
            .FirstOrDefault() ?? headerText;
    }

    public async Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)
    {
        var prijemka = BuildPrijemkaElement(existingId, headerText, date: null, allLines, freight);
        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Doplnenie príjemky {existingId}: {headerText}", prijemka);
        await _client.SendOrThrowAsync(dataPack, ct);
    }

    private static XElement BuildPrijemkaElement(string? headerId, string headerText, DateOnly? date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight)
    {
        var headerChildren = new List<XElement>();
        if (headerId is not null)
        {
            headerChildren.Add(new XElement(Prj + "id", headerId));
        }

        headerChildren.Add(new XElement(Prj + "date", (date ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd")));
        headerChildren.Add(new XElement(Prj + "text", headerText));

        var detailItems = lines.Select(BuildLineElement).ToList();
        if (freight is > 0)
        {
            detailItems.Add(BuildFreightElement(freight.Value));
        }

        return new XElement(Prj + "prijemka",
            new XAttribute("version", "2.0"),
            new XElement(Prj + "prijemkaHeader", headerChildren),
            new XElement(Prj + "prijemkaDetail", detailItems));
    }

    private static XElement BuildLineElement(ReceiptLineToWrite line) =>
        new(Prj + "prijemkaItem",
            new XElement(Prj + "text", line.Text),
            new XElement(Prj + "quantity", line.Quantity.ToString(CultureInfo.InvariantCulture)),
            new XElement(Prj + "unit", line.Unit),
            new XElement(Prj + "payVAT", "false"),
            new XElement(Prj + "rateVAT", "none"),
            new XElement(Prj + "discountPercentage", line.DiscountPercent.ToString(CultureInfo.InvariantCulture)),
            new XElement(Prj + "homeCurrency", new XElement(Typ + "unitPrice", line.UnitPrice.ToString(CultureInfo.InvariantCulture))),
            // A doubly-nested "stockItem": the OUTER element is prijemka.xsd's own field (type
            // typ:stockItemType), which requires an INNER child ALSO named "stockItem" (this one
            // from type.xsd, type typ:stockRefType, holding the actual reference via typ:ids) —
            // confirmed against the published type.xsd after a live schema-validation error showed
            // "Expecting: {type.xsd}stockItem" as the content of prijemka.xsd's stockItem element.
            new XElement(Prj + "stockItem", new XElement(Typ + "stockItem", new XElement(Typ + "ids", line.PohodaStockCode))));

    // prijemkaAccessoryChargesItemType is NOT the same shape as prijemkaItemType — confirmed against
    // the published prijemka.xsd after a live schema-validation error on "text" (unexpected here).
    // It has no text/unit/stockItem at all; the description goes in "note" instead.
    private static XElement BuildFreightElement(decimal freightAmount) =>
        new(Prj + "prijemkaAccessoryChargesItem",
            new XElement(Prj + "note", FreightNote),
            new XElement(Prj + "quantity", "1"),
            new XElement(Prj + "payVAT", "false"),
            new XElement(Prj + "rateVAT", "none"),
            new XElement(Prj + "homeCurrency", new XElement(Typ + "unitPrice", freightAmount.ToString(CultureInfo.InvariantCulture))));
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: `Passed! - Failed: 0, Passed: 14` (7 from Tasks 1–2 + 7 new: 5 facts + 2 theory cases).

- [ ] **Step 5: Commit**

```bash
git add src/Pohoda/Pohoda.Server/Services/ReceiptService.cs src/Pohoda/Pohoda.Server.Tests/ReceiptServiceTests.cs
git commit -m "feat: add ReceiptService (IReceiptService over Pohoda.MServer) to Pohoda.Server"
```

---

## Task 4: `Receipts.Contract` + `Receipts.Gabel` (parser plugin) + `Receipts.Tests` with the ported parser tests

**Files:**
- Create: `src/Receipts/Receipts.Contract/Receipts.Contract.csproj`
- Create: `src/Receipts/Receipts.Contract/ParsedInvoice.cs`
- Create: `src/Receipts/Receipts.Contract/ReceiptProposal.cs`
- Create: `src/Receipts/Receipts.Contract/IInvoiceParser.cs`
- Create: `src/Receipts/Receipts.Contract/IVariantDetector.cs`
- Create: `src/Receipts/Receipts.Gabel/Receipts.Gabel.csproj`
- Create: `src/Receipts/Receipts.Gabel/GabelVariantDetector.cs`
- Create: `src/Receipts/Receipts.Gabel/GabelInvoiceParser.cs`
- Create: `src/Receipts/Receipts.Tests/Receipts.Tests.csproj`
- Create: `src/Receipts/Receipts.Tests/TestData/*.pdf` (6 files copied from `C:\git\Gabel.PrijemkaSync\src\Gabel.PrijemkaSync.Tests\TestData\`)
- Test: `src/Receipts/Receipts.Tests/GabelInvoiceParserTests.cs`
- Test: `src/Receipts/Receipts.Tests/GabelVariantDetectorTests.cs`

**Interfaces:**
- Consumes: `Common.Documents.PdfReader.PdfRowReader.ReadRows(string pdfPath) : IReadOnlyList<string>` (PR1).
- Produces (used by Tasks 5–8):
  - `Receipts.Contract.InvoiceLine { SupplierCode, Description, Unit, Quantity, UnitPrice, DiscountPercent, LineAmount, VariantSizeHint?, VariantBaseName? }`
  - `Receipts.Contract.ParsedInvoice { SupplierName, InvoiceNumber, InvoiceDate, Currency, Lines, FreightAmount, PrintedGoodsNetTotal, PrintedGrandTotal, SourceFilePath, SourceFileSha256, Warnings : List<string>, ComputedGoodsNetTotal }`
  - `Receipts.Contract.ReceiptProposalType { CreateReceipt, AppendLines }`, `Receipts.Contract.ProposalStatus { PendingApproval, Applied, Failed }`
  - `Receipts.Contract.ReceiptLineProposal { SupplierCode, Description, Unit, Quantity, UnitPrice, DiscountPercent, PohodaStockCode, IsNewStockItem, VariantSizeHint?, VariantBaseName?, MasterCardCode?, MasterCardSearched }`
  - `Receipts.Contract.ReceiptProposal { Id, InvoiceNumber, InvoiceDate, SourceFilePath, SourceFileSha256, Type, ExistingReceiptId?, ExistingReceiptNumber?, HeaderText?, LinesToAdd, FreightToAdd?, Warnings, Status (settable), FailureReason?, ResultReceiptNumber?, CreatedAt, AppliedAt? }`
  - `Receipts.Contract.IInvoiceParser.TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason) : bool`
  - `Receipts.Contract.IVariantDetector.LooksLikeSizeVariant(string text) : bool`
  - `Receipts.Gabel.GabelInvoiceParser : IInvoiceParser` (parameterless ctor), `Receipts.Gabel.GabelVariantDetector : IVariantDetector` (parameterless ctor) with `static (string? SizeHint, string? BaseName) Detect(string text)`

- [ ] **Step 1: Scaffold `Receipts.Contract`**

Create `src/Receipts/Receipts.Contract/Receipts.Contract.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

Create `src/Receipts/Receipts.Contract/ParsedInvoice.cs`:

```csharp
namespace Receipts.Contract;

/// <summary>One line item extracted from a supplier's final invoice.</summary>
public sealed class InvoiceLine
{
    /// <summary>The supplier's own product code (for Gabel: the 13-digit "ARTICOLO"). Confirmed live against Pohoda: existing skladové karty store this verbatim in stk:code, NOT stk:EAN (which holds a separate, genuine barcode we don't have from the invoice).</summary>
    public required string SupplierCode { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountPercent { get; init; }

    /// <summary>Printed line amount — should equal UnitPrice * Quantity * (1 - DiscountPercent/100).</summary>
    public required decimal LineAmount { get; init; }

    /// <summary>
    /// Set when <see cref="Description"/> contains a single-size pattern like "135CM"/"CM.130"/"Cm 115"
    /// — a strong hint that this is a size variant of a product family. A number RANGE before "CM"
    /// (e.g. "73-110CM") means an adjustable-length pole instead and does NOT set this. The Pohoda XML
    /// agenda has NO field linking a variant to a master/parent card — this hint can only drive a
    /// warning, a best-effort `stk:orderName`, and a by-name master-card lookup (<see cref="VariantBaseName"/>),
    /// never an automatic master-card assignment.
    /// </summary>
    public string? VariantSizeHint { get; init; }

    /// <summary>
    /// Set together with <see cref="VariantSizeHint"/>: <see cref="Description"/> with the size token
    /// removed, e.g. "DH PLATINUM SERIES" from "DH PLATINUM SERIES CM.120". A master card (when one
    /// exists) is typically named identically to its variants minus the size — used to search Pohoda
    /// for a possible master card.
    /// </summary>
    public string? VariantBaseName { get; init; }
}

/// <summary>Result of parsing one supplier final-invoice PDF.</summary>
public sealed class ParsedInvoice
{
    /// <summary>Human-readable supplier name, e.g. "Gabel" — used as the prefix of a new receipt's header text ("Gabel FT 2/260220").</summary>
    public required string SupplierName { get; init; }

    /// <summary>Normalized reference, e.g. "FT 2/260220" — this is what gets embedded in the příjemka text and used to find/match existing receipts.</summary>
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<InvoiceLine> Lines { get; init; }

    /// <summary>Freight/transport charge — mapped to a příjemka accessory charge, never a stock line.</summary>
    public required decimal FreightAmount { get; init; }

    /// <summary>Printed goods net total — sum of line amounts, used as a sanity-check target.</summary>
    public required decimal PrintedGoodsNetTotal { get; init; }

    /// <summary>Printed grand total — goods net + freight.</summary>
    public required decimal PrintedGrandTotal { get; init; }

    public required string SourceFilePath { get; init; }
    public required string SourceFileSha256 { get; init; }

    public List<string> Warnings { get; } = [];

    public decimal ComputedGoodsNetTotal => Lines.Sum(l => l.LineAmount);
}
```

Create `src/Receipts/Receipts.Contract/ReceiptProposal.cs`:

```csharp
namespace Receipts.Contract;

public enum ReceiptProposalType
{
    /// <summary>No existing příjemka references this invoice number — create a new one with all lines.</summary>
    CreateReceipt,

    /// <summary>An existing příjemka already references this invoice number — only append lines missing from it.</summary>
    AppendLines
}

public enum ProposalStatus
{
    PendingApproval,
    Applied,
    Failed
}

public sealed class ReceiptLineProposal
{
    public required string SupplierCode { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountPercent { get; init; }

    /// <summary>Pohoda stock code this line resolves to (may not exist yet — see <see cref="IsNewStockItem"/>).</summary>
    public required string PohodaStockCode { get; init; }

    /// <summary>True if a new skladová karta must be created for this line before/with the příjemka.</summary>
    public required bool IsNewStockItem { get; init; }

    /// <summary>Copied from <see cref="InvoiceLine.VariantSizeHint"/> — set when this looks like a size variant (e.g. "120 cm"). Used to set `stk:orderName` on create and to warn the reviewer that mServer cannot auto-link it to a master card.</summary>
    public string? VariantSizeHint { get; init; }

    /// <summary>Copied from <see cref="InvoiceLine.VariantBaseName"/> — the description without the size, used to search Pohoda by name for a possible master card.</summary>
    public string? VariantBaseName { get; init; }

    /// <summary>
    /// Result of searching Pohoda for a stock card named <see cref="VariantBaseName"/>: its code if
    /// exactly one non-variant candidate was found, or null (only meaningful when
    /// <see cref="VariantSizeHint"/> is set — see <see cref="MasterCardSearched"/> to distinguish
    /// "not searched" from "searched, not found").
    /// </summary>
    public string? MasterCardCode { get; init; }

    /// <summary>True once a master-card-by-name search was attempted for this line (regardless of outcome).</summary>
    public bool MasterCardSearched { get; init; }
}

/// <summary>A planned, not-yet-applied change to Pohoda, derived from one parsed invoice.</summary>
public sealed class ReceiptProposal
{
    public required string Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required DateOnly InvoiceDate { get; init; }
    public required string SourceFilePath { get; init; }
    public required string SourceFileSha256 { get; init; }
    public required ReceiptProposalType Type { get; init; }

    /// <summary>Set only for <see cref="ReceiptProposalType.AppendLines"/> — the Pohoda document id/number found.</summary>
    public string? ExistingReceiptId { get; init; }
    public string? ExistingReceiptNumber { get; init; }

    /// <summary>Header text for a new příjemka, e.g. "Gabel FT 2/260220". Null for AppendLines.</summary>
    public string? HeaderText { get; init; }

    public required IReadOnlyList<ReceiptLineProposal> LinesToAdd { get; init; }

    /// <summary>Freight amount to add as an accessory charge. Null if absent.</summary>
    public decimal? FreightToAdd { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public ProposalStatus Status { get; set; } = ProposalStatus.PendingApproval;
    public string? FailureReason { get; set; }
    public string? ResultReceiptNumber { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? AppliedAt { get; set; }
}
```

Create `src/Receipts/Receipts.Contract/IInvoiceParser.cs`:

```csharp
namespace Receipts.Contract;

/// <summary>
/// Supplier plugin: turns one invoice PDF into a <see cref="ParsedInvoice"/>. One implementation per
/// supplier (e.g. <c>Receipts.Gabel.GabelInvoiceParser</c>); <c>Receipts.Core</c> never depends on a
/// specific supplier's types.
/// </summary>
public interface IInvoiceParser
{
    /// <summary>
    /// Returns true and a populated <paramref name="invoice"/> for a final invoice this parser
    /// understands. Returns false with a human-readable <paramref name="skipReason"/> for anything
    /// else (a proforma/pre-invoice, an unrecognized layout) — skipping is a normal outcome, not an error.
    /// </summary>
    bool TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason);
}
```

Create `src/Receipts/Receipts.Contract/IVariantDetector.cs`:

```csharp
namespace Receipts.Contract;

/// <summary>
/// Supplier plugin: decides whether a product name (from the invoice or from a Pohoda stock card)
/// looks like a fixed-size variant of a product family. <c>Receipts.Core</c> uses it to exclude
/// sibling variants from a master-card search, whose naming convention is supplier-specific.
/// </summary>
public interface IVariantDetector
{
    bool LooksLikeSizeVariant(string text);
}
```

- [ ] **Step 2: Scaffold `Receipts.Gabel` and `Receipts.Tests`, copy the PDF fixtures, register everything in the solution**

Create `src/Receipts/Receipts.Gabel/Receipts.Gabel.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Common\Documents\Common.Documents.PdfReader\Common.Documents.PdfReader.csproj" />
    <ProjectReference Include="..\Receipts.Contract\Receipts.Contract.csproj" />
  </ItemGroup>

</Project>
```

Create `src/Receipts/Receipts.Tests/Receipts.Tests.csproj`:

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
    <None Include="TestData\**\*.pdf" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Receipts.Contract\Receipts.Contract.csproj" />
    <ProjectReference Include="..\Receipts.Gabel\Receipts.Gabel.csproj" />
  </ItemGroup>

</Project>
```

Copy the six real invoice fixtures (from the repo root, Git Bash):

```bash
mkdir -p src/Receipts/Receipts.Tests/TestData
cp "C:/git/Gabel.PrijemkaSync/src/Gabel.PrijemkaSync.Tests/TestData/"*.pdf src/Receipts/Receipts.Tests/TestData/
ls src/Receipts/Receipts.Tests/TestData
```

Expected listing: `ft2-250334del20251118.pdf ft2-260014del20260130.pdf ft2-260097del20260331.pdf ft2-260180del20260528.pdf ft2-260220del20260623.pdf pr1-5605del20260123.pdf`.

Register the three projects:

```bash
dotnet sln src/eCommerce.sln add --solution-folder Receipts src/Receipts/Receipts.Contract/Receipts.Contract.csproj src/Receipts/Receipts.Gabel/Receipts.Gabel.csproj src/Receipts/Receipts.Tests/Receipts.Tests.csproj
```

- [ ] **Step 3: Write the failing tests**

Create `src/Receipts/Receipts.Tests/GabelInvoiceParserTests.cs` — this is Gabel.PrijemkaSync's test file moved over. The only edits: namespace, the `using Receipts.Gabel;`, and one added `SupplierName` assertion (marked below).

```csharp
using Receipts.Gabel;

namespace Receipts.Tests;

/// <summary>
/// Assertions here are cross-checked by hand against the printed totals in the real sample PDFs —
/// not just internal consistency. Moved unchanged from Gabel.PrijemkaSync.Tests to prove the parser
/// was not altered by the relocation.
/// </summary>
public class GabelInvoiceParserTests
{
    private static string TestDataPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    [Fact]
    public void Parses_invoice_250334_with_19_lines_and_no_warnings()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("ft2-250334del20251118.pdf"), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);
        Assert.Equal("Gabel", invoice.SupplierName); // added in the merge — the only new assertion
        Assert.Equal("FT 2/250334", invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2025, 11, 18), invoice.InvoiceDate);
        Assert.Equal("EUR", invoice.Currency);
        Assert.Equal(19, invoice.Lines.Count);
        Assert.Equal(160.00m, invoice.FreightAmount);
        Assert.Equal(2491.41m, invoice.PrintedGoodsNetTotal);
        Assert.Equal(2651.41m, invoice.PrintedGrandTotal);
        Assert.Equal(invoice.PrintedGoodsNetTotal, invoice.ComputedGoodsNetTotal);
        Assert.Empty(invoice.Warnings);

        var firstLine = invoice.Lines[0];
        Assert.Equal("7009190321200", firstLine.SupplierCode);
        Assert.Equal("DH PLATINUM SERIES CM.120", firstLine.Description);
        Assert.Equal("PA", firstLine.Unit);
        Assert.Equal(1.0000m, firstLine.Quantity);
        Assert.Equal(94.500m, firstLine.UnitPrice);
        Assert.Equal(35.00m, firstLine.DiscountPercent);
        Assert.Equal(61.43m, firstLine.LineAmount);

        // "CM.120" pattern — confirmed live that this corresponds to a Pohoda card with stk:orderName "120 cm".
        Assert.Equal("120 cm", firstLine.VariantSizeHint);
    }

    [Fact]
    public void Detects_fixed_size_variant_hints_in_their_three_real_forms()
    {
        var parser = new GabelInvoiceParser();

        var ok250334 = parser.TryParse(TestDataPath("ft2-250334del20251118.pdf"), out var invoice250334, out var skipReason1);
        Assert.True(ok250334, skipReason1);
        Assert.NotNull(invoice250334);

        var ok260220 = parser.TryParse(TestDataPath("ft2-260220del20260623.pdf"), out var invoice260220, out var skipReason2);
        Assert.True(ok260220, skipReason2);
        Assert.NotNull(invoice260220);

        // Form 1: "CM.NNN" — e.g. "DH PLATINUM SERIES CM.120".
        var dhPlatinum = invoice250334.Lines.Single(l => l.Description.Contains("DH PLATINUM"));
        Assert.Equal("120 cm", dhPlatinum.VariantSizeHint);

        // Form 2: "NNNCM" — e.g. "X-1.35 ACTIVE ORANGE 135CM".
        var activeOrange135 = invoice260220.Lines.Single(l => l.Description.Contains("135CM"));
        Assert.Equal("135 cm", activeOrange135.VariantSizeHint);

        // Form 3: "Cm NNN" — e.g. "VIPER 3S-TR Cm 115".
        var viper115 = invoice250334.Lines.Single(l => l.Description.Contains("Cm 115"));
        Assert.Equal("115 cm", viper115.VariantSizeHint);
    }

    [Fact]
    public void Does_not_flag_adjustable_length_ranges_or_plain_colour_variants_as_size_variants()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("ft2-260220del20260623.pdf"), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);

        // "73-110CM" is an adjustable-length range (telescopic pole), not a fixed-size variant.
        var nordicEnergy = invoice.Lines.Single(l => l.Description.Contains("NORDIC ENERGY"));
        Assert.Null(nordicEnergy.VariantSizeHint);

        // Colour-only variants (no size in the name) must NOT be flagged as size variants.
        var stretchLiteLilac = invoice.Lines.Single(l => l.Description.Contains("STRETCH LITE CARBON LILAC"));
        Assert.Null(stretchLiteLilac.VariantSizeHint);
    }

    [Fact]
    public void Parses_invoice_260220_with_14_lines_and_no_warnings()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("ft2-260220del20260623.pdf"), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);
        Assert.Equal("FT 2/260220", invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 6, 23), invoice.InvoiceDate);
        Assert.Equal(14, invoice.Lines.Count);
        Assert.Equal(180.00m, invoice.FreightAmount);
        Assert.Equal(3277.11m, invoice.PrintedGoodsNetTotal);
        Assert.Equal(3457.11m, invoice.PrintedGrandTotal);
        Assert.Empty(invoice.Warnings);

        // This invoice is the actual delivery for pre-invoice 5761, which ordered 2 extra IBEX lines
        // that were backordered and are correctly absent here — confirms lines come only from the
        // final invoice, never the pre-invoice.
        Assert.DoesNotContain(invoice.Lines, l => l.Description.Contains("IBEX", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ft2-260014del20260130.pdf")]
    [InlineData("ft2-260097del20260331.pdf")]
    [InlineData("ft2-260180del20260528.pdf")]
    public void Parses_remaining_sample_invoices_without_warnings(string fileName)
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath(fileName), out var invoice, out var skipReason);

        Assert.True(ok, skipReason);
        Assert.NotNull(invoice);
        Assert.NotEmpty(invoice.Lines);
        Assert.Empty(invoice.Warnings);
        Assert.Equal(invoice.PrintedGoodsNetTotal, invoice.ComputedGoodsNetTotal);
    }

    [Fact]
    public void Skips_proforma_preinvoice_with_explanatory_reason()
    {
        var parser = new GabelInvoiceParser();

        var ok = parser.TryParse(TestDataPath("pr1-5605del20260123.pdf"), out var invoice, out var skipReason);

        Assert.False(ok);
        Assert.Null(invoice);
        Assert.Contains("PROFORMA", skipReason, StringComparison.OrdinalIgnoreCase);
    }
}
```

Create `src/Receipts/Receipts.Tests/GabelVariantDetectorTests.cs`:

```csharp
using Receipts.Contract;
using Receipts.Gabel;

namespace Receipts.Tests;

public class GabelVariantDetectorTests
{
    [Theory]
    [InlineData("DH PLATINUM SERIES CM.120", "120 cm", "DH PLATINUM SERIES")]
    [InlineData("X-1.35 ACTIVE ORANGE 135CM", "135 cm", "X-1.35 ACTIVE ORANGE")]
    [InlineData("VIPER 3S-TR Cm 115", "115 cm", "VIPER 3S-TR")]
    [InlineData("SLD KIT LIGHT BLUE 130CM", "130 cm", "SLD KIT LIGHT BLUE")]
    public void Detect_returns_the_size_hint_and_the_name_without_the_size_token(string text, string expectedHint, string expectedBaseName)
    {
        var (sizeHint, baseName) = GabelVariantDetector.Detect(text);

        Assert.Equal(expectedHint, sizeHint);
        Assert.Equal(expectedBaseName, baseName);
    }

    [Theory]
    [InlineData("NORDIC ENERGY FL 73-110CM")]
    [InlineData("TREKKING 77/130CM")]
    [InlineData("STRETCH LITE CARBON LILAC")]
    [InlineData("Gabel SLK-R - pretekárska palica")]
    public void Detect_returns_nulls_for_adjustable_ranges_and_names_without_a_size(string text)
    {
        var (sizeHint, baseName) = GabelVariantDetector.Detect(text);

        Assert.Null(sizeHint);
        Assert.Null(baseName);
    }

    [Fact]
    public void LooksLikeSizeVariant_is_exposed_through_IVariantDetector()
    {
        IVariantDetector detector = new GabelVariantDetector();

        Assert.True(detector.LooksLikeSizeVariant("Gabel SLK-R - pretekárska palica 125 cm"));
        Assert.False(detector.LooksLikeSizeVariant("Gabel SLK-R - pretekárska palica"));
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: build error — `GabelInvoiceParser` / `GabelVariantDetector` do not exist.

- [ ] **Step 5: Implement `GabelVariantDetector` and `GabelInvoiceParser`**

Create `src/Receipts/Receipts.Gabel/GabelVariantDetector.cs`:

```csharp
using System.Text.RegularExpressions;
using Receipts.Contract;

namespace Receipts.Gabel;

/// <summary>
/// Gabel's size-variant naming convention, used both by <see cref="GabelInvoiceParser"/> (to tag
/// invoice lines, via the static <see cref="Detect"/>) and by <c>Receipts.Core</c> through
/// <see cref="IVariantDetector"/> (to exclude sibling variants — which also contain the base name as
/// a substring — from a master-card search).
/// </summary>
public sealed partial class GabelVariantDetector : IVariantDetector
{
    // Detects a fixed-size variant: "CM" (any case) plus a single number, in either order, with
    // optional whitespace/dot between them — "CM.130", "135CM", "Cm 115".
    [GeneratedRegex(@"(?<a>\d{2,3})\s*CM\b|CM\.?\s*(?<b>\d{2,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex VariantSizeRegex();

    // A number RANGE before "CM" (e.g. "73-110CM", "77/130CM") means an adjustable-length pole, not
    // a fixed-size variant — must be excluded even though its trailing number alone would otherwise
    // match VariantSizeRegex (e.g. the "110" in "73-110CM").
    [GeneratedRegex(@"\d+\s*[-/]\s*\d+\s*CM\b", RegexOptions.IgnoreCase)]
    private static partial Regex VariantRangeRegex();

    /// <summary>True if <paramref name="text"/> contains a fixed-size variant pattern (not a range).</summary>
    public bool LooksLikeSizeVariant(string text) => Detect(text).SizeHint is not null;

    /// <summary>
    /// Detects a fixed-size variant in <paramref name="text"/> and returns both the size hint (e.g.
    /// "120 cm") and the text with the size token removed (e.g. "DH PLATINUM SERIES" from
    /// "DH PLATINUM SERIES CM.120"). Returns (null, null) if no fixed-size pattern is found.
    /// </summary>
    public static (string? SizeHint, string? BaseName) Detect(string text)
    {
        if (VariantRangeRegex().IsMatch(text))
        {
            return (null, null);
        }

        var match = VariantSizeRegex().Match(text);
        if (!match.Success)
        {
            return (null, null);
        }

        var size = match.Groups["a"].Success ? match.Groups["a"].Value : match.Groups["b"].Value;
        var baseName = text.Remove(match.Index, match.Length).Trim();
        return ($"{size} cm", baseName);
    }
}
```

Create `src/Receipts/Receipts.Gabel/GabelInvoiceParser.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Common.Documents.PdfReader;
using Receipts.Contract;

namespace Receipts.Gabel;

/// <summary>Deterministic parser for Gabel final invoices (`ft2-*.pdf`). Ported unchanged from Gabel.PrijemkaSync.Extraction.</summary>
public sealed partial class GabelInvoiceParser : IInvoiceParser
{
    public const string SupplierName = "Gabel";

    // Header line, e.g.: "33.05548 SK2121512712 57.00039 DISTRIBUTORI CEE EUR FT 2/260220 23/06/2026 1"
    [GeneratedRegex(@"(?<currency>[A-Z]{3})\s+FT\s+(?<num>\d+/\d+)\s+(?<date>\d{2}/\d{2}/\d{4})")]
    private static partial Regex HeaderRegex();

    // Summary line, e.g.: "5.041,70 3.277,11 180,00 3.457,11" (LORDO / NETTO MERCE / SPESE TRASP. / TOTALE IMPONIBILE)
    [GeneratedRegex(@"^(?<lordo>[\d.]+,\d{2})\s+(?<netto>[\d.]+,\d{2})\s+(?<spese>[\d.]+,\d{2})\s+(?<imponibile>[\d.]+,\d{2})$")]
    private static partial Regex SummaryRegex();

    // Item line, e.g.: "7008352650000 STRETCH LITE CARBON LILAC PA 20 35,000 35,00 455,00 I41"
    [GeneratedRegex(@"^(?<code>\d{10,14})\s+(?<desc>.+?)\s+(?<unit>PA|PZ)\s+(?<qty>\d+(?:,\d+)?)\s+(?<price>\d+(?:,\d+)?)\s+(?<discount>\d+(?:,\d+)?)\s+(?<amount>\d+(?:,\d+)?)(?:\s+\S+)?$")]
    private static partial Regex ItemLineRegex();

    [GeneratedRegex(@"^\d{10,14}\b")]
    private static partial Regex LooksLikeItemLineRegex();

    /// <summary>
    /// Parses a Gabel final invoice PDF (`ft2-*.pdf`) into a <see cref="ParsedInvoice"/>.
    /// Returns false with a human-readable <paramref name="skipReason"/> if the PDF is not a final
    /// invoice (e.g. it is a PROFORMA/pre-invoice, which is intentionally out of scope).
    /// </summary>
    public bool TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason)
    {
        var rows = PdfRowReader.ReadRows(pdfPath);
        var fullText = string.Join('\n', rows);

        var headerMatch = HeaderRegex().Match(fullText);
        if (!headerMatch.Success)
        {
            invoice = null;
            skipReason = fullText.Contains("PROFORMA INVOICE", StringComparison.OrdinalIgnoreCase)
                ? "PROFORMA INVOICE (pre-invoice) — mimo rozsahu automatizácie, ignorované."
                : "Nenašlo sa číslo faktúry vo formáte 'FT n/nnnnnn' — pravdepodobne nejde o finálnu Gabel faktúru.";
            return false;
        }

        var invoiceNumber = $"FT {headerMatch.Groups["num"].Value}";
        var invoiceDate = DateOnly.ParseExact(headerMatch.Groups["date"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        var currency = headerMatch.Groups["currency"].Value;

        var warnings = new List<string>();
        var lines = new List<InvoiceLine>();

        foreach (var row in rows)
        {
            var itemMatch = ItemLineRegex().Match(row);
            if (itemMatch.Success)
            {
                var description = itemMatch.Groups["desc"].Value.Trim();
                var (variantSizeHint, variantBaseName) = GabelVariantDetector.Detect(description);
                lines.Add(new InvoiceLine
                {
                    SupplierCode = itemMatch.Groups["code"].Value,
                    Description = description,
                    Unit = itemMatch.Groups["unit"].Value,
                    Quantity = ParseItalianDecimal(itemMatch.Groups["qty"].Value),
                    UnitPrice = ParseItalianDecimal(itemMatch.Groups["price"].Value),
                    DiscountPercent = ParseItalianDecimal(itemMatch.Groups["discount"].Value),
                    VariantSizeHint = variantSizeHint,
                    VariantBaseName = variantBaseName,
                    LineAmount = ParseItalianDecimal(itemMatch.Groups["amount"].Value)
                });
            }
            else if (LooksLikeItemLineRegex().IsMatch(row))
            {
                warnings.Add($"Riadok vyzerá ako položka, ale nesedí na očakávaný formát a bol preskočený: \"{row}\"");
            }
        }

        var summaryMatch = rows.Select(r => SummaryRegex().Match(r)).FirstOrDefault(m => m.Success);
        decimal printedNet;
        decimal freight;
        decimal printedGrandTotal;

        if (summaryMatch is not null)
        {
            printedNet = ParseItalianDecimal(summaryMatch.Groups["netto"].Value);
            freight = ParseItalianDecimal(summaryMatch.Groups["spese"].Value);
            printedGrandTotal = ParseItalianDecimal(summaryMatch.Groups["imponibile"].Value);
        }
        else
        {
            warnings.Add("Súhrnný riadok (NETTO MERCE / SPESE TRASP. / TOTALE IMPONIBILE) sa nenašiel — súčty nebolo možné overiť.");
            printedNet = lines.Sum(l => l.LineAmount);
            freight = 0m;
            printedGrandTotal = printedNet;
        }

        var parsed = new ParsedInvoice
        {
            SupplierName = SupplierName,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            Currency = currency,
            Lines = lines,
            FreightAmount = freight,
            PrintedGoodsNetTotal = printedNet,
            PrintedGrandTotal = printedGrandTotal,
            SourceFilePath = pdfPath,
            SourceFileSha256 = ComputeSha256(pdfPath)
        };

        foreach (var warning in warnings)
        {
            parsed.Warnings.Add(warning);
        }

        var computedVsPrinted = Math.Abs(parsed.ComputedGoodsNetTotal - parsed.PrintedGoodsNetTotal);
        if (computedVsPrinted > 0.02m)
        {
            parsed.Warnings.Add(
                $"Súčet riadkov ({parsed.ComputedGoodsNetTotal:F2}) nesedí na tlačenú hodnotu NETTO MERCE ({parsed.PrintedGoodsNetTotal:F2}) — over manuálne pred zápisom.");
        }

        var computedGrandTotal = parsed.ComputedGoodsNetTotal + parsed.FreightAmount;
        var grandTotalDiff = Math.Abs(computedGrandTotal - parsed.PrintedGrandTotal);
        if (grandTotalDiff > 0.02m)
        {
            parsed.Warnings.Add(
                $"Súčet riadkov + doprava ({computedGrandTotal:F2}) nesedí na tlačený TOTALE IMPONIBILE/NET TO PAY ({parsed.PrintedGrandTotal:F2}) — over manuálne pred zápisom.");
        }

        invoice = parsed;
        skipReason = null;
        return true;
    }

    private static decimal ParseItalianDecimal(string value) =>
        decimal.Parse(value.Replace(".", string.Empty).Replace(',', '.'), CultureInfo.InvariantCulture);

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 17` (8 parser test cases: 5 facts + 3 theory rows; 9 detector cases: 4 + 4 theory rows + 1 fact).

- [ ] **Step 7: Commit**

```bash
git add src/eCommerce.sln src/Receipts/Receipts.Contract src/Receipts/Receipts.Gabel src/Receipts/Receipts.Tests
git commit -m "feat: add Receipts.Contract and Receipts.Gabel parser plugin with ported invoice tests"
```

---

## Task 5: `Receipts.Core` persistence — `StockMappingCache`, `ProposalRepository`, `ReceiptsStateStore`

**Files:**
- Create: `src/Receipts/Receipts.Core/Receipts.Core.csproj`
- Create: `src/Receipts/Receipts.Core/Persistence/StockMappingCache.cs`
- Create: `src/Receipts/Receipts.Core/Persistence/ProposalRepository.cs`
- Create: `src/Receipts/Receipts.Core/Persistence/ReceiptsStateStore.cs`
- Modify: `src/Receipts/Receipts.Tests/Receipts.Tests.csproj` (add project references)
- Create: `src/Receipts/Receipts.Tests/Fakes/Make.cs`
- Test: `src/Receipts/Receipts.Tests/ReceiptsStateStoreTests.cs`

**Interfaces:**
- Consumes: `Common.Data.Sqlite.SqliteDatabase.OpenAsync(string dbPath, IReadOnlyList<string> schemaStatements, CancellationToken)`, `SqliteDatabase.Connection`, `ProcessedFileTracker(SqliteConnection)` with `Schema`/`IsProcessedAsync`/`MarkProcessedAsync`/`ForgetAsync(referenceId)`, `AuditLog(SqliteConnection)` with `Schema`/`AppendAsync(source, message)`/`ReadAllAsync()` (PR1); `ReceiptProposal`, `ReceiptLineProposal`, `ProposalStatus`, `ReceiptProposalType` (Task 4).
- Produces (used by Tasks 6–8):
  - `Receipts.Core.Persistence.StockMappingCache(SqliteConnection)` — `const string Schema`, `Task<string?> GetAsync(string supplierCode, CancellationToken ct = default)`, `Task SaveAsync(string supplierCode, string pohodaStockCode, CancellationToken ct = default)`
  - `Receipts.Core.Persistence.ProposalRepository(SqliteConnection)` — `const string Schema`, `Task SaveAsync(ReceiptProposal, CancellationToken ct = default)`, `Task<ReceiptProposal?> GetAsync(string id, …)`, `Task<IReadOnlyList<ReceiptProposal>> GetByStatusAsync(ProposalStatus, …)`, `Task ResetAsync(string id, …)`
  - `Receipts.Core.Persistence.ReceiptsStateStore : IAsyncDisposable` — `static Task<ReceiptsStateStore> OpenAsync(string dbPath, CancellationToken ct = default)`, properties `ProcessedFiles : ProcessedFileTracker`, `Audit : AuditLog`, `StockMappings : StockMappingCache`, `Proposals : ProposalRepository`, `Task ResetProposalAsync(string proposalId, CancellationToken ct = default)`
  - test helper `Receipts.Tests.Fakes.Make` — `Line(...)`, `Invoice(...)`, `LineProposal(...)`, `Proposal(...)` builders (exact signatures in Step 2)

- [ ] **Step 1: Scaffold `Receipts.Core` and wire the test project to it**

Create `src/Receipts/Receipts.Core/Receipts.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.11" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.11" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Common\Data\Common.Data.Sqlite\Common.Data.Sqlite.csproj" />
    <ProjectReference Include="..\..\Pohoda\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\Receipts.Contract\Receipts.Contract.csproj" />
  </ItemGroup>

</Project>
```

```bash
dotnet sln src/eCommerce.sln add --solution-folder Receipts src/Receipts/Receipts.Core/Receipts.Core.csproj
```

Edit `src/Receipts/Receipts.Tests/Receipts.Tests.csproj` — replace the `<ProjectReference>` item group with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\Common\Data\Common.Data.Sqlite\Common.Data.Sqlite.csproj" />
    <ProjectReference Include="..\..\Pohoda\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\Receipts.Contract\Receipts.Contract.csproj" />
    <ProjectReference Include="..\Receipts.Core\Receipts.Core.csproj" />
    <ProjectReference Include="..\Receipts.Gabel\Receipts.Gabel.csproj" />
  </ItemGroup>
```

- [ ] **Step 2: Write the test-data builders**

Create `src/Receipts/Receipts.Tests/Fakes/Make.cs` (shared by Tasks 5–8):

```csharp
using Receipts.Contract;

namespace Receipts.Tests.Fakes;

/// <summary>Small builders so tests state only what matters. Defaults mirror real Gabel invoice FT 2/260220.</summary>
internal static class Make
{
    public static InvoiceLine Line(
        string supplierCode,
        string description,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m,
        string? variantSizeHint = null,
        string? variantBaseName = null) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            LineAmount = Math.Round(quantity * unitPrice * (1 - discountPercent / 100m), 2),
            VariantSizeHint = variantSizeHint,
            VariantBaseName = variantBaseName
        };

    public static ParsedInvoice Invoice(
        IReadOnlyList<InvoiceLine> lines,
        decimal freight = 180m,
        string invoiceNumber = "FT 2/260220",
        string supplierName = "Gabel",
        params string[] warnings)
    {
        var goodsNet = lines.Sum(l => l.LineAmount);
        var invoice = new ParsedInvoice
        {
            SupplierName = supplierName,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            Currency = "EUR",
            Lines = lines,
            FreightAmount = freight,
            PrintedGoodsNetTotal = goodsNet,
            PrintedGrandTotal = goodsNet + freight,
            SourceFilePath = @"C:\faktury\ft2-260220del20260623.pdf",
            SourceFileSha256 = "SHA-260220"
        };
        invoice.Warnings.AddRange(warnings);
        return invoice;
    }

    public static ReceiptLineProposal LineProposal(
        string supplierCode,
        string description,
        bool isNewStockItem,
        string? pohodaStockCode = null,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m,
        string? variantSizeHint = null,
        string? variantBaseName = null,
        string? masterCardCode = null,
        bool masterCardSearched = false) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            PohodaStockCode = pohodaStockCode ?? supplierCode,
            IsNewStockItem = isNewStockItem,
            VariantSizeHint = variantSizeHint,
            VariantBaseName = variantBaseName,
            MasterCardCode = masterCardCode,
            MasterCardSearched = masterCardSearched
        };

    public static ReceiptProposal Proposal(
        IReadOnlyList<ReceiptLineProposal> lines,
        ReceiptProposalType type = ReceiptProposalType.CreateReceipt,
        string? existingReceiptId = null,
        string? existingReceiptNumber = null,
        decimal? freightToAdd = 180m,
        string id = "proposal-1",
        string invoiceNumber = "FT 2/260220",
        params string[] warnings) =>
        new()
        {
            Id = id,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            SourceFilePath = @"C:\faktury\ft2-260220del20260623.pdf",
            SourceFileSha256 = "SHA-260220",
            Type = type,
            ExistingReceiptId = existingReceiptId,
            ExistingReceiptNumber = existingReceiptNumber,
            HeaderText = type == ReceiptProposalType.CreateReceipt ? $"Gabel {invoiceNumber}" : null,
            LinesToAdd = lines,
            FreightToAdd = freightToAdd,
            Warnings = warnings,
            CreatedAt = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero)
        };
}
```

- [ ] **Step 3: Write the failing tests**

Create `src/Receipts/Receipts.Tests/ReceiptsStateStoreTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Receipts.Contract;
using Receipts.Core.Persistence;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class ReceiptsStateStoreTests
{
    /// <summary>Opens a fresh temp-file database; disposing removes the file (same convention as Common.Data.Sqlite.Tests).</summary>
    private sealed class TempStore : IAsyncDisposable
    {
        private readonly string _dbPath;
        public ReceiptsStateStore Store { get; }

        private TempStore(string dbPath, ReceiptsStateStore store)
        {
            _dbPath = dbPath;
            Store = store;
        }

        public static async Task<TempStore> OpenAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"receipts-state-test-{Guid.NewGuid():N}.db");
            return new TempStore(dbPath, await ReceiptsStateStore.OpenAsync(dbPath));
        }

        public async ValueTask DisposeAsync()
        {
            await Store.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }

    [Fact]
    public async Task StockMappings_return_null_for_unknown_codes_and_upsert_known_ones()
    {
        await using var temp = await TempStore.OpenAsync();
        var mappings = temp.Store.StockMappings;

        Assert.Null(await mappings.GetAsync("7009190321200"));

        await mappings.SaveAsync("7009190321200", "7009190321200");
        await mappings.SaveAsync("7009190321200", "GN-7009190321200");

        Assert.Equal("GN-7009190321200", await mappings.GetAsync("7009190321200"));
    }

    [Fact]
    public async Task Proposals_round_trip_every_field_including_lines_and_warnings()
    {
        await using var temp = await TempStore.OpenAsync();

        var proposal = Make.Proposal(
            [
                Make.LineProposal("7009190321200", "DH PLATINUM SERIES CM.120", isNewStockItem: false, quantity: 2m, unitPrice: 94.5m),
                Make.LineProposal("7009181471300", "SLD KIT LIGHT BLUE 130CM", isNewStockItem: true,
                    variantSizeHint: "130 cm", variantBaseName: "SLD KIT LIGHT BLUE", masterCardCode: "7009181470000", masterCardSearched: true)
            ],
            type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42",
            existingReceiptNumber: "26PR00012",
            freightToAdd: 160.5m,
            warnings: ["prvé upozornenie", "druhé upozornenie"]);

        await temp.Store.Proposals.SaveAsync(proposal);
        var loaded = await temp.Store.Proposals.GetAsync("proposal-1");

        Assert.NotNull(loaded);
        Assert.Equal(proposal.Id, loaded.Id);
        Assert.Equal(proposal.InvoiceNumber, loaded.InvoiceNumber);
        Assert.Equal(proposal.InvoiceDate, loaded.InvoiceDate);
        Assert.Equal(proposal.SourceFilePath, loaded.SourceFilePath);
        Assert.Equal(proposal.SourceFileSha256, loaded.SourceFileSha256);
        Assert.Equal(ReceiptProposalType.AppendLines, loaded.Type);
        Assert.Equal("42", loaded.ExistingReceiptId);
        Assert.Equal("26PR00012", loaded.ExistingReceiptNumber);
        Assert.Null(loaded.HeaderText);
        Assert.Equal(160.5m, loaded.FreightToAdd);
        Assert.Equal(["prvé upozornenie", "druhé upozornenie"], loaded.Warnings);
        Assert.Equal(ProposalStatus.PendingApproval, loaded.Status);
        Assert.Null(loaded.FailureReason);
        Assert.Null(loaded.ResultReceiptNumber);
        Assert.Equal(proposal.CreatedAt, loaded.CreatedAt);
        Assert.Null(loaded.AppliedAt);

        Assert.Equal(2, loaded.LinesToAdd.Count);
        var variant = loaded.LinesToAdd[1];
        Assert.Equal("7009181471300", variant.SupplierCode);
        Assert.Equal("SLD KIT LIGHT BLUE 130CM", variant.Description);
        Assert.True(variant.IsNewStockItem);
        Assert.Equal("130 cm", variant.VariantSizeHint);
        Assert.Equal("SLD KIT LIGHT BLUE", variant.VariantBaseName);
        Assert.Equal("7009181470000", variant.MasterCardCode);
        Assert.True(variant.MasterCardSearched);
        Assert.Equal(94.5m, loaded.LinesToAdd[0].UnitPrice);
        Assert.Equal(2m, loaded.LinesToAdd[0].Quantity);
    }

    [Fact]
    public async Task Proposals_GetAsync_returns_null_for_an_unknown_id()
    {
        await using var temp = await TempStore.OpenAsync();

        Assert.Null(await temp.Store.Proposals.GetAsync("missing"));
    }

    [Fact]
    public async Task Proposals_SaveAsync_updates_status_result_and_applied_time_of_an_existing_row()
    {
        await using var temp = await TempStore.OpenAsync();
        var proposal = Make.Proposal([Make.LineProposal("A", "Line A", isNewStockItem: false)]);
        await temp.Store.Proposals.SaveAsync(proposal);

        proposal.Status = ProposalStatus.Applied;
        proposal.ResultReceiptNumber = "26PR00013";
        proposal.AppliedAt = new DateTimeOffset(2026, 9, 11, 9, 30, 0, TimeSpan.Zero);
        await temp.Store.Proposals.SaveAsync(proposal);

        var loaded = await temp.Store.Proposals.GetAsync(proposal.Id);
        Assert.NotNull(loaded);
        Assert.Equal(ProposalStatus.Applied, loaded.Status);
        Assert.Equal("26PR00013", loaded.ResultReceiptNumber);
        Assert.Equal(proposal.AppliedAt, loaded.AppliedAt);
    }

    [Fact]
    public async Task Proposals_GetByStatusAsync_filters_by_status_in_creation_order()
    {
        await using var temp = await TempStore.OpenAsync();
        var pendingOld = Make.Proposal([Make.LineProposal("A", "A", false)], id: "p-old");
        var pendingNew = Make.Proposal([Make.LineProposal("B", "B", false)], id: "p-new");
        var failed = Make.Proposal([Make.LineProposal("C", "C", false)], id: "p-failed");
        failed.Status = ProposalStatus.Failed;
        failed.FailureReason = "mServer vrátil chybový stav.";

        // Save the newer one first to prove ordering is by created_at, not insertion.
        await temp.Store.Proposals.SaveAsync(new ReceiptProposal
        {
            Id = pendingNew.Id, InvoiceNumber = pendingNew.InvoiceNumber, InvoiceDate = pendingNew.InvoiceDate,
            SourceFilePath = pendingNew.SourceFilePath, SourceFileSha256 = pendingNew.SourceFileSha256, Type = pendingNew.Type,
            HeaderText = pendingNew.HeaderText, LinesToAdd = pendingNew.LinesToAdd, FreightToAdd = pendingNew.FreightToAdd,
            Warnings = pendingNew.Warnings, CreatedAt = pendingNew.CreatedAt.AddMinutes(5)
        });
        await temp.Store.Proposals.SaveAsync(pendingOld);
        await temp.Store.Proposals.SaveAsync(failed);

        var pending = await temp.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval);
        var failedList = await temp.Store.Proposals.GetByStatusAsync(ProposalStatus.Failed);

        Assert.Equal(["p-old", "p-new"], pending.Select(p => p.Id));
        var onlyFailed = Assert.Single(failedList);
        Assert.Equal("mServer vrátil chybový stav.", onlyFailed.FailureReason);
    }

    [Fact]
    public async Task ResetProposalAsync_returns_the_proposal_to_PendingApproval_and_forgets_its_source_file()
    {
        await using var temp = await TempStore.OpenAsync();
        var proposal = Make.Proposal([Make.LineProposal("A", "A", false)]);
        proposal.Status = ProposalStatus.Failed;
        proposal.FailureReason = "boom";
        proposal.ResultReceiptNumber = "26PR00099";
        proposal.AppliedAt = DateTimeOffset.UtcNow;
        await temp.Store.Proposals.SaveAsync(proposal);
        await temp.Store.ProcessedFiles.MarkProcessedAsync(proposal.SourceFileSha256, proposal.SourceFilePath, proposal.Id);

        await temp.Store.ResetProposalAsync(proposal.Id);

        var loaded = await temp.Store.Proposals.GetAsync(proposal.Id);
        Assert.NotNull(loaded);
        Assert.Equal(ProposalStatus.PendingApproval, loaded.Status);
        Assert.Null(loaded.FailureReason);
        Assert.Null(loaded.ResultReceiptNumber);
        Assert.Null(loaded.AppliedAt);
        Assert.False(await temp.Store.ProcessedFiles.IsProcessedAsync(proposal.SourceFileSha256));
    }

    [Fact]
    public async Task Audit_and_ProcessedFiles_share_the_same_database()
    {
        await using var temp = await TempStore.OpenAsync();

        await temp.Store.Audit.AppendAsync(@"C:\faktury\a.pdf", "Vytvorená nová príjemka.");
        await temp.Store.ProcessedFiles.MarkProcessedAsync("sha-a", @"C:\faktury\a.pdf", "proposal-a");

        var audit = await temp.Store.Audit.ReadAllAsync();
        Assert.Single(audit);
        Assert.Equal("Vytvorená nová príjemka.", audit[0].Message);
        Assert.True(await temp.Store.ProcessedFiles.IsProcessedAsync("sha-a"));
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: build error — `Receipts.Core.Persistence` types do not exist.

- [ ] **Step 5: Implement the persistence classes**

Create `src/Receipts/Receipts.Core/Persistence/StockMappingCache.cs`:

```csharp
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
```

Create `src/Receipts/Receipts.Core/Persistence/ProposalRepository.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Receipts.Contract;

namespace Receipts.Core.Persistence;

/// <summary>
/// Proposals awaiting / having received `apply`. Lines and warnings are stored as JSON columns —
/// this is a handful of rows for a single-operator tool, not a reporting database.
/// </summary>
public sealed class ProposalRepository(SqliteConnection connection)
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS proposals (
            id TEXT PRIMARY KEY,
            invoice_number TEXT NOT NULL,
            invoice_date TEXT NOT NULL,
            source_file_path TEXT NOT NULL,
            source_file_sha256 TEXT NOT NULL,
            type TEXT NOT NULL,
            existing_receipt_id TEXT NULL,
            existing_receipt_number TEXT NULL,
            header_text TEXT NULL,
            lines_json TEXT NOT NULL,
            freight_to_add TEXT NULL,
            warnings_json TEXT NOT NULL,
            status TEXT NOT NULL,
            failure_reason TEXT NULL,
            result_receipt_number TEXT NULL,
            created_at TEXT NOT NULL,
            applied_at TEXT NULL
        );
        """;

    /// <summary>Inserts a new proposal, or — for an existing id — updates only its outcome columns (status, failure, result, applied_at).</summary>
    public async Task SaveAsync(ReceiptProposal proposal, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO proposals (id, invoice_number, invoice_date, source_file_path, source_file_sha256, type,
                existing_receipt_id, existing_receipt_number, header_text, lines_json, freight_to_add,
                warnings_json, status, failure_reason, result_receipt_number, created_at, applied_at)
            VALUES ($id, $invNum, $invDate, $path, $sha, $type, $exId, $exNum, $header, $lines, $freight,
                $warnings, $status, $failure, $result, $createdAt, $appliedAt)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                failure_reason = excluded.failure_reason,
                result_receipt_number = excluded.result_receipt_number,
                applied_at = excluded.applied_at
            """;
        cmd.Parameters.AddWithValue("$id", proposal.Id);
        cmd.Parameters.AddWithValue("$invNum", proposal.InvoiceNumber);
        cmd.Parameters.AddWithValue("$invDate", proposal.InvoiceDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$path", proposal.SourceFilePath);
        cmd.Parameters.AddWithValue("$sha", proposal.SourceFileSha256);
        cmd.Parameters.AddWithValue("$type", proposal.Type.ToString());
        cmd.Parameters.AddWithValue("$exId", (object?)proposal.ExistingReceiptId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$exNum", (object?)proposal.ExistingReceiptNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$header", (object?)proposal.HeaderText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lines", JsonSerializer.Serialize(proposal.LinesToAdd));
        cmd.Parameters.AddWithValue("$freight", proposal.FreightToAdd?.ToString(CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$warnings", JsonSerializer.Serialize(proposal.Warnings));
        cmd.Parameters.AddWithValue("$status", proposal.Status.ToString());
        cmd.Parameters.AddWithValue("$failure", (object?)proposal.FailureReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$result", (object?)proposal.ResultReceiptNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", proposal.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$appliedAt", proposal.AppliedAt?.ToString("O") ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ReceiptProposal>> GetByStatusAsync(ProposalStatus status, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM proposals WHERE status = $status ORDER BY created_at";
        cmd.Parameters.AddWithValue("$status", status.ToString());
        return await ReadProposalsAsync(cmd, ct);
    }

    public async Task<ReceiptProposal?> GetAsync(string id, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM proposals WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        var results = await ReadProposalsAsync(cmd, ct);
        return results.FirstOrDefault();
    }

    /// <summary>Resets a proposal back to PendingApproval, clearing failure/applied bookkeeping. See <see cref="ReceiptsStateStore.ResetProposalAsync"/> for the full operator-facing reset.</summary>
    public async Task ResetAsync(string id, CancellationToken ct = default)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE proposals
            SET status = $status, failure_reason = NULL, result_receipt_number = NULL, applied_at = NULL
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$status", ProposalStatus.PendingApproval.ToString());
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<IReadOnlyList<ReceiptProposal>> ReadProposalsAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var results = new List<ReceiptProposal>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var lines = JsonSerializer.Deserialize<List<ReceiptLineProposal>>(reader.GetString(reader.GetOrdinal("lines_json"))) ?? [];
            var warnings = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("warnings_json"))) ?? [];
            var freightOrdinal = reader.GetOrdinal("freight_to_add");

            results.Add(new ReceiptProposal
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                InvoiceNumber = reader.GetString(reader.GetOrdinal("invoice_number")),
                InvoiceDate = DateOnly.Parse(reader.GetString(reader.GetOrdinal("invoice_date")), CultureInfo.InvariantCulture),
                SourceFilePath = reader.GetString(reader.GetOrdinal("source_file_path")),
                SourceFileSha256 = reader.GetString(reader.GetOrdinal("source_file_sha256")),
                Type = Enum.Parse<ReceiptProposalType>(reader.GetString(reader.GetOrdinal("type"))),
                ExistingReceiptId = ReadNullableString(reader, "existing_receipt_id"),
                ExistingReceiptNumber = ReadNullableString(reader, "existing_receipt_number"),
                HeaderText = ReadNullableString(reader, "header_text"),
                LinesToAdd = lines,
                FreightToAdd = reader.IsDBNull(freightOrdinal) ? null : decimal.Parse(reader.GetString(freightOrdinal), CultureInfo.InvariantCulture),
                Warnings = warnings,
                Status = Enum.Parse<ProposalStatus>(reader.GetString(reader.GetOrdinal("status"))),
                FailureReason = ReadNullableString(reader, "failure_reason"),
                ResultReceiptNumber = ReadNullableString(reader, "result_receipt_number"),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at")), CultureInfo.InvariantCulture),
                AppliedAt = ReadNullableString(reader, "applied_at") is { } appliedAt
                    ? DateTimeOffset.Parse(appliedAt, CultureInfo.InvariantCulture)
                    : null
            });
        }

        return results;
    }

    private static string? ReadNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
```

Create `src/Receipts/Receipts.Core/Persistence/ReceiptsStateStore.cs`:

```csharp
using Common.Data.Sqlite;

namespace Receipts.Core.Persistence;

/// <summary>
/// The receipt-sync pipeline's local state: one SQLite file holding the two generic tables from
/// <c>Common.Data.Sqlite</c> (processed files for idempotency, audit log) and the two receipt-specific
/// tables (<see cref="StockMappingCache"/>, <see cref="ProposalRepository"/>).
/// </summary>
public sealed class ReceiptsStateStore : IAsyncDisposable
{
    private readonly SqliteDatabase _db;

    public ProcessedFileTracker ProcessedFiles { get; }
    public AuditLog Audit { get; }
    public StockMappingCache StockMappings { get; }
    public ProposalRepository Proposals { get; }

    private ReceiptsStateStore(SqliteDatabase db)
    {
        _db = db;
        ProcessedFiles = new ProcessedFileTracker(db.Connection);
        Audit = new AuditLog(db.Connection);
        StockMappings = new StockMappingCache(db.Connection);
        Proposals = new ProposalRepository(db.Connection);
    }

    public static async Task<ReceiptsStateStore> OpenAsync(string dbPath, CancellationToken ct = default)
    {
        var db = await SqliteDatabase.OpenAsync(dbPath,
            [ProcessedFileTracker.Schema, AuditLog.Schema, StockMappingCache.Schema, ProposalRepository.Schema], ct);
        return new ReceiptsStateStore(db);
    }

    /// <summary>
    /// Resets a proposal back to PendingApproval and forgets that its source file was processed, so it
    /// can be re-scanned or re-applied from scratch. Use this when `apply` reported an outcome that
    /// doesn't match what actually happened in Pohoda and the operator needs a clean retry.
    /// </summary>
    public async Task ResetProposalAsync(string proposalId, CancellationToken ct = default)
    {
        await Proposals.ResetAsync(proposalId, ct);
        await ProcessedFiles.ForgetAsync(proposalId, ct);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 24` (17 from Task 4 + 7 new).

- [ ] **Step 7: Commit**

```bash
git add src/eCommerce.sln src/Receipts/Receipts.Core src/Receipts/Receipts.Tests/Receipts.Tests.csproj src/Receipts/Receipts.Tests/Fakes/Make.cs src/Receipts/Receipts.Tests/ReceiptsStateStoreTests.cs
git commit -m "feat: add Receipts.Core persistence (stock mapping cache, proposal repository, state store)"
```

---

## Task 6: `Receipts.Core.ProposalBuilder` (read-only matching)

**Files:**
- Create: `src/Receipts/Receipts.Core/ProposalBuilder.cs`
- Create: `src/Receipts/Receipts.Tests/Fakes/FakeStockCardService.cs`
- Create: `src/Receipts/Receipts.Tests/Fakes/FakeVariantDetector.cs`
- Test: `src/Receipts/Receipts.Tests/ProposalBuilderTests.cs`

**Interfaces:**
- Consumes: `IStockCardService`, `StockNameMatch`, `ExistingReceipt`, `ExistingReceiptLine` (Task 1); `ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, `ReceiptLineProposal`, `ReceiptProposalType`, `IVariantDetector` (Task 4); `StockMappingCache`, `ReceiptsStateStore` (Task 5); `Make` (Task 5).
- Produces: `Receipts.Core.ProposalBuilder(IStockCardService stockCards, IVariantDetector variantDetector, StockMappingCache stockMappings, ILogger<ProposalBuilder> logger)` with `Task<ReceiptProposal> BuildAsync(ParsedInvoice invoice, IReadOnlyList<ExistingReceipt> recentReceipts, CancellationToken ct = default)`; test fakes `FakeStockCardService` (`ExistingCodes : HashSet<string>`, `NameSearchResults : List<StockNameMatch>`, `FindCalls : List<string>`, `SearchCalls : List<string>`, `CreateCalls : List<(string Code, string Name, string? OrderName)>`) and `FakeVariantDetector` (treats names ending in `" cm"` or containing `"CM"` as variants) — both reused by Task 7.

- [ ] **Step 1: Write the fakes**

Create `src/Receipts/Receipts.Tests/Fakes/FakeStockCardService.cs`:

```csharp
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Receipts.Tests.Fakes;

/// <summary>In-memory Pohoda stock agenda: a set of existing card codes plus a canned name-search result.</summary>
internal sealed class FakeStockCardService : IStockCardService
{
    public HashSet<string> ExistingCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StockNameMatch> NameSearchResults { get; } = [];

    public List<string> FindCalls { get; } = [];
    public List<string> SearchCalls { get; } = [];
    public List<(string Code, string Name, string? OrderName)> CreateCalls { get; } = [];

    public Task<string?> FindBySupplierCodeAsync(string supplierCode, CancellationToken ct = default)
    {
        FindCalls.Add(supplierCode);
        return Task.FromResult(ExistingCodes.Contains(supplierCode) ? supplierCode : null);
    }

    public Task<IReadOnlyList<StockNameMatch>> SearchByNameContainingAsync(string nameContains, CancellationToken ct = default)
    {
        SearchCalls.Add(nameContains);
        IReadOnlyList<StockNameMatch> results = NameSearchResults
            .Where(m => m.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<string> CreateAsync(string code, string name, string? orderName, CancellationToken ct = default)
    {
        CreateCalls.Add((code, name, orderName));
        ExistingCodes.Add(code);
        return Task.FromResult(code);
    }
}
```

Create `src/Receipts/Receipts.Tests/Fakes/FakeVariantDetector.cs`:

```csharp
using Receipts.Contract;

namespace Receipts.Tests.Fakes;

/// <summary>Deliberately simplistic, supplier-neutral rule so Core tests do not depend on Receipts.Gabel.</summary>
internal sealed class FakeVariantDetector : IVariantDetector
{
    public bool LooksLikeSizeVariant(string text) =>
        text.EndsWith(" cm", StringComparison.OrdinalIgnoreCase) || text.Contains("CM", StringComparison.Ordinal);
}
```

- [ ] **Step 2: Write the failing tests**

Create `src/Receipts/Receipts.Tests/ProposalBuilderTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Pohoda.Contract.Models;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class ProposalBuilderTests
{
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _dbPath;
        public ReceiptsStateStore Store { get; }
        public FakeStockCardService StockCards { get; } = new();
        public ProposalBuilder Builder { get; }

        private Harness(string dbPath, ReceiptsStateStore store)
        {
            _dbPath = dbPath;
            Store = store;
            Builder = new ProposalBuilder(StockCards, new FakeVariantDetector(), store.StockMappings, NullLogger<ProposalBuilder>.Instance);
        }

        public static async Task<Harness> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"proposal-builder-test-{Guid.NewGuid():N}.db");
            return new Harness(dbPath, await ReceiptsStateStore.OpenAsync(dbPath));
        }

        public async ValueTask DisposeAsync()
        {
            await Store.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }

    private static readonly InvoiceLine Platinum = Make.Line("7009190321200", "DH PLATINUM SERIES CM.120", quantity: 1m, unitPrice: 94.5m, variantSizeHint: "120 cm", variantBaseName: "DH PLATINUM SERIES");
    private static readonly InvoiceLine Lilac = Make.Line("7008352650000", "STRETCH LITE CARBON LILAC", quantity: 20m, unitPrice: 35m);

    private static ExistingReceipt ReceiptFor(string text, params ExistingReceiptLine[] lines) =>
        new("42", "26PR00012", text, lines);

    [Fact]
    public async Task BuildAsync_proposes_a_new_receipt_when_no_recent_receipt_mentions_the_invoice_number()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var invoice = Make.Invoice([Platinum, Lilac], freight: 180m, warnings: "parser warning");
        var unrelated = ReceiptFor("Gabel FT 2/250334");

        var proposal = await h.Builder.BuildAsync(invoice, [unrelated]);

        Assert.Equal(ReceiptProposalType.CreateReceipt, proposal.Type);
        Assert.Equal("Gabel FT 2/260220", proposal.HeaderText);
        Assert.Null(proposal.ExistingReceiptId);
        Assert.Equal("FT 2/260220", proposal.InvoiceNumber);
        Assert.Equal(invoice.SourceFileSha256, proposal.SourceFileSha256);
        Assert.Equal(180m, proposal.FreightToAdd);
        Assert.Equal(ProposalStatus.PendingApproval, proposal.Status);
        Assert.False(string.IsNullOrWhiteSpace(proposal.Id));

        Assert.Equal(2, proposal.LinesToAdd.Count);
        var platinum = proposal.LinesToAdd[0];
        Assert.Equal("7009190321200", platinum.PohodaStockCode);
        Assert.False(platinum.IsNewStockItem);
        Assert.Equal("120 cm", platinum.VariantSizeHint);
        Assert.False(platinum.MasterCardSearched); // existing card — no master-card lookup

        var lilac = proposal.LinesToAdd[1];
        Assert.Equal("7008352650000", lilac.PohodaStockCode); // new cards use the supplier code verbatim
        Assert.True(lilac.IsNewStockItem);
        Assert.Equal(20m, lilac.Quantity);
        Assert.Equal(35m, lilac.UnitPrice);

        Assert.Equal(["parser warning"], proposal.Warnings); // invoice warnings carried over, nothing else
        Assert.Empty(h.StockCards.CreateCalls); // scan never writes
        Assert.Equal("7009190321200", await h.Store.StockMappings.GetAsync("7009190321200")); // found card cached
        Assert.Null(await h.Store.StockMappings.GetAsync("7008352650000")); // missing card NOT cached
    }

    [Fact]
    public async Task BuildAsync_appends_only_the_lines_missing_from_a_receipt_that_already_references_the_invoice()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.StockCards.ExistingCodes.Add("7008352650000");
        var invoice = Make.Invoice([Platinum, Lilac], freight: 180m);
        var existing = ReceiptFor("Príjemka gabel ft 2/260220 (1. časť)",
            new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 1m, 94.5m, 35m));

        var proposal = await h.Builder.BuildAsync(invoice, [existing]);

        Assert.Equal(ReceiptProposalType.AppendLines, proposal.Type);
        Assert.Equal("42", proposal.ExistingReceiptId);
        Assert.Equal("26PR00012", proposal.ExistingReceiptNumber);
        Assert.Null(proposal.HeaderText);
        var onlyLine = Assert.Single(proposal.LinesToAdd);
        Assert.Equal("7008352650000", onlyLine.SupplierCode);
        Assert.Equal(180m, proposal.FreightToAdd);
        Assert.Contains(proposal.Warnings, w => w.Contains("Príjemka 26PR00012 už existuje") && w.Contains("dopravu"));
    }

    [Fact]
    public async Task BuildAsync_uses_the_cached_stock_mapping_before_asking_Pohoda()
    {
        await using var h = await Harness.CreateAsync();
        await h.Store.StockMappings.SaveAsync("7008352650000", "7008352650000");
        var invoice = Make.Invoice([Lilac], freight: 0m);

        var proposal = await h.Builder.BuildAsync(invoice, []);

        var line = Assert.Single(proposal.LinesToAdd);
        Assert.False(line.IsNewStockItem);
        Assert.Empty(h.StockCards.FindCalls);
        Assert.Null(proposal.FreightToAdd);
    }

    [Fact]
    public async Task BuildAsync_suggests_a_master_card_when_exactly_one_non_variant_candidate_matches()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.NameSearchResults.Add(new StockNameMatch("7009190321250", "Gabel DH PLATINUM SERIES 125 cm"));
        h.StockCards.NameSearchResults.Add(new StockNameMatch("7009190320000", "Gabel DH PLATINUM SERIES"));
        var invoice = Make.Invoice([Platinum], freight: 0m);

        var proposal = await h.Builder.BuildAsync(invoice, []);

        var line = Assert.Single(proposal.LinesToAdd);
        Assert.True(line.IsNewStockItem);
        Assert.True(line.MasterCardSearched);
        Assert.Equal("7009190320000", line.MasterCardCode);
        Assert.Equal(["DH PLATINUM SERIES"], h.StockCards.SearchCalls);
        Assert.Contains(proposal.Warnings, w => w.Contains("Možná materská karta nájdená: 7009190320000"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task BuildAsync_reports_no_master_card_when_candidates_are_absent_or_ambiguous(int nonVariantCandidates)
    {
        await using var h = await Harness.CreateAsync();
        for (var i = 0; i < nonVariantCandidates; i++)
        {
            h.StockCards.NameSearchResults.Add(new StockNameMatch($"70091903200{i}", $"Gabel DH PLATINUM SERIES verzia {i}"));
        }
        h.StockCards.NameSearchResults.Add(new StockNameMatch("7009190321250", "Gabel DH PLATINUM SERIES 125 cm"));
        var invoice = Make.Invoice([Platinum], freight: 0m);

        var proposal = await h.Builder.BuildAsync(invoice, []);

        var line = Assert.Single(proposal.LinesToAdd);
        Assert.True(line.MasterCardSearched);
        Assert.Null(line.MasterCardCode);
        Assert.Contains(proposal.Warnings, w => w.Contains("sa v Pohode nenašla"));
    }

    [Fact]
    public async Task BuildAsync_warns_when_the_existing_receipt_already_contains_every_line_and_there_is_no_freight()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7008352650000");
        var invoice = Make.Invoice([Lilac], freight: 0m);
        var existing = ReceiptFor("Gabel FT 2/260220",
            new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m));

        var proposal = await h.Builder.BuildAsync(invoice, [existing]);

        Assert.Equal(ReceiptProposalType.AppendLines, proposal.Type);
        Assert.Empty(proposal.LinesToAdd);
        Assert.Null(proposal.FreightToAdd);
        Assert.Contains("Všetky položky z faktúry sa v existujúcej príjemke už nachádzajú — proposal nepridáva nič nové.", proposal.Warnings);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: build error — `ProposalBuilder` does not exist.

- [ ] **Step 4: Implement `ProposalBuilder`**

Create `src/Receipts/Receipts.Core/ProposalBuilder.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;
using Receipts.Contract;
using Receipts.Core.Persistence;

namespace Receipts.Core;

/// <summary>
/// Turns one <see cref="ParsedInvoice"/> plus a snapshot of recently-listed Pohoda příjemky into a
/// <see cref="ReceiptProposal"/>. Read-only against Pohoda (only stock lookups) — nothing is created
/// here; `scan` only proposes, `apply` writes.
/// </summary>
public sealed class ProposalBuilder
{
    private readonly IStockCardService _stockCards;
    private readonly IVariantDetector _variantDetector;
    private readonly StockMappingCache _stockMappings;
    private readonly ILogger<ProposalBuilder> _logger;

    public ProposalBuilder(
        IStockCardService stockCards,
        IVariantDetector variantDetector,
        StockMappingCache stockMappings,
        ILogger<ProposalBuilder> logger)
    {
        _stockCards = stockCards;
        _variantDetector = variantDetector;
        _stockMappings = stockMappings;
        _logger = logger;
    }

    public async Task<ReceiptProposal> BuildAsync(ParsedInvoice invoice, IReadOnlyList<ExistingReceipt> recentReceipts, CancellationToken ct = default)
    {
        var existing = recentReceipts.FirstOrDefault(r =>
            r.Text.Contains(invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase));

        var existingStockCodes = existing?.Lines
            .Select(l => l.PohodaStockCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var linesToAdd = new List<ReceiptLineProposal>();
        var warnings = new List<string>(invoice.Warnings);

        foreach (var line in invoice.Lines)
        {
            var (stockCode, isNew) = await ResolveStockCodeAsync(line, ct);

            if (existing is not null && existingStockCodes.Contains(stockCode))
            {
                _logger.LogDebug("Riadok {Code} sa v príjemke {Number} už nachádza, preskakujem.", line.SupplierCode, existing.Number);
                continue;
            }

            string? masterCardCode = null;
            var masterCardSearched = false;

            if (isNew && line.VariantSizeHint is not null && line.VariantBaseName is not null)
            {
                masterCardSearched = true;
                masterCardCode = await FindMasterCardAsync(line.VariantBaseName, ct);

                warnings.Add(masterCardCode is not null
                    ? $"'{line.Description}' vyzerá ako variantný produkt (veľkosť {line.VariantSizeHint}). Možná materská karta nájdená: {masterCardCode} ('{line.VariantBaseName}') — mServer XML ju nevie automaticky priradiť, priraď ju ručne v Pohode po založení."
                    : $"'{line.Description}' vyzerá ako variantný produkt (veľkosť {line.VariantSizeHint}), ale karta s názvom '{line.VariantBaseName}' sa v Pohode nenašla — over ručne, či materská karta produktovej rady existuje (možno pod iným názvom), prípadne ju bude treba tiež založiť.");
            }

            linesToAdd.Add(new ReceiptLineProposal
            {
                SupplierCode = line.SupplierCode,
                Description = line.Description,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                PohodaStockCode = stockCode,
                IsNewStockItem = isNew,
                VariantSizeHint = line.VariantSizeHint,
                VariantBaseName = line.VariantBaseName,
                MasterCardCode = masterCardCode,
                MasterCardSearched = masterCardSearched
            });
        }

        decimal? freightToAdd = invoice.FreightAmount > 0 ? invoice.FreightAmount : null;
        if (existing is not null && freightToAdd is not null)
        {
            warnings.Add(
                $"Príjemka {existing.Number} už existuje — over ručne, či už obsahuje dopravu ({freightToAdd:F2} €) ako vedľajšie náklady, aby sa nezdvojila (list response ju momentálne nevie spoľahlivo rozlíšiť od bežnej položky).");
        }

        if (existing is not null && linesToAdd.Count == 0 && freightToAdd is null)
        {
            warnings.Add("Všetky položky z faktúry sa v existujúcej príjemke už nachádzajú — proposal nepridáva nič nové.");
        }

        return new ReceiptProposal
        {
            Id = Guid.NewGuid().ToString("N"),
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            SourceFilePath = invoice.SourceFilePath,
            SourceFileSha256 = invoice.SourceFileSha256,
            Type = existing is null ? ReceiptProposalType.CreateReceipt : ReceiptProposalType.AppendLines,
            ExistingReceiptId = existing?.PohodaId,
            ExistingReceiptNumber = existing?.Number,
            HeaderText = existing is null ? $"{invoice.SupplierName} {invoice.InvoiceNumber}" : null,
            LinesToAdd = linesToAdd,
            FreightToAdd = freightToAdd,
            Warnings = warnings,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Searches Pohoda for a master card named <paramref name="baseName"/> (the variant's description
    /// with the size removed). The underlying search is a substring match, which also returns sibling
    /// variants (e.g. "Gabel SLK-R ... 125 cm" contains "SLK-R"). Those are filtered out via
    /// <see cref="IVariantDetector.LooksLikeSizeVariant"/>, leaving only non-variant candidates.
    /// Returns a code only when exactly one such candidate remains — an ambiguous (more than one) or
    /// empty result both come back as "not found" (null), since guessing among several candidates
    /// would be worse than asking the reviewer to check manually.
    /// </summary>
    private async Task<string?> FindMasterCardAsync(string baseName, CancellationToken ct)
    {
        var matches = await _stockCards.SearchByNameContainingAsync(baseName, ct);
        var candidates = matches.Where(m => !_variantDetector.LooksLikeSizeVariant(m.Name)).ToList();

        if (candidates.Count != 1)
        {
            if (candidates.Count > 1)
            {
                _logger.LogDebug(
                    "Vyhľadanie materskej karty pre '{BaseName}' vrátilo {Count} nejednoznačných kandidátov, preskakujem automatický výber.",
                    baseName, candidates.Count);
            }

            return null;
        }

        return candidates[0].Code;
    }

    private async Task<(string Code, bool IsNew)> ResolveStockCodeAsync(InvoiceLine line, CancellationToken ct)
    {
        var cached = await _stockMappings.GetAsync(line.SupplierCode, ct);
        if (cached is not null)
        {
            return (cached, false);
        }

        var found = await _stockCards.FindBySupplierCodeAsync(line.SupplierCode, ct);
        if (found is not null)
        {
            await _stockMappings.SaveAsync(line.SupplierCode, found, ct);
            return (found, false);
        }

        // New cards use the supplier product code verbatim as stk:code — confirmed live that existing
        // Gabel cards follow this convention with no prefix.
        return (line.SupplierCode, true);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 31` (24 from Tasks 4–5 + 7 new: 5 facts + 2 theory rows).

- [ ] **Step 6: Commit**

```bash
git add src/Receipts/Receipts.Core/ProposalBuilder.cs src/Receipts/Receipts.Tests/Fakes/FakeStockCardService.cs src/Receipts/Receipts.Tests/Fakes/FakeVariantDetector.cs src/Receipts/Receipts.Tests/ProposalBuilderTests.cs
git commit -m "feat: add ProposalBuilder to Receipts.Core (invoice-to-receipt matching, read-only)"
```

---

## Task 7: `Receipts.Core.ProposalApplier` + `MatchingOptions` (the only Pohoda writer)

**Files:**
- Create: `src/Receipts/Receipts.Core/MatchingOptions.cs`
- Create: `src/Receipts/Receipts.Core/ProposalApplier.cs`
- Create: `src/Receipts/Receipts.Tests/Fakes/FakeReceiptService.cs`
- Test: `src/Receipts/Receipts.Tests/ProposalApplierTests.cs`

**Interfaces:**
- Consumes: `IReceiptService`, `IStockCardService`, `ExistingReceipt`, `ExistingReceiptLine`, `ReceiptLineToWrite` (Task 1); `ReceiptProposal`, `ReceiptProposalType`, `ProposalStatus` (Task 4); `ReceiptsStateStore` (Task 5); `Make`, `FakeStockCardService` (Tasks 5–6).
- Produces: `Receipts.Core.MatchingOptions { const string SectionName = "Matching"; int LookbackMonths = 12; int ReceiptListLimit = 500 }`; `Receipts.Core.ProposalApplier(IStockCardService stockCards, IReceiptService receipts, ReceiptsStateStore store, IOptions<MatchingOptions> matching, ILogger<ProposalApplier> logger)` with `Task ApplyAsync(ReceiptProposal proposal, CancellationToken ct = default)`; test fake `FakeReceiptService` (`RecentReceipts : List<ExistingReceipt>`, `CreateCalls`, `UpdateCalls`, `NextCreatedNumber`).

- [ ] **Step 1: Write the fake receipt service**

Create `src/Receipts/Receipts.Tests/Fakes/FakeReceiptService.cs`:

```csharp
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Receipts.Tests.Fakes;

internal sealed class FakeReceiptService : IReceiptService
{
    public List<ExistingReceipt> RecentReceipts { get; } = [];
    public string NextCreatedNumber { get; set; } = "26PR00013";

    public List<(string HeaderText, DateOnly Date, IReadOnlyList<ReceiptLineToWrite> Lines, decimal? Freight)> CreateCalls { get; } = [];
    public List<(string ExistingId, string HeaderText, IReadOnlyList<ReceiptLineToWrite> AllLines, decimal? Freight)> UpdateCalls { get; } = [];

    public Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ExistingReceipt>>(RecentReceipts.ToList());

    public Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)
    {
        CreateCalls.Add((headerText, date, lines, freight));
        return Task.FromResult(NextCreatedNumber);
    }

    public Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)
    {
        UpdateCalls.Add((existingId, headerText, allLines, freight));
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `src/Receipts/Receipts.Tests/ProposalApplierTests.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Models;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class ProposalApplierTests
{
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _dbPath;
        public ReceiptsStateStore Store { get; }
        public FakeStockCardService StockCards { get; } = new();
        public FakeReceiptService Receipts { get; } = new();
        public ProposalApplier Applier { get; }

        private Harness(string dbPath, ReceiptsStateStore store)
        {
            _dbPath = dbPath;
            Store = store;
            Applier = new ProposalApplier(StockCards, Receipts, store, Options.Create(new MatchingOptions()), NullLogger<ProposalApplier>.Instance);
        }

        public static async Task<Harness> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"proposal-applier-test-{Guid.NewGuid():N}.db");
            return new Harness(dbPath, await ReceiptsStateStore.OpenAsync(dbPath));
        }

        public async ValueTask DisposeAsync()
        {
            await Store.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }

    private static ReceiptLineProposal ExistingCardLine() =>
        Make.LineProposal("7009190321200", "DH PLATINUM SERIES CM.120", isNewStockItem: false, quantity: 1m, unitPrice: 94.5m, variantSizeHint: "120 cm");

    private static ReceiptLineProposal NewCardLine() =>
        Make.LineProposal("7009181471300", "SLD KIT LIGHT BLUE 130CM", isNewStockItem: true, quantity: 2m, unitPrice: 40m,
            variantSizeHint: "130 cm", variantBaseName: "SLD KIT LIGHT BLUE", masterCardCode: "7009181470000", masterCardSearched: true);

    [Fact]
    public async Task ApplyAsync_creates_missing_cards_then_a_new_receipt_with_every_line_and_freight_and_marks_the_proposal_applied()
    {
        await using var h = await Harness.CreateAsync();
        var proposal = Make.Proposal([ExistingCardLine(), NewCardLine()], freightToAdd: 160m);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        // The new card was created with the variant size as orderName; the existing one was left alone.
        var created = Assert.Single(h.StockCards.CreateCalls);
        Assert.Equal(("7009181471300", "SLD KIT LIGHT BLUE 130CM", "130 cm"), created);
        Assert.Equal("7009181471300", await h.Store.StockMappings.GetAsync("7009181471300"));

        var call = Assert.Single(h.Receipts.CreateCalls);
        Assert.Equal("Gabel FT 2/260220", call.HeaderText);
        Assert.Equal(new DateOnly(2026, 6, 23), call.Date);
        Assert.Equal(160m, call.Freight);
        Assert.Equal(
            [
                new ReceiptLineToWrite("7009190321200", "DH PLATINUM SERIES CM.120", 1m, "PA", 94.5m, 35m),
                new ReceiptLineToWrite("7009181471300", "SLD KIT LIGHT BLUE 130CM", 2m, "PA", 40m, 35m)
            ],
            call.Lines);
        Assert.Empty(h.Receipts.UpdateCalls);

        Assert.Equal(ProposalStatus.Applied, proposal.Status);
        Assert.Equal("26PR00013", proposal.ResultReceiptNumber);
        Assert.NotNull(proposal.AppliedAt);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Equal("26PR00013", stored.ResultReceiptNumber);

        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.All(audit, entry => Assert.Equal(proposal.SourceFilePath, entry.Source));
        Assert.Contains(audit, e => e.Message.StartsWith("Vytvorená nová príjemka 'Gabel FT 2/260220' (č. 26PR00013)."));
        Assert.Contains(audit, e => e.Message.Contains("Založená nová skladová karta 7009181471300") && e.Message.Contains("možná materská karta 7009181470000"));
        Assert.Contains(audit, e => e.Message.Contains("Pridaná doprava (vedľajšie náklady): 160"));
        Assert.Equal(2, audit.Count(e => e.Message.StartsWith("Riadok: ")));
    }

    [Fact]
    public async Task ApplyAsync_does_not_create_a_card_that_appeared_in_Pohoda_since_the_scan()
    {
        await using var h = await Harness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009181471300");
        var proposal = Make.Proposal([NewCardLine()], freightToAdd: null);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        Assert.Empty(h.StockCards.CreateCalls);
        Assert.Equal(["7009181471300"], h.StockCards.FindCalls);
        Assert.Equal("7009181471300", await h.Store.StockMappings.GetAsync("7009181471300"));
        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message.Contains("medzičasom vznikla"));
        Assert.Null(Assert.Single(h.Receipts.CreateCalls).Freight);
    }

    [Fact]
    public async Task ApplyAsync_appends_by_resending_the_existing_lines_followed_by_the_new_ones()
    {
        await using var h = await Harness.CreateAsync();
        var existingLines = new[]
        {
            new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m),
            new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 1m, 94.5m, 35m)
        };
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("42", "26PR00012", "Gabel FT 2/260220", existingLines));
        var proposal = Make.Proposal([NewCardLine()], type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42", existingReceiptNumber: "26PR00012", freightToAdd: 180m);
        await h.Store.Proposals.SaveAsync(proposal);

        await h.Applier.ApplyAsync(proposal);

        Assert.Empty(h.Receipts.CreateCalls);
        var update = Assert.Single(h.Receipts.UpdateCalls);
        Assert.Equal("42", update.ExistingId);
        Assert.Equal("Gabel FT 2/260220", update.HeaderText);
        Assert.Equal(180m, update.Freight);
        Assert.Equal(
            [
                new ReceiptLineToWrite("7008352650000", "STRETCH LITE CARBON LILAC", 20m, "PA", 35m, 35m),
                new ReceiptLineToWrite("7009190321200", "DH PLATINUM SERIES CM.120", 1m, "PA", 94.5m, 35m),
                new ReceiptLineToWrite("7009181471300", "SLD KIT LIGHT BLUE 130CM", 2m, "PA", 40m, 35m)
            ],
            update.AllLines);

        Assert.Equal(ProposalStatus.Applied, proposal.Status);
        Assert.Equal("26PR00012", proposal.ResultReceiptNumber);
        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message.StartsWith("Doplnená existujúca príjemka 'Gabel FT 2/260220' (č. 26PR00012) o 1 riadkov."));
    }

    [Fact]
    public async Task ApplyAsync_throws_and_leaves_the_proposal_pending_when_the_existing_receipt_is_gone()
    {
        await using var h = await Harness.CreateAsync();
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("99", "26PR00099", "iná príjemka", []));
        var proposal = Make.Proposal([ExistingCardLine()], type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42", existingReceiptNumber: "26PR00012");
        await h.Store.Proposals.SaveAsync(proposal);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.Applier.ApplyAsync(proposal));

        Assert.Contains("Príjemka 42 sa už v Pohode nenašla", ex.Message);
        Assert.Empty(h.Receipts.UpdateCalls);
        Assert.Equal(ProposalStatus.PendingApproval, proposal.Status);
        Assert.Empty(await h.Store.Audit.ReadAllAsync());
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: build error — `MatchingOptions` / `ProposalApplier` do not exist.

- [ ] **Step 4: Implement `MatchingOptions` and `ProposalApplier`**

Create `src/Receipts/Receipts.Core/MatchingOptions.cs`:

```csharp
namespace Receipts.Core;

/// <summary>How far back to list Pohoda příjemky when matching an invoice to an existing receipt (config section "Matching").</summary>
public sealed class MatchingOptions
{
    public const string SectionName = "Matching";

    public int LookbackMonths { get; init; } = 12;

    /// <summary>Maximum number of příjemky requested from mServer in one list call.</summary>
    public int ReceiptListLimit { get; init; } = 500;
}
```

Create `src/Receipts/Receipts.Core/ProposalApplier.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;
using Receipts.Contract;
using Receipts.Core.Persistence;

namespace Receipts.Core;

/// <summary>
/// Executes an already-approved <see cref="ReceiptProposal"/> against Pohoda: (re-)verifies stock
/// mappings, creates missing skladové karty, then creates or updates the příjemka. This is the only
/// place in the receipt-sync pipeline that performs a write against Pohoda mServer.
/// </summary>
public sealed class ProposalApplier
{
    private readonly IStockCardService _stockCards;
    private readonly IReceiptService _receipts;
    private readonly ReceiptsStateStore _store;
    private readonly MatchingOptions _matching;
    private readonly ILogger<ProposalApplier> _logger;

    public ProposalApplier(
        IStockCardService stockCards,
        IReceiptService receipts,
        ReceiptsStateStore store,
        IOptions<MatchingOptions> matching,
        ILogger<ProposalApplier> logger)
    {
        _stockCards = stockCards;
        _receipts = receipts;
        _store = store;
        _matching = matching.Value;
        _logger = logger;
    }

    public async Task ApplyAsync(ReceiptProposal proposal, CancellationToken ct = default)
    {
        var auditLines = new List<string>();
        var linesToWrite = new List<ReceiptLineToWrite>();

        foreach (var line in proposal.LinesToAdd)
        {
            var stockCode = line.PohodaStockCode;

            if (line.IsNewStockItem)
            {
                // Re-verify: the card may have been created since `scan` ran.
                var nowExists = await _stockCards.FindBySupplierCodeAsync(line.SupplierCode, ct);
                if (nowExists is null)
                {
                    await _stockCards.CreateAsync(line.PohodaStockCode, line.Description, line.VariantSizeHint, ct);
                    var variantNote = line.VariantSizeHint is not null
                        ? line.MasterCardCode is not null
                            ? $" [variant {line.VariantSizeHint} — možná materská karta {line.MasterCardCode}, priraď ručne v Pohode]"
                            : $" [variant {line.VariantSizeHint} — materská karta sa nenašla, over ručne v Pohode]"
                        : string.Empty;
                    auditLines.Add($"Založená nová skladová karta {line.PohodaStockCode} '{line.Description}'.{variantNote}");
                }
                else
                {
                    stockCode = nowExists;
                    auditLines.Add($"Skladová karta pre kód {line.SupplierCode} medzičasom vznikla ({nowExists}), nová sa nezakladala.");
                }

                await _store.StockMappings.SaveAsync(line.SupplierCode, stockCode, ct);
            }

            linesToWrite.Add(new ReceiptLineToWrite(stockCode, line.Description, line.Quantity, line.Unit, line.UnitPrice, line.DiscountPercent));
            auditLines.Add($"Riadok: {line.Description} ({line.SupplierCode}) — {line.Quantity} {line.Unit} × {line.UnitPrice:F3}, zľava {line.DiscountPercent:F2} %.");
        }

        string resultNumber;

        if (proposal.Type == ReceiptProposalType.CreateReceipt)
        {
            resultNumber = await _receipts.CreateAsync(proposal.HeaderText!, proposal.InvoiceDate, linesToWrite, proposal.FreightToAdd, ct);
            auditLines.Insert(0, $"Vytvorená nová príjemka '{proposal.HeaderText}' (č. {resultNumber}).");
        }
        else
        {
            var since = DateOnly.FromDateTime(DateTime.Today.AddMonths(-_matching.LookbackMonths));
            var recent = await _receipts.ListRecentAsync(since, _matching.ReceiptListLimit, ct);
            var existing = recent.FirstOrDefault(r => r.PohodaId == proposal.ExistingReceiptId)
                ?? throw new InvalidOperationException($"Príjemka {proposal.ExistingReceiptId} sa už v Pohode nenašla — mohla byť medzičasom zmenená alebo zmazaná. Spusti 'scan' znova.");

            var mergedLines = existing.Lines
                .Select(l => new ReceiptLineToWrite(l.PohodaStockCode, l.Text, l.Quantity, l.Unit, l.UnitPrice, l.DiscountPercent))
                .Concat(linesToWrite)
                .ToList();

            var freight = proposal.FreightToAdd; // note: possible duplicate if the existing příjemka already carries freight — flagged as a Warning in the preview, not auto-detected.
            await _receipts.UpdateAsync(proposal.ExistingReceiptId!, existing.Text, mergedLines, freight, ct);
            resultNumber = existing.Number;
            auditLines.Insert(0, $"Doplnená existujúca príjemka '{existing.Text}' (č. {resultNumber}) o {linesToWrite.Count} riadkov.");
        }

        if (proposal.FreightToAdd is > 0)
        {
            auditLines.Add($"Pridaná doprava (vedľajšie náklady): {proposal.FreightToAdd:F2} €.");
        }

        proposal.Status = ProposalStatus.Applied;
        proposal.ResultReceiptNumber = resultNumber;
        proposal.AppliedAt = DateTimeOffset.UtcNow;
        await _store.Proposals.SaveAsync(proposal, ct);

        foreach (var line in auditLines)
        {
            _logger.LogInformation("{Line}", line);
            await _store.Audit.AppendAsync(proposal.SourceFilePath, line, ct);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 35` (31 from Tasks 4–6 + 4 new).

- [ ] **Step 6: Commit**

```bash
git add src/Receipts/Receipts.Core/MatchingOptions.cs src/Receipts/Receipts.Core/ProposalApplier.cs src/Receipts/Receipts.Tests/Fakes/FakeReceiptService.cs src/Receipts/Receipts.Tests/ProposalApplierTests.cs
git commit -m "feat: add ProposalApplier and MatchingOptions to Receipts.Core"
```

---

## Task 8: `Receipts.Core.PreviewWriter` (Markdown preview for human approval)

**Files:**
- Create: `src/Receipts/Receipts.Core/PreviewWriter.cs`
- Test: `src/Receipts/Receipts.Tests/PreviewWriterTests.cs`

**Interfaces:**
- Consumes: `ReceiptProposal`, `ReceiptLineProposal`, `ReceiptProposalType` (Task 4); `Make` (Task 5).
- Produces: `Receipts.Core.PreviewWriter.Render(ReceiptProposal proposal) : string` (static).

Numbers are formatted with the current culture (`:F2`/`:F3`), exactly as Gabel's preview did — the preview is read by a Slovak operator on the machine that runs the job. Tests therefore compare against `value.ToString("F2")` rather than a hard-coded separator.

- [ ] **Step 1: Write the failing tests**

Create `src/Receipts/Receipts.Tests/PreviewWriterTests.cs`:

```csharp
using Receipts.Contract;
using Receipts.Core;
using Receipts.Tests.Fakes;

namespace Receipts.Tests;

public class PreviewWriterTests
{
    [Fact]
    public void Render_describes_a_new_receipt_with_its_lines_variant_flags_freight_and_apply_command()
    {
        var proposal = Make.Proposal(
            [
                Make.LineProposal("7009190321200", "DH PLATINUM SERIES CM.120", isNewStockItem: false, quantity: 1m, unitPrice: 94.5m, variantSizeHint: "120 cm"),
                Make.LineProposal("7009181471300", "SLD KIT LIGHT BLUE 130CM", isNewStockItem: true, quantity: 2m, unitPrice: 40m,
                    variantSizeHint: "130 cm", variantBaseName: "SLD KIT LIGHT BLUE", masterCardCode: "7009181470000", masterCardSearched: true),
                Make.LineProposal("7009999990000", "NEW POLE 140CM", isNewStockItem: true, variantSizeHint: "140 cm", variantBaseName: "NEW POLE", masterCardSearched: true)
            ],
            freightToAdd: 160m,
            id: "abc123");

        var markdown = PreviewWriter.Render(proposal);

        Assert.StartsWith("# Nová príjemka — FT 2/260220", markdown);
        Assert.Contains("- **Zdrojový súbor**: `C:\\faktury\\ft2-260220del20260623.pdf`", markdown);
        Assert.Contains("- **Proposal ID**: `abc123`", markdown);
        Assert.Contains("- **Dátum faktúry**: 2026-06-23", markdown);
        Assert.Contains("- **Text novej príjemky**: `Gabel FT 2/260220`", markdown);
        Assert.DoesNotContain("Existujúca príjemka", markdown);

        Assert.Contains("## Riadky na pridanie (3)", markdown);
        Assert.Contains("| Kód dodávateľa | Popis | Množstvo | MJ | Cena | Zľava % | Skladová karta |", markdown);
        Assert.Contains($"| 7009190321200 | DH PLATINUM SERIES CM.120 | 1 | PA | {94.5m.ToString("F3")} | {35m.ToString("F2")} | 7009190321200 ⚑ variant 120 cm |", markdown);
        Assert.Contains("| **7009181471300 (nová karta)** ⚑ variant 130 cm — materská karta: 7009181470000 |", markdown);
        Assert.Contains("| **7009999990000 (nová karta)** ⚑ variant 140 cm — materská karta nenájdená |", markdown);

        Assert.Contains($"**Doprava (vedľajšie náklady)**: {160m.ToString("F2")} €", markdown);
        Assert.Contains("**Nové skladové karty na založenie**: 2", markdown);
        Assert.Contains("**⚑ Z toho vyzerá ako variantný produkt**: 2 (označené v tabuľke)", markdown);
        Assert.DoesNotContain("## ⚠ Upozornenia", markdown);
        Assert.EndsWith("Na zápis do Pohody spusti: `apply --id abc123`" + Environment.NewLine, markdown);
    }

    [Fact]
    public void Render_describes_an_append_to_an_existing_receipt_and_lists_warnings()
    {
        var proposal = Make.Proposal(
            [],
            type: ReceiptProposalType.AppendLines,
            existingReceiptId: "42",
            existingReceiptNumber: "26PR00012",
            freightToAdd: null,
            invoiceNumber: "FT 2/250334",
            warnings: ["prvé upozornenie", "druhé upozornenie"]);

        var markdown = PreviewWriter.Render(proposal);

        Assert.StartsWith("# Doplnenie príjemky — FT 2/250334", markdown);
        Assert.Contains("- **Existujúca príjemka**: 26PR00012 (id `42`)", markdown);
        Assert.DoesNotContain("Text novej príjemky", markdown);
        Assert.Contains("_Žiadne nové riadky na pridanie._", markdown);
        Assert.DoesNotContain("Doprava", markdown);
        Assert.DoesNotContain("Nové skladové karty", markdown);
        Assert.Contains("## ⚠ Upozornenia", markdown);
        Assert.Contains("- prvé upozornenie", markdown);
        Assert.Contains("- druhé upozornenie", markdown);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: build error — `PreviewWriter` does not exist.

- [ ] **Step 3: Implement `PreviewWriter`**

Create `src/Receipts/Receipts.Core/PreviewWriter.cs`:

```csharp
using System.Text;
using Receipts.Contract;

namespace Receipts.Core;

/// <summary>Writes a human-readable Markdown preview of a proposal for review before `apply`.</summary>
public static class PreviewWriter
{
    public static string Render(ReceiptProposal proposal)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {(proposal.Type == ReceiptProposalType.CreateReceipt ? "Nová príjemka" : "Doplnenie príjemky")} — {proposal.InvoiceNumber}");
        sb.AppendLine();
        sb.AppendLine($"- **Zdrojový súbor**: `{proposal.SourceFilePath}`");
        sb.AppendLine($"- **Proposal ID**: `{proposal.Id}`");
        sb.AppendLine($"- **Dátum faktúry**: {proposal.InvoiceDate:yyyy-MM-dd}");

        if (proposal.Type == ReceiptProposalType.CreateReceipt)
        {
            sb.AppendLine($"- **Text novej príjemky**: `{proposal.HeaderText}`");
        }
        else
        {
            sb.AppendLine($"- **Existujúca príjemka**: {proposal.ExistingReceiptNumber} (id `{proposal.ExistingReceiptId}`)");
        }

        sb.AppendLine();

        if (proposal.LinesToAdd.Count == 0)
        {
            sb.AppendLine("_Žiadne nové riadky na pridanie._");
        }
        else
        {
            sb.AppendLine($"## Riadky na pridanie ({proposal.LinesToAdd.Count})");
            sb.AppendLine();
            sb.AppendLine("| Kód dodávateľa | Popis | Množstvo | MJ | Cena | Zľava % | Skladová karta |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var line in proposal.LinesToAdd)
            {
                var stockNote = line.IsNewStockItem ? $"**{line.PohodaStockCode} (nová karta)**" : line.PohodaStockCode;
                if (line.VariantSizeHint is not null)
                {
                    stockNote += $" ⚑ variant {line.VariantSizeHint}";
                    stockNote += line.MasterCardCode is not null
                        ? $" — materská karta: {line.MasterCardCode}"
                        : line.MasterCardSearched ? " — materská karta nenájdená" : string.Empty;
                }
                sb.AppendLine($"| {line.SupplierCode} | {line.Description} | {line.Quantity} | {line.Unit} | {line.UnitPrice:F3} | {line.DiscountPercent:F2} | {stockNote} |");
            }
        }

        sb.AppendLine();

        if (proposal.FreightToAdd is > 0)
        {
            sb.AppendLine($"**Doprava (vedľajšie náklady)**: {proposal.FreightToAdd:F2} €");
            sb.AppendLine();
        }

        var newCardCount = proposal.LinesToAdd.Count(l => l.IsNewStockItem);
        if (newCardCount > 0)
        {
            sb.AppendLine($"**Nové skladové karty na založenie**: {newCardCount}");
            sb.AppendLine();
        }

        var newVariantCount = proposal.LinesToAdd.Count(l => l.IsNewStockItem && l.VariantSizeHint is not null);
        if (newVariantCount > 0)
        {
            sb.AppendLine(
                $"**⚑ Z toho vyzerá ako variantný produkt**: {newVariantCount} (označené v tabuľke) — mServer XML nevie automaticky priradiť materskú kartu, over ručne v Pohode.");
            sb.AppendLine();
        }

        if (proposal.Warnings.Count > 0)
        {
            sb.AppendLine("## ⚠ Upozornenia");
            sb.AppendLine();
            foreach (var warning in proposal.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Na zápis do Pohody spusti: `apply --id {proposal.Id}`");

        return sb.ToString();
    }
}
```

Note on the table header: Gabel's preview titled the first column "Kód (Gabel)". `Receipts.Core` is supplier-agnostic, so the label is the neutral "Kód dodávateľa" — the one deliberate user-facing string change in this PR (Global Constraints).

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Receipts/Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 37` (35 from Tasks 4–7 + 2 new).

- [ ] **Step 5: Commit**

```bash
git add src/Receipts/Receipts.Core/PreviewWriter.cs src/Receipts/Receipts.Tests/PreviewWriterTests.cs
git commit -m "feat: add PreviewWriter to Receipts.Core"
```

---

## Task 9: Full-solution verification + README

**Files:**
- Modify: `README.md` (structure tree, module descriptions, project count)

**Interfaces:** none — this task checks that Tasks 1–8 didn't disturb anything already in `eCommerce.sln` and documents the new modules.

- [ ] **Step 1: Build the entire solution**

```bash
dotnet build src/eCommerce.sln
```

Expected: succeeds with 0 errors. Pre-existing warnings (CS8618 etc. in `Import.Wiky`, `Expando.Connector`, …) are unchanged from before this PR.

- [ ] **Step 2: Run every test project in the solution**

```bash
dotnet test src/eCommerce.sln
```

Expected: all pass — `Common.Data.Sqlite.Tests` 9, `Common.Documents.PdfReader.Tests` 2, `Pohoda.MServer.Tests` 19 (PR1, unchanged) + `Pohoda.Server.Tests` 14 + `Receipts.Tests` 37 = **81 tests, 0 failures**.

- [ ] **Step 3: Confirm no pre-existing code file was modified**

```bash
git diff --stat 7a05581 HEAD -- src/Jobs src/Imports src/Nop src/Expando src/Common src/Pohoda/Pohoda.Server/MServer src/Pohoda/Pohoda.Server/Services/StockService.cs src/Pohoda/Pohoda.Server/Services/CategoryService.cs src/Pohoda/Pohoda.Server/Services/OrderService.cs src/Pohoda/Pohoda.Server/Services/ParameterService.cs src/Pohoda/Pohoda.Contract/Services/IStockService.cs src/Pohoda/Pohoda.Contract/IStorageSystem.cs
```

Expected: **empty output** (`7a05581` is the last PR1 commit).

```bash
git diff --stat 7a05581 HEAD -- src/Pohoda/Pohoda.Server src/Pohoda/Pohoda.Contract | grep -v "^ src/Pohoda/Pohoda.Server/Xml/\|^ src/Pohoda/Pohoda.Server/Services/ReceiptService.cs\|^ src/Pohoda/Pohoda.Server/Services/StockCardService.cs\|^ src/Pohoda/Pohoda.Contract/Models/\|^ src/Pohoda/Pohoda.Contract/Services/IReceiptService.cs\|^ src/Pohoda/Pohoda.Contract/Services/IStockCardService.cs"
```

Expected: exactly one file line — `src/Pohoda/Pohoda.Server/Pohoda.Server.csproj | 1 +` — plus the summary line.

```bash
grep -rn "Gabel" src/Receipts/Receipts.Core --include=*.cs | grep -v "///"
```

Expected: empty output (`Receipts.Core` mentions Gabel only in doc comments).

- [ ] **Step 4: Update `README.md`**

Make these edits, matching the existing README's Slovak style:

1. In "Technológie", change `21 projektov (vrátane 3 testovacích)` to `26 projektov (vrátane 5 testovacích)`.

2. In the "Štruktúra riešenia" tree, add under `Pohoda/` (after `Pohoda.MServer.Tests/`):
   ```
       │   ├── Pohoda.Server/
       │   └── Pohoda.Server.Tests/
   ```
   (replacing the current final `└── Pohoda.Server/` line), and add a new top-level `Receipts/` folder between `Nop/` and `Pohoda/`:
   ```
       ├── Receipts/
       │   ├── Receipts.Contract/
       │   ├── Receipts.Core/
       │   ├── Receipts.Gabel/
       │   └── Receipts.Tests/
   ```
   Adjust the `├──`/`└──` connectors so the tree stays well-formed.

3. In "Moduly a ich funkcionalita", add a new section before "### Pohoda – integrácia s ERP systémom":

   ```markdown
   ### Receipts – automatizácia skladových príjemiek z dodávateľských faktúr

   Portované zo samostatného riešenia `Gabel.PrijemkaSync`: z PDF faktúry dodávateľa sa vyťažia položky a pripraví sa **návrh** (proposal) skladovej príjemky do Pohody. Návrh sa ukladá lokálne (SQLite) a zapisuje ako Markdown náhľad na schválenie; samotný zápis do Pohody je oddelený, explicitne spúšťaný krok (dry-run → apply). Spustiteľné aplikácie (`Jobs.Receipts.Scan`, `Jobs.Receipts.Apply`) pribudnú v ďalšom kroku; tento krok obsahuje len knižnice.

   - **Receipts.Contract** – doménové modely (`ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, `ReceiptLineProposal`) a dve rozhrania pre dodávateľské pluginy: `IInvoiceParser` (PDF → `ParsedInvoice`) a `IVariantDetector` (rozpoznanie veľkostného variantu v názve produktu).
   - **Receipts.Core** – dodávateľsky nezávislá logika: `ProposalBuilder` (párovanie faktúry na existujúcu príjemku podľa výskytu čísla faktúry v texte dokladu, rozlíšenie existujúcich vs. nových skladových kariet, návrh materskej karty pre varianty), `ProposalApplier` (jediné miesto, ktoré zapisuje do Pohody – založí chýbajúce karty a vytvorí/doplní príjemku; doprava ide vždy ako vedľajšie náklady), `PreviewWriter` (Markdown náhľad) a `ReceiptsStateStore` (SQLite stav nad `Common.Data.Sqlite`: spracované súbory, audit log, cache mapovania kódov, uložené proposaly). Závisí len na `Receipts.Contract`, `Pohoda.Contract` a `Common.Data.Sqlite`.
   - **Receipts.Gabel** – prvý dodávateľský plugin: `GabelInvoiceParser` (deterministický regex parser finálnych faktúr `FT n/nnnnnn` nad `Common.Documents.PdfReader`; proforma faktúry preskakuje) a `GabelVariantDetector` (vzory `CM.120` / `135CM` / `Cm 115`, rozsahy ako `73-110CM` sa nepovažujú za variant). Ďalší dodávateľ = nový projekt `Receipts.<Dodávateľ>`, `Receipts.Core` sa nemení.
   - **Receipts.Tests** – xUnit testy vrátane pôvodných testov parsera nad reálnymi PDF vzorkami (`TestData/`), testov `ProposalBuilder`/`ProposalApplier` s ručnými fake implementáciami Pohoda služieb a testov SQLite stavu.
   ```

4. In the "### Pohoda – integrácia s ERP systémom" section:
   - Extend the **Pohoda.Contract** bullet: after `IStockService`, add `, IReceiptService` (agenda príjemiek: zoznam/vytvorenie/aktualizácia)`, `IStockCardService` (vyhľadanie skladovej karty podľa kódu dodávateľa, hľadanie podľa časti názvu, založenie karty – prechodné rozhranie, ktoré sa pri migrácii `Pohoda.Server` na `Pohoda.MServer` zlúči do `IStockService`)` and mention the models `ExistingReceipt`, `ReceiptLineToWrite`, `StockNameMatch`.
   - Extend the **Pohoda.Server** bullet with a second sentence: `Nové služby ReceiptService a StockCardService (implementácie IReceiptService/IStockCardService) už komunikujú cez zdieľaný Pohoda.MServer.MServerClient a XML obálku Pohoda.Server.Xml.DataPackBuilder; pôvodné služby (StockService, CategoryService, …) zatiaľ ostávajú na pôvodnom MServer.cs.`
   - Add a bullet after Pohoda.Server: `- **Pohoda.Server.Tests** – xUnit testy ReceiptService a StockCardService proti falošnému mServeru (FakeMServer) – overujú presný tvar XML požiadaviek (namespace elementov requestPrijemka/requestStock, limit/count, dvojito vnorený stockItem, prijemkaAccessoryChargesItem pre dopravu) a parsovanie odpovedí.`

- [ ] **Step 5: Commit the README update**

```bash
git add README.md
git commit -m "docs: document Receipts.* libraries, IReceiptService/IStockCardService and Pohoda.Server.Tests"
```

---

## Self-Review

**Spec coverage** (against `docs/superpowers/specs/2026-08-24-gabel-prijemkasync-merge-design.md`):
- §3 `Receipts.Contract` — `ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, `IInvoiceParser` — Task 4 (plus `IVariantDetector`, needed so `Receipts.Core` stays supplier-agnostic). ✅
- §3 `Receipts.Core` — `ProposalBuilder` (Task 6), `ProposalApplier` (Task 7), invoice-to-receipt matching (Task 6), depends only on `Pohoda.Contract` + `Common.Data.Sqlite` (+ `Receipts.Contract`) — verified by Task 9 Step 3 grep. ✅ `PreviewWriter` (Task 8) and `ReceiptsStateStore` (Task 5) are the App/Data pieces that had nowhere else to live. ✅
- §3 `Receipts.Gabel` — `GabelInvoiceParser`, `GabelVariantDetector` implementing the plugin interfaces — Task 4. ✅
- §3 `Pohoda.Contract` gains `IReceiptService`; stock read/search/create members — Task 1 (as transitional `IStockCardService`, see Ruling in the ledger; fold into `IStockService` is PR4). ✅
- §3 `Pohoda.Server` gains `ReceiptService` built from `PrijemkaClient` — Task 3; `StockCardService` from `StockClient` — Task 2. ✅
- §3 business rules (final invoices only, text-contains matching, never overwrite existing lines, freight → accessory charge, master card suggest-only) — carried verbatim in Tasks 4, 6, 7 and asserted in their tests. ✅
- §5 PR2 "doesn't touch existing Jobs", §6 "Gabel's xUnit tests move over unchanged" — Task 4 (tests) and Task 9 Step 3 (diff guard). ✅
- §7 `Matching:*` config — `MatchingOptions` Task 7 (`PrijemkaListLimit` renamed `ReceiptListLimit`; PR3's `appsettings.json` uses the new key). ✅
- `Jobs.Receipts.*`, `Watch:*` options, mServer lifecycle, PDF archive move, `scan`/`apply`/`status`/`reset` CLI — correctly deferred to PR3.

**Placeholder scan:** every step has literal code; no TODO/TBD. The "known open items" in Task 3 are spec §8 carry-overs stated as facts, not deferred work.

**Type consistency:**
- `IReceiptService`/`IStockCardService` signatures (Task 1) = `ReceiptService`/`StockCardService` implementations (Tasks 2–3) = `FakeReceiptService`/`FakeStockCardService` (Tasks 6–7) = calls in `ProposalBuilder`/`ProposalApplier` (Tasks 6–7). ✅
- `ReceiptLineToWrite(PohodaStockCode, Text, Quantity, Unit, UnitPrice, DiscountPercent)` positional order is identical in Task 1, `ReceiptService.BuildLineElement` (Task 3), `ProposalApplier` (Task 7) and the Task 7 test expectations. `ExistingReceiptLine(PohodaStockCode, Text, Unit, Quantity, UnitPrice, DiscountPercent)` — note the different `Unit`/`Quantity` order — is used consistently in Task 3 parser, Task 6/7 tests and `ProposalApplier`'s merge. ✅
- `ReceiptsStateStore.StockMappings : StockMappingCache` (Task 5) is what `ProposalBuilder` takes (Task 6) and `ProposalApplier` uses via `_store.StockMappings` (Task 7). `Audit.AppendAsync(source, message)` / `ReadAllAsync()` tuple `(Timestamp, Source, Message)` match PR1's `AuditLog`. ✅
- `Make.LineProposal(..., isNewStockItem, pohodaStockCode = null → supplierCode, ...)` default matches `ProposalBuilder`'s "new cards use the supplier code verbatim" rule, so Task 7/8 fixtures mirror real proposals. ✅
- Test counts: Pohoda.Server.Tests 1 + 6 + 7 = 14; Receipts.Tests 17 + 7 + 7 + 4 + 2 = 37; totals in Task 9 Step 2 = 30 + 14 + 37 = 81. ✅

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-11-gabel-prijemkasync-merge-pr2-receipts-libraries.md`. Execution: **Subagent-Driven** (superpowers:subagent-driven-development) — a fresh implementer subagent per task, spec + quality review after each, whole-branch review at the end — as requested by the user for this merge.
