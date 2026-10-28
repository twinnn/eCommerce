# Rewrite the README in English with a Slovak translation

Status: ready-for-agent
Blocked by: 01

## Why

Code and docs are in English because the system may later be offered to other businesses; the Distributor's team reads Slovak. The current `README.md` is Slovak and describes integrations that ticket 01 removes, so translating it before 01 would waste work.

## What to do

1. Rewrite `README.md` in English for the solution as it stands after ticket 01: purpose (AI Agents supporting AcEcom's exclusive Gabel distribution; today's working parts are Receipts and the Pohoda mServer client), solution structure, Receipts Scan → Apply flow including the "Before the first live `apply`" checklist, build/run, CI, and secrets configuration (with the renamed `Pohoda:AcEcom:*` keys).
2. Use the vocabulary of `GLOSSARY.md` (Distributor, Supplier, Supplier Invoice, Receipt, Proposal, Agent, Go-Noow…), and link to `GLOSSARY.md` and `docs/adr/`.
3. Create `README.sk.md` as a faithful Slovak translation. Keep code identifiers, commands and config keys untranslated; use Pohoda's Slovak terms (príjemka, skladová karta, prijatá faktúra) where the English uses the glossary term.
4. Put a language switch on the first line of both files: `English | [Slovensky](README.sk.md)` and `[English](README.md) | Slovensky`.

## Done when

Both files describe the same content, every path and command in them exists, and nothing mentions Expando, nopCommerce, Wiky or Hrackys.
