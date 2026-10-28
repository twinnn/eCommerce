[English](README.md) | Slovensky

# eCommerce – AI Agenti pre distribúciu Gabel

## Účel

Toto riešenie obsahuje AI Agentov, ktorí podporujú AcEcom s.r.o., **Distribútora** s exkluzívnou distribúciou palíc Gabel na Slovensku a v Českej republike. Každý **Agent** zodpovedá za jeden obchodný proces: zo systémov Distribútora voľne číta a mení ich len cez **návrhy (Proposals)**, ktoré schváli človek, až kým mu človek pre daný typ akcie neudelí autonómiu.

Funkčné časti sú dnes:

- **Receipts** – z PDF **prijatej faktúry** od Gabelu pripraví návrh skladovej **príjemky** v Pohode a do Pohody ju zapíše až potom, čo ju človek skontroloval a spustil zápis.
- **Klient Pohoda mServer** – zdieľaný nízkoúrovňový klient, cez ktorý Agenti čítajú a zapisujú do Pohody (STORMWARE), účtovného a skladového ERP systému Distribútora.

**Go-Noow**, vlastný e-shop Distribútora, nie je súčasťou tohto riešenia; stavia ho NextCom, ktorý ho aj synchronizuje s Pohodou.

Doménový slovník je v [GLOSSARY.md](GLOSSARY.md) a architektonické rozhodnutia sú zaznamenané v [docs/adr/](docs/adr/):

- [0001 – Agenti sa integrujú s Pohodou cez mServer, nie cez úpravy od NextComu](docs/adr/0001-agents-integrate-with-pohoda-via-mserver.md)
- [0002 – Agenti konajú cez návrhy, autonómia sa udeľuje po typoch akcií](docs/adr/0002-agents-act-through-proposals-with-per-action-autonomy.md)

Kód a dokumentácia sú v angličtine, aby sa systém dal neskôr ponúknuť aj iným firmám; tento README má pre tím Distribútora aj slovenský preklad, [README.sk.md](README.sk.md). Výstupy pre obsluhu (logy, nápoveda príkazového riadku `Jobs.Receipts.Apply`, Markdown náhľady) sú po slovensky.

## Technológie

- **.NET 10.0** – riešenie `src/eCommerce.sln` (Visual Studio alebo `dotnet` CLI), 18 projektov vrátane 6 testovacích (xUnit).
- **Integrácia s Pohodou** – HTTP/XML voči lokálnemu POHODA **mServeru** (kódovanie Windows-1250); mServer sa podľa potreby spúšťa a zastavuje cez `pohoda.exe /http start|stop`.
- **Čítanie PDF** – PdfPig, obalený knižnicou `Common.Documents.PdfReader`.
- **Lokálny stav** – SQLite (spracované súbory, audit log, cache mapovania kódov skladových kariet, návrhy).
- **Logovanie** – predvolené `Microsoft.Extensions.Logging` providery generického .NET hostu.
- **CI** – Azure Pipelines (`CI/azure-pipelines.yml`).

## Štruktúra riešenia

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

## Moduly

### Common – zdieľané knižnice

- **Common.Contract** – zdieľané DTO a pomocné triedy, ktoré používajú agendové služby Pohody: `StockDTO`, `OrderDTO`/`OrderItemDTO`, `PohodaCategoryDto`, `PohodaParameterDto`, generický kontrakt `IBuilder<D, T>`, XML serializer (`Utf8Serializer`) a LINQ rozšírenia.
- **Common.Caching** – jednoduchý signál pre invalidáciu cache (`CacheSignal`).
- **Common.Data.Sqlite** – znovupoužiteľný bootstrap SQLite databázy (`SqliteDatabase.OpenAsync` otvorí pripojenie a vykoná zadané schema DDL príkazy) a nad ním dve doménovo neviazané tabuľky: `ProcessedFileTracker` (SHA-256 idempotencia: bol už tento súbor spracovaný?) a `AuditLog` (append-only chronologický audit log).
- **Common.Documents.PdfReader** – statická trieda `PdfRowReader` (nad PdfPig), ktorá rekonštruuje text PDF ako vizuálne riadky zhlukovaním slov podľa ich bounding boxu. Pri jednoduchých jednostĺpcových tabuľkách, napríklad riadkoch prijatej faktúry, je to spoľahlivejšie než poradie textu v raw content streame.

### Pohoda – integrácia s ERP systémom

- **Pohoda.Contract** – rozhrania služieb: `IReceiptService` (agenda príjemiek: zoznam, vytvorenie, aktualizácia), `IStockCardService` (vyhľadanie skladovej karty podľa kódu dodávateľa, hľadanie podľa časti názvu, založenie karty; prechodné rozhranie, ktoré sa zlúči do `IStockService`, keď `Pohoda.Server` prejde na `Pohoda.MServer`) a staršie `ICategoryService`, `IOrderService`, `IParameterService` a `IStockService`. Obsahuje aj abstraktný `AbstractOrderBuilder`, modely `ExistingReceipt`, `ReceiptLineToWrite` a `StockNameMatch` a oficiálne XSD schémy mServer API (Order, Stock, Filter, …).
- **Pohoda.MServer** – zdieľaný nízkoúrovňový klient pre POHODA mServer, nezávislý od `Pohoda.Server`: `MServerClient` (HTTP cez `IHttpClientFactory`, kódovanie Windows-1250, parsovanie `state`/`error` odpovede), `MServerLauncher` (spúšťa a zastavuje `pohoda.exe /http start|stop` priamo cez `ProcessStartInfo.ArgumentList`, bez `cmd.exe`) a `ServiceCollectionExtensions.AddPohodaMServer`, ktorý cez keyed DI registruje viacero mServer inštancií súčasne (konfiguračná sekcia `Pohoda:<instanceKey>:*`).
- **Pohoda.Server** – agendové služby Pohody. `ReceiptService` a `StockCardService` (implementácie `IReceiptService`/`IStockCardService`) komunikujú cez zdieľaný `Pohoda.MServer.MServerClient` a XML obálku `Pohoda.Server.Xml.DataPackBuilder` a pre každú inštanciu ich registruje `AddPohodaAgendaServices`. Staršie služby (`StockService`, `CategoryService`, `OrderService`, `ParameterService` spolu so `Storages` a `Mappers`) stále používajú pôvodný `MServer/MServer.cs` a dnes ich nepoužíva žiadna spustiteľná aplikácia.
- **Pohoda.MServer.Tests**, **Pohoda.Server.Tests** – xUnit testy. Sada `Pohoda.Server.Tests` spúšťa `ReceiptService` a `StockCardService` proti falošnému mServeru (`FakeMServer`) a overuje presný tvar XML požiadaviek (namespace elementov `requestPrijemka`/`requestStock`, limit/count, dvojito vnorený `stockItem`, `prijemkaAccessoryChargesItem` pre dopravu) a parsovanie odpovedí.

### Receipts – skladové príjemky z prijatých faktúr

Portované zo samostatného riešenia `Gabel.PrijemkaSync`. Z PDF prijatej faktúry sa vyťažia položky a pripraví sa z nich návrh príjemky do Pohody. Návrh sa uloží lokálne (SQLite) a zapíše ako Markdown náhľad na schválenie; zápis do Pohody je samostatný, explicitne spúšťaný krok. Spustiteľné aplikácie sú `Jobs.Receipts.Scan` a `Jobs.Receipts.Apply` (viď [Tok príjemiek: Scan → Apply](#tok-príjemiek-scan--apply)).

- **Receipts.Contract** – doménové modely (`ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, `ReceiptLineProposal`) a dve rozhrania, ktoré implementuje plugin dodávateľa: `IInvoiceParser` (PDF → `ParsedInvoice`) a `IVariantDetector` (rozpozná veľkostný variant v názve produktu).
- **Receipts.Core** – logika nezávislá od dodávateľa: `ProposalBuilder` (spáruje prijatú faktúru s existujúcou príjemkou podľa výskytu jej čísla v texte príjemky, rozlíši existujúce a nové skladové karty, navrhne materskú kartu pre varianty), `ProposalApplier` (jediný kód, ktorý zapisuje do Pohody: založí chýbajúce skladové karty a vytvorí alebo doplní príjemku; doprava ide vždy ako vedľajšie náklady), `PreviewWriter` (Markdown náhľad) a `ReceiptsStateStore` (SQLite stav nad `Common.Data.Sqlite`: spracované súbory, audit log, cache mapovania kódov skladových kariet, uložené návrhy). Závisí len na `Receipts.Contract`, `Pohoda.Contract` a `Common.Data.Sqlite`.
- **Receipts.Gabel** – prvý plugin dodávateľa: `GabelInvoiceParser` (deterministický regex parser finálnych prijatých faktúr `FT n/nnnnnn` nad `Common.Documents.PdfReader`; proforma faktúry preskakuje, keďže nie sú prijatými faktúrami) a `GabelVariantDetector` (vzory `CM.120` / `135CM` / `Cm 115`; rozsahy ako `73-110CM` sa za variant nepovažujú). Ďalší dodávateľ znamená nový projekt `Receipts.<Supplier>`; `Receipts.Core` sa nemení.
- **Receipts.Tests** – xUnit testy vrátane testov parsera nad reálnymi PDF vzorkami (`TestData/`), testov `ProposalBuilder`/`ProposalApplier` s ručne písanými fake implementáciami služieb Pohody a testov SQLite stavu.

### Jobs – spustiteľné aplikácie

- **Jobs.Receipts.Scan** – worker service; viď nižšie.
- **Jobs.Receipts.Apply** – konzolová aplikácia; viď nižšie.
- **Jobs.Receipts.Tests** – xUnit testy `ScanRunner`, `ApplyRunner` a parsera príkazového riadku.

## Tok príjemiek: Scan → Apply

1. **Scan** (`Jobs.Receipts.Scan`) beží nepretržite. Každý `Watch:ScanInterval` prejde priečinok `Watch:InboxFolder` s PDF prijatými faktúrami. Pre každú novú finálnu prijatú faktúru postaví návrh (buď *vytvoriť novú príjemku*, alebo *doplniť chýbajúce riadky do existujúcej príjemky*), uloží ho do SQLite databázy v `Watch:DatabasePath` a zapíše čitateľný Markdown náhľad do `Watch:PendingApprovalFolder`. Scan do Pohody **nikdy nezapisuje**, iba číta (zoznam príjemiek, vyhľadanie skladových kariet). Každý cyklus si v prípade potreby spustí mServer a po skončení ho zastaví, ale len ak ho sám spustil.
2. **Kontrola** – človek si prečíta náhľad a rozhodne, či je návrh správny.
3. **Apply** (`Jobs.Receipts.Apply`) sa po kontrole spúšťa ručne. Je to **jediné miesto v riešení, ktoré zapisuje príjemky a zakladá skladové karty v Pohode**. Po úspešnom zápise sa PDF presunie do `Watch:ArchiveFolder`. Návrh, ktorého zápis zlyhá, sa označí ako `Failed` a zvyšok dávky pokračuje.

```
Jobs.Receipts.Apply.exe status            # čakajúce a zlyhané návrhy
Jobs.Receipts.Apply.exe apply --id <id>   # zápis jedného schváleného návrhu
Jobs.Receipts.Apply.exe apply --all       # zápis všetkých čakajúcich návrhov
Jobs.Receipts.Apply.exe reset --id <id>   # návrat návrhu na PendingApproval a presun jeho PDF späť do inboxu (opakovaný pokus)
```

`status` a `reset` pracujú len s lokálnou SQLite databázou (`reset` navyše presunie PDF z `Watch:ArchiveFolder` späť do `Watch:InboxFolder`) a mServer nepotrebujú.

Obe aplikácie **musia** mať v `appsettings.json` rovnaké `Watch:DatabasePath`: Scan a Apply si cez túto SQLite databázu odovzdávajú návrhy, takže dve rôzne cesty znamenajú dve nezávislé databázy a Apply nikdy neuvidí, čo Scan vytvoril. Preto obe trackované konfigurácie používajú rovnakú absolútnu cestu namiesto relatívnej (tá by sa pri dvoch `.exe` v dvoch rôznych priečinkoch `bin/` vyriešila na dva rôzne súbory).

### Pred prvým ostrým `apply`

Zápis do Pohody cez XML agendu (`create`/`update` príjemky, založenie skladovej karty) **zatiaľ nikdy naživo neprebehol**, ani v pôvodnom riešení. Prvé spustenie preto patrí k dozorovaným krokom:

1. Spusti ho, keď je pri tom niekto, kto vidí stav v Pohode, a ideálne najprv proti testovacej účtovnej jednotke.
2. Prvý zápis urob vždy ako `apply --id <id>` pre jeden konkrétny návrh, nikdy `apply --all`.
3. Ako prvý otestuj návrh typu `AppendLines` (doplnenie existujúcej príjemky) na doklade, o ktorý nie je škoda. `UpdateAsync` posiela **kompletný zoznam riadkov** (existujúce + nové); ak by Pohoda riadky pripájala namiesto nahradenia, každý existujúci riadok by sa zduplikoval. Toto je najdôležitejšia neoverená vlastnosť celého reťazca.
4. `UpdateAsync` zapisuje do hlavičky **dnešný dátum**; over, či to prepísalo pôvodný dátum príjemky a či to vadí.
5. Po zápise skontroluj v Pohode zaradenie novej skladovej karty (`storage`) a jej `orderName` pri veľkostných variantoch. Priradenie variantu k materskej karte treba aj tak urobiť ručne, XML agenda naň nemá pole.
6. Over, či je `rateVAT="none"` správny token pre položky oslobodené od DPH.

Body 3 až 6 sú otvorené otázky prenesené z pôvodného riešenia, nie nové riziká zavedené presunom.

## Build, testy a spustenie

- **Build**: otvor `src/eCommerce.sln` vo Visual Studiu (s .NET 10 SDK), alebo spusti `dotnet build src/eCommerce.sln`.
- **Testy**: `dotnet test src/eCommerce.sln`.
- **Spustenie**: obe aplikácie sa konfigurujú cez vlastný `appsettings.json` (sekcie `Pohoda:AcEcom`, `Watch`, `Matching`):

  ```bash
  dotnet run --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
  dotnet run --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj -- status
  ```

  V produkcii beží skompilovaný `Jobs.Receipts.Scan.exe` ako dlhodobý proces a `Jobs.Receipts.Apply.exe` sa spúšťa ručne. Orchestračný skript ani docker-compose v repozitári nie je.

## CI

`CI/azure-pipelines.yml` beží pri každom push do `main` na agentovi `windows-latest`: NuGet restore → VSBuild (Release) → VSTest, ktorý nájde a spustí testovacie projekty.

## Konfigurácia citlivých údajov

Trackované `appsettings.json` súbory `Jobs.Receipts.Scan` a `Jobs.Receipts.Apply` obsahujú len netajné predvolené hodnoty; `Pohoda:AcEcom:Username` a `Pohoda:AcEcom:Password` sú zámerne prázdne (`""`). Obe aplikácie ich potrebujú na prihlásenie do mServer inštancie AcEcom (port 444).

`AcEcom` je kľúč inštancie: vyberá konfiguračnú sekciu `Pohoda:AcEcom:*` aj keyed DI registrácie. `ConfigName` a `DefaultStorageCode` v tejto sekcii ostávajú `"GoNoow"`, pretože ide o skutočné názvy mServer konfigurácie a členenia skladu v Pohode.

**Lokálny vývoj** – hodnoty nastav cez .NET User Secrets (mimo repozitára, viazané na `<UserSecretsId>` v danom `.csproj`), pre každú aplikáciu zvlášť:

```bash
dotnet user-secrets set "Pohoda:AcEcom:Username" "<hodnota>" --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
dotnet user-secrets set "Pohoda:AcEcom:Password" "<hodnota>" --project src/Jobs/Jobs.Receipts.Scan/Jobs.Receipts.Scan.csproj
dotnet user-secrets set "Pohoda:AcEcom:Username" "<hodnota>" --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj
dotnet user-secrets set "Pohoda:AcEcom:Password" "<hodnota>" --project src/Jobs/Jobs.Receipts.Apply/Jobs.Receipts.Apply.csproj
```

User Secrets sa načítajú iba v prostredí `Development`, preto `Properties/launchSettings.json` každého projektu nastavuje `DOTNET_ENVIRONMENT=Development`. Spustenie skompilovaného `.exe` priamo, alebo `dotnet run --no-launch-profile`, túto premennú obíde a User Secrets sa nenačítajú.

**Produkcia** – rovnaké kľúče nastav ako environment variables na cieľovom stroji, s dvojitým podčiarkovníkom pre vnorené sekcie: `Pohoda__AcEcom__Username` a `Pohoda__AcEcom__Password`. .NET host ich prečíta automaticky a prebijú `appsettings.json`.

Staršie verzie používali kľúč inštancie `GoNoow`. Ak sú User Secrets alebo environment variables ešte nastavené pod `Pohoda:GoNoow:*` / `Pohoda__GoNoow__*`, nastav ich znova pod novými názvami.

Nikdy nezapisuj skutočné hodnoty do `appsettings.json` ani do žiadneho iného súboru trackovaného gitom.
