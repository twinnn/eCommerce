using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Příjemka (goods receipt) agenda operations over <see cref="MServerClient"/>. Ported from
/// Gabel.PrijemkaSync's <c>PrijemkaClient</c>.
///
/// IMPORTANT — residual risk: <see cref="ListRecentAsync"/> is live-verified against production data;
/// <see cref="CreateAsync"/>/<see cref="UpdateAsync"/> wire shapes were assembled from the published
/// XSDs plus live schema-validation errors, but no write has been executed against a live mServer
/// yet. The first real write stays gated behind explicit human approval (spec §6).
/// </summary>
public sealed class ReceiptService : IReceiptService
{
    /// <summary>Note written on the accessory-charge line that carries the invoice's freight amount.</summary>
    public const string FreightNote = "Doprava (vedľajšie náklady)";

    private readonly MServerClient _client;
    private readonly MServerConfiguration _config;

    public ReceiptService(MServerClient client, MServerConfiguration config)
    {
        _client = client;
        _config = config;
    }

    public async Task<IReadOnlyList<ExistingReceipt>> ListRecentAsync(DateOnly since, int limit, CancellationToken ct = default)
    {
        var request = new XElement(Lst + "listPrijemkaRequest",
            new XAttribute("version", "2.0"),
            new XAttribute("prijemkaVersion", "2.0"),
            // ftr:limitType is a complex type (idFrom/count), not plain text — confirmed against a
            // live mServer validation error ("Text is not allowed... element 'limit'").
            new XElement(Lst + "limit", new XElement(Ftr + "count", limit)),
            // The element name "requestPrijemka" is declared in list.xsd (lst) even though its
            // *type* is ftr:requestPrijemkaType — confirmed against a live mServer schema-validation
            // error ("Expecting: {list.xsd}requestPrijemka").
            new XElement(Lst + "requestPrijemka",
                new XElement(Ftr + "filter",
                    new XElement(Ftr + "dateFrom", since.ToString("yyyy-MM-dd")))));

        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Zoznam príjemiek od {since:yyyy-MM-dd}", request);
        var result = await _client.SendOrThrowAsync(dataPack, ct);

        return result.Document.Descendants()
            .Where(e => e.Name.LocalName == "prijemka")
            .Select(ParseExistingReceipt)
            .ToList();
    }

    private static ExistingReceipt ParseExistingReceipt(XElement prijemkaEl)
    {
        var header = prijemkaEl.Elements().FirstOrDefault(e => e.Name.LocalName == "prijemkaHeader");
        var id = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value ?? string.Empty;
        var number = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "number")?.Value ?? string.Empty;
        var text = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? string.Empty;

        var dateText = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "date")?.Value;
        DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);

        var detail = prijemkaEl.Elements().FirstOrDefault(e => e.Name.LocalName == "prijemkaDetail");
        var lines = (detail?.Elements().Where(e => e.Name.LocalName == "prijemkaItem") ?? [])
            .Select(item =>
            {
                var stockCode = item.Descendants()
                    .Where(e => e.Name.LocalName is "code" or "ids")
                    .Select(e => e.Value)
                    .FirstOrDefault() ?? string.Empty;
                var itemText = item.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? string.Empty;
                var unit = item.Elements().FirstOrDefault(e => e.Name.LocalName == "unit")?.Value ?? string.Empty;

                var qtyText = item.Elements().FirstOrDefault(e => e.Name.LocalName == "quantity")?.Value;
                var qtyOk = decimal.TryParse(qtyText, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);

                var discountText = item.Elements().FirstOrDefault(e => e.Name.LocalName == "discountPercentage")?.Value;
                var discountOk = decimal.TryParse(discountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var discount);

                var priceText = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "unitPrice")?.Value;
                var priceOk = decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out var unitPrice);

                var hasUnparsedNumbers = !qtyOk || !discountOk || !priceOk;

                return new ExistingReceiptLine(stockCode, itemText, unit, qty, unitPrice, discount, hasUnparsedNumbers);
            })
            .ToList();

        return new ExistingReceipt(id, number, text, lines, date);
    }

    public async Task<string> CreateAsync(string headerText, DateOnly date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight, CancellationToken ct = default)
    {
        var prijemka = BuildPrijemkaElement(headerId: null, headerText, date, lines, freight);
        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Nová príjemka: {headerText}", prijemka);
        var result = await _client.SendOrThrowAsync(dataPack, ct);

        return result.Document.Descendants()
            .Where(e => e.Name.LocalName == "number")
            .Select(e => e.Value)
            .FirstOrDefault() ?? headerText;
    }

    public async Task UpdateAsync(string existingId, string headerText, IReadOnlyList<ReceiptLineToWrite> allLines, decimal? freight, CancellationToken ct = default)
    {
        var prijemka = BuildPrijemkaElement(existingId, headerText, date: null, allLines, freight);
        var dataPack = DataPackBuilder.Wrap(_config.Ico, $"Doplnenie príjemky {existingId}: {headerText}", prijemka);
        await _client.SendOrThrowAsync(dataPack, ct);
    }

    private static XElement BuildPrijemkaElement(string? headerId, string headerText, DateOnly? date, IReadOnlyList<ReceiptLineToWrite> lines, decimal? freight)
    {
        var headerChildren = new List<XElement>();
        if (headerId is not null)
        {
            headerChildren.Add(new XElement(Prj + "id", headerId));
        }

        headerChildren.Add(new XElement(Prj + "date", (date ?? DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd")));
        headerChildren.Add(new XElement(Prj + "text", headerText));

        var detailItems = lines.Select(BuildLineElement).ToList();
        if (freight is > 0)
        {
            detailItems.Add(BuildFreightElement(freight.Value));
        }

        return new XElement(Prj + "prijemka",
            new XAttribute("version", "2.0"),
            new XElement(Prj + "prijemkaHeader", headerChildren),
            new XElement(Prj + "prijemkaDetail", detailItems));
    }

    private static XElement BuildLineElement(ReceiptLineToWrite line) =>
        new(Prj + "prijemkaItem",
            new XElement(Prj + "text", line.Text),
            new XElement(Prj + "quantity", line.Quantity.ToString(CultureInfo.InvariantCulture)),
            new XElement(Prj + "unit", line.Unit),
            new XElement(Prj + "payVAT", "false"),
            new XElement(Prj + "rateVAT", "none"),
            new XElement(Prj + "discountPercentage", line.DiscountPercent.ToString(CultureInfo.InvariantCulture)),
            new XElement(Prj + "homeCurrency", new XElement(Typ + "unitPrice", line.UnitPrice.ToString(CultureInfo.InvariantCulture))),
            // A doubly-nested "stockItem": the OUTER element is prijemka.xsd's own field (type
            // typ:stockItemType), which requires an INNER child ALSO named "stockItem" (this one
            // from type.xsd, type typ:stockRefType, holding the actual reference via typ:ids) —
            // confirmed against the published type.xsd after a live schema-validation error showed
            // "Expecting: {type.xsd}stockItem" as the content of prijemka.xsd's stockItem element.
            new XElement(Prj + "stockItem", new XElement(Typ + "stockItem", new XElement(Typ + "ids", line.PohodaStockCode))));

    // prijemkaAccessoryChargesItemType is NOT the same shape as prijemkaItemType — confirmed against
    // the published prijemka.xsd after a live schema-validation error on "text" (unexpected here).
    // It has no text/unit/stockItem at all; the description goes in "note" instead.
    private static XElement BuildFreightElement(decimal freightAmount) =>
        new(Prj + "prijemkaAccessoryChargesItem",
            new XElement(Prj + "note", FreightNote),
            new XElement(Prj + "quantity", "1"),
            new XElement(Prj + "payVAT", "false"),
            new XElement(Prj + "rateVAT", "none"),
            new XElement(Prj + "homeCurrency", new XElement(Typ + "unitPrice", freightAmount.ToString(CultureInfo.InvariantCulture))));
}
