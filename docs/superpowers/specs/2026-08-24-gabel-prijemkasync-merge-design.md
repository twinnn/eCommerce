# Merging Gabel.PrijemkaSync into the eCommerce portfolio — design

Date: 2026-08-24 · Status: proposed

## 1. Purpose and scope

`C:\git\Gabel.PrijemkaSync` is a standalone solution that automates turning a Gabel
supplier invoice (PDF) into a Pohoda warehouse receipt (příjemka), via Pohoda mServer.
It was built independent of `C:\git\eCommerce` on purpose, to avoid risking the running
production Jobs while it was under active development.

This design merges it into the eCommerce portfolio as a set of small, single-purpose
modules, with **zero duplicated functionality** — in particular, one shared Pohoda
mServer client instead of the two independent ones that exist today.

Two other future automations were mentioned as context for module boundaries — syncing
product-catalog PDFs into Pohoda, and turning an incoming customer order email into an
invoice + shipping label — but are **out of scope for this design**. They are separate
sub-projects that will each get their own brainstorm; this design only shapes the new
modules (`Common.Documents.PdfReader`, `Common.Data.Sqlite`, `Pohoda.MServer`,
`Receipts.*`) so that work stays plausible later, without building it now.

## 2. Key decisions (from brainstorming)

1. **Single repo/solution.** Gabel.PrijemkaSync's projects move into `eCommerce/src` and
   `eCommerce.sln`, reusing `Common`/`Pohoda.*` directly via project references. The
   `Gabel.PrijemkaSync` repo is retired once the migration is verified.
2. **Generic receipt-sync capability, Gabel is the first instance.** Modeled like
   `Imports/` (one project per supplier feed): a supplier-agnostic pipeline/contract,
   with Gabel's PDF parser as a plugin. A second supplier later only needs a new parser
   project.
3. **Migrate the existing production Jobs onto the shared, fixed mServer client now**,
   not as a deferred follow-up — `Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`
   all move onto `Pohoda.MServer` in this effort (staged, see §6).
4. **Automate the `scan` step, keep `apply` manual.** A new worker service watches the
   invoice inbox and builds proposals on a schedule; the actual Pohoda write
   (`apply`) stays an explicitly human-triggered CLI step — the dry-run/approval
   principle from Gabel.PrijemkaSync's own design is preserved unchanged.

## 3. Target module layout

```
src/
├── Common/
│   ├── Contract/, Caching/, Email/, Readers/         (unchanged)
│   ├── Documents/Common.Documents.PdfReader/         NEW
│   └── Data/Common.Data.Sqlite/                       NEW
├── Pohoda/
│   ├── Pohoda.Contract/                                extended
│   ├── Pohoda.MServer/                                NEW
│   └── Pohoda.Server/                                  refactored to sit on Pohoda.MServer
├── Nop/, Expando/, Imports/                            unchanged (Imports now uses Pohoda.MServer)
├── Receipts/                                           NEW
│   ├── Receipts.Contract/
│   ├── Receipts.Core/
│   └── Receipts.Gabel/
└── Jobs/
    ├── Jobs.Nop.Synch/, Jobs.Expando.Orders/           unchanged, now on Pohoda.MServer
    ├── Jobs.Receipts.Scan/                              NEW (worker service)
    └── Jobs.Receipts.Apply/                              NEW (CLI)
```

### Common.Documents.PdfReader
Generalized from Gabel's `PdfRowReader`: wraps UglyToad.PdfPig to turn a PDF into
visual text rows (word grouping by Y-coordinate, left-to-right ordering). No
Gabel-specific parsing logic — that stays in `Receipts.Gabel`.

### Common.Data.Sqlite
Generalized from Gabel's `SqliteStateStore`: a small, dependency-free (no EF Core,
direct `Microsoft.Data.Sqlite`) helper for processed-file idempotency tracking,
key/value mapping caches, and an append-only audit log. Table shapes stay
consumer-defined; this module provides the connection/migration bootstrap and
generic helpers Gabel's store already implements.

### Pohoda.MServer
The one shared low-level mServer client, replacing the transport code currently
embedded in `Pohoda.Server/MServer/MServer.cs`. Built from
`Gabel.PrijemkaSync.Pohoda`'s implementation, since it is the more correct one:

- **Error handling** — parses `state`/`error` per `PohodaResponseParser`, returns
  `PohodaResult{Success, Errors, Warnings}`. Today's `MServer.SendRequest` returns the
  raw response regardless of Pohoda-level failure; `Jobs.Nop.Synch` /
  `Jobs.Expando.Orders` currently have no reliable way to detect a failed write.
- **Process launch** — `MServerLauncher` (`ProcessStartInfo.ArgumentList`, no
  `cmd.exe`) with backoff retry, replacing today's `cmd.exe`-piped start/stop.
- **Encoding** — explicit `CodePagesEncodingProvider` registration and
  Windows-1250 byte handling, matching what's proven live against Gabel's mServer.
- **Auth** stays composition-root-configured (`STW-Authorization` header set when the
  typed `HttpClient` is registered) — this is already correct in production today and
  doesn't change.
- **Multi-instance support** — eCommerce needs two live mServer instances side by
  side: port `5336` / config `"StwTest"` (existing Jobs) and port `444` / config
  `"GoNoow"` (Receipts), same Pohoda company (IČO 53870441), different warehouses.
  Registered via keyed DI, one named/typed `HttpClient` + options section per instance
  (`services.AddPohodaMServer("StwTest", ...)`, `services.AddPohodaMServer("GoNoow", ...)`),
  consumers resolve the instance they need by key instead of the single ambient
  `IStorageSystem` used today.

### Pohoda.Contract / Pohoda.Server
`Pohoda.Contract` gains `IReceiptService` (Prijemka agenda: list/create/update) and
`IStockService` gains the read/search members Gabel's `StockClient` needs
(`FindBySupplierCode`, `Create`, `SearchByNameContaining`) — one stock interface
instead of two incompatible ones. `Pohoda.Server`'s existing services
(`StockService`, `CategoryService`, `OrderService`, `ParameterService`) stop
implementing transport themselves and take a `Pohoda.MServer` client instead; a new
`ReceiptService` is added, built from Gabel's `PrijemkaClient`.

### Receipts.Contract / Receipts.Core / Receipts.Gabel
- `Receipts.Contract` — `ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, and the
  `IInvoiceParser` plugin interface.
- `Receipts.Core` — `ProposalBuilder`, `ProposalApplier`, invoice-to-receipt matching.
  Supplier-agnostic; depends only on `Pohoda.Contract` and `Common.Data.Sqlite`, never
  on a specific supplier's types.
- `Receipts.Gabel` — `GabelInvoiceParser`, `GabelVariantDetector`, implementing
  `IInvoiceParser`. A second supplier later = a new `Receipts.<Supplier>` project;
  `Receipts.Core` doesn't change.

All business rules carry over unchanged from Gabel.PrijemkaSync's current behavior —
this is a relocation of working logic, not a rewrite:
- Only final invoices (`FT n/nnnnnn`) are parsed; proforma (`pr1-*.pdf`) is skipped.
- Matching an invoice to an existing příjemka is by `"FT n/nnnnnn"` occurring in the
  document text (no server-side text filter exists in the Pohoda XML agenda).
- Existing receipt lines are never silently overwritten; `AppendLines` only adds
  missing lines, differences on existing lines are logged as warnings.
- Freight (`SPESE TRASP.`) always goes to `prijemkaAccessoryChargesItem`, never mapped
  to a stock line.
- Variant/master-card linkage (size variants like `"120 cm"`) can be *found and
  suggested* (`SearchStockByNameContainingAsync`) but never *linked* automatically —
  the Pohoda XML agenda has no field for that relationship; linking stays a manual
  step in the Pohoda desktop UI. This is a permanent property of the mServer API, not
  something this merge attempts to fix.

## 4. Data flow

```
Jobs.Receipts.Scan (worker, scheduled/file-watch on inbox folder)
   │
   ▼
Common.Documents.PdfReader → visual rows
   │
   ▼
Receipts.Gabel: GabelInvoiceParser.TryParse   (skips PROFORMA/unrecognized)
   │
   ▼
Receipts.Core: ProposalBuilder
   │  ├─ Pohoda.Server.ReceiptService.ListRecentAsync   (via Pohoda.MServer "GoNoow")
   │  ├─ Pohoda.Server.StockService.FindBySupplierCode  (via Pohoda.MServer "GoNoow")
   │  └─ builds ReceiptProposal (CreateReceipt/AppendLines)
   ▼
Common.Data.Sqlite: proposal saved, status PendingApproval
   ▼
Preview file written to na_schvalenie/

────────── human reviews preview, runs Jobs.Receipts.Apply ──────────

Jobs.Receipts.Apply (CLI: apply --id / --all / status)
   │
   ▼
Receipts.Core: ProposalApplier
   │  ├─ Pohoda.Server.StockService.Create (missing stock cards)
   │  └─ Pohoda.Server.ReceiptService.Create/Update (the actual write)
   ▼
PDF moved to Archive/, audit log row written via Common.Data.Sqlite
```

## 5. Rollout sequencing

Even though the end state migrates everything, landing it as one change is unnecessary
risk to the running production jobs. Staged, independently revertible PRs:

1. **PR1 — additive only.** `Pohoda.MServer`, `Common.Data.Sqlite`,
   `Common.Documents.PdfReader` added with no consumers yet. Existing code, existing
   jobs, existing tests: unchanged, still green.
2. **PR2 — Receipts logic, isolated.** `Receipts.Contract`, `Receipts.Core`,
   `Receipts.Gabel` added, porting Gabel's `Extraction`/`Domain`/business logic onto
   the new shared libs. Doesn't touch existing Jobs. Gabel's xUnit tests (real PDF
   fixtures) move over unchanged.
3. **PR3 — Receipts executables.** `Jobs.Receipts.Scan` + `Jobs.Receipts.Apply`, wired
   to `Pohoda.MServer` "GoNoow". Verify `scan` live against Gabel's mServer instance
   (already proven safe — read-only). `apply` stays gated behind the same manual
   approval Gabel.PrijemkaSync already required — the first-ever live `apply` was
   never executed there either, so this isn't a new risk introduced by the merge.
4. **PR4 — migrate Pohoda.Server onto Pohoda.MServer.** `StockService`,
   `CategoryService`, `OrderService`, `ParameterService` switch from the old inline
   `MServer.cs` to `Pohoda.MServer`, keeping public interfaces stable so
   `Jobs.Nop.Synch` / `Jobs.Expando.Orders` / `Import.Wiky` need no code changes beyond
   DI registration (config section rename, see §7). Old `MServer.cs` /
   `MServerConfiguration.cs` deleted.
5. **PR5 — re-verify existing jobs.** Supervised run of `Jobs.Nop.Synch`,
   `Jobs.Expando.Orders`, `Import.Wiky` against their mServer instance ("StwTest", port
   5336), confirming category/product/order sync still behaves identically. Because
   `Pohoda.MServer` now actually parses `state="error"` (unlike today's client), this
   step may surface previously-silent failures — expected, but needs a real supervised
   pass rather than being assumed safe from the diff alone.
6. **PR6 — retire Gabel.PrijemkaSync.** Once Jobs.Receipts.* are confirmed working
   end-to-end from the eCommerce repo, archive the old repo and fold its
   `technicka-dokumentacia.md` / `pouzivatelska-prirucka.md` content into eCommerce's
   module docs.

## 6. Testing

- Gabel.PrijemkaSync.Tests (xUnit, real PDF fixtures in `TestData/`) moves into
  `src/Receipts/Receipts.Tests` unchanged — proves the parser wasn't altered by the
  relocation.
- New unit tests for `Pohoda.MServer`'s response parser (error/warning extraction),
  currently only covered indirectly through Gabel's live testing notes.
- PR5's existing-jobs re-verification is manual (matches current reality: no automated
  test harness exists today for live mServer interaction; `eCommerce`'s own README
  notes there are no test projects for the Jobs).
- `Jobs.Receipts.Apply`'s actual Pohoda write path (create/update příjemka) — first
  live test remains gated behind explicit human approval, per Gabel's own dry-run
  principle. This merge relocates that gate, it doesn't remove it.

## 7. Configuration

- Two Pohoda mServer config sections instead of one flat `Pohoda:username/password`:
  `Pohoda:StwTest:*` (existing Jobs) and `Pohoda:GoNoow:*` (Receipts), matching the
  keyed-DI registration in §3. Existing user-secrets entries need updating to the new
  section names as part of PR4 — documented in the migration notes.
- `Watch:*` (inbox/archive/pending folders) and `Matching:*` (lookback, limit) config
  sections carry over unchanged into `Jobs.Receipts.Scan`'s `appsettings.json`.
- `DefaultStorageCode` / `DefaultTypePriceCode` stay under the `GoNoow`-scoped Pohoda
  config section.
- Same secrets-handling convention as the rest of the repo: empty defaults in tracked
  `appsettings.json`, real values via .NET User Secrets locally / environment
  variables (`__` nesting) in production.

## 8. Risks and open items (carried over, not introduced by this merge)

- First real `apply` (create/update příjemka, create stock card) is still unverified
  live — remains a manual, explicitly-approved step after the merge, same as before it.
- Exact `prijemkaItem`/`stockItem` XML nesting, `UpdateAsync`'s full-resend semantics,
  and the `rateVAT` token for VAT-exempt lines are still open questions per
  Gabel.PrijemkaSync's own docs (§3.6) — unaffected by the relocation.
- Variant/master-card linkage stays a permanent manual step in the Pohoda desktop UI
  (no field exists in the XML agenda for it) — not something this merge changes or
  attempts to fix.
- PR5 (re-verifying existing jobs) is the highest-risk step in this plan, since it's
  the only one touching already-running production code paths. It should not be
  rushed, and should be done with someone able to watch Pohoda/nopCommerce state
  during the run.

## 9. Explicitly out of scope

- Product-catalog PDF sync and email→invoice→shipping-label automation — future
  sub-projects, each gets its own brainstorm. This design only keeps the door open by
  making `Common.Documents.PdfReader` and `Receipts.Contract` generic enough to be
  reused, without building either feature now.
- Any chatbot/approval-UI layer for `apply` — CLI stays the interface for now, per the
  automation-shape decision in §2.
