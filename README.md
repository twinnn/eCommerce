English | [Slovensky](README.sk.md)

# eCommerce – AI Agents for the Gabel distribution

## Purpose

This solution holds the AI Agents that support AcEcom s.r.o., the **Distributor** holding the exclusive distribution of Gabel poles in Slovakia and the Czech Republic. Each **Agent** owns one business process: it reads freely from the Distributor's systems and changes them only through **Proposals** that a person approves, until a person grants it autonomy for a given action type.

The working parts today are:

- **Receipts** – turns a **Supplier Invoice** PDF from Gabel into a Proposal for a stock **Receipt** in Pohoda, and writes it to Pohoda only after a person has reviewed and applied it.
- **The Pohoda mServer client** – the shared, low-level client through which Agents read and write Pohoda (STORMWARE), the Distributor's accounting and stock ERP.

**Go-Noow**, the Distributor's own e-shop, is not part of this solution; NextCom builds it and synchronizes it with Pohoda.

Domain vocabulary is defined in [GLOSSARY.md](GLOSSARY.md), and architectural decisions are recorded in [docs/adr/](docs/adr/):

- [0001 – Agents integrate with Pohoda through mServer, not NextCom customizations](docs/adr/0001-agents-integrate-with-pohoda-via-mserver.md)
- [0002 – Agents act through Proposals, with autonomy granted per action type](docs/adr/0002-agents-act-through-proposals-with-per-action-autonomy.md)

Code and documentation are in English so the system can later be offered to other businesses; this README also has a Slovak translation, [README.sk.md](README.sk.md), for the Distributor's team. Operator-facing output (log messages, the command-line help of `Jobs.Receipts.Apply`, the Markdown previews) is in Slovak.

## Technology

- **.NET 10.0** – solution `src/eCommerce.sln` (Visual Studio or the `dotnet` CLI), 18 projects including 6 test projects (xUnit).
- **Pohoda integration** – HTTP/XML against the local POHODA **mServer** (Windows-1250 encoding), with mServer started and stopped on demand through `pohoda.exe /http start|stop`.
- **PDF parsing** – PdfPig, wrapped by `Common.Documents.PdfReader`.
- **Local state** – SQLite (processed files, audit log, stock-code mapping cache, Proposals).
- **Logging** – the default `Microsoft.Extensions.Logging` providers of the .NET generic host.
- **CI** – Azure Pipelines (`CI/azure-pipelines.yml`).

## Solution structure

```
eCommerce/
├── CI/
│   └── azure-pipelines.yml
├── docs/
│   └── adr/
├── GLOSSARY.md
└── src/
    ├── eCommerce.sln
    ├── Common/
    │   ├── Caching/Common.Caching/
    │   ├── Contract/Common.Contract/
    │   ├── Data/
    │   │   ├── Common.Data.Sqlite/
    │   │   └── Common.Data.Sqlite.Tests/
    │   └── Documents/
    │       ├── Common.Documents.PdfReader/
    │       └── Common.Documents.PdfReader.Tests/
    ├── Jobs/
    │   ├── Jobs.Receipts.Apply/
    │   ├── Jobs.Receipts.Scan/
    │   └── Jobs.Receipts.Tests/
    ├── Pohoda/
    │   ├── Pohoda.Contract/
    │   ├── Pohoda.MServer/
    │   ├── Pohoda.MServer.Tests/
    │   ├── Pohoda.Server/
    │   └── Pohoda.Server.Tests/
    └── Receipts/
        ├── Receipts.Contract/
        ├── Receipts.Core/
        ├── Receipts.Gabel/
        └── Receipts.Tests/
```

## Modules

### Common – shared libraries

- **Common.Contract** – shared DTOs and helpers used by the Pohoda agenda services: `StockDTO`, `OrderDTO`/`OrderItemDTO`, `PohodaCategoryDto`, `PohodaParameterDto`, the generic `IBuilder<D, T>` contract, an XML serializer (`Utf8Serializer`) and LINQ extensions.
- **Common.Caching** – a simple cache-invalidation signal (`CacheSignal`).
- **Common.Data.Sqlite** – reusable SQLite bootstrap (`SqliteDatabase.OpenAsync` opens a connection and runs the given schema DDL) and two domain-agnostic tables on top of it: `ProcessedFileTracker` (SHA-256 idempotency: has this file already been processed?) and `AuditLog` (an append-only chronological audit log).
- **Common.Documents.PdfReader** – the static `PdfRowReader` class (over PdfPig), which rebuilds a PDF's text as visual rows by clustering words by their bounding boxes. This is more reliable than the raw content-stream order for simple single-column tables such as the lines of a Supplier Invoice.

### Pohoda – the ERP integration

- **Pohoda.Contract** – service interfaces: `IReceiptService` (the Receipt agenda: list, create, update), `IStockCardService` (find a stock card by supplier code, search by part of its name, create a card; a transitional interface that will merge into `IStockService` when `Pohoda.Server` moves onto `Pohoda.MServer`), and the older `ICategoryService`, `IOrderService`, `IParameterService` and `IStockService`. It also holds the abstract `AbstractOrderBuilder`, the models `ExistingReceipt`, `ReceiptLineToWrite` and `StockNameMatch`, and the official mServer XSD schemas (Order, Stock, Filter, …).
- **Pohoda.MServer** – the shared low-level POHODA mServer client, independent of `Pohoda.Server`: `MServerClient` (HTTP through `IHttpClientFactory`, Windows-1250 encoding, parsing of the `state`/`error` response), `MServerLauncher` (starts and stops `pohoda.exe /http start|stop` directly through `ProcessStartInfo.ArgumentList`, without `cmd.exe`) and `ServiceCollectionExtensions.AddPohodaMServer`, which registers several mServer instances side by side through keyed DI (config section `Pohoda:<instanceKey>:*`).
- **Pohoda.Server** – the Pohoda agenda services. `ReceiptService` and `StockCardService` (implementing `IReceiptService`/`IStockCardService`) talk through the shared `Pohoda.MServer.MServerClient` and the `Pohoda.Server.Xml.DataPackBuilder` XML envelope, and are registered per instance by `AddPohodaAgendaServices`. The older services (`StockService`, `CategoryService`, `OrderService`, `ParameterService`, with their `Storages` and `Mappers`) still use the original `MServer/MServer.cs` and are not used by any executable today.
- **Pohoda.MServer.Tests**, **Pohoda.Server.Tests** – xUnit tests. The `Pohoda.Server.Tests` suite runs `ReceiptService` and `StockCardService` against a fake mServer (`FakeMServer`) and checks the exact shape of the XML requests (element namespaces of `requestPrijemka`/`requestStock`, limit/count, the doubly nested `stockItem`, `prijemkaAccessoryChargesItem` for freight) and the parsing of the responses.

### Receipts – stock Receipts from Supplier Invoices

Ported from the standalone `Gabel.PrijemkaSync` solution. Line items are extracted from a Supplier Invoice PDF and turned into a Proposal for a Receipt in Pohoda. The Proposal is stored locally (SQLite) and written out as a Markdown preview for approval; writing to Pohoda is a separate, explicitly run step. The executables are `Jobs.Receipts.Scan` and `Jobs.Receipts.Apply` (see [Receipts flow: Scan → Apply](#receipts-flow-scan--apply)).

- **Receipts.Contract** – domain models (`ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, `ReceiptLineProposal`) and the two interfaces a Supplier plugin implements: `IInvoiceParser` (PDF → `ParsedInvoice`) and `IVariantDetector` (recognizes a size variant in a product name).
- **Receipts.Core** – Supplier-agnostic logic: `ProposalBuilder` (matches the Supplier Invoice to an existing Receipt by looking for its invoice number in the Receipt text, tells existing stock cards from new ones, suggests a master card for variants), `ProposalApplier` (the only code that writes to Pohoda: creates missing stock cards and creates or extends the Receipt; freight always goes in as an accessory charge), `PreviewWriter` (the Markdown preview) and `ReceiptsStateStore` (SQLite state over `Common.Data.Sqlite`: processed files, audit log, stock-code mapping cache, stored Proposals). It depends only on `Receipts.Contract`, `Pohoda.Contract` and `Common.Data.Sqlite`.
- **Receipts.Gabel** – the first Supplier plugin: `GabelInvoiceParser` (a deterministic regex parser of final Supplier Invoices `FT n/nnnnnn` over `Common.Documents.PdfReader`; pro forma invoices are skipped, since they are not Supplier Invoices) and `GabelVariantDetector` (patterns `CM.120` / `135CM` / `Cm 115`; ranges such as `73-110CM` are not treated as a variant). Another Supplier means a new `Receipts.<Supplier>` project; `Receipts.Core` does not change.
- **Receipts.Tests** – xUnit tests, including the parser tests over real PDF samples (`TestData/`), tests of `ProposalBuilder`/`ProposalApplier` with hand-written fakes of the Pohoda services, and tests of the SQLite state.

### Jobs – executables

- **Jobs.Receipts.Scan** – a worker service; see below.
- **Jobs.Receipts.Apply** – a console application; see below.
- **Jobs.Receipts.Tests** – xUnit tests of `ScanRunner`, `ApplyRunner` and the command-line parser.

## Receipts flow: Scan → Apply

1. **Scan** (`Jobs.Receipts.Scan`) runs continuously. Every `Watch:ScanInterval` it sweeps `Watch:InboxFolder` for Supplier Invoice PDFs. For each new final Supplier Invoice it builds a Proposal (either *create a new Receipt* or *append the missing lines to an existing Receipt*), stores it in the SQLite database at `Watch:DatabasePath`, and writes a readable Markdown preview into `Watch:PendingApprovalFolder`. Scan **never writes** to Pohoda; it only reads (the Receipt list, stock card lookups). Each cycle starts mServer if needed and stops it afterwards, but only if the cycle started it.
2. **Review** – a person reads the preview and decides whether the Proposal is right.
3. **Apply** (`Jobs.Receipts.Apply`) is run by hand after the review. It is **the only place in the solution that writes Receipts and creates stock cards in Pohoda**. After a successful write the PDF moves to `Watch:ArchiveFolder`. A Proposal whose write fails is marked `Failed` and the rest of the batch continues.

```
Jobs.Receipts.Apply.exe status            # pending and failed Proposals
Jobs.Receipts.Apply.exe apply --id <id>   # write one approved Proposal
Jobs.Receipts.Apply.exe apply --all       # write all pending Proposals
Jobs.Receipts.Apply.exe reset --id <id>   # return a Proposal to PendingApproval and move its PDF back to the inbox (retry)
```

`status` and `reset` work only with the local SQLite database (`reset` also moves the PDF from `Watch:ArchiveFolder` back to `Watch:InboxFolder`) and do not need mServer.

Both applications **must** use the same `Watch:DatabasePath` in their `appsettings.json`: Scan and Apply hand Proposals over through that SQLite database, so two different paths mean two independent databases and Apply never sees what Scan created. That is why both tracked configurations use the same absolute path rather than a relative one (which would resolve to two different files for two `.exe` files in two different `bin/` folders).

### Before the first live `apply`

Writing to Pohoda through the XML agenda (`create`/`update` of a Receipt, creating a stock card) **has never run live yet**, not even in the original solution. The first run is therefore a supervised step:

1. Run it while someone who can see the state in Pohoda is present, ideally against a test accounting unit first.
2. Always make the first write `apply --id <id>` for one specific Proposal, never `apply --all`.
3. Test an `AppendLines` Proposal (extending an existing Receipt) first, on a document you can afford to lose. `UpdateAsync` sends the **full list of lines** (existing + new); if Pohoda appends lines instead of replacing them, every existing line is duplicated. This is the most important unverified property of the whole chain.
4. `UpdateAsync` writes **today's date** into the header; check whether it overwrote the Receipt's original date and whether that matters.
5. After the write, check in Pohoda the placement of the new stock card (`storage`) and its `orderName` for size variants. Linking a variant to its master card still has to be done by hand; the XML agenda has no field for it.
6. Check that `rateVAT="none"` is the right token for VAT-exempt items.

Points 3 to 6 are open questions carried over from the original solution, not new risks introduced by the move.

## Build, test and run

- **Build**: open `src/eCommerce.sln` in Visual Studio (with the .NET 10 SDK), or run `dotnet build src/eCommerce.sln`.
- **Test**: `dotnet test src/eCommerce.sln`.
- **Run**: both executables are configured through their own `appsettings.json` (sections `Pohoda:AcEcom`, `Watch`, `Matching`):

  ```bash
  dotnet run --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
  dotnet run --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj -- status
  ```

  In production, run the built `Jobs.Receipts.Scan.exe` as a long-running process and `Jobs.Receipts.Apply.exe` by hand. There is no orchestration script or docker-compose in the repository.

## CI

`CI/azure-pipelines.yml` runs on every push to `main` on a `windows-latest` agent: NuGet restore → VSBuild (Release) → VSTest, which discovers and runs the test projects.

## Secrets configuration

The tracked `appsettings.json` files of `Jobs.Receipts.Scan` and `Jobs.Receipts.Apply` hold only non-secret defaults; `Pohoda:AcEcom:Username` and `Pohoda:AcEcom:Password` are deliberately empty (`""`). Both applications need them to log in to the AcEcom mServer instance (port 444).

`AcEcom` is the instance key: it selects the config section `Pohoda:AcEcom:*` and the keyed DI registrations. `ConfigName` and `DefaultStorageCode` in that section stay `"GoNoow"`, because those are the real mServer configuration and warehouse names inside Pohoda.

**Local development** – set the values through .NET User Secrets (outside the repository, bound to the `<UserSecretsId>` of each `.csproj`), once per application:

```bash
dotnet user-secrets set "Pohoda:AcEcom:Username" "<value>" --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
dotnet user-secrets set "Pohoda:AcEcom:Password" "<value>" --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
dotnet user-secrets set "Pohoda:AcEcom:Username" "<value>" --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj
dotnet user-secrets set "Pohoda:AcEcom:Password" "<value>" --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj
```

User Secrets load only in the `Development` environment, which is why each project's `Properties/launchSettings.json` sets `DOTNET_ENVIRONMENT=Development`. Running the built `.exe` directly, or `dotnet run --no-launch-profile`, bypasses that variable and User Secrets are not loaded.

**Production** – set the same keys as environment variables on the target machine, with double underscores for nested sections: `Pohoda__AcEcom__Username` and `Pohoda__AcEcom__Password`. The .NET host reads them automatically and they override `appsettings.json`.

Earlier versions used the instance key `GoNoow`. If User Secrets or environment variables are still set under `Pohoda:GoNoow:*` / `Pohoda__GoNoow__*`, set them again under the new names.

Never write real values into `appsettings.json` or any other file tracked by git.
