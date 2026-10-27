# eCommerce – integračný middleware

## Účel projektu

Toto riešenie **nie je samotný e-shop**, ale sada .NET worker services a konzolových aplikácií, ktoré prepájajú a synchronizujú dáta medzi backendovými systémami internetového obchodu:

- **Pohoda** (STORMWARE) – účtovný a skladový ERP systém, zdroj pravdy pre sklad, ceny, kategórie a objednávky. Komunikácia prebieha cez lokálny HTTP/XML server **mServer**.
- **nopCommerce** – samotná e-shop platforma (vitrína), do ktorej sa cez jej Web API synchronizujú produkty, kategórie a špecifikačné atribúty.
- **Expando** – platforma na správu objednávok/dropshipping, odkiaľ sa objednávky sťahujú a zapisujú do Pohody.
- **XML feedy dodávateľov** (Wiky, Hrackys) – externé produktové feedy veľkoobchodných partnerov, importované do katalógu.
- **Packeta** – prepravca; v konfigurácii existujú prístupové údaje, ale samostatný klient/služba zatiaľ nie je implementovaná.

Zjednodušene: **dodávateľské feedy → Pohoda (ERP) → nopCommerce (e-shop)**, a súbežne **Expando objednávky → Pohoda**.

## Technológie

- **.NET** – riešenie `src/eCommerce.sln` (Visual Studio alebo `dotnet` CLI), 29 projektov (vrátane 6 testovacích), všetky na **.NET 10.0**.
- **Logovanie**: predvolené `Microsoft.Extensions.Logging` providery z `Host.CreateDefaultBuilder` (Console/Debug) — `Serilog` balíček nie je v riešení referencovaný. Tri `appsettings.json` súbory (`Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`) obsahujú nepoužívaný blok `Serilog`, ktorý sa v kóde nikde nečíta.
- **Komunikácia**: `HttpClient`, vlastné XML parsovanie (streamovacie čítanie veľkých feedov), JSON serializácia.
- **Dátová vrstva**: bez EF Core/DbContext – ide o bezstavový integračný middleware; niektoré projekty používajú jednoduché súborové úložisko stavu (napr. `lastId.txt`) alebo pripravené (nie plne zapojené) embedded úložisko.
- **CI**: Azure Pipelines (`CI/azure-pipelines.yml`) – NuGet restore → VSBuild → VSTest.

## Štruktúra riešenia

```
eCommerce/
├── CI/
│   └── azure-pipelines.yml
└── src/
    ├── eCommerce.sln
    ├── Common/
    │   ├── Caching/Common.Caching/
    │   ├── Contract/Common.Contract/
    │   ├── Data/
    │   │   ├── Common.Data.Sqlite/
    │   │   └── Common.Data.Sqlite.Tests/
    │   ├── Documents/
    │   │   ├── Common.Documents.PdfReader/
    │   │   └── Common.Documents.PdfReader.Tests/
    │   ├── Email/Common.Email.EmailService/
    │   └── Readers/
    │       ├── Common.Readers.BaseApiConsumer/
    │       └── Common.Readers.XmlReader/
    ├── Expando/
    │   ├── Expando.Contract/
    │   └── Expando.Connector/
    ├── Imports/
    │   ├── Import.Hrackys/
    │   └── Import.Wiky/
    ├── Jobs/
    │   ├── Jobs.Expando.Orders/
    │   ├── Jobs.Nop.Synch/
    │   ├── Jobs.Receipts.Apply/
    │   ├── Jobs.Receipts.Scan/
    │   └── Jobs.Receipts.Tests/
    ├── Nop/
    │   ├── Nop.Contract/
    │   └── Nop.ApiConsumer/
    ├── Receipts/
    │   ├── Receipts.Contract/
    │   ├── Receipts.Core/
    │   ├── Receipts.Gabel/
    │   └── Receipts.Tests/
    └── Pohoda/
        ├── Pohoda.Contract/
        ├── Pohoda.MServer/
        ├── Pohoda.MServer.Tests/
        ├── Pohoda.Server/
        └── Pohoda.Server.Tests/
```

## Moduly a ich funkcionalita

### Common – zdieľané knižnice

- **Common.Contract** – spoločné DTO a kontrakty používané naprieč projektmi: `StockDTO`, `OrderDTO`/`OrderItemDTO`, `PohodaCategoryDto`, `PohodaParameterDto`, generický `IBuilder<TIn,TOut>`/`IOrderBuilder`, XML serializer a LINQ pomocné rozšírenia.
- **Common.Caching** – jednoduchý signál pre invalidáciu cache (`CacheSignal`).
- **Common.Data.Sqlite** – znovupoužiteľný bootstrap SQLite databázy (`SqliteDatabase.OpenAsync` otvorí pripojenie a vykoná zadané schema DDL príkazy) a dve doménovo neviazané tabuľky nad ním: `ProcessedFileTracker` (SHA-256 idempotencia – či už bol daný súbor spracovaný) a `AuditLog` (append-only chronologický audit log).
- **Common.Documents.PdfReader** – statická trieda `PdfRowReader` (nad PdfPig), ktorá rekonštruuje text PDF dokumentu ako vizuálne riadky zhlukovaním slov podľa ich bounding boxu – spoľahlivejšie než poradie textu v raw content streame pri jednoduchých jednostĺpcových tabuľkách (napr. riadky faktúry).
- **Common.Email.EmailService** – rozhranie `IEmailService` (konkrétna implementácia odosielania e-mailov v repozitári zatiaľ chýba, aj keď SMTP konfigurácia existuje).
- **Common.Readers.BaseApiConsumer** – generický, znovupoužiteľný REST klient (`Get/Post/Put/Delete`), základ pre komunikáciu s nopCommerce API.
- **Common.Readers.XmlReader** – generický asynchrónny streamovací čítač XML uzlov, používaný na spracovanie veľkých produktových/objednávkových feedov bez načítania celého súboru do pamäte.

### Expando – integrácia objednávkovej platformy

- **Expando.Contract** – POCO modely objednávok a kontrakty služieb.
- **Expando.Connector** – klient sťahujúci objednávky z Expando XML feedu (konfigurovateľné URL, prístupový token, počet dní spätne).

### Imports – import dodávateľských produktových feedov

- **Import.Hrackys** – konzolová aplikácia streamujúca XML produktový feed dodávateľa Hrackys.cz (aktuálne skôr vo fáze PoC/základného importu).
- **Import.Wiky** – .NET 10 worker service pre import XML feedu veľkoobchodu Wiky.sk vrátane kontroly/vytvárania kategórií v Pohode; obsahuje aj dátový model pre sledovanie balíkov/zásielok (Pohoda order ID, tracking číslo, dopravca, štítok) pre budúcu prepravnú integráciu (časť logiky je aktuálne zakomentovaná/nedokončená).

### Jobs – synchronizačné úlohy

- **Jobs.Expando.Orders** – worker service, ktorý sťahuje objednávky z Expanda, mapuje ich na spoločný `OrderDTO` a vytvára ich v Pohode.
- **Jobs.Nop.Synch** – kľúčová synchronizačná úloha katalógu; postupne vytvára v nopCommerce špecifikačné atribúty, kategórie a produkty na základe dát z Pohody:
  - `CategoryProcessor` – prechádza stromovú štruktúru kategórií v Pohode (filtrovanú whitelistom) a rekurzívne vytvára zodpovedajúce kategórie/podkategórie v nopCommerce (vrátane obrázkov a poradia zobrazenia).
  - `AttributeProcessor` – mapuje parametre Pohody (s prefixom `GN:`) na špecifikačné atribúty nopCommerce.
  - `ProductProcessor` – sťahuje skladové položky z Pohody, rozlišuje jednoduché a variantné/skupinové produkty, vytvára ich v nopCommerce vrátane obrázkov, priradenia kategórií a hodnôt špecifikačných atribútov.
  - Mapovacie pomocné triedy (`DtoToNopStockMapper`, `DtoToNopCategory`, `DtoToNopAttributeMapper`, `ImageHelper`) prevádzajú DTO z Pohody na formát požadovaný nopCommerce API.
- **Jobs.Receipts.Scan** – worker service, ktorý v nastavenom intervale (`Watch:ScanInterval`) prejde priečinok s PDF faktúrami dodávateľa, pre každú novú finálnu faktúru postaví návrh skladovej príjemky a zapíše čitateľný Markdown náhľad do priečinka na schválenie. Do Pohody **nezapisuje** – iba číta (zoznam príjemiek, vyhľadanie skladových kariet). Každý cyklus si v prípade potreby sám spustí mServer a po skončení ho zastaví, ak ho sám spustil.
- **Jobs.Receipts.Apply** – konzolová aplikácia, ktorá po ľudskej kontrole náhľadu vykoná samotný zápis do Pohody. Je to **jediné miesto v celom riešení, ktoré zapisuje príjemky a zakladá skladové karty**, a spúšťa sa vždy ručne:
  ```
  Jobs.Receipts.Apply.exe status            # čakajúce a zlyhané návrhy
  Jobs.Receipts.Apply.exe apply --id <id>   # zápis jedného schváleného návrhu
  Jobs.Receipts.Apply.exe apply --all       # zápis všetkých čakajúcich návrhov
  Jobs.Receipts.Apply.exe reset --id <id>   # návrat návrhu na PendingApproval (opakovaný pokus)
  ```
  Príkazy `status` a `reset` pracujú len s lokálnou SQLite databázou a mServer nepotrebujú.

  Obe aplikácie **musia** mať v `appsettings.json` nastavené rovnaké `Watch:DatabasePath` — cez túto SQLite databázu si `scan` a `apply` odovzdávajú návrhy, takže dve rôzne cesty znamenajú dve nezávislé databázy a `apply` nikdy neuvidí, čo `scan` vytvoril. Preto obe trackované konfigurácie používajú rovnakú absolútnu cestu namiesto pôvodnej relatívnej (tá by sa pri dvoch samostatných `.exe` v rôznych priečinkoch bin/ vyriešila na dva rôzne súbory). Skompilovaný `.exe` spustený mimo Visual Studia navyše nenačíta User Secrets (tie sa načítajú iba v prostredí `Development` — viď sekciu Konfigurácia citlivých údajov nižšie), takže v produkcii treba `Pohoda:GoNoow:Username`/`Pohoda:GoNoow:Password` nastaviť ako environment variables `Pohoda__GoNoow__Username` a `Pohoda__GoNoow__Password`.

### Nop – integrácia s nopCommerce

- **Nop.Contract** – POCO modely (`NopCategory`, `NopStock`, `NopSpecificationAttribute`) a rozhrania služieb pre komunikáciu s nopCommerce.
- **Nop.ApiConsumer** – implementácia klienta nad nopCommerce Web API pluginom: vytváranie/aktualizácia/mazanie produktov (stránkované načítanie s cache), vyhľadávanie kategórií podľa mena s rozlíšením podľa rodičovskej kategórie, priraďovanie produktov ku kategóriám a špecifikačným atribútom.

### Receipts – automatizácia skladových príjemiek z dodávateľských faktúr

Portované zo samostatného riešenia `Gabel.PrijemkaSync`: z PDF faktúry dodávateľa sa vyťažia položky a pripraví sa **návrh** (proposal) skladovej príjemky do Pohody. Návrh sa ukladá lokálne (SQLite) a zapisuje ako Markdown náhľad na schválenie; samotný zápis do Pohody je oddelený, explicitne spúšťaný krok (dry-run → apply). Spustiteľné aplikácie sú Jobs.Receipts.Scan (návrhy) a Jobs.Receipts.Apply (zápis) – viď sekciu Jobs.

- **Receipts.Contract** – doménové modely (`ParsedInvoice`, `InvoiceLine`, `ReceiptProposal`, `ReceiptLineProposal`) a dve rozhrania pre dodávateľské pluginy: `IInvoiceParser` (PDF → `ParsedInvoice`) a `IVariantDetector` (rozpoznanie veľkostného variantu v názve produktu).
- **Receipts.Core** – dodávateľsky nezávislá logika: `ProposalBuilder` (párovanie faktúry na existujúcu príjemku podľa výskytu čísla faktúry v texte dokladu, rozlíšenie existujúcich vs. nových skladových kariet, návrh materskej karty pre varianty), `ProposalApplier` (jediné miesto, ktoré zapisuje do Pohody – založí chýbajúce karty a vytvorí/doplní príjemku; doprava ide vždy ako vedľajšie náklady), `PreviewWriter` (Markdown náhľad) a `ReceiptsStateStore` (SQLite stav nad `Common.Data.Sqlite`: spracované súbory, audit log, cache mapovania kódov, uložené proposaly). Závisí len na `Receipts.Contract`, `Pohoda.Contract` a `Common.Data.Sqlite`.
- **Receipts.Gabel** – prvý dodávateľský plugin: `GabelInvoiceParser` (deterministický regex parser finálnych faktúr `FT n/nnnnnn` nad `Common.Documents.PdfReader`; proforma faktúry preskakuje) a `GabelVariantDetector` (vzory `CM.120` / `135CM` / `Cm 115`, rozsahy ako `73-110CM` sa nepovažujú za variant). Ďalší dodávateľ = nový projekt `Receipts.<Dodávateľ>`, `Receipts.Core` sa nemení.
- **Receipts.Tests** – xUnit testy vrátane pôvodných testov parsera nad reálnymi PDF vzorkami (`TestData/`), testov `ProposalBuilder`/`ProposalApplier` s ručnými fake implementáciami Pohoda služieb a testov SQLite stavu.

#### Pred prvým ostrým `apply`

Zápis do Pohody cez XML agendu (`create`/`update` príjemky, založenie skladovej karty) **zatiaľ nikdy naživo neprebehol** – ani v pôvodnom riešení. Prvé spustenie preto patrí k dozorovaným krokom:

1. Spusti ho, keď je pri tom niekto, kto vidí stav v Pohode, a ideálne najprv proti testovacej účtovnej jednotke.
2. Prvý zápis vždy ako `apply --id <id>` pre jeden konkrétny návrh, nikdy `apply --all`.
3. Ako prvý otestuj návrh typu `AppendLines` (doplnenie existujúcej príjemky) na doklade, o ktorý nie je škoda. `UpdateAsync` posiela **kompletný zoznam riadkov** (existujúce + nové); ak by Pohoda riadky pripájala namiesto nahradenia, každý existujúci riadok by sa zduplikoval. Toto je najdôležitejšia neoverená vlastnosť celého reťazca.
4. `UpdateAsync` zapisuje do hlavičky **dnešný dátum** – over, či to prepísalo pôvodný dátum príjemky a či to vadí.
5. Po zápise skontroluj v Pohode zaradenie novej skladovej karty (`storage`) a jej `orderName` pri veľkostných variantoch; priradenie variantu k materskej karte treba aj tak urobiť ručne, XML agenda naň nemá pole.
6. Over, či je `rateVAT="none"` správny token pre položky oslobodené od DPH.

Body 3 až 6 sú prenesené otvorené otázky z pôvodného riešenia, nie nové riziká zavedené presunom.

### Pohoda – integrácia s ERP systémom

- **Pohoda.Contract** – rozhrania (`ICategoryService`, `IOrderService`, `IParameterService`, `IStockService`, `IReceiptService` (agenda príjemiek: zoznam/vytvorenie/aktualizácia), `IStockCardService` (vyhľadanie skladovej karty podľa kódu dodávateľa, hľadanie podľa časti názvu, založenie karty – prechodné rozhranie, ktoré sa pri migrácii `Pohoda.Server` na `Pohoda.MServer` zlúči do `IStockService`)), abstraktný `AbstractOrderBuilder`, modely `ExistingReceipt`, `ReceiptLineToWrite`, `StockNameMatch` a oficiálne XSD schémy mServer API (Order, Stock, Filter, ...).
- **Pohoda.MServer** – zdieľaný nízkoúrovňový klient pre POHODA mServer (nezávislý od `Pohoda.Server`): `MServerClient` (HTTP komunikácia cez `IHttpClientFactory`, kódovanie Windows-1250, parsovanie `state`/`error` odpovede), `MServerLauncher` (spúšťanie/zastavovanie `pohoda.exe /http start|stop` priamo cez `ProcessStartInfo.ArgumentList`, bez `cmd.exe`) a `ServiceCollectionExtensions.AddPohodaMServer` na registráciu viacerých mServer inštancií súčasne cez keyed DI (konfiguračná sekcia `Pohoda:<instanceKey>:*`).
- **Pohoda.Server** – implementácia komunikácie s lokálnym Pohoda **mServer** (vrátane spúšťania/zastavovania procesu `pohoda.exe /http start|stop` a konverzie kódovania Windows-1250 → UTF-8): `StockService`, `CategoryService`, `OrderService`, `ParameterService`, `Storages` (medzipamäť spracovaných XML odpovedí) a `Mappers` prevádzajúce XML dáta z Pohody na zdieľané DTO z `Common.Contract`. Nové služby ReceiptService a StockCardService (implementácie IReceiptService/IStockCardService) už komunikujú cez zdieľaný Pohoda.MServer.MServerClient a XML obálku Pohoda.Server.Xml.DataPackBuilder; pôvodné služby (StockService, CategoryService, …) zatiaľ ostávajú na pôvodnom MServer.cs.
- **Pohoda.Server.Tests** – xUnit testy ReceiptService a StockCardService proti falošnému mServeru (FakeMServer) – overujú presný tvar XML požiadaviek (namespace elementov requestPrijemka/requestStock, limit/count, dvojito vnorený stockItem, prijemkaAccessoryChargesItem pre dopravu) a parsovanie odpovedí.

## Build a spustenie

- **Build**: otvoriť `src/eCommerce.sln` vo Visual Studio (s podporou pre .NET 10 SDK), alebo `dotnet build src/eCommerce.sln`.
- **Spustenie**: každý z projektov `Import.Hrackys`, `Import.Wiky`, `Jobs.Expando.Orders`, `Jobs.Nop.Synch` je samostatne spustiteľná aplikácia (`dotnet run --project src/<priečinok>/<projekt>/<projekt>.csproj`), konfigurovaná cez vlastný `appsettings.json`. Spoločný orchestračný skript alebo docker-compose v repozitári nie je.
- **CI**: `CI/azure-pipelines.yml` – beží pri push do `main` (NuGet restore → VSBuild → VSTest). Testovacie projekty (`*.Tests.csproj`) sú v riešení prítomné (viď vyššie) a CI pipeline ich detekuje a spúšťa prostredníctvom VSTest kroku.

## Konfigurácia citlivých údajov

appsettings.json v jednotlivých projektoch (`Import.Wiky`, `Jobs.Expando.Orders`, `Jobs.Nop.Synch`) obsahuje len netajné defaulty — hodnoty ako heslá a API kľúče sú v repozitári zámerne prázdne (`""`).

**Lokálny vývoj** — hodnoty sa nastavujú cez .NET User Secrets (mimo repozitára, viazané na `<UserSecretsId>` v danom `.csproj`):

```bash
dotnet user-secrets set "Pohoda:password" "<hodnota>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
```

Vyžadované kľúče, ktoré kód skutočne číta: `Pohoda:username` a `Pohoda:password` (používajú všetky tri aplikácie — `Import.Wiky`, `Jobs.Expando.Orders`, `Jobs.Nop.Synch`). `Jobs.Nop.Synch` naviac vyžaduje `NopCommerce:Username` a `NopCommerce:Password` (cez `NopCommerceSettings`).
`ExpandoKey`, `Email:Password` a `Packeta:ApiPassword` sú v konfigurácii prítomné pre budúcu kompatibilitu, no aktuálne ich žiadny kód nečíta — ich nastavenie je dnes voliteľné, aj tak sa však odporúča držať skutočné hodnoty mimo trackovaného appsettings.json. Jobs.Receipts.Scan a Jobs.Receipts.Apply čítajú Pohoda:GoNoow:Username a Pohoda:GoNoow:Password (v produkcii Pohoda__GoNoow__Username a Pohoda__GoNoow__Password) – ide o inú mServer inštanciu (port 444) než tú, ktorú používajú ostatné joby (Pohoda:username/password, port 5336).

**Produkcia** — rovnaké kľúče sa nastavia ako environment variables na cieľovom stroji, s dvojitou podčiarkovníkovou notáciou pre vnorené sekcie (napr. `Pohoda__password`, `NopCommerce__Password`). `Host.CreateDefaultBuilder` ich automaticky prečíta a prebije nimi appsettings.json.

User Secrets sa načítajú iba v prostredí `Development` — preto nové `launchSettings.json` súbory nastavujú `DOTNET_ENVIRONMENT=Development`. Spustenie skompilovaného `.exe` priamo, alebo `dotnet run --no-launch-profile`, tento environment variable obíde a User Secrets sa nenačítajú.

Nikdy nezapisujte skutočné hodnoty do appsettings.json ani do žiadneho súboru trackovaného gitom.
