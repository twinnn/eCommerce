# Gabel.PrijemkaSync Merge — PR3: Receipts Executables Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the two runnable Receipts applications to `eCommerce.sln` — `Jobs.Receipts.Scan` (a worker service that sweeps the invoice inbox on a schedule and writes approval previews) and `Jobs.Receipts.Apply` (a CLI that performs the actual Pohoda write after a human has approved a proposal) — both wired to the `Pohoda.MServer` instance "GoNoow", with no change to any existing Job.

**Architecture:** Both applications are thin composition roots over the PR2 libraries. The logic that is worth testing lives in one plain class per application — `ScanRunner` (one inbox sweep) and `ApplyRunner` (apply / status / reset) — constructed by hand after the SQLite store is opened asynchronously; the `Worker` background service and the CLI `Program` only compose dependencies, own the mServer start/stop lifecycle, and translate results into log lines and exit codes. Pohoda access goes through the keyed DI registrations: `AddPohodaMServer(configuration, "GoNoow")` from PR1 plus a new `AddPohodaAgendaServices("GoNoow")` in `Pohoda.Server` that registers keyed `IReceiptService`/`IStockCardService`. The supplier plugin (`GabelInvoiceParser`, `GabelVariantDetector`) is chosen in each application's composition root, never inside `Receipts.Core`.

**Tech Stack:** .NET 10.0, `Microsoft.Extensions.Hosting` (worker + generic host), `Microsoft.Extensions.Options`/`Configuration`, keyed dependency injection, xUnit.

**Spec:** [docs/superpowers/specs/2026-08-24-gabel-prijemkasync-merge-design.md](../specs/2026-08-24-gabel-prijemkasync-merge-design.md) — this plan implements PR3 of that spec's §5 rollout sequencing, and realizes the data flow in its §4 and the configuration shape in its §7. PR1 (shared libraries) and PR2 (receipt libraries) are complete on this branch; the PR2 plan is [2026-09-11-gabel-prijemkasync-merge-pr2-receipts-libraries.md](2026-09-11-gabel-prijemkasync-merge-pr2-receipts-libraries.md). PR4 (migrating `Pohoda.Server`'s existing services and the production Jobs onto `Pohoda.MServer`), PR5 (supervised re-verification) and PR6 (retiring the old repo) are later plans.

**Source being ported:** `C:\git\Gabel.PrijemkaSync\src\Gabel.PrijemkaSync.App\` — `Program.cs` (the `scan`/`apply`/`status`/`reset` command bodies) and `AppOptions.cs` (`WatchOptions`). Every code block in this plan is the complete ported content; the implementer does not need to open the old repo.

## Global Constraints

- Target framework `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` on every new project. Pin `Microsoft.Extensions.*` to `10.0.11`. Test projects pin `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.4`, `Microsoft.NET.Test.Sdk` `17.14.1`, `coverlet.collector` `6.0.4`, `<IsPackable>false</IsPackable>`, global `<Using Include="Xunit" />`.
- **Namespace hazard — `Receipts`.** Inside `namespace Jobs.Receipts.*`, the simple name `Receipts` resolves to `Jobs.Receipts`, not to the top-level `Receipts` namespace. Keep every `using` directive at the top of the file, above the file-scoped `namespace X;` declaration — there it is a compilation-unit using and resolves against the global namespace. Never write a qualified `Receipts.Core.Something` inside such a namespace; use the simple type name via a using, or `global::Receipts.Core.Something` if a qualified reference is unavoidable.
- **Namespace hazard — `MServer`.** Inside `namespace Pohoda.Server.*`, the simple name `MServer` resolves to the legacy namespace `Pohoda.Server.MServer`. Import `using Pohoda.MServer;` (at the top of the file) and use the simple type names `MServerClient` / `MServerConfiguration`. Never add `using Pohoda.Server.MServer;` to a new file.
- `Pohoda.Contract` and `Pohoda.Server` have NO `<ImplicitUsings>` — new files there need explicit `using` directives.
- **No pre-existing `.cs` file changes.** This PR adds files. The only pre-existing files it may modify are `src/eCommerce.sln` (via `dotnet sln add`), `src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj` (one added `PackageReference`) and `README.md` (Task 6). `Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`, `Pohoda.Server`'s existing services and the legacy `MServer/MServer.cs` are untouched — Task 6 verifies this with `git diff`.
- **Secrets never enter the repository.** The old `Gabel.PrijemkaSync` `appsettings.json` contains a real username and password; those must NOT be copied. Tracked `appsettings.json` carries empty strings for `Username` and `Password`, exactly like `Jobs.Nop.Synch`'s. Real values come from .NET User Secrets locally and environment variables with `__` nesting in production, per the README's existing convention. Non-secret deployment values (`BaseUrl`, `Port`, `ConfigName`, `PohodaExePath`, `Ico`, `DefaultStorageCode`, `DefaultTypePriceCode`, folders) do belong in the tracked file.
- Configuration shape per spec §7: the mServer instance is `Pohoda:GoNoow:*`, the folders are `Watch:*`, the matching window is `Matching:*` with the PR2 key name `ReceiptListLimit`.
- **`scan` never writes to Pohoda; `apply` is the only writer**, and it stays a manually invoked command. Do not add a scheduler, a service registration, or any automatic invocation of `apply`.
- Business behavior carries over from the old CLI unchanged: proforma and unparsable PDFs are skipped with a logged reason; a PDF whose SHA-256 is already in `processed_files` is skipped; `apply --id` accepts a `PendingApproval` or `Failed` proposal while `apply --all` takes only `PendingApproval`; a successful apply moves the PDF to the archive folder; a failed apply sets `Failed` + `FailureReason`, appends a `CHYBA pri zápise:` audit row and continues with the next proposal; `reset --id` returns a proposal to `PendingApproval` and moves its PDF back out of the archive.
- Slovak user-facing strings (log messages, CLI usage text, audit rows) are carried over verbatim from the old `Program.cs`. Where a message named the old application or the `Prijemka` spelling, the PR2 identifier rename applies (`ResultPrijemkaNumber` → `ResultReceiptNumber`).
- The `ReceiptsStateStore`'s SQLite connection is not thread-safe: each application opens one store per run (the CLI) or per sweep (the worker) and processes files strictly sequentially. Do not introduce parallel iteration over the inbox.
- `MServerLauncher.StopAsync` is called only when `EnsureRunningAsync` reported `StartedByUs == true`, and always from a `finally` block.

---

## File Structure

```
src/Receipts/Receipts.Core/
  WatchOptions.cs                      NEW — folder/database locations, config section "Watch"

src/Pohoda/Pohoda.Server/Services/
  ServiceCollectionExtensions.cs       NEW — AddPohodaAgendaServices(instanceKey): keyed IReceiptService/IStockCardService

src/Pohoda/Pohoda.Server.Tests/
  ServiceCollectionExtensionsTests.cs  NEW
  Pohoda.Server.Tests.csproj           MODIFIED — adds Microsoft.Extensions.Hosting

src/Jobs/Jobs.Receipts.Scan/           NEW — worker service
  Jobs.Receipts.Scan.csproj
  PohodaInstance.cs                    — the mServer instance key this app talks to
  ScanRunner.cs                        — one inbox sweep (the testable logic)
  Worker.cs                            — BackgroundService loop + mServer lifecycle
  Program.cs                           — composition root
  appsettings.json
  Properties/launchSettings.json

src/Jobs/Jobs.Receipts.Apply/          NEW — CLI
  Jobs.Receipts.Apply.csproj
  PohodaInstance.cs
  CommandLine.cs                       — argument parsing + usage text (pure, testable)
  ApplyRunner.cs                       — apply / status / reset (the testable logic)
  Program.cs                           — composition root + mServer lifecycle + exit codes
  appsettings.json
  Properties/launchSettings.json

src/Jobs/Jobs.Receipts.Tests/          NEW — one test project covering both applications
  Jobs.Receipts.Tests.csproj
  Fakes/FakeInvoiceParser.cs
  Fakes/FakeVariantDetector.cs
  Fakes/FakeStockCardService.cs
  Fakes/FakeReceiptService.cs
  Fakes/PipelineHarness.cs             — temp folders + temp SQLite store + wired runners
  ScanRunnerTests.cs
  ApplyRunnerTests.cs
  CommandLineTests.cs
```

Each application keeps exactly one class with branching logic (`ScanRunner`, `ApplyRunner`) plus one pure parser (`CommandLine`); everything else is composition. `Worker` and the two `Program.cs` files are deliberately thin and are not unit-tested — they contain no branch that a test could meaningfully pin down, and the host/mServer lifecycle they own is exercised by the supervised live run the spec's §5 PR3 requires.

---

## Task 1: `WatchOptions` + keyed agenda-service registration

**Files:**
- Create: `src/Receipts/Receipts.Core/WatchOptions.cs`
- Create: `src/Pohoda/Pohoda.Server/Services/ServiceCollectionExtensions.cs`
- Modify: `src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj`
- Test: `src/Pohoda/Pohoda.Server.Tests/ServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `Pohoda.Contract.Services.IReceiptService` / `IStockCardService`; `Pohoda.Server.Services.ReceiptService(MServerClient, MServerConfiguration)` / `StockCardService(MServerClient, MServerConfiguration)`; `Pohoda.MServer.ServiceCollectionExtensions.AddPohodaMServer(IServiceCollection, IConfiguration, string instanceKey)` which registers a keyed `MServerClient`, a keyed `MServerLauncher` and a named `MServerConfiguration` options instance under the same key.
- Produces (used by Tasks 2–5):
  - `Receipts.Core.WatchOptions` — `const string SectionName = "Watch"`; `required string InboxFolder`, `required string ArchiveFolder`, `required string PendingApprovalFolder`; `string DatabasePath = "data/receipts.db"`; `TimeSpan ScanInterval = TimeSpan.FromMinutes(15)`.
  - `Pohoda.Server.Services.ServiceCollectionExtensions.AddPohodaAgendaServices(this IServiceCollection services, string instanceKey) : IServiceCollection`.

- [ ] **Step 1: Add `WatchOptions` to `Receipts.Core`**

Create `src/Receipts/Receipts.Core/WatchOptions.cs`:

```csharp
namespace Receipts.Core;

/// <summary>
/// Folder and database locations for the receipt-sync pipeline (config section "Watch").
/// Relative paths are resolved by each executable against its own base directory, so a built
/// `.exe` behaves the same whatever the current working directory is.
/// </summary>
public sealed class WatchOptions
{
    public const string SectionName = "Watch";

    /// <summary>Folder the scan job reads supplier invoice PDFs from (top level only, `*.pdf`).</summary>
    public required string InboxFolder { get; init; }

    /// <summary>Folder a PDF is moved into once its proposal has been successfully applied to Pohoda.</summary>
    public required string ArchiveFolder { get; init; }

    /// <summary>Folder the human-readable Markdown previews are written to, for review before `apply`.</summary>
    public required string PendingApprovalFolder { get; init; }

    /// <summary>SQLite file holding processed files, the audit log, the stock-code mapping cache and the proposals.</summary>
    public string DatabasePath { get; init; } = "data/receipts.db";

    /// <summary>How long the scan worker waits between two inbox sweeps. Bound from a "hh:mm:ss" string.</summary>
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMinutes(15);
}
```

- [ ] **Step 2: Add the keyed agenda-service registration to `Pohoda.Server`**

Create `src/Pohoda/Pohoda.Server/Services/ServiceCollectionExtensions.cs`. Note the usings sit above the file-scoped namespace, and `MServerClient`/`MServerConfiguration` are written as simple names so they bind to `Pohoda.MServer`, not to the legacy `Pohoda.Server.MServer` namespace:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Services;
using Pohoda.MServer;

namespace Pohoda.Server.Services;

/// <summary>
/// Registers the Pohoda agenda services that sit on top of one mServer instance.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers keyed <see cref="IReceiptService"/> and <see cref="IStockCardService"/> implementations
    /// bound to the mServer instance registered under <paramref name="instanceKey"/>. Call
    /// <c>AddPohodaMServer(configuration, instanceKey)</c> for the same key first — this method resolves
    /// that instance's <see cref="MServerClient"/> and its named <see cref="MServerConfiguration"/>.
    /// Consumers resolve the instance they need with
    /// <c>GetRequiredKeyedService&lt;IReceiptService&gt;("GoNoow")</c> or the matching
    /// <c>[FromKeyedServices("GoNoow")]</c> constructor parameter.
    /// </summary>
    public static IServiceCollection AddPohodaAgendaServices(this IServiceCollection services, string instanceKey)
    {
        services.AddKeyedSingleton<IReceiptService>(instanceKey, (sp, key) =>
            new ReceiptService(
                sp.GetRequiredKeyedService<MServerClient>(key),
                sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get((string)key!)));

        services.AddKeyedSingleton<IStockCardService>(instanceKey, (sp, key) =>
            new StockCardService(
                sp.GetRequiredKeyedService<MServerClient>(key),
                sp.GetRequiredService<IOptionsMonitor<MServerConfiguration>>().Get((string)key!)));

        return services;
    }
}
```

If the build reports that `IServiceCollection`, `IOptionsMonitor<T>` or `AddKeyedSingleton` cannot be found, add the missing pin to `src/Pohoda/Pohoda.Server/Pohoda.Server.csproj`'s existing `PackageReference` group and nothing else:

```xml
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.11" />
```

- [ ] **Step 3: Give the test project the hosting package**

Edit `src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj` — add one line to the existing `PackageReference` group, keeping the group alphabetical:

```xml
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
```

- [ ] **Step 4: Write the failing test**

Create `src/Pohoda/Pohoda.Server.Tests/ServiceCollectionExtensionsTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Pohoda.Server.Services;

namespace Pohoda.Server.Tests;

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
            ["Pohoda:GoNoow:DefaultTypePriceCode"] = "GABEL"
        });

        builder.Services.AddPohodaMServer(builder.Configuration, "StwTest");
        builder.Services.AddPohodaMServer(builder.Configuration, "GoNoow");
        builder.Services.AddPohodaAgendaServices("StwTest");
        builder.Services.AddPohodaAgendaServices("GoNoow");

        return builder.Build();
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_a_keyed_IReceiptService_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<IReceiptService>("StwTest");
        var goNoow = host.Services.GetKeyedService<IReceiptService>("GoNoow");

        Assert.IsType<ReceiptService>(stwTest);
        Assert.IsType<ReceiptService>(goNoow);
        Assert.NotSame(stwTest, goNoow);
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_a_keyed_IStockCardService_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<IStockCardService>("StwTest");
        var goNoow = host.Services.GetKeyedService<IStockCardService>("GoNoow");

        Assert.IsType<StockCardService>(stwTest);
        Assert.IsType<StockCardService>(goNoow);
        Assert.NotSame(stwTest, goNoow);
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_nothing_under_the_default_unkeyed_registration()
    {
        using var host = BuildHost();

        Assert.Null(host.Services.GetService<IReceiptService>());
        Assert.Null(host.Services.GetService<IStockCardService>());
    }

    [Fact]
    public void Each_keyed_agenda_service_talks_to_its_own_mServer_instance()
    {
        using var host = BuildHost();

        // The service is constructed from the keyed MServerClient, so resolving the same key twice
        // must hand back the one singleton, and the two keys must never share it.
        var goNoowFirst = host.Services.GetRequiredKeyedService<IReceiptService>("GoNoow");
        var goNoowSecond = host.Services.GetRequiredKeyedService<IReceiptService>("GoNoow");
        var stwTest = host.Services.GetRequiredKeyedService<IReceiptService>("StwTest");

        Assert.Same(goNoowFirst, goNoowSecond);
        Assert.NotSame(goNoowFirst, stwTest);

        var goNoowClient = host.Services.GetRequiredKeyedService<MServerClient>("GoNoow");
        var stwTestClient = host.Services.GetRequiredKeyedService<MServerClient>("StwTest");
        Assert.NotSame(goNoowClient, stwTestClient);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

```bash
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: build error — `AddPohodaAgendaServices` does not exist (if Steps 1–2 are done first, this step instead confirms the tests compile and pass; run it before writing the implementation to see the red state).

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet build src/eCommerce.sln
dotnet test src/Pohoda/Pohoda.Server.Tests
```

Expected: build succeeds with 0 errors (about 351 pre-existing warnings from legacy projects is the known baseline); `Passed! - Failed: 0, Passed: 19` (15 existing + 4 new).

- [ ] **Step 7: Commit**

```bash
git add src/Receipts/Receipts.Core/WatchOptions.cs src/Pohoda/Pohoda.Server/Services/ServiceCollectionExtensions.cs src/Pohoda/Pohoda.Server/Pohoda.Server.csproj src/Pohoda/Pohoda.Server.Tests
git commit -m "feat: add WatchOptions and keyed Pohoda agenda-service registration"
```

---

## Task 2: `Jobs.Receipts.Scan` project + `ScanRunner` + the shared test project

**Files:**
- Create: `src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj`
- Create: `src/Jobs/Jobs.Receipts.Scan/PohodaInstance.cs`
- Create: `src/Jobs/Jobs.Receipts.Scan/ScanRunner.cs`
- Create: `src/Jobs/Jobs.Receipts.Scan/Worker.cs`
- Create: `src/Jobs/Jobs.Receipts.Scan/Program.cs`
- Create: `src/Jobs/Jobs.Receipts.Tests/Jobs.Receipts.Tests.csproj`
- Create: `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeInvoiceParser.cs`
- Create: `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeVariantDetector.cs`
- Create: `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeStockCardService.cs`
- Create: `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeReceiptService.cs`
- Create: `src/Jobs/Jobs.Receipts.Tests/Fakes/Make.cs`
- Create: `src/Jobs/Jobs.Receipts.Tests/Fakes/PipelineHarness.cs`
- Test: `src/Jobs/Jobs.Receipts.Tests/ScanRunnerTests.cs`

`Worker.cs` and `Program.cs` are created here rather than in Task 3 because `Microsoft.NET.Sdk.Worker` forces `<OutputType>Exe</OutputType>`: without an entry point the project cannot compile at all, so neither it nor the test project that references it could be built at the end of this task.

**Interfaces:**
- Consumes: `Receipts.Contract.IInvoiceParser` / `IVariantDetector` / `ParsedInvoice` / `InvoiceLine` / `ReceiptProposal` / `ProposalStatus`; `Receipts.Core.ProposalBuilder(IStockCardService, IVariantDetector, StockMappingCache, ILogger<ProposalBuilder>)` with `BuildAsync(ParsedInvoice, IReadOnlyList<ExistingReceipt>, CancellationToken)`; `Receipts.Core.PreviewWriter.Render(ReceiptProposal)`; `Receipts.Core.MatchingOptions { LookbackMonths, ReceiptListLimit }`; `Receipts.Core.WatchOptions` (Task 1); `Receipts.Core.Persistence.ReceiptsStateStore.OpenAsync(string, CancellationToken)` with `ProcessedFiles.IsProcessedAsync(sha, ct)` / `ProcessedFiles.MarkProcessedAsync(sha, path, referenceId, ct)` / `Proposals.SaveAsync(proposal, ct)` / `Proposals.GetByStatusAsync(status, ct)` / `StockMappings`; `Pohoda.Contract.Services.IReceiptService.ListRecentAsync(DateOnly, int, CancellationToken)`; `Pohoda.Contract.Models.ExistingReceipt` / `ExistingReceiptLine` / `StockNameMatch` / `ReceiptLineToWrite`.
- Produces (used by Tasks 3–5):
  - `Jobs.Receipts.Scan.PohodaInstance.Key` — `internal const string`, value `"GoNoow"`.
  - `Jobs.Receipts.Scan.ScanResult(int FilesFound, int ProposalsCreated, int Skipped)` — public readonly record struct.
  - `Jobs.Receipts.Scan.ScanRunner(IReceiptService receipts, IInvoiceParser parser, ProposalBuilder builder, ReceiptsStateStore store, WatchOptions watch, MatchingOptions matching, ILogger<ScanRunner> logger)` with `Task<ScanResult> RunOnceAsync(CancellationToken ct = default)`.
  - Test helpers `Jobs.Receipts.Tests.Fakes.FakeInvoiceParser` / `FakeVariantDetector` / `FakeStockCardService` / `FakeReceiptService` / `Make` / `PipelineHarness` — Task 4 extends `Make` and `PipelineHarness`, so keep their members exactly as written here.

- [ ] **Step 1: Scaffold `Jobs.Receipts.Scan` and register it**

Create `src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Pohoda\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\..\Pohoda\Pohoda.MServer\Pohoda.MServer.csproj" />
    <ProjectReference Include="..\..\Pohoda\Pohoda.Server\Pohoda.Server.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Contract\Receipts.Contract.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Core\Receipts.Core.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Gabel\Receipts.Gabel.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

Create `src/Jobs/Jobs.Receipts.Scan/PohodaInstance.cs`:

```csharp
namespace Jobs.Receipts.Scan;

/// <summary>
/// Which POHODA mServer instance this application talks to. "GoNoow" is the Receipts instance
/// (port 444, its own warehouse); the existing Jobs use "StwTest" on port 5336 against the same
/// company. The key selects both the config section (<c>Pohoda:GoNoow:*</c>) and the keyed DI
/// registrations made by <c>AddPohodaMServer</c> / <c>AddPohodaAgendaServices</c>.
/// </summary>
internal static class PohodaInstance
{
    public const string Key = "GoNoow";
}
```

Register the project (from the repo root):

```bash
dotnet sln src/eCommerce.sln add --solution-folder Jobs src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
```

- [ ] **Step 2: Scaffold the shared test project and register it**

Create `src/Jobs/Jobs.Receipts.Tests/Jobs.Receipts.Tests.csproj`. It references both executables — `Jobs.Receipts.Apply` does not exist yet, so its reference is added in Task 4; do not add it now:

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
    <ProjectReference Include="..\..\Pohoda\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Contract\Receipts.Contract.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Core\Receipts.Core.csproj" />
    <ProjectReference Include="..\Jobs.Receipts.Scan\Jobs.Receipts.Scan.csproj" />
  </ItemGroup>

</Project>
```

```bash
dotnet sln src/eCommerce.sln add --solution-folder Jobs src/Jobs/Jobs.Receipts.Tests/Jobs.Receipts.Tests.csproj
```

- [ ] **Step 3: Write the worker**

Create `src/Jobs/Jobs.Receipts.Scan/Worker.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Scan;

/// <summary>
/// Sweeps the invoice inbox on a schedule. Each cycle starts mServer if it is not already running,
/// opens the local state store, runs one <see cref="ScanRunner"/> sweep and stops mServer again — but
/// only if this process was the one that started it, so a cycle never shuts an instance out from under
/// Pohoda E1 or another integration.
///
/// A failing cycle is logged and the loop continues: a scan is read-only against Pohoda and fully
/// repeatable, so a transient mServer or file-system fault must not take the service down.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly MServerLauncher _launcher;
    private readonly IReceiptService _receipts;
    private readonly IStockCardService _stockCards;
    private readonly IInvoiceParser _parser;
    private readonly IVariantDetector _variantDetector;
    private readonly WatchOptions _watch;
    private readonly MatchingOptions _matching;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(
        [FromKeyedServices(PohodaInstance.Key)] MServerLauncher launcher,
        [FromKeyedServices(PohodaInstance.Key)] IReceiptService receipts,
        [FromKeyedServices(PohodaInstance.Key)] IStockCardService stockCards,
        IInvoiceParser parser,
        IVariantDetector variantDetector,
        IOptions<WatchOptions> watch,
        IOptions<MatchingOptions> matching,
        ILoggerFactory loggerFactory,
        ILogger<Worker> logger)
    {
        _launcher = launcher;
        _receipts = receipts;
        _stockCards = stockCards;
        _parser = parser;
        _variantDetector = variantDetector;
        _watch = watch.Value;
        _matching = matching.Value;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cyklus skenovania zlyhal, skúsim znova o {Interval}.", _watch.ScanInterval);
            }

            try
            {
                await Task.Delay(_watch.ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var availability = await _launcher.EnsureRunningAsync(ct);
        if (!availability.IsAvailable)
        {
            _logger.LogError(
                "mServer '{Instance}' nie je dostupný a automatický štart zlyhal. Skontroluj, či beží (Pohoda E1 → Databáze → POHODA mServer) a či je v appsettings.json nastavená správna 'Pohoda:{Instance}:PohodaExePath'.",
                PohodaInstance.Key, PohodaInstance.Key);
            return;
        }

        try
        {
            await using var store = await ReceiptsStateStore.OpenAsync(ResolvePath(_watch.DatabasePath), ct);

            var builder = new ProposalBuilder(
                _stockCards, _variantDetector, store.StockMappings, _loggerFactory.CreateLogger<ProposalBuilder>());
            var runner = new ScanRunner(
                _receipts, _parser, builder, store, _watch, _matching, _loggerFactory.CreateLogger<ScanRunner>());

            await runner.RunOnceAsync(ct);
        }
        finally
        {
            if (availability.StartedByUs)
            {
                // Not the caller's cancellation token: the instance we started must be stopped even
                // when the sweep was cancelled by a shutdown.
                await _launcher.StopAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>Relative configured paths resolve against the application directory, not the current working directory.</summary>
    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
```

- [ ] **Step 4: Write the composition root**

Create `src/Jobs/Jobs.Receipts.Scan/Program.cs`:

```csharp
using Jobs.Receipts.Scan;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.MServer;
using Pohoda.Server.Services;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Gabel;

// The content root is the application directory rather than the current working directory, so the
// built .exe finds its appsettings.json wherever it is launched from (a scheduled task, a service
// wrapper, or `dotnet run`). Setting it here keeps the default configuration order intact — user
// secrets and environment variables still override appsettings.json.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddPohodaMServer(builder.Configuration, PohodaInstance.Key);
builder.Services.AddPohodaAgendaServices(PohodaInstance.Key);

builder.Services.Configure<WatchOptions>(builder.Configuration.GetSection(WatchOptions.SectionName));
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection(MatchingOptions.SectionName));

// The supplier plugin. A second supplier later means registering a different IInvoiceParser /
// IVariantDetector pair here — Receipts.Core never names a supplier.
builder.Services.AddSingleton<IInvoiceParser, GabelInvoiceParser>();
builder.Services.AddSingleton<IVariantDetector, GabelVariantDetector>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
```

- [ ] **Step 5: Write the test fakes and harness**

Every file below keeps its `using` directives above the file-scoped namespace — inside `namespace Jobs.Receipts.Tests.Fakes` the simple name `Receipts` would bind to `Jobs.Receipts`, so a using written inside the namespace would not resolve.

Create `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeInvoiceParser.cs`:

```csharp
using Receipts.Contract;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>
/// Stands in for a supplier's PDF parser: returns a pre-registered <see cref="ParsedInvoice"/> for a
/// known file name and "skips" everything else, so scan tests need no real PDF.
/// </summary>
internal sealed class FakeInvoiceParser : IInvoiceParser
{
    /// <summary>Keyed by file name. Any inbox file missing from this map is reported as skipped.</summary>
    public Dictionary<string, ParsedInvoice> Invoices { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string SkipReason { get; set; } = "PROFORMA INVOICE (pre-invoice) — mimo rozsahu automatizácie, ignorované.";

    /// <summary>Every path the runner handed to the parser, in order.</summary>
    public List<string> ParsedPaths { get; } = [];

    public bool TryParse(string pdfPath, out ParsedInvoice? invoice, out string? skipReason)
    {
        ParsedPaths.Add(pdfPath);

        if (Invoices.TryGetValue(Path.GetFileName(pdfPath), out var found))
        {
            invoice = found;
            skipReason = null;
            return true;
        }

        invoice = null;
        skipReason = SkipReason;
        return false;
    }
}
```

Create `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeVariantDetector.cs`:

```csharp
using Receipts.Contract;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>Deliberately simplistic, supplier-neutral rule — these tests are about the job, not about variant naming.</summary>
internal sealed class FakeVariantDetector : IVariantDetector
{
    public bool LooksLikeSizeVariant(string text) =>
        text.EndsWith(" cm", StringComparison.OrdinalIgnoreCase) || text.Contains("CM", StringComparison.Ordinal);
}
```

Create `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeStockCardService.cs`:

```csharp
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Jobs.Receipts.Tests.Fakes;

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

Create `src/Jobs/Jobs.Receipts.Tests/Fakes/FakeReceiptService.cs`:

```csharp
using Pohoda.Contract.Models;
using Pohoda.Contract.Services;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>In-memory Pohoda receipt agenda that records every call the jobs make.</summary>
internal sealed class FakeReceiptService : IReceiptService
{
    public List<ExistingReceipt> RecentReceipts { get; } = [];
    public string NextCreatedNumber { get; set; } = "26PR00013";

    public List<(DateOnly Since, int Limit)> ListCalls { get; } = [];
    public List<(string HeaderText, DateOnly Date, IReadOnlyList<ReceiptLineToWrite> Lines, decimal? Freight)> CreateCalls { get; } = [];
    public List<(string ExistingId, string HeaderText, IReadOnlyList<ReceiptLineToWrite> AllLines, decimal? Freight)> UpdateCalls { get; } = [];

    /// <summary>When set, every write throws this message — used to drive the apply job's failure path.</summary>
    public string? ThrowOnWrite { get; set; }

    public Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default)
    {
        ListCalls.Add((since, limit));
        return Task.FromResult<IReadOnlyList<ExistingReceipt>>(RecentReceipts.ToList());
    }

    public Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)
    {
        CreateCalls.Add((headerText, date, lines, freight));
        if (ThrowOnWrite is not null)
        {
            throw new InvalidOperationException(ThrowOnWrite);
        }

        return Task.FromResult(NextCreatedNumber);
    }

    public Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)
    {
        UpdateCalls.Add((existingId, headerText, allLines, freight));
        if (ThrowOnWrite is not null)
        {
            throw new InvalidOperationException(ThrowOnWrite);
        }

        return Task.CompletedTask;
    }
}
```

Create `src/Jobs/Jobs.Receipts.Tests/Fakes/Make.cs`:

```csharp
using Receipts.Contract;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>Small builders so job tests state only what matters.</summary>
internal static class Make
{
    public static InvoiceLine Line(
        string supplierCode,
        string description,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            LineAmount = Math.Round(quantity * unitPrice * (1 - discountPercent / 100m), 2)
        };
}
```

Create `src/Jobs/Jobs.Receipts.Tests/Fakes/PipelineHarness.cs`:

```csharp
using Jobs.Receipts.Scan;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Tests.Fakes;

/// <summary>
/// A throwaway pipeline: temp inbox / archive / pending folders, a temp-file SQLite store, fake
/// Pohoda agendas and a real <see cref="ProposalBuilder"/>. Disposing closes the store and deletes
/// the whole temp tree.
/// </summary>
internal sealed class PipelineHarness : IAsyncDisposable
{
    public string Root { get; }
    public WatchOptions Watch { get; }
    public MatchingOptions Matching { get; } = new();
    public ReceiptsStateStore Store { get; }

    public FakeReceiptService Receipts { get; } = new();
    public FakeStockCardService StockCards { get; } = new();
    public FakeInvoiceParser Parser { get; } = new();
    public ProposalBuilder Builder { get; }

    private PipelineHarness(string root, WatchOptions watch, ReceiptsStateStore store)
    {
        Root = root;
        Watch = watch;
        Store = store;
        Builder = new ProposalBuilder(StockCards, new FakeVariantDetector(), store.StockMappings, NullLogger<ProposalBuilder>.Instance);
    }

    public static async Task<PipelineHarness> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"receipts-jobs-test-{Guid.NewGuid():N}");
        var watch = new WatchOptions
        {
            InboxFolder = Path.Combine(root, "inbox"),
            ArchiveFolder = Path.Combine(root, "archive"),
            PendingApprovalFolder = Path.Combine(root, "pending"),
            DatabasePath = Path.Combine(root, "receipts.db")
        };

        Directory.CreateDirectory(watch.InboxFolder);
        var store = await ReceiptsStateStore.OpenAsync(watch.DatabasePath);
        return new PipelineHarness(root, watch, store);
    }

    public ScanRunner CreateScanRunner() =>
        new(Receipts, Parser, Builder, Store, Watch, Matching, NullLogger<ScanRunner>.Instance);

    /// <summary>Drops a placeholder file into the inbox (the parser is faked, so the bytes never matter) and returns its full path.</summary>
    public string AddInboxFile(string fileName)
    {
        var path = Path.Combine(Watch.InboxFolder, fileName);
        File.WriteAllText(path, "placeholder — the invoice parser is faked in these tests");
        return path;
    }

    /// <summary>Drops a file into the inbox and registers a parsed invoice for it.</summary>
    public ParsedInvoice AddParsableInvoice(
        string fileName,
        string invoiceNumber,
        string sha256,
        params InvoiceLine[] lines)
    {
        var path = AddInboxFile(fileName);
        var goodsNet = lines.Sum(l => l.LineAmount);
        var invoice = new ParsedInvoice
        {
            SupplierName = "TestDodávateľ",
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            Currency = "EUR",
            Lines = lines,
            FreightAmount = 0m,
            PrintedGoodsNetTotal = goodsNet,
            PrintedGrandTotal = goodsNet,
            SourceFilePath = path,
            SourceFileSha256 = sha256
        };

        Parser.Invoices[fileName] = invoice;
        return invoice;
    }

    public async ValueTask DisposeAsync()
    {
        await Store.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
```

- [ ] **Step 6: Write the failing tests**

Create `src/Jobs/Jobs.Receipts.Tests/ScanRunnerTests.cs`:

```csharp
using Jobs.Receipts.Scan;
using Jobs.Receipts.Tests.Fakes;
using Pohoda.Contract.Models;
using Receipts.Contract;

namespace Jobs.Receipts.Tests;

public class ScanRunnerTests
{
    [Fact]
    public async Task RunOnceAsync_writes_a_proposal_a_preview_file_and_marks_the_source_processed()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7009190321200", "DH PLATINUM SERIES", quantity: 2m, unitPrice: 94.5m));

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 1, Skipped: 0), result);

        var proposal = Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.Equal("FT 2/260220", proposal.InvoiceNumber);
        Assert.Equal(ReceiptProposalType.CreateReceipt, proposal.Type);
        Assert.True(await h.Store.ProcessedFiles.IsProcessedAsync("SHA-260220"));

        var previewPath = Path.Combine(h.Watch.PendingApprovalFolder, $"{proposal.Id}_ft2-260220.md");
        Assert.True(File.Exists(previewPath));
        var preview = await File.ReadAllTextAsync(previewPath);
        Assert.Contains("FT 2/260220", preview);
        Assert.Contains($"apply --id {proposal.Id}", preview);
    }

    [Fact]
    public async Task RunOnceAsync_never_writes_to_Pohoda()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7008352650000", "STRETCH LITE CARBON LILAC"));

        await h.CreateScanRunner().RunOnceAsync();

        Assert.Empty(h.StockCards.CreateCalls);
        Assert.Empty(h.Receipts.CreateCalls);
        Assert.Empty(h.Receipts.UpdateCalls);
    }

    [Fact]
    public async Task RunOnceAsync_skips_a_file_the_parser_rejects_without_creating_a_proposal()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddInboxFile("pr1-5605.pdf");

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 0, Skipped: 1), result);
        Assert.Empty(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.False(Directory.EnumerateFiles(h.Watch.PendingApprovalFolder).Any());
    }

    [Fact]
    public async Task RunOnceAsync_skips_a_file_whose_hash_was_already_processed()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7008352650000", "STRETCH LITE CARBON LILAC"));
        await h.Store.ProcessedFiles.MarkProcessedAsync("SHA-260220", "whatever.pdf", "an-earlier-proposal");

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 1, ProposalsCreated: 0, Skipped: 1), result);
        Assert.Empty(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
    }

    [Fact]
    public async Task RunOnceAsync_lists_recent_receipts_once_using_the_configured_window_and_limit()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.AddParsableInvoice("a.pdf", "FT 2/260001", "SHA-A", Make.Line("A", "A"));
        h.AddParsableInvoice("b.pdf", "FT 2/260002", "SHA-B", Make.Line("B", "B"));

        await h.CreateScanRunner().RunOnceAsync();

        var call = Assert.Single(h.Receipts.ListCalls);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddMonths(-h.Matching.LookbackMonths)), call.Since);
        Assert.Equal(h.Matching.ReceiptListLimit, call.Limit);
    }

    [Fact]
    public async Task RunOnceAsync_matches_an_existing_receipt_and_proposes_an_append()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.Receipts.RecentReceipts.Add(new ExistingReceipt("42", "26PR00012", "Gabel FT 2/260220",
            [new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m)]));
        h.AddParsableInvoice("ft2-260220.pdf", "FT 2/260220", "SHA-260220",
            Make.Line("7009190321200", "DH PLATINUM SERIES"));

        await h.CreateScanRunner().RunOnceAsync();

        var proposal = Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.Equal(ReceiptProposalType.AppendLines, proposal.Type);
        Assert.Equal("42", proposal.ExistingReceiptId);
    }

    [Fact]
    public async Task RunOnceAsync_creates_the_inbox_and_pending_folders_when_they_are_missing()
    {
        await using var h = await PipelineHarness.CreateAsync();
        Directory.Delete(h.Watch.InboxFolder, recursive: true);

        var result = await h.CreateScanRunner().RunOnceAsync();

        Assert.Equal(new ScanResult(FilesFound: 0, ProposalsCreated: 0, Skipped: 0), result);
        Assert.True(Directory.Exists(h.Watch.InboxFolder));
        Assert.True(Directory.Exists(h.Watch.PendingApprovalFolder));
    }
}
```

- [ ] **Step 7: Run the tests to verify they fail**

```bash
dotnet test src/Jobs/Jobs.Receipts.Tests
```

Expected: build error — `ScanRunner` and `ScanResult` do not exist.

- [ ] **Step 8: Implement `ScanRunner`**

Create `src/Jobs/Jobs.Receipts.Scan/ScanRunner.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Pohoda.Contract.Services;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Scan;

/// <summary>Outcome of one inbox sweep.</summary>
public readonly record struct ScanResult(int FilesFound, int ProposalsCreated, int Skipped);

/// <summary>
/// One sweep of the invoice inbox: parse each PDF, skip the ones that are not a final invoice or were
/// already processed, build a <see cref="ReceiptProposal"/> for the rest and write a Markdown preview
/// for a human to approve. Read-only against Pohoda — the only writes are to the local state store and
/// the preview folder; <c>Jobs.Receipts.Apply</c> is the only thing that writes to Pohoda.
///
/// Files are processed strictly sequentially: the <see cref="ReceiptsStateStore"/>'s SQLite connection
/// is not thread-safe, and proposals are cheap enough that a parallel sweep would buy nothing.
/// </summary>
public sealed class ScanRunner
{
    private readonly IReceiptService _receipts;
    private readonly IInvoiceParser _parser;
    private readonly ProposalBuilder _builder;
    private readonly ReceiptsStateStore _store;
    private readonly WatchOptions _watch;
    private readonly MatchingOptions _matching;
    private readonly ILogger<ScanRunner> _logger;

    public ScanRunner(
        IReceiptService receipts,
        IInvoiceParser parser,
        ProposalBuilder builder,
        ReceiptsStateStore store,
        WatchOptions watch,
        MatchingOptions matching,
        ILogger<ScanRunner> logger)
    {
        _receipts = receipts;
        _parser = parser;
        _builder = builder;
        _store = store;
        _watch = watch;
        _matching = matching;
        _logger = logger;
    }

    public async Task<ScanResult> RunOnceAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_watch.InboxFolder);
        Directory.CreateDirectory(_watch.PendingApprovalFolder);

        // Sorted so a sweep processes the inbox in a predictable order — Directory.EnumerateFiles
        // makes no ordering guarantee.
        var pdfFiles = Directory.EnumerateFiles(_watch.InboxFolder, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _logger.LogInformation("Nájdených {Count} PDF v priečinku {Folder}.", pdfFiles.Count, _watch.InboxFolder);

        var since = DateOnly.FromDateTime(DateTime.Today.AddMonths(-_matching.LookbackMonths));
        var recentReceipts = await _receipts.ListRecentAsync(since, _matching.ReceiptListLimit, ct);
        _logger.LogInformation("Načítaných {Count} príjemiek z Pohody od {Since:yyyy-MM-dd} na porovnanie.", recentReceipts.Count, since);

        var created = 0;
        var skipped = 0;

        foreach (var pdfPath in pdfFiles)
        {
            ct.ThrowIfCancellationRequested();

            if (!_parser.TryParse(pdfPath, out var invoice, out var skipReason))
            {
                _logger.LogInformation("Preskočené {File}: {Reason}", Path.GetFileName(pdfPath), skipReason);
                skipped++;
                continue;
            }

            if (await _store.ProcessedFiles.IsProcessedAsync(invoice!.SourceFileSha256, ct))
            {
                _logger.LogInformation("Preskočené {File}: už bolo spracované skôr.", Path.GetFileName(pdfPath));
                skipped++;
                continue;
            }

            var proposal = await _builder.BuildAsync(invoice, recentReceipts, ct);
            await _store.Proposals.SaveAsync(proposal, ct);
            await _store.ProcessedFiles.MarkProcessedAsync(invoice.SourceFileSha256, pdfPath, proposal.Id, ct);

            var previewPath = Path.Combine(
                _watch.PendingApprovalFolder,
                $"{proposal.Id}_{Path.GetFileNameWithoutExtension(pdfPath)}.md");
            await File.WriteAllTextAsync(previewPath, PreviewWriter.Render(proposal), ct);

            _logger.LogInformation(
                "{File} → proposal {Id} ({Type}), {LineCount} riadkov na pridanie, {WarningCount} upozornení. Náhľad: {Preview}",
                Path.GetFileName(pdfPath), proposal.Id, proposal.Type, proposal.LinesToAdd.Count, proposal.Warnings.Count, previewPath);

            created++;
        }

        _logger.LogInformation(
            "Scan hotový. Vytvorených {Count} nových proposalov na schválenie v {Folder}.", created, _watch.PendingApprovalFolder);

        return new ScanResult(pdfFiles.Count, created, skipped);
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

```bash
dotnet test src/Jobs/Jobs.Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 7`.

- [ ] **Step 10: Commit**

```bash
git add src/eCommerce.sln src/Jobs/Jobs.Receipts.Scan src/Jobs/Jobs.Receipts.Tests
git commit -m "feat: add Jobs.Receipts.Scan project with ScanRunner and its test project"
```

---

## Task 3: `Jobs.Receipts.Scan` configuration and start-up verification

**Files:**
- Create: `src/Jobs/Jobs.Receipts.Scan/appsettings.json`
- Create: `src/Jobs/Jobs.Receipts.Scan/Properties/launchSettings.json`
- Modify: `src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj` (the `UserSecretsId` written by `dotnet user-secrets init`)

**Interfaces:**
- Consumes: `Worker`, `Program.cs`, `ScanRunner`, `PohodaInstance.Key` (all Task 2); `WatchOptions` (Task 1); `Pohoda.MServer.MServerLauncher.EnsureRunningAsync(ct) : Task<MServerAvailability>` with `MServerAvailability(bool IsAvailable, bool StartedByUs)` and `StopAsync(ct)`; `Pohoda.MServer.ServiceCollectionExtensions.AddPohodaMServer(configuration, key)`; `Pohoda.Server.Services.ServiceCollectionExtensions.AddPohodaAgendaServices(key)` (Task 1); `Receipts.Gabel.GabelInvoiceParser` / `GabelVariantDetector` (parameterless, implementing `IInvoiceParser` / `IVariantDetector`).
- Produces: a runnable worker. Nothing later in this plan consumes its types.

- [ ] **Step 1: Write the configuration**

Create `src/Jobs/Jobs.Receipts.Scan/appsettings.json`. `Username` and `Password` stay empty in the repository — real values come from User Secrets locally and from `Pohoda__GoNoow__Username` / `Pohoda__GoNoow__Password` environment variables in production:

```json
{
  "Pohoda": {
    "GoNoow": {
      "BaseUrl": "http://127.0.0.1",
      "Port": 444,
      "ConfigName": "GoNoow",
      "PohodaExePath": "C:\\Program Files (x86)\\STORMWARE\\POHODA SK E1\\Pohoda.exe",
      "Username": "",
      "Password": "",
      "Ico": "53870441",
      "DefaultStorageCode": "GoNoow",
      "DefaultTypePriceCode": "GABEL"
    }
  },
  "Watch": {
    "InboxFolder": "C:\\tmp\\faktury",
    "ArchiveFolder": "C:\\tmp\\faktury\\spracovane",
    "PendingApprovalFolder": "C:\\tmp\\faktury\\na_schvalenie",
    "DatabasePath": "data\\receipts.db",
    "ScanInterval": "00:15:00"
  },
  "Matching": {
    "LookbackMonths": 12,
    "ReceiptListLimit": 500
  }
}
```

Create `src/Jobs/Jobs.Receipts.Scan/Properties/launchSettings.json`, matching the convention the README documents (User Secrets load only in the Development environment):

```json
{
  "profiles": {
    "Jobs.Receipts.Scan": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Initialize the User Secrets id (this writes a `<UserSecretsId>` element into the csproj):

```bash
dotnet user-secrets init --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
```

- [ ] **Step 2: Verify the application composes**

The host builds its whole object graph at startup, so a missing or mistyped registration fails immediately. Confirm the build and a real start-up, then stop it:

```bash
dotnet build src/eCommerce.sln
```

Expected: 0 errors.

```bash
dotnet run --project src/Jobs/Jobs.Receipts.Scan --no-launch-profile
```

Expected: it starts, logs `Nájdených 0 PDF v priečinku ...` if mServer happens to be reachable, or logs the `mServer 'GoNoow' nie je dostupný ...` error and keeps waiting for the next interval if it is not. Either outcome proves the object graph resolved. It must NOT throw an `InvalidOperationException` about an unresolvable service. Stop it with Ctrl+C.

If mServer is not available on this machine, the error path is the expected result — note which of the two you saw in your report.

- [ ] **Step 3: Commit**

```bash
git add src/Jobs/Jobs.Receipts.Scan
git commit -m "feat: add Jobs.Receipts.Scan configuration and verify start-up"
```

---

## Task 4: `Jobs.Receipts.Apply` project + `ApplyRunner`

**Files:**
- Create: `src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj`
- Create: `src/Jobs/Jobs.Receipts.Apply/PohodaInstance.cs`
- Create: `src/Jobs/Jobs.Receipts.Apply/ApplyRunner.cs`
- Modify: `src/Jobs/Jobs.Receipts.Tests/Jobs.Receipts.Tests.csproj` (add the reference to the new project)
- Modify: `src/Jobs/Jobs.Receipts.Tests/Fakes/Make.cs` (add the proposal builders)
- Modify: `src/Jobs/Jobs.Receipts.Tests/Fakes/PipelineHarness.cs` (add `CreateApplyRunner` and `AddPendingProposalAsync`)
- Test: `src/Jobs/Jobs.Receipts.Tests/ApplyRunnerTests.cs`

**Interfaces:**
- Consumes: `Receipts.Core.ProposalApplier(IStockCardService, IReceiptService, ReceiptsStateStore, IOptions<MatchingOptions>, ILogger<ProposalApplier>)` with `ApplyAsync(ReceiptProposal, CancellationToken)`; `Receipts.Core.WatchOptions` (Task 1); `ReceiptsStateStore.Proposals.GetAsync/GetByStatusAsync/SaveAsync`, `.Audit.AppendAsync`, `.ResetProposalAsync`; `Receipts.Contract.ReceiptProposal` / `ReceiptLineProposal` / `ReceiptProposalType` / `ProposalStatus`; the fakes and `PipelineHarness` from Task 2.
- Produces (used by Task 5):
  - `Jobs.Receipts.Apply.PohodaInstance.Key` — `internal const string`, value `"GoNoow"`.
  - `Jobs.Receipts.Apply.ApplyRunner(ProposalApplier applier, ReceiptsStateStore store, WatchOptions watch, ILogger<ApplyRunner> logger)` with `Task<int> ApplyAsync(string? id, bool all, CancellationToken ct = default)`, `Task<int> StatusAsync(CancellationToken ct = default)`, `Task<int> ResetAsync(string id, CancellationToken ct = default)` — each returning a process exit code, `0` for success.

- [ ] **Step 1: Scaffold `Jobs.Receipts.Apply` and register it**

Create `src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Pohoda\Pohoda.Contract\Pohoda.Contract.csproj" />
    <ProjectReference Include="..\..\Pohoda\Pohoda.MServer\Pohoda.MServer.csproj" />
    <ProjectReference Include="..\..\Pohoda\Pohoda.Server\Pohoda.Server.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Contract\Receipts.Contract.csproj" />
    <ProjectReference Include="..\..\Receipts\Receipts.Core\Receipts.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

`Receipts.Gabel` is deliberately absent: `apply` replays a stored proposal and never parses a PDF, so it needs no supplier plugin.

`<OutputType>Exe</OutputType>` is deliberately absent too, so the project builds as a library for now. Its entry point (`Program.cs`) references `CommandLine` and `ApplyRunner` and therefore cannot be written before both exist; declaring the project an executable here would fail the build with `CS5001` (no `Main`) and block this task's tests. Task 5 adds the element together with `Program.cs`.

Create `src/Jobs/Jobs.Receipts.Apply/PohodaInstance.cs`:

```csharp
namespace Jobs.Receipts.Apply;

/// <summary>
/// Which POHODA mServer instance this application writes to. "GoNoow" is the Receipts instance
/// (port 444, its own warehouse); the existing Jobs use "StwTest" on port 5336 against the same
/// company. The key selects both the config section (<c>Pohoda:GoNoow:*</c>) and the keyed DI
/// registrations made by <c>AddPohodaMServer</c> / <c>AddPohodaAgendaServices</c>.
/// </summary>
internal static class PohodaInstance
{
    public const string Key = "GoNoow";
}
```

Register the project and wire it into the test project (from the repo root):

```bash
dotnet sln src/eCommerce.sln add --solution-folder Jobs src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj
```

Edit `src/Jobs/Jobs.Receipts.Tests/Jobs.Receipts.Tests.csproj` — add one line to the existing `ProjectReference` group, keeping it alphabetical:

```xml
    <ProjectReference Include="..\Jobs.Receipts.Apply\Jobs.Receipts.Apply.csproj" />
```

- [ ] **Step 2: Extend the test builders and harness**

Append to `src/Jobs/Jobs.Receipts.Tests/Fakes/Make.cs`, inside the existing `Make` class (the file's `using Receipts.Contract;` already covers these types):

```csharp
    public static ReceiptLineProposal LineProposal(
        string supplierCode,
        string description,
        bool isNewStockItem = false,
        decimal quantity = 1m,
        decimal unitPrice = 10m,
        decimal discountPercent = 35m) =>
        new()
        {
            SupplierCode = supplierCode,
            Description = description,
            Unit = "PA",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            PohodaStockCode = supplierCode,
            IsNewStockItem = isNewStockItem
        };

    public static ReceiptProposal Proposal(
        string sourceFilePath,
        string id = "proposal-1",
        string invoiceNumber = "FT 2/260220",
        IReadOnlyList<ReceiptLineProposal>? lines = null,
        ReceiptProposalType type = ReceiptProposalType.CreateReceipt,
        string? existingReceiptId = null,
        string? existingReceiptNumber = null,
        decimal? freightToAdd = null) =>
        new()
        {
            Id = id,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = new DateOnly(2026, 6, 23),
            SourceFilePath = sourceFilePath,
            SourceFileSha256 = $"SHA-{id}",
            Type = type,
            ExistingReceiptId = existingReceiptId,
            ExistingReceiptNumber = existingReceiptNumber,
            HeaderText = type == ReceiptProposalType.CreateReceipt ? $"TestDodávateľ {invoiceNumber}" : null,
            LinesToAdd = lines ?? [LineProposal("7009190321200", "DH PLATINUM SERIES")],
            FreightToAdd = freightToAdd,
            Warnings = [],
            CreatedAt = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero)
        };
```

Append to `src/Jobs/Jobs.Receipts.Tests/Fakes/PipelineHarness.cs`, inside the existing `PipelineHarness` class, and add `using Jobs.Receipts.Apply;`, `using Microsoft.Extensions.Options;` to the file's using block at the top:

```csharp
    public ApplyRunner CreateApplyRunner() =>
        new(
            new ProposalApplier(StockCards, Receipts, Store, Options.Create(Matching), NullLogger<ProposalApplier>.Instance),
            Store,
            Watch,
            NullLogger<ApplyRunner>.Instance);

    /// <summary>Drops a placeholder PDF into the inbox and saves a proposal pointing at it.</summary>
    public async Task<ReceiptProposal> AddPendingProposalAsync(
        string fileName = "ft2-260220.pdf",
        string id = "proposal-1",
        IReadOnlyList<ReceiptLineProposal>? lines = null,
        ReceiptProposalType type = ReceiptProposalType.CreateReceipt,
        string? existingReceiptId = null,
        string? existingReceiptNumber = null)
    {
        var path = AddInboxFile(fileName);
        var proposal = Make.Proposal(path, id, lines: lines, type: type,
            existingReceiptId: existingReceiptId, existingReceiptNumber: existingReceiptNumber);
        await Store.Proposals.SaveAsync(proposal);
        return proposal;
    }
```

- [ ] **Step 3: Write the failing tests**

Create `src/Jobs/Jobs.Receipts.Tests/ApplyRunnerTests.cs`:

```csharp
using Jobs.Receipts.Tests.Fakes;
using Pohoda.Contract.Models;
using Receipts.Contract;

namespace Jobs.Receipts.Tests;

public class ApplyRunnerTests
{
    [Fact]
    public async Task ApplyAsync_by_id_writes_the_receipt_archives_the_pdf_and_marks_the_proposal_applied()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var proposal = await h.AddPendingProposalAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(0, exitCode);
        var call = Assert.Single(h.Receipts.CreateCalls);
        Assert.Equal("TestDodávateľ FT 2/260220", call.HeaderText);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Equal("26PR00013", stored.ResultReceiptNumber);

        Assert.False(File.Exists(proposal.SourceFilePath));
        Assert.True(File.Exists(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf")));
    }

    [Fact]
    public async Task ApplyAsync_by_id_returns_one_for_an_unknown_proposal_and_writes_nothing()
    {
        await using var h = await PipelineHarness.CreateAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync("missing", all: false);

        Assert.Equal(1, exitCode);
        Assert.Empty(h.Receipts.CreateCalls);
    }

    [Fact]
    public async Task ApplyAsync_by_id_refuses_a_proposal_that_was_already_applied()
    {
        await using var h = await PipelineHarness.CreateAsync();
        var proposal = await h.AddPendingProposalAsync();
        proposal.Status = ProposalStatus.Applied;
        proposal.ResultReceiptNumber = "26PR00010";
        await h.Store.Proposals.SaveAsync(proposal);

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(1, exitCode);
        Assert.Empty(h.Receipts.CreateCalls);
    }

    [Fact]
    public async Task ApplyAsync_by_id_retries_a_failed_proposal()
    {
        await using var h = await PipelineHarness.CreateAsync();
        var proposal = await h.AddPendingProposalAsync();
        proposal.Status = ProposalStatus.Failed;
        proposal.FailureReason = "predchádzajúci pokus zlyhal";
        await h.Store.Proposals.SaveAsync(proposal);

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(0, exitCode);
        Assert.Single(h.Receipts.CreateCalls);
        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Applied, stored!.Status);
        Assert.Null(stored.FailureReason);
    }

    [Fact]
    public async Task ApplyAsync_all_applies_every_pending_proposal_and_skips_failed_ones()
    {
        await using var h = await PipelineHarness.CreateAsync();
        await h.AddPendingProposalAsync("a.pdf", id: "p-a");
        await h.AddPendingProposalAsync("b.pdf", id: "p-b");
        var failed = await h.AddPendingProposalAsync("c.pdf", id: "p-c");
        failed.Status = ProposalStatus.Failed;
        failed.FailureReason = "staršia chyba";
        await h.Store.Proposals.SaveAsync(failed);

        var exitCode = await h.CreateApplyRunner().ApplyAsync(id: null, all: true);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, h.Receipts.CreateCalls.Count);
        Assert.Equal(ProposalStatus.Failed, (await h.Store.Proposals.GetAsync("p-c"))!.Status);
    }

    [Fact]
    public async Task ApplyAsync_all_returns_zero_when_nothing_is_pending()
    {
        await using var h = await PipelineHarness.CreateAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync(id: null, all: true);

        Assert.Equal(0, exitCode);
        Assert.Empty(h.Receipts.CreateCalls);
    }

    [Fact]
    public async Task ApplyAsync_records_a_failed_write_and_leaves_the_pdf_in_the_inbox()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        h.Receipts.ThrowOnWrite = "mServer vrátil chybový stav.";
        var proposal = await h.AddPendingProposalAsync();

        var exitCode = await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        Assert.Equal(1, exitCode);

        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.Failed, stored!.Status);
        Assert.Contains("mServer vrátil chybový stav.", stored.FailureReason);
        Assert.Null(stored.ResultReceiptNumber);

        var audit = await h.Store.Audit.ReadAllAsync();
        Assert.Contains(audit, e => e.Message.StartsWith("CHYBA pri zápise:") && e.Source == proposal.SourceFilePath);

        Assert.True(File.Exists(proposal.SourceFilePath));
        Assert.False(Directory.Exists(h.Watch.ArchiveFolder) && File.Exists(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf")));
    }

    [Fact]
    public async Task ApplyAsync_all_keeps_going_after_one_proposal_fails()
    {
        await using var h = await PipelineHarness.CreateAsync();
        await h.AddPendingProposalAsync("a.pdf", id: "p-a");
        await h.AddPendingProposalAsync("b.pdf", id: "p-b");
        h.Receipts.ThrowOnWrite = "mServer vrátil chybový stav.";

        var exitCode = await h.CreateApplyRunner().ApplyAsync(id: null, all: true);

        Assert.Equal(1, exitCode);
        Assert.Equal(2, h.Receipts.CreateCalls.Count);
        Assert.Equal(ProposalStatus.Failed, (await h.Store.Proposals.GetAsync("p-a"))!.Status);
        Assert.Equal(ProposalStatus.Failed, (await h.Store.Proposals.GetAsync("p-b"))!.Status);
    }

    [Fact]
    public async Task StatusAsync_returns_zero_and_reads_both_pending_and_failed_proposals()
    {
        await using var h = await PipelineHarness.CreateAsync();
        await h.AddPendingProposalAsync("a.pdf", id: "p-a");
        var failed = await h.AddPendingProposalAsync("b.pdf", id: "p-b");
        failed.Status = ProposalStatus.Failed;
        failed.FailureReason = "chyba";
        await h.Store.Proposals.SaveAsync(failed);

        Assert.Equal(0, await h.CreateApplyRunner().StatusAsync());

        Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval));
        Assert.Single(await h.Store.Proposals.GetByStatusAsync(ProposalStatus.Failed));
    }

    [Fact]
    public async Task ResetAsync_returns_the_proposal_to_pending_and_moves_the_pdf_back_from_the_archive()
    {
        await using var h = await PipelineHarness.CreateAsync();
        h.StockCards.ExistingCodes.Add("7009190321200");
        var proposal = await h.AddPendingProposalAsync();
        await h.Store.ProcessedFiles.MarkProcessedAsync(proposal.SourceFileSha256, proposal.SourceFilePath, proposal.Id);
        await h.CreateApplyRunner().ApplyAsync(proposal.Id, all: false);

        var exitCode = await h.CreateApplyRunner().ResetAsync(proposal.Id);

        Assert.Equal(0, exitCode);
        var stored = await h.Store.Proposals.GetAsync(proposal.Id);
        Assert.Equal(ProposalStatus.PendingApproval, stored!.Status);
        Assert.Null(stored.ResultReceiptNumber);
        Assert.False(await h.Store.ProcessedFiles.IsProcessedAsync(proposal.SourceFileSha256));
        Assert.True(File.Exists(proposal.SourceFilePath));
        Assert.False(File.Exists(Path.Combine(h.Watch.ArchiveFolder, "ft2-260220.pdf")));
    }

    [Fact]
    public async Task ResetAsync_returns_one_for_an_unknown_proposal()
    {
        await using var h = await PipelineHarness.CreateAsync();

        Assert.Equal(1, await h.CreateApplyRunner().ResetAsync("missing"));
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
dotnet test src/Jobs/Jobs.Receipts.Tests
```

Expected: build error — `ApplyRunner` does not exist.

- [ ] **Step 5: Implement `ApplyRunner`**

Create `src/Jobs/Jobs.Receipts.Apply/ApplyRunner.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Receipts.Contract;
using Receipts.Core;
using Receipts.Core.Persistence;

namespace Jobs.Receipts.Apply;

/// <summary>
/// The bodies of the `apply`, `status` and `reset` commands. Every method returns a process exit code
/// (0 = success). This is the only part of the receipt-sync pipeline that is allowed to trigger a
/// write to Pohoda, and it only ever runs because a human invoked it for a proposal they reviewed.
///
/// <see cref="ProposalApplier"/> promotes a proposal to Applied on success but leaves the failure
/// transition to its caller — that transition lives here: a proposal whose write throws is stored as
/// <see cref="ProposalStatus.Failed"/> with the exception message, an audit row is appended, and the
/// loop continues with the next proposal so one bad invoice cannot block a batch.
/// </summary>
public sealed class ApplyRunner
{
    private readonly ProposalApplier _applier;
    private readonly ReceiptsStateStore _store;
    private readonly WatchOptions _watch;
    private readonly ILogger<ApplyRunner> _logger;

    public ApplyRunner(
        ProposalApplier applier,
        ReceiptsStateStore store,
        WatchOptions watch,
        ILogger<ApplyRunner> logger)
    {
        _applier = applier;
        _store = store;
        _watch = watch;
        _logger = logger;
    }

    public async Task<int> ApplyAsync(string? id, bool all, CancellationToken ct = default)
    {
        var toApply = new List<ReceiptProposal>();

        if (all)
        {
            toApply.AddRange(await _store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval, ct));
        }
        else
        {
            var proposal = await _store.Proposals.GetAsync(id!, ct);
            if (proposal is null)
            {
                _logger.LogError("Proposal {Id} sa nenašiel.", id);
                return 1;
            }

            // Retrying a Failed proposal by explicit --id is allowed: a Failed proposal never reached
            // ProposalStatus.Applied, so no write was recorded as complete for it — safe to retry after
            // fixing whatever caused the failure. `apply --all` intentionally does NOT auto-retry Failed
            // proposals, to avoid silently re-attempting known-bad ones in bulk.
            if (proposal.Status is not (ProposalStatus.PendingApproval or ProposalStatus.Failed))
            {
                _logger.LogError("Proposal {Id} má stav {Status}, nie je čakajúci na schválenie ani zlyhaný.", id, proposal.Status);
                return 1;
            }

            toApply.Add(proposal);
        }

        if (toApply.Count == 0)
        {
            _logger.LogInformation("Žiadne proposaly na spracovanie.");
            return 0;
        }

        var failures = 0;

        foreach (var proposal in toApply)
        {
            try
            {
                // Retrying a previously Failed proposal must not leave its stale reason behind once
                // the write succeeds. Clearing it before the call is enough: ProposalApplier saves the
                // proposal after a successful write, and that upsert persists every outcome column.
                proposal.FailureReason = null;

                await _applier.ApplyAsync(proposal, ct);

                Directory.CreateDirectory(_watch.ArchiveFolder);
                var archivePath = Path.Combine(_watch.ArchiveFolder, Path.GetFileName(proposal.SourceFilePath));
                if (File.Exists(proposal.SourceFilePath))
                {
                    File.Move(proposal.SourceFilePath, archivePath, overwrite: true);
                }

                _logger.LogInformation(
                    "Proposal {Id} úspešne zapísaný → príjemka {Number}. PDF presunuté do {Archive}.",
                    proposal.Id, proposal.ResultReceiptNumber, archivePath);
            }
            catch (Exception ex)
            {
                failures++;
                proposal.Status = ProposalStatus.Failed;
                proposal.FailureReason = ex.Message;

                // Deliberately not the caller's token: the failure must be recorded even when the run
                // was cancelled, otherwise a half-finished write leaves no trace at all.
                await _store.Proposals.SaveAsync(proposal, CancellationToken.None);
                await _store.Audit.AppendAsync(proposal.SourceFilePath, $"CHYBA pri zápise: {ex.Message}", CancellationToken.None);

                _logger.LogError(ex, "Proposal {Id} zlyhal.", proposal.Id);
            }
        }

        return failures == 0 ? 0 : 1;
    }

    public async Task<int> StatusAsync(CancellationToken ct = default)
    {
        var pending = await _store.Proposals.GetByStatusAsync(ProposalStatus.PendingApproval, ct);
        var failed = await _store.Proposals.GetByStatusAsync(ProposalStatus.Failed, ct);

        _logger.LogInformation("Čakajúce na schválenie ({Count}):", pending.Count);
        foreach (var proposal in pending)
        {
            _logger.LogInformation("  {Id} — {InvoiceNumber} ({Type}), {LineCount} riadkov, {File}",
                proposal.Id, proposal.InvoiceNumber, proposal.Type, proposal.LinesToAdd.Count, Path.GetFileName(proposal.SourceFilePath));
        }

        _logger.LogInformation("Zlyhané ({Count}):", failed.Count);
        foreach (var proposal in failed)
        {
            _logger.LogInformation("  {Id} — {InvoiceNumber}: {Reason}", proposal.Id, proposal.InvoiceNumber, proposal.FailureReason);
        }

        return 0;
    }

    public async Task<int> ResetAsync(string id, CancellationToken ct = default)
    {
        var proposal = await _store.Proposals.GetAsync(id, ct);
        if (proposal is null)
        {
            _logger.LogError("Proposal {Id} sa nenašiel.", id);
            return 1;
        }

        await _store.ResetProposalAsync(id, ct);

        // If the proposal's PDF was already moved to the archive folder (by a previous `apply`), move
        // it back so `scan` / `apply --id` can act on it again.
        var archivedPath = Path.Combine(_watch.ArchiveFolder, Path.GetFileName(proposal.SourceFilePath));
        if (!File.Exists(proposal.SourceFilePath) && File.Exists(archivedPath))
        {
            File.Move(archivedPath, proposal.SourceFilePath, overwrite: false);
            _logger.LogInformation("PDF presunuté späť z archívu: {Path}", proposal.SourceFilePath);
        }

        _logger.LogInformation("Proposal {Id} bol resetovaný na PendingApproval.", id);
        return 0;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test src/Jobs/Jobs.Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 18` (7 from Task 2 + 11 new).

- [ ] **Step 7: Commit**

```bash
git add src/eCommerce.sln src/Jobs/Jobs.Receipts.Apply src/Jobs/Jobs.Receipts.Tests
git commit -m "feat: add Jobs.Receipts.Apply project with ApplyRunner"
```

---

## Task 5: `Jobs.Receipts.Apply` command line, composition root and configuration

**Files:**
- Create: `src/Jobs/Jobs.Receipts.Apply/CommandLine.cs`
- Create: `src/Jobs/Jobs.Receipts.Apply/Program.cs`
- Create: `src/Jobs/Jobs.Receipts.Apply/appsettings.json`
- Create: `src/Jobs/Jobs.Receipts.Apply/Properties/launchSettings.json`
- Modify: `src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj` (adds `<OutputType>Exe</OutputType>`, plus the `UserSecretsId` written by `dotnet user-secrets init`)
- Test: `src/Jobs/Jobs.Receipts.Tests/CommandLineTests.cs`

**Interfaces:**
- Consumes: `ApplyRunner`, `PohodaInstance.Key` (Task 4); `WatchOptions` (Task 1); `Pohoda.MServer.MServerLauncher`, `PohodaResponseException`; `AddPohodaMServer` / `AddPohodaAgendaServices`.
- Produces:
  - `Jobs.Receipts.Apply.ReceiptsCommandKind { Usage, Apply, Status, Reset }`.
  - `Jobs.Receipts.Apply.ReceiptsCommand(ReceiptsCommandKind Kind, string? Id = null, bool All = false, string? Error = null)` — public readonly record struct.
  - `Jobs.Receipts.Apply.CommandLine.Parse(string[] args) : ReceiptsCommand` and `CommandLine.Usage : string`.

- [ ] **Step 1: Write the failing tests**

Create `src/Jobs/Jobs.Receipts.Tests/CommandLineTests.cs`:

```csharp
using Jobs.Receipts.Apply;

namespace Jobs.Receipts.Tests;

public class CommandLineTests
{
    [Fact]
    public void Parse_returns_Usage_for_no_arguments()
    {
        var command = CommandLine.Parse([]);

        Assert.Equal(ReceiptsCommandKind.Usage, command.Kind);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_returns_Usage_for_an_unknown_command()
    {
        Assert.Equal(ReceiptsCommandKind.Usage, CommandLine.Parse(["scan"]).Kind);
    }

    [Fact]
    public void Parse_reads_apply_with_an_id()
    {
        var command = CommandLine.Parse(["apply", "--id", "abc123"]);

        Assert.Equal(ReceiptsCommandKind.Apply, command.Kind);
        Assert.Equal("abc123", command.Id);
        Assert.False(command.All);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_reads_apply_with_all()
    {
        var command = CommandLine.Parse(["apply", "--all"]);

        Assert.Equal(ReceiptsCommandKind.Apply, command.Kind);
        Assert.True(command.All);
        Assert.Null(command.Id);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_prefers_all_when_both_all_and_id_are_given()
    {
        var command = CommandLine.Parse(["apply", "--all", "--id", "abc123"]);

        Assert.True(command.All);
        Assert.Null(command.Id);
    }

    [Fact]
    public void Parse_reports_an_error_when_apply_has_neither_id_nor_all()
    {
        var command = CommandLine.Parse(["apply"]);

        Assert.Equal(ReceiptsCommandKind.Apply, command.Kind);
        Assert.Equal("Použi 'apply --id <id>' alebo 'apply --all'.", command.Error);
    }

    [Theory]
    [InlineData("apply", "Použi 'apply --id <id>' alebo 'apply --all'.")]
    [InlineData("reset", "Použi 'reset --id <id>'.")]
    public void Parse_reports_an_error_when_id_has_no_value(string verb, string expectedError)
    {
        var command = CommandLine.Parse([verb, "--id"]);

        Assert.Equal(expectedError, command.Error);
        Assert.Null(command.Id);
    }

    [Fact]
    public void Parse_reads_status()
    {
        var command = CommandLine.Parse(["status"]);

        Assert.Equal(ReceiptsCommandKind.Status, command.Kind);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_reads_reset_with_an_id()
    {
        var command = CommandLine.Parse(["reset", "--id", "abc123"]);

        Assert.Equal(ReceiptsCommandKind.Reset, command.Kind);
        Assert.Equal("abc123", command.Id);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_reports_an_error_when_reset_has_no_id_at_all()
    {
        var command = CommandLine.Parse(["reset"]);

        Assert.Equal(ReceiptsCommandKind.Reset, command.Kind);
        Assert.Equal("Použi 'reset --id <id>'.", command.Error);
    }

    [Fact]
    public void Usage_lists_every_supported_command()
    {
        Assert.Contains("apply --id <id>", CommandLine.Usage);
        Assert.Contains("apply --all", CommandLine.Usage);
        Assert.Contains("status", CommandLine.Usage);
        Assert.Contains("reset --id <id>", CommandLine.Usage);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test src/Jobs/Jobs.Receipts.Tests
```

Expected: build error — `CommandLine`, `ReceiptsCommand` and `ReceiptsCommandKind` do not exist.

- [ ] **Step 3: Implement the command-line parser**

Create `src/Jobs/Jobs.Receipts.Apply/CommandLine.cs`:

```csharp
namespace Jobs.Receipts.Apply;

/// <summary>Which command the operator asked for. <see cref="Usage"/> means "print the help and stop".</summary>
public enum ReceiptsCommandKind
{
    Usage,
    Apply,
    Status,
    Reset
}

/// <summary>
/// A parsed command line. <see cref="Error"/> is a ready-to-log Slovak message set when the command
/// was recognized but its arguments were unusable — the caller logs it and exits with code 1.
/// </summary>
public readonly record struct ReceiptsCommand(
    ReceiptsCommandKind Kind,
    string? Id = null,
    bool All = false,
    string? Error = null);

/// <summary>
/// Parses this application's positional sub-commands. Deliberately hand-written and dependency-free:
/// the host's command-line configuration provider cannot be used here, because it rejects a bare
/// positional argument like "apply".
/// </summary>
public static class CommandLine
{
    public const string Usage = """
        Jobs.Receipts.Apply

        Príkazy:
          apply --id <id>      Zapíše konkrétny schválený proposal do Pohody.
          apply --all          Zapíše všetky proposaly čakajúce na schválenie.
          status               Vypíše čakajúce a zlyhané proposaly.
          reset --id <id>      Vráti proposal na PendingApproval a presunie PDF späť z archívu (na opakovaný pokus po oprave chyby).
        """;

    public static ReceiptsCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ReceiptsCommand(ReceiptsCommandKind.Usage);
        }

        return args[0] switch
        {
            "apply" => ParseApply(args),
            "status" => new ReceiptsCommand(ReceiptsCommandKind.Status),
            "reset" => ParseReset(args),
            _ => new ReceiptsCommand(ReceiptsCommandKind.Usage)
        };
    }

    private static ReceiptsCommand ParseApply(string[] args)
    {
        if (args.Contains("--all"))
        {
            return new ReceiptsCommand(ReceiptsCommandKind.Apply, All: true);
        }

        var id = ReadId(args);
        return id is null
            ? new ReceiptsCommand(ReceiptsCommandKind.Apply, Error: "Použi 'apply --id <id>' alebo 'apply --all'.")
            : new ReceiptsCommand(ReceiptsCommandKind.Apply, Id: id);
    }

    private static ReceiptsCommand ParseReset(string[] args)
    {
        var id = ReadId(args);
        return id is null
            ? new ReceiptsCommand(ReceiptsCommandKind.Reset, Error: "Použi 'reset --id <id>'.")
            : new ReceiptsCommand(ReceiptsCommandKind.Reset, Id: id);
    }

    private static string? ReadId(string[] args)
    {
        var index = Array.IndexOf(args, "--id");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test src/Jobs/Jobs.Receipts.Tests
```

Expected: `Passed! - Failed: 0, Passed: 30` (18 from Tasks 2 and 4 + 12 new: 11 facts plus a 2-case theory counted as 2, i.e. 10 facts + 2 theory cases).

- [ ] **Step 5: Write the composition root**

First turn the project into an executable — edit `src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj` and add one line as the first element of the existing `PropertyGroup`, so it reads:

```xml
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
```

Then create `src/Jobs/Jobs.Receipts.Apply/Program.cs`:

```csharp
using Jobs.Receipts.Apply;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Pohoda.Server.Services;
using Receipts.Core;
using Receipts.Core.Persistence;

var command = CommandLine.Parse(args);
if (command.Kind == ReceiptsCommandKind.Usage)
{
    Console.WriteLine(CommandLine.Usage);
    return 1;
}

// The host is built WITHOUT `args`: the command-line configuration provider throws on a bare
// positional sub-command such as "apply". The command line is parsed above instead. The content root
// is the application directory so the built .exe finds its appsettings.json wherever it is launched
// from, while leaving user secrets and environment variables free to override it.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddPohodaMServer(builder.Configuration, PohodaInstance.Key);
builder.Services.AddPohodaAgendaServices(PohodaInstance.Key);
builder.Services.Configure<WatchOptions>(builder.Configuration.GetSection(WatchOptions.SectionName));
builder.Services.Configure<MatchingOptions>(builder.Configuration.GetSection(MatchingOptions.SectionName));

using var host = builder.Build();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var log = loggerFactory.CreateLogger("Jobs.Receipts.Apply");

if (command.Error is not null)
{
    log.LogError("{Error}", command.Error);
    return 1;
}

var watch = host.Services.GetRequiredService<IOptions<WatchOptions>>().Value;
var matching = host.Services.GetRequiredService<IOptions<MatchingOptions>>();
var launcher = host.Services.GetRequiredKeyedService<MServerLauncher>(PohodaInstance.Key);
var receipts = host.Services.GetRequiredKeyedService<IReceiptService>(PohodaInstance.Key);
var stockCards = host.Services.GetRequiredKeyedService<IStockCardService>(PohodaInstance.Key);

await using var store = await ReceiptsStateStore.OpenAsync(ResolvePath(watch.DatabasePath));

var applier = new ProposalApplier(stockCards, receipts, store, matching, loggerFactory.CreateLogger<ProposalApplier>());
var runner = new ApplyRunner(applier, store, watch, loggerFactory.CreateLogger<ApplyRunner>());

try
{
    return command.Kind switch
    {
        ReceiptsCommandKind.Status => await runner.StatusAsync(),
        ReceiptsCommandKind.Reset => await runner.ResetAsync(command.Id!),
        _ => await RunApplyAsync()
    };
}
catch (PohodaResponseException ex)
{
    log.LogError("Chyba pri komunikácii s mServer: {Message}", ex.Message);
    return 1;
}
catch (Exception ex)
{
    log.LogError(ex, "Neočakávaná chyba.");
    return 1;
}

// `status` and `reset` are local-only and never touch mServer, so only `apply` pays for starting it.
async Task<int> RunApplyAsync()
{
    var availability = await launcher.EnsureRunningAsync();
    if (!availability.IsAvailable)
    {
        log.LogError(
            "mServer '{Instance}' nie je dostupný a automatický štart zlyhal. Skontroluj, či beží (Pohoda E1 → Databáze → POHODA mServer) a či je v appsettings.json nastavená správna 'Pohoda:{Instance}:PohodaExePath'.",
            PohodaInstance.Key, PohodaInstance.Key);
        return 1;
    }

    try
    {
        return await runner.ApplyAsync(command.Id, command.All);
    }
    finally
    {
        if (availability.StartedByUs)
        {
            await launcher.StopAsync();
        }
    }
}

static string ResolvePath(string path) =>
    Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
```

- [ ] **Step 6: Write the configuration**

Create `src/Jobs/Jobs.Receipts.Apply/appsettings.json`. It carries the same `Watch` section as the scan job — both applications must agree on the folders and on the database file, and `reset` moves a PDF back into the inbox that `scan` reads. `Username` and `Password` stay empty in the repository:

```json
{
  "Pohoda": {
    "GoNoow": {
      "BaseUrl": "http://127.0.0.1",
      "Port": 444,
      "ConfigName": "GoNoow",
      "PohodaExePath": "C:\\Program Files (x86)\\STORMWARE\\POHODA SK E1\\Pohoda.exe",
      "Username": "",
      "Password": "",
      "Ico": "53870441",
      "DefaultStorageCode": "GoNoow",
      "DefaultTypePriceCode": "GABEL"
    }
  },
  "Watch": {
    "InboxFolder": "C:\\tmp\\faktury",
    "ArchiveFolder": "C:\\tmp\\faktury\\spracovane",
    "PendingApprovalFolder": "C:\\tmp\\faktury\\na_schvalenie",
    "DatabasePath": "data\\receipts.db",
    "ScanInterval": "00:15:00"
  },
  "Matching": {
    "LookbackMonths": 12,
    "ReceiptListLimit": 500
  }
}
```

Create `src/Jobs/Jobs.Receipts.Apply/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "Jobs.Receipts.Apply": {
      "commandName": "Project",
      "commandLineArgs": "status",
      "dotnetRunMessages": true,
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

```bash
dotnet user-secrets init --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj
```

- [ ] **Step 7: Verify the CLI runs**

```bash
dotnet build src/eCommerce.sln
dotnet run --project src/Jobs/Jobs.Receipts.Apply --no-launch-profile
```

Expected: the usage text is printed and the exit code is 1 (check with `echo $?` in Git Bash or `$LASTEXITCODE` in PowerShell).

```bash
dotnet run --project src/Jobs/Jobs.Receipts.Apply --no-launch-profile -- status
```

Expected: it composes, opens (and creates) the SQLite database under the configured `Watch:DatabasePath`, then logs `Čakajúce na schválenie (0):` and `Zlyhané (0):` and exits 0. mServer is not contacted for `status`, so this must succeed even with Pohoda closed. It must NOT throw about an unresolvable service.

Do NOT run `apply` here — the first live write is a separate, supervised step (see Task 6).

- [ ] **Step 8: Commit**

```bash
git add src/Jobs/Jobs.Receipts.Apply src/Jobs/Jobs.Receipts.Tests
git commit -m "feat: add Jobs.Receipts.Apply command line, composition root and configuration"
```

---

## Task 6: Full-solution verification, isolation guards and documentation

**Files:**
- Modify: `README.md`

**Interfaces:** none — this task checks that Tasks 1–5 disturbed nothing already in `eCommerce.sln` and documents the two new applications.

- [ ] **Step 1: Build the entire solution**

```bash
dotnet build src/eCommerce.sln
```

Expected: 0 errors. About 351 warnings, all pre-existing in legacy projects (`Import.Wiky`, `Expando.Connector`, …), is the known baseline — no new warning may come from `Jobs.Receipts.*`, `Receipts.*` or `Pohoda.Server`.

- [ ] **Step 2: Run every test project**

```bash
dotnet test src/eCommerce.sln
```

Expected, per project: `Common.Data.Sqlite.Tests` 9, `Common.Documents.PdfReader.Tests` 2, `Pohoda.MServer.Tests` 19 (these three unchanged since PR1), `Pohoda.Server.Tests` 19, `Receipts.Tests` 38, `Jobs.Receipts.Tests` 30 — **117 tests, 0 failures**. Report the actual numbers you observe; a different total is only a problem if a test failed or if a test this plan specifies is missing.

- [ ] **Step 3: Prove the isolation guarantees**

`e06ddb6` is the last PR2 commit, so it is the baseline for "what PR3 changed".

```bash
git diff --stat e06ddb6 HEAD -- src/Jobs/Jobs.Nop.Synch src/Jobs/Jobs.Expando.Orders src/Imports src/Nop src/Expando src/Common src/Pohoda/Pohoda.Server/MServer src/Pohoda/Pohoda.Server/Services/StockService.cs src/Pohoda/Pohoda.Server/Services/CategoryService.cs src/Pohoda/Pohoda.Server/Services/OrderService.cs src/Pohoda/Pohoda.Server/Services/ParameterService.cs src/Pohoda/Pohoda.Server/Services/ReceiptService.cs src/Pohoda/Pohoda.Server/Services/StockCardService.cs src/Pohoda/Pohoda.Contract src/Receipts/Receipts.Contract src/Receipts/Receipts.Gabel
```

Expected: **empty output**. Every one of those paths is either a pre-existing production path or a PR2 deliverable this PR must not touch.

List every pre-existing file this PR did change, and confirm the list is exactly the four expected ones:

```bash
git diff --name-only e06ddb6 HEAD | while read f; do git cat-file -e "e06ddb6:$f" 2>/dev/null && echo "PRE-EXISTING: $f"; done
```

Expected: exactly `README.md`, `src/eCommerce.sln`, `src/Pohoda/Pohoda.Server.Tests/Pohoda.Server.Tests.csproj`, and — only if Task 1 Step 2 needed it — `src/Pohoda/Pohoda.Server/Pohoda.Server.csproj`. Nothing else.

Confirm no credential leaked into the repository:

```bash
grep -n '"Username"\|"Password"' src/Jobs/Jobs.Receipts.Scan/appsettings.json src/Jobs/Jobs.Receipts.Apply/appsettings.json
```

Expected: every one of the four lines ends in `"",` — empty strings only.

Confirm `Receipts.Core` is still supplier-agnostic:

```bash
grep -rn "Gabel" src/Receipts/Receipts.Core --include=*.cs | grep -v "///"
```

Expected: empty output.

- [ ] **Step 4: Update `README.md`**

Make these edits, matching the file's existing Slovak style:

1. In "Technológie", change `26 projektov (vrátane 5 testovacích)` to `29 projektov (vrátane 6 testovacích)`.

2. In the "Štruktúra riešenia" tree, extend the `Jobs/` block so it reads:
   ```
       ├── Jobs/
       │   ├── Jobs.Expando.Orders/
       │   ├── Jobs.Nop.Synch/
       │   ├── Jobs.Receipts.Apply/
       │   ├── Jobs.Receipts.Scan/
       │   └── Jobs.Receipts.Tests/
   ```
   Keep the surrounding connectors well-formed.

3. In the "### Jobs – synchronizačné úlohy" section, add two bullets after the existing ones:

   ```markdown
   - **Jobs.Receipts.Scan** – worker service, ktorý v nastavenom intervale (`Watch:ScanInterval`) prejde priečinok s PDF faktúrami dodávateľa, pre každú novú finálnu faktúru postaví návrh skladovej príjemky a zapíše čitateľný Markdown náhľad do priečinka na schválenie. Do Pohody **nezapisuje** – iba číta (zoznam príjemiek, vyhľadanie skladových kariet). Každý cyklus si v prípade potreby sám spustí mServer a po skončení ho zastaví, ak ho sám spustil.
   - **Jobs.Receipts.Apply** – konzolová aplikácia, ktorá po ľudskej kontrole náhľadu vykoná samotný zápis do Pohody. Je to **jediné miesto v celom riešení, ktoré zapisuje príjemky a zakladá skladové karty**, a spúšťa sa vždy ručne:
     ```
     Jobs.Receipts.Apply.exe status            # čakajúce a zlyhané návrhy
     Jobs.Receipts.Apply.exe apply --id <id>   # zápis jedného schváleného návrhu
     Jobs.Receipts.Apply.exe apply --all       # zápis všetkých čakajúcich návrhov
     Jobs.Receipts.Apply.exe reset --id <id>   # návrat návrhu na PendingApproval (opakovaný pokus)
     ```
     Príkazy `status` a `reset` pracujú len s lokálnou SQLite databázou a mServer nepotrebujú.
   ```

4. In the "### Receipts – automatizácia skladových príjemiek z dodávateľských faktúr" section, replace the sentence that says the executables will arrive later with: `Spustiteľné aplikácie sú Jobs.Receipts.Scan (návrhy) a Jobs.Receipts.Apply (zápis) – viď sekciu Jobs.`

5. At the end of the Receipts section, add the pre-flight checklist for the first real write:

   ```markdown
   #### Pred prvým ostrým `apply`

   Zápis do Pohody cez XML agendu (`create`/`update` príjemky, založenie skladovej karty) **zatiaľ nikdy naživo neprebehol** – ani v pôvodnom riešení. Prvé spustenie preto patrí k dozorovaným krokom:

   1. Spusti ho, keď je pri tom niekto, kto vidí stav v Pohode, a ideálne najprv proti testovacej účtovnej jednotke.
   2. Prvý zápis vždy ako `apply --id <id>` pre jeden konkrétny návrh, nikdy `apply --all`.
   3. Ako prvý otestuj návrh typu `AppendLines` (doplnenie existujúcej príjemky) na doklade, o ktorý nie je škoda. `UpdateAsync` posiela **kompletný zoznam riadkov** (existujúce + nové); ak by Pohoda riadky pripájala namiesto nahradenia, každý existujúci riadok by sa zduplikoval. Toto je najdôležitejšia neoverená vlastnosť celého reťazca.
   4. `UpdateAsync` zapisuje do hlavičky **dnešný dátum** – over, či to prepísalo pôvodný dátum príjemky a či to vadí.
   5. Po zápise skontroluj v Pohode zaradenie novej skladovej karty (`storage`) a jej `orderName` pri veľkostných variantoch; priradenie variantu k materskej karte treba aj tak urobiť ručne, XML agenda naň nemá pole.
   6. Over, či je `rateVAT="none"` správny token pre položky oslobodené od DPH.

   Body 3 až 6 sú prenesené otvorené otázky z pôvodného riešenia, nie nové riziká zavedené presunom.
   ```

6. In "Konfigurácia citlivých údajov", extend the list of required keys with a sentence: `Jobs.Receipts.Scan a Jobs.Receipts.Apply čítajú Pohoda:GoNoow:Username a Pohoda:GoNoow:Password (v produkcii Pohoda__GoNoow__Username a Pohoda__GoNoow__Password) – ide o inú mServer inštanciu (port 444) než tú, ktorú používajú ostatné joby (Pohoda:username/password, port 5336).`

- [ ] **Step 5: Commit the documentation**

```bash
git add README.md
git commit -m "docs: document Jobs.Receipts.Scan and Jobs.Receipts.Apply"
```

---

## Self-Review

**Spec coverage** (against `docs/superpowers/specs/2026-08-24-gabel-prijemkasync-merge-design.md`):
- §5 PR3 `Jobs.Receipts.Scan` + `Jobs.Receipts.Apply`, wired to `Pohoda.MServer` "GoNoow" — Tasks 2–5, with the instance key fixed in `PohodaInstance.Key` and the keyed registrations from Task 1. ✅
- §5 PR3 "`apply` stays gated behind the same manual approval" — `apply` is a CLI with no scheduler and no service registration; `scan` never writes; Task 6 Step 4 documents the gate and the pre-flight checklist. ✅
- §4 data flow, scan half (inbox → parser → `ProposalBuilder` → store → preview in `na_schvalenie/`) — `ScanRunner`, Task 2. ✅
- §4 data flow, apply half (`ProposalApplier` → stock card create → receipt create/update → PDF to `Archive/` → audit row) — `ApplyRunner`, Task 4; the audit rows are written by `ProposalApplier` (PR2) on success and by `ApplyRunner` on failure. ✅
- §2 decision 4 "automate `scan`, keep `apply` manual" — the worker schedules only the scan. ✅
- §7 two config sections, `Watch:*` and `Matching:*` carried over, `DefaultStorageCode`/`DefaultTypePriceCode` under the GoNoow-scoped section, secrets empty in tracked files with User Secrets / `__` env vars — Tasks 3 and 5, guarded in Task 6 Step 3. ✅
- §6 "no automated test harness exists for live mServer interaction" — honored: every new test is offline, and the live verification is the supervised run the checklist describes. ✅
- §5 PR4–PR6 (migrating `Pohoda.Server`'s existing services and the Jobs, re-verification, retiring the old repo) — correctly out of scope; Task 6 Step 3 proves none of them were touched.

**Carried over from the PR2 final review** (all three were routed into this plan): the `UpdateAsync` replace-versus-append question and the header-date rewrite are items 3 and 4 of the pre-flight checklist; the shared `SqliteConnection` must not be used concurrently — `ScanRunner` documents and enforces sequential processing and each run opens its own store; the CLI owns the `PendingApproval → Failed` transition and `scan` owns `MarkProcessedAsync` — both implemented and tested (Task 4 tests 7 and 8, Task 2 test 1).

**Placeholder scan:** every step carries literal, complete code or a literal command. The two `dotnet user-secrets init` steps generate a GUID by design; nothing else is left to the implementer's invention.

**Type consistency:**
- `ScanRunner(IReceiptService, IInvoiceParser, ProposalBuilder, ReceiptsStateStore, WatchOptions, MatchingOptions, ILogger<ScanRunner>)` — the order in Task 2's implementation, in `PipelineHarness.CreateScanRunner`, and in `Worker.RunCycleAsync` all match. ✅
- `ApplyRunner(ProposalApplier, ReceiptsStateStore, WatchOptions, ILogger<ApplyRunner>)` — Task 4's implementation, `PipelineHarness.CreateApplyRunner` and Task 5's `Program.cs` all match. ✅
- `ProposalApplier` takes `IOptions<MatchingOptions>`, so the harness wraps with `Options.Create(Matching)` and `Program.cs` passes the resolved `IOptions<MatchingOptions>` directly, never `.Value`. ✅
- `ProposalBuilder(IStockCardService, IVariantDetector, StockMappingCache, ILogger<ProposalBuilder>)` — the harness and `Worker` construct it identically, both passing `store.StockMappings`. ✅
- `WatchOptions.ScanInterval` is a `TimeSpan` and the tracked configs supply `"00:15:00"`, which the configuration binder converts. `MatchingOptions.ReceiptListLimit` is the PR2 name and is what both configs and `ScanRunner` use — the old `PrijemkaListLimit` appears nowhere. ✅
- `ReceiptsCommand` is constructed only with named arguments after the first positional one, so adding a field later cannot silently change a call site. ✅
- Test counts: Task 2 adds 7, Task 4 adds 11 (7 → 18), Task 5 adds 12 (18 → 30); Task 1 adds 4 to `Pohoda.Server.Tests` (15 → 19); Task 6 expects 9 + 2 + 19 + 19 + 38 + 30 = 117. ✅

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-11-gabel-prijemkasync-merge-pr3-receipts-executables.md`. Execution: **Subagent-Driven** (superpowers:subagent-driven-development) — a fresh implementer subagent per task, spec + quality review after each, whole-branch review at the end — the same approach used for PR2.
