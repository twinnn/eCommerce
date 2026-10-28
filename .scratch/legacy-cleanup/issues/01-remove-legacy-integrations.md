# Remove legacy integrations and rename the Pohoda instance key to AcEcom

Status: done

## Why

The solution is being refocused on AI Agents for the Distributor (AcEcom s.r.o., exclusive Gabel distribution in SK/CZ); see `GLOSSARY.md` and `docs/adr/`. The Expando, supplier-feed import and nopCommerce integrations belong to an older, unrelated setup: the Distributor's e-shop is Go-Noow on NextCom, not nopCommerce. Git history keeps the old code.

## What to do

1. Remove these 11 projects from `src/eCommerce.sln` and delete their folders:
   - `Expando/Expando.Contract`, `Expando/Expando.Connector`, `Jobs/Jobs.Expando.Orders`
   - `Imports/Import.Wiky`, `Imports/Import.Hrackys`
   - `Nop/Nop.Contract`, `Nop/Nop.ApiConsumer`, `Jobs/Jobs.Nop.Synch`
   - `Common/Readers/Common.Readers.BaseApiConsumer`, `Common/Readers/Common.Readers.BaseXmlReader` (folder `Common.Readers.XmlReader`), `Common/Email/Common.Email.EmailService`

   Project references confirm nothing outside this set uses them. Keep `Common.Contract`, `Common.Caching`, everything under `Pohoda/`, `Receipts/` and `Jobs/Jobs.Receipts.*`.
2. Resolve the duplicate `Common.Contract.csproj` (there is one at `src/Common/Contract/` and one at `src/Common/Contract/Common.Contract/`): keep the one the solution and `Pohoda.Contract`/`Pohoda.Server` reference, delete the other.
3. Rename the Pohoda **instance key** `GoNoow` → `AcEcom` (Pohoda belongs to AcEcom; GoNoow is only a warehouse segment): `PohodaInstance.Key` in `Jobs.Receipts.Scan` and `Jobs.Receipts.Apply`, the `Pohoda:GoNoow` config sections in both `appsettings.json`, the keyed-DI keys and config keys in `Pohoda.MServer.Tests` and `Pohoda.Server.Tests`, and the doc comments that name the instance.
   - **Do not rename** `ConfigName` (`"GoNoow"`, the mServer configuration name inside Pohoda), `DefaultStorageCode` (`"GoNoow"`, the real warehouse segment), the `"GoNoow/Palice"` storage path in `StockService`, or the storage assertion in `StockCardServiceTests`. Those are real Pohoda values.
4. Delete the untracked `build_output.log` at the repo root (`*.log` is already git-ignored, so this is just tidying).

## Done when

- `dotnet build src/eCommerce.sln` succeeds and `dotnet test src/eCommerce.sln` passes.
- `grep -rn "Expando\|nopCommerce\|Nop\.\|Wiky\|Hrackys" src` finds nothing outside `bin/`/`obj/`.
- The only remaining `GoNoow` strings in `src` are the Pohoda values listed under step 3.

## Heads-up for production

The renamed key changes the secret names: `Pohoda__GoNoow__Username`/`Pohoda__GoNoow__Password` become `Pohoda__AcEcom__Username`/`Pohoda__AcEcom__Password`, and User Secrets need re-setting under `Pohoda:AcEcom:*`. Say so in the commit message.

## Comments

**Done** in commit fd00a9b on branch `cleanup/legacy-integrations`. Build is clean and 120/120 tests pass. Code review (Standards + Spec) found no hard violations and no missing requirements. Follow-ups:
- `README.md` still documents the removed projects and the old `Pohoda:GoNoow:*` secret names, so its operator instructions are wrong until ticket 02 lands.
- Judgement call: `PohodaInstance` (key + doc comment) and the mServer-unavailable log message are duplicated across Jobs.Receipts.Scan and Jobs.Receipts.Apply (that duplication predates this ticket); a shared home would make the next rename a single edit.
