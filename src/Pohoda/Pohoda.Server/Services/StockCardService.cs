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
