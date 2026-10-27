# Zabezpečenie citlivých údajov v appsettings.json — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Odstrániť naživo uložené heslá/tokeny z troch `appsettings.json` súborov v riešení `eCommerce`, nahradiť ich bezpečným mechanizmom (.NET User Secrets pre lokálny dev, environment variables pre produkciu) a zrotovať už unikuté prístupové údaje.

**Architecture:** .NET `Host.CreateDefaultBuilder` má vstavanú podporu pre viacúrovňové configuration providers (appsettings.json → appsettings.{Environment}.json → User Secrets [len Development] → Environment Variables → Command line). Aktuálne to nefunguje, lebo každý `Program.cs` explicitne pridáva `appsettings.json` ešte raz *po* týchto krokoch, čím ho posúva na koniec reťazca a jeho hodnoty prebijú env vars aj user secrets. Plán opravuje toto poradie, zapína User Secrets pre lokálny vývoj, čistí súbory v repozitári a dokumentuje produkčný override cez premenné prostredia. Rotácia unikutých hesiel je samostatný, prioritný krok mimo kódu.

**Tech Stack:** .NET 6 (Generic Host / Worker Service), `Microsoft.Extensions.Configuration`, `dotnet user-secrets`, Git.

**Spec:** Táto sekcia zhŕňa požiadavku používateľa: "naplánuj zabezpečenie citlivých údajov v rámci appsettings.json skrz celý eCommerce solution." Zdrojový stav bol zistený prieskumom repozitára (pozri zistenia nižšie) — žiadny predchádzajúci spec dokument neexistuje.

### Zistený súčasný stav (dôkazy)

Tri appsettings.json súbory obsahujú naživo uložené secrets, committnuté do vzdialeného Azure DevOps repozitára (`origin = https://dev.azure.com/CustomDev/eCommerce`) už od `a909046` (initial commit):

- `src/Imports/Import.Wiky/appsettings.json`
- `src/Jobs/Jobs.Expando.Orders/appsettings.json`
- `src/Jobs/Jobs.Nop.Synch/appsettings.json` (naviac obsahuje `NopCommerce` blok)

Exponované kľúče vo všetkých troch (Nop.Synch naviac obsahuje `NopCommerce`):
`ExpandoKey`, `Pohoda:username`, `Pohoda:password`, `Email:Password`, `Packeta:ApiPassword`, (v Nop.Synch aj `NopCommerce:Password`).

Bug v precedencii configu — vo všetkých troch `Program.cs` (`src/Jobs/Jobs.Nop.Synch/Program.cs:26`, `src/Jobs/Jobs.Expando.Orders/Program.cs:19`, `src/Imports/Import.Wiky/Program.cs:14`):

```csharp
.ConfigureAppConfiguration(app => { app.AddJsonFile("appsettings.json"); })
```

Toto pridáva `appsettings.json` ešte raz *po* tom, čo `Host.CreateDefaultBuilder` už pridal appsettings.json, appsettings.{Environment}.json, User Secrets, Environment Variables a command-line args (v tomto poradí). Keďže .NET configuration provider naposledy pridaný vyhráva, táto redundantná línia spôsobuje, že hodnoty z `appsettings.json` **prebijú aj environment variables aj user secrets** — takže dnes by ani nastavenie env var nefungovalo ako override. Toto treba opraviť ako predpoklad pre zvyšok plánu.

`Import.Hrackys` (`src/Imports/Import.Hrackys/Program.cs`) nepoužíva Host builder ani appsettings.json — nevyžaduje zmeny.

`Jobs.Expando.Orders.csproj` už má `<UserSecretsId>` a funkčný `Properties/launchSettings.json` s `DOTNET_ENVIRONMENT=Development` — použije sa ako vzor pre ostatné dva projekty.

## Global Constraints

- Nikdy nezapisovať skutočné (rotované ani staré) hodnoty secrets do súborov trackovaných gitom — ani do appsettings.json, ani do dokumentácie, ani do commit správ.
- appsettings.json zostáva v repozitári (obsahuje netajné nastavenia ako `LabelsLocation`, `IdStorageFile`, `Database:Path`, `NopCommerce:BaseUrl`, `Serilog`) — mení sa len tak, že tajné hodnoty sa nahradia prázdnym reťazcom `""`.
- Force-push / prepisovanie git histórie sa nesmie spustiť bez explicitného potvrdenia používateľa bezprostredne pred behom (Task 7 je vedomě oddelený a nespúšťa sa automaticky).
- Rotácia hesiel (Task 1) je manuálny krok mimo tohto repozitára — vykonáva ho používateľ vo webových/administrátorských rozhraniach Pohoda/nopCommerce/Packeta/e-mailu/Expanda.

---

## Task 1: Rotácia unikutých prístupových údajov (manuálne, najvyššia priorita)

**Files:** žiadne (operačný krok mimo repozitára — robí ho používateľ, nie agent)

Tento krok má prebehnúť **pred alebo súbežne** s kódovými zmenami nižšie, pretože súčasné hodnoty sú už verejné v git histórii vzdialeného repozitára a samotná zmena kódu ich neanuluje.

- [ ] **Krok 1: Zrotovať Pohoda prístupové údaje** — v Pohoda/mServer administrácii zmeniť heslo účtu `admin` (aktuálne uniknuté heslo je v appsettings.json ako `Pohoda:password`).
- [ ] **Krok 2: Zrotovať e-mailové heslo** — zmeniť heslo schránky `noreply@azetcool.com` (uniknuté ako `Email:Password`) u poskytovateľa e-mailu.
- [ ] **Krok 3: Zrotovať Packeta API heslo** — v Packeta klientskej zóne vygenerovať nový `ApiPassword` a zneplatniť starý.
- [ ] **Krok 4: Zrotovať nopCommerce API heslo** — zmeniť heslo účtu `peter.pojtek@azetcool.com` použitého pre nopCommerce Web API (`NopCommerce:Password` v `Jobs.Nop.Synch/appsettings.json`).
- [ ] **Krok 5: Zrotovať Expando access key** — v Expando administrácii vygenerovať nový `ExpandoKey` a zneplatniť starý (starý kľúč — pozri git históriu appsettings.json pred touto zmenou, alebo Pohoda/Expando administráciu).
- [ ] **Krok 6: Zapísať nové hodnoty výhradne do User Secrets / produkčných env vars** (pozri Task 3 a Task 6) — nikdy nie do súborov v repozitári.

---

## Task 2: Oprava precedencie configuration providers v Program.cs

**Files:**
- Modify: `src/Jobs/Jobs.Nop.Synch/Program.cs:26`
- Modify: `src/Jobs/Jobs.Expando.Orders/Program.cs:19`
- Modify: `src/Imports/Import.Wiky/Program.cs:14`

**Interfaces:**
- Produces: funkčnú precedenciu configu (`appsettings.json` < `appsettings.{Environment}.json` < User Secrets < Environment Variables < command line), na ktorej stavajú Task 3 a Task 6.

`Host.CreateDefaultBuilder(args)` už appsettings.json pridáva interne. Redundantné volanie `app.AddJsonFile("appsettings.json")` v používateľskom `ConfigureAppConfiguration` ho len znova pridá na koniec reťazca a tým prebije všetky neskoršie zdroje. Riešenie: túto redundantnú líniu odstrániť; `ConfigureAppConfiguration` delegát v Program.cs už nie je potrebný vôbec (appsettings.json sa načíta automaticky).

- [ ] **Krok 1: Upraviť `src/Jobs/Jobs.Nop.Synch/Program.cs`**

Pôvodne (riadok 25-27):
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(app => { app.AddJsonFile("appsettings.json"); })
    .ConfigureServices((context, services) =>
```

Zmeniť na:
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
```

- [ ] **Krok 2: Upraviť `src/Jobs/Jobs.Expando.Orders/Program.cs`**

Pôvodne (riadok 18-20):
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(app => { app.AddJsonFile("appsettings.json"); })
    .ConfigureServices((context, services) =>
```

Zmeniť na:
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
```

- [ ] **Krok 3: Upraviť `src/Imports/Import.Wiky/Program.cs`**

Pôvodne (riadok 13-15):
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(app => { app.AddJsonFile("appsettings.json"); })
    .ConfigureServices((context, services) =>
```

Zmeniť na:
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
```

- [ ] **Krok 4: Odstrániť teraz nepoužívaný using, ak zostal osamotený**

Vo všetkých troch súboroch skontrolovať, či `using Microsoft.Extensions.Configuration;` ešte niekde inde v súbore treba (napr. `Jobs.Nop.Synch/Program.cs` ho používa aj pre `ConfigureFromConfigurationOptions` na riadku 86 — tam ho **ponechať**). V `Jobs.Expando.Orders/Program.cs` a `Import.Wiky/Program.cs` skontrolovať, či sa `Microsoft.Extensions.Configuration` používa inde; ak nie, using odstrániť (build s `TreatWarningsAsErrors` by inak nemal padnúť, keďže nevyužitý using je len IDE-hint, ale pre čistotu ho odstrániť).

- [ ] **Krok 5: Overiť build**

Run: `dotnet build src/eCommerce.sln -c Debug`
Expected: Build succeeded, 0 Error(s).

- [ ] **Krok 6: Manuálne overiť, že env var teraz prebíja appsettings.json**

Dočasne (bez commitovania) spustiť:
```bash
DOTNET_ENVIRONMENT=Development ExpandoKey=test-override-value dotnet run --project src/Jobs/Jobs.Expando.Orders/Jobs.Expando.Orders.csproj
```
V Serilog výstupe (alebo dočasným `Console.WriteLine(context.Configuration["ExpandoKey"])` pred `services.AddHostedService`, ktorý sa hneď potom odstráni) potvrdiť, že sa použije `test-override-value`, nie hodnota z appsettings.json. Po overení dočasný debug výpis odstrániť.

- [ ] **Krok 7: Commit**

```bash
git add src/Jobs/Jobs.Nop.Synch/Program.cs src/Jobs/Jobs.Expando.Orders/Program.cs src/Imports/Import.Wiky/Program.cs
git commit -m "fix: remove redundant appsettings.json reload so env vars/user secrets can override it"
```

---

## Task 3: Zapnúť .NET User Secrets pre lokálny vývoj

**Files:**
- Modify: `src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj`
- Modify: `src/Imports/Import.Wiky/Import.Wiky.csproj`
- Create: `src/Jobs/Jobs.Nop.Synch/Properties/launchSettings.json`
- Create: `src/Imports/Import.Wiky/Properties/launchSettings.json`
- Reference vzor (bez zmeny): `src/Jobs/Jobs.Expando.Orders/Properties/launchSettings.json` (už existuje a je funkčný)

**Interfaces:**
- Consumes: opravenú precedenciu z Task 2.
- Produces: `DOTNET_ENVIRONMENT=Development` pri behu z IDE/`dotnet run`, čo aktivuje User Secrets provider definovaný pomocou `<UserSecretsId>`.

`Jobs.Expando.Orders.csproj` už má `<UserSecretsId>dotnet-Jobs.Expando.Orders-89135500-0AED-4856-84F2-AEBCD861F954</UserSecretsId>` — rovnaký vzor sa doplní do zvyšných dvoch projektov.

- [ ] **Krok 1: Vygenerovať User Secrets ID pre Jobs.Nop.Synch**

Run: `dotnet user-secrets init --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj`

Tento príkaz sám doplní do `Jobs.Nop.Synch.csproj` nový unikátny `<UserSecretsId>` GUID do `<PropertyGroup>`.

- [ ] **Krok 2: Vygenerovať User Secrets ID pre Import.Wiky**

Run: `dotnet user-secrets init --project src/Imports/Import.Wiky/Import.Wiky.csproj`

- [ ] **Krok 3: Overiť, že sa csproj zmenil ako očakávané**

Skontrolovať, že `src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj` a `src/Imports/Import.Wiky/Import.Wiky.csproj` teraz obsahujú `<UserSecretsId>...</UserSecretsId>` v `<PropertyGroup>` (rovnaký tvar ako v `Jobs.Expando.Orders.csproj`).

- [ ] **Krok 4: Vytvoriť `src/Jobs/Jobs.Nop.Synch/Properties/launchSettings.json`**

```json
{
  "profiles": {
    "Jobs.Nop.Synch": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Krok 5: Vytvoriť `src/Imports/Import.Wiky/Properties/launchSettings.json`**

```json
{
  "profiles": {
    "Import.Wiky": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Krok 6: Naplniť lokálne User Secrets skutočnými (už rotovanými z Task 1) hodnotami**

Pre každý z troch projektov (`Jobs.Nop.Synch`, `Jobs.Expando.Orders`, `Import.Wiky`) spustiť (hodnoty nižšie sú príklad tvaru kľúča, nie skutočné secrets — vyplniť novými hodnotami z Task 1):

```bash
dotnet user-secrets set "ExpandoKey" "<novy-expando-key>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
dotnet user-secrets set "Pohoda:username" "<pohoda-username>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
dotnet user-secrets set "Pohoda:password" "<nove-pohoda-heslo>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
dotnet user-secrets set "Email:Password" "<nove-email-heslo>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
dotnet user-secrets set "Packeta:ApiPassword" "<nove-packeta-heslo>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
dotnet user-secrets set "NopCommerce:Password" "<nove-nopcommerce-heslo>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
```

Rovnaké príkazy (bez `NopCommerce:Password`) zopakovať s `--project src/Jobs/Jobs.Expando.Orders/Jobs.Expando.Orders.csproj` a s `--project src/Imports/Import.Wiky/Import.Wiky.csproj`.

Tieto hodnoty sa ukladajú do `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` **mimo repozitára** — nikdy sa negitujú.

- [ ] **Krok 7: Overiť lokálny beh**

Run: `dotnet run --project src/Jobs/Jobs.Expando.Orders/Jobs.Expando.Orders.csproj`
Expected: aplikácia nabehne bez chyby autentifikácie voči Pohoda mServer (log potvrdí úspešné spojenie, nie 401).

- [ ] **Krok 8: Commit**

```bash
git add src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj src/Imports/Import.Wiky/Import.Wiky.csproj src/Jobs/Jobs.Nop.Synch/Properties/launchSettings.json src/Imports/Import.Wiky/Properties/launchSettings.json
git commit -m "feat: enable User Secrets for local dev config in Jobs.Nop.Synch and Import.Wiky"
```

---

## Task 4: Odstrániť secrets zo súborov appsettings.json v repozitári

**Files:**
- Modify: `src/Imports/Import.Wiky/appsettings.json`
- Modify: `src/Jobs/Jobs.Expando.Orders/appsettings.json`
- Modify: `src/Jobs/Jobs.Nop.Synch/appsettings.json`

**Interfaces:**
- Consumes: User Secrets/env vars z Task 3, ktoré teraz (vďaka Task 2) hodnoty z appsettings.json prebijú.

- [ ] **Krok 1: Nahradiť tajné hodnoty v `src/Imports/Import.Wiky/appsettings.json`**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "System": "Warning"
      }
    }
  },
  "ExpandoKey": "",
  "Pohoda": {
    "username": "",
    "password": ""
  },
  "Email": {
    "Login": "noreply@azetcool.com",
    "Recipient": "peter@customdev.sk",
    "Cc": "pojtek@gmail.com",
    "Password": ""
  },
  "Packeta": {
    "ApiPassword": "",
    "LabelsLocation": "..\\..\\Labels"
  },
  "IdStorageFile": "..\\..\\lastId.txt",
  "Database": {
    "Path": "..\\..\\database.db"
  }
}
```

- [ ] **Krok 2: Nahradiť tajné hodnoty v `src/Jobs/Jobs.Expando.Orders/appsettings.json`**

Rovnaká zmena ako v Kroku 1 (identická štruktúra súboru).

- [ ] **Krok 3: Nahradiť tajné hodnoty v `src/Jobs/Jobs.Nop.Synch/appsettings.json`**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "System": "Warning"
      }
    }
  },
  "ExpandoKey": "",
  "Pohoda": {
    "username": "",
    "password": ""
  },
  "Email": {
    "Login": "noreply@azetcool.com",
    "Recipient": "peter@customdev.sk",
    "Cc": "pojtek@gmail.com",
    "Password": ""
  },
  "Packeta": {
    "ApiPassword": "",
    "LabelsLocation": "..\\..\\Labels"
  },
  "IdStorageFile": "..\\..\\lastId.txt",
  "Database": {
    "Path": "..\\..\\database.db"
  },
  "NopCommerce": {
    "BaseUrl": "http://20.113.184.46:8088/api/",
    "Username": "",
    "Password": ""
  }
}
```

Poznámka: `NopCommerce:BaseUrl` a `NopCommerce:Username`(login, nie heslo) ostávajú netajné konfiguračné údaje — pre konzistentnosť a jednoduchosť je `Username` tiež presunutý do User Secrets/env vars, keďže identifikuje konkrétny API účet.

- [ ] **Krok 4: Overiť, že aplikácie stále bežia s hodnotami z User Secrets (nie prázdnymi)**

Run: `dotnet run --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj`
Expected: rovnaké správanie ako pred zmenou (žiadna degradácia) — appsettings.json už neobsahuje reálne hodnoty, ale User Secrets z Task 3 Krok 6 ich doplnia.

- [ ] **Krok 5: Commit**

```bash
git add src/Imports/Import.Wiky/appsettings.json src/Jobs/Jobs.Expando.Orders/appsettings.json src/Jobs/Jobs.Nop.Synch/appsettings.json
git commit -m "security: strip live secrets from committed appsettings.json files"
```

---

## Task 5: Ochrana pred budúcim úniku — .gitignore a pre-commit povedomie

**Files:**
- Modify: `.gitignore`

**Interfaces:** žiadne (statická konfigurácia repozitára).

- [ ] **Krok 1: Doplniť do `.gitignore` vzory pre lokálne secret-overlay súbory**

Pridať na koniec `.gitignore` novú sekciu:

```
# Local secret overrides — never commit real credentials.
# appsettings.json in each project must only ever contain empty/non-secret
# placeholder values; real values live in dotnet user-secrets (dev) or
# environment variables (production).
appsettings.Development.json
appsettings.Local.json
appsettings.*.local.json
```

- [ ] **Krok 2: Overiť, že appsettings.json (bez sufixu) NIE JE v tomto zozname**

Skontrolovať, že `appsettings.json` (base súbor, teraz bez secrets vďaka Task 4) zostáva trackovaný gitom — musí sa naďalej commitovať, obsahuje len netajné defaulty.

- [ ] **Krok 3: Commit**

```bash
git add .gitignore
git commit -m "chore: gitignore local appsettings overlay files"
```

---

## Task 6: Zdokumentovať produkčný override mechanizmus

**Files:**
- Modify: `README.md`

**Interfaces:** žiadne.

- [ ] **Krok 1: Doplniť do `README.md` sekciu s presným návodom, nahradiť existujúcu bezpečnostnú poznámku**

Nájsť sekciu `## ⚠️ Bezpečnostná poznámka` na konci `README.md` a nahradiť ju:

```markdown
## Konfigurácia citlivých údajov

appsettings.json v jednotlivých projektoch (`Import.Wiky`, `Jobs.Expando.Orders`, `Jobs.Nop.Synch`) obsahuje len netajné defaulty — hodnoty ako heslá a API kľúče sú v repozitári zámerne prázdne (`""`).

**Lokálny vývoj** — hodnoty sa nastavujú cez .NET User Secrets (mimo repozitára, viazané na `<UserSecretsId>` v danom `.csproj`):

```bash
dotnet user-secrets set "Pohoda:password" "<hodnota>" --project src/Jobs/Jobs.Nop.Synch/Jobs.Nop.Synch.csproj
```

Vyžadované kľúče pre `Import.Wiky` a `Jobs.Expando.Orders`: `ExpandoKey`, `Pohoda:username`, `Pohoda:password`, `Email:Password`, `Packeta:ApiPassword`.
`Jobs.Nop.Synch` naviac vyžaduje: `NopCommerce:Username`, `NopCommerce:Password`.

**Produkcia** — rovnaké kľúče sa nastavia ako environment variables na cieľovom stroji, s dvojitou podčiarkovníkovou notáciou pre vnorené sekcie (napr. `Pohoda__password`, `NopCommerce__Password`). `Host.CreateDefaultBuilder` ich automaticky prečíta a prebije nimi appsettings.json.

Nikdy nezapisujte skutočné hodnoty do appsettings.json ani do žiadneho súboru trackovaného gitom.
```

- [ ] **Krok 2: Odstrániť pôvodný odsek `## ⚠️ Bezpečnostná poznámka`, ktorý popisoval problém (teraz už vyriešený) — nahradený sekciou z Kroku 1.**

- [ ] **Krok 3: Commit**

```bash
git add README.md
git commit -m "docs: document secret configuration via user-secrets and env vars"
```

---

## Task 7 (voliteľné, vyžaduje explicitné potvrdenie pred spustením): Vyčistenie git histórie

**Files:** žiadne (operuje na `.git` histórii celého repozitára)

⚠️ **Nespúšťať automaticky.** Ide o deštruktívnu operáciu — prepisuje históriu všetkých commitov, ktoré kedy obsahovali `appsettings.json` s hodnotami secrets (siaha až po `a909046`, initial commit), vyžaduje force-push do `origin` (Azure DevOps) a **všetci ostatní prispievatelia si musia repozitár znovu naklonovať** (ich lokálne branch history sa rozíde). Vykonať len na explicitnú žiadosť používateľa, po dokončení Task 1 (rotácia — bez nej je scrubbing histórie zbytočný, staré heslá by aj tak boli neplatné, ale scrubbing má zmysel navyše pre username/login hodnoty a štruktúru, ktoré rotácia nerieši).

- [ ] **Krok 1: Overiť, že Task 1 (rotácia) je hotový** — bez rotácie nemá scrubbing zmysel, keďže secrets už mohli byť stiahnuté kýmkoľvek s prístupom k repozitáru pred scrubom.
- [ ] **Krok 2: Upozorniť všetkých spolupracovníkov vopred**, že príde force-push a budú si musieť repozitár znova naklonovať (nie len `git pull`).
- [ ] **Krok 3: Nainštalovať `git-filter-repo`** (odporúčaný nástroj namiesto zastaraného BFG/`filter-branch`):

Run: `pip install git-filter-repo` alebo `winget install git-filter-repo` (podľa dostupnosti na stroji).

- [ ] **Krok 4: Spustiť filter-repo na vyčistenie troch appsettings.json súborov z celej histórie**

Run (na čerstvom `--bare` klone, nie na pracovnej kópii):
```bash
git clone --bare https://dev.azure.com/CustomDev/eCommerce/_git/eCommerce eCommerce-scrub.git
cd eCommerce-scrub.git
git filter-repo --path src/Imports/Import.Wiky/appsettings.json --path src/Jobs/Jobs.Expando.Orders/appsettings.json --path src/Jobs/Jobs.Nop.Synch/appsettings.json --invert-paths --force
```

Toto úplne odstráni tieto tri súbory zo všetkých historických commitov (nie len z posledného) — po scrube sa Task 4 verzia týchto súborov (bez secrets) musí znova pridať ako nový commit na vrchol.

- [ ] **Krok 5: Force-push vyčistenej histórie**

Run: `git push --force --all origin` a `git push --force --tags origin`

- [ ] **Krok 6: Informovať tím, aby zmazali svoje lokálne kópie a naklonovali repozitár nanovo.**

---

## Self-Review Summary

- **Spec coverage**: rotácia (Task 1), oprava precedencie configu (Task 2), User Secrets pre dev (Task 3), odstránenie secrets zo súborov (Task 4), .gitignore ochrana (Task 5), dokumentácia produkčného mechanizmu (Task 6), voliteľné vyčistenie histórie (Task 7) — pokrývajú "appsettings.json skrz celý eCommerce solution" (všetky 3 relevantné projekty; `Import.Hrackys` overený, že nepotrebuje zmeny).
- **Placeholder scan**: žiadne "TBD"/"handle appropriately" zostatky — každý krok má presný súbor, presný diff/JSON alebo presný shell príkaz.
- **Type/key consistency**: kľúče `ExpandoKey`, `Pohoda:username/password`, `Email:Password`, `Packeta:ApiPassword`, `NopCommerce:Username/Password` sú konzistentné naprieč Task 3 (user-secrets set), Task 4 (appsettings.json) a Task 6 (dokumentácia).
