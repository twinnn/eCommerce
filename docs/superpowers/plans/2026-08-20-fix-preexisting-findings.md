# Oprava pred-existujúcich zistení z .NET 10 upgrade review — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Opraviť dve pred-existujúce (na upgrade nezávislé) zistenia, ktoré vzišli z finálneho code review .NET 10 upgrade plánu (`docs/superpowers/plans/2026-08-19-net10-upgrade.md`): tichú stratu argumentu v štruktúrovanom logovaní (`ParametersStorage.cs:71`) a nepravdivé tvrdenie v `README.md`, že riešenie používa Serilog.

**Architecture:** Obe zmeny sú izolované, nezávislé opravy bez architektonického dopadu — jedna upravuje volanie `ILogger.LogInformation` tak, aby template obsahoval placeholder zodpovedajúci odovzdanému argumentu, druhá opravuje jeden riadok dokumentácie tak, aby zodpovedal skutočne použitému logovaciemu mechanizmu (predvolené `Microsoft.Extensions.Logging` providery z `Host.CreateDefaultBuilder`, nie Serilog).

**Tech Stack:** .NET 10, `Microsoft.Extensions.Logging` (analyzer pravidlo `CA2017`), Markdown (`README.md`).

**Spec:** Priama požiadavka používateľa: "please, plan changes related to point 2. 'pre-existing / out-of-scope' findings" — odkazuje na tieto dve položky z finálneho review .NET 10 upgrade plánu (zaznamenané v jeho ledgeri, teraz zmazanom, ale zhrnuté v správe používateľovi):

> "A few pre-existing, out-of-scope findings (a logging-args bug in `ParametersStorage.cs:71`, a stale 'Serilog' claim in the README) — noted but untouched."

Žiadny iný spec dokument neexistuje. (Ostatné dve pôvodne parkované minor zistenia — chýbajúci `global.json` a nadbytočná `Microsoft.Extensions.Hosting.Abstractions` v `Import.Wiky` — **nie sú** súčasťou tohto plánu; používateľ explicitne požiadal len o "point 2", čo boli tieto dve položky.)

### Zistený súčasný stav (dôkazy)

- `src/Pohoda/Pohoda.Server/Storages/ParametersStorage.cs:71`:
  ```csharp
  _logger.LogInformation("Deserializing response", response);
  ```
  Template `"Deserializing response"` neobsahuje žiadny `{Placeholder}`, no odovzdáva sa mu jeden pozičný argument `response` (XML string z `_mServer.SendRequest(request)`, riadok 69). `Microsoft.Extensions.Logging` extra argument bez zodpovedajúceho placeholderu do vyrenderovanej správy nepremietne — hodnota `response` sa v logovanom texte reálne stratí (analyzer `CA2017`, overené v `dotnet build src/eCommerce.sln -c Release --no-incremental`, jediný výskyt v celom riešení).
- `README.md:18`: `- **Logovanie**: Serilog.` — v riešení **neexistuje** žiadna `PackageReference` na `Serilog` (overené cez `grep -rn "PackageReference.*Serilog" src/` aj cez plný zoznam balíčkov v `.csproj` súboroch) a žiadny `.cs` súbor `Serilog`/`UseSerilog` nepoužíva. `src/Jobs/Jobs.Nop.Synch/Program.cs:24` (a analogicky `Import.Wiky`, `Jobs.Expando.Orders`) používa `Host.CreateDefaultBuilder(args)` bez akéhokoľvek `.ConfigureLogging`/`UseSerilog` volania — skutočné logovanie beží na predvolených `Microsoft.Extensions.Logging` provideroch (Console/Debug/EventSource), ktoré `CreateDefaultBuilder` pridáva automaticky.
- Tri `appsettings.json` súbory (`Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`) obsahujú blok `"Serilog": { "MinimumLevel": {...} }` — tento blok sa nikde v kóde nečíta (žiadny Serilog provider ho nekonzumuje), je to mŕtva konfigurácia. **Tento plán sa `appsettings.json` súborov nedotýka** — sú mimo rozsahu žiadosti ("point 2" sa týkala len kódu a README), a tieto súbory už boli predmetom samostatného, opatrne navrhnutého plánu (`docs/superpowers/plans/2026-08-19-appsettings-secrets-security.md`) kvôli citlivým údajom, ktoré obsahujú.

## Global Constraints

- `dotnet build src/eCommerce.sln -c Release` musí po oboch úlohách stále ukazovať `0 Error(s)`.
- Po Task 1 už `dotnet build` nesmie hlásiť `CA2017` pre `ParametersStorage.cs:71`.
- Žiadne `appsettings.json` súbory sa v tomto pláne nemenia.
- Žiadne ďalšie parkované zistenia (`global.json`, `Import.Wiky` redundantný balíček) nie sú súčasťou tohto plánu.

---

## Task 1: Opraviť CA2017 — chýbajúci placeholder v `ParametersStorage.cs`

**Files:**
- Modify: `src/Pohoda/Pohoda.Server/Storages/ParametersStorage.cs:71`

**Interfaces:** žiadne verejné rozhranie sa nemení — `GetParametersFromPohoda()` zostáva `private`, jeho signatúra a návratový typ (`Task<GetParametersResponse.responsePack>`) sú nezmenené.

- [ ] **Krok 1: Upraviť logovacie volanie**

Aktuálny riadok 71:
```csharp
            _logger.LogInformation("Deserializing response", response);
```

Nový riadok 71:
```csharp
            _logger.LogInformation("Deserializing response: {Response}", response);
```

(Pridaný pomenovaný placeholder `{Response}` zodpovedajúci existujúcemu argumentu `response` — žiadna iná časť súboru sa nemení.)

- [ ] **Krok 2: Build overenie**

Run: `dotnet build src/Pohoda/Pohoda.Server/Pohoda.Server.csproj -c Release --no-incremental`

Expected: `0 Error(s)`, a v zozname warningov už **nie je** žiadny `CA2017` odkazujúci na `ParametersStorage.cs`. (Ostatné pred-existujúce nullable/CS86xx warningy v riešení nesúvisia s touto zmenou a môžu ostať.)

- [ ] **Krok 3: Commit**

```bash
git add src/Pohoda/Pohoda.Server/Storages/ParametersStorage.cs
git commit -m "fix: add missing logging placeholder in ParametersStorage (CA2017)"
```

---

## Task 2: Opraviť nepravdivé tvrdenie o Serilog v `README.md`

**Files:**
- Modify: `README.md:18`

**Interfaces:** žiadne — čisto dokumentačná zmena.

- [ ] **Krok 1: Upraviť riadok o logovaní**

Aktuálny riadok 18:
```markdown
- **Logovanie**: Serilog.
```

Nový riadok 18:
```markdown
- **Logovanie**: predvolené `Microsoft.Extensions.Logging` providery z `Host.CreateDefaultBuilder` (Console/Debug) — `Serilog` balíček nie je v riešení referencovaný. Tri `appsettings.json` súbory (`Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`) obsahujú nepoužívaný blok `Serilog`, ktorý sa v kóde nikde nečíta.
```

- [ ] **Krok 2: Overiť, že sa nezmenilo nič iné**

Run: `git diff README.md`

Expected: presne jeden zmenený riadok (18), žiadne iné časti súboru dotknuté.

- [ ] **Krok 3: Commit**

```bash
git add README.md
git commit -m "docs: correct README logging claim (no Serilog in this solution)"
```

---

## Self-Review — pokrytie požiadaviek

- "logging-args bug in ParametersStorage.cs:71" → Task 1, presná lokácia a fix overené proti aktuálnemu obsahu súboru a proti reálnemu `CA2017` build warningu.
- "stale 'Serilog' claim in the README" → Task 2, presná lokácia (riadok 18) a fix overené proti aktuálnemu obsahu README a proti reálnemu stavu kódu (žiadna Serilog PackageReference, `Host.CreateDefaultBuilder` bez `UseSerilog`).
- Obe úlohy sú nezávislé (rôzne súbory), možno ich vykonať v ľubovoľnom poradí alebo dávkovo jedným implementérom.
