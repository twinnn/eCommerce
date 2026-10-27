using Pohoda.Contract.Models;
using Pohoda.Server.Services;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Tests;

public class ReceiptServiceTests
{
    private static (FakeMServer Server, ReceiptService Service) Create()
    {
        var server = new FakeMServer();
        return (server, new ReceiptService(server.Client, server.Config));
    }

    private const string OneReceiptListing = """
        <lst:listPrijemka version="2.0" prijemkaVersion="2.0">
          <lst:prijemka version="2.0">
            <pri:prijemkaHeader>
              <pri:id>42</pri:id>
              <pri:number><typ:numberRequested>26PR00012</typ:numberRequested></pri:number>
              <pri:date>2025-11-20</pri:date>
              <pri:text>Gabel FT 2/250334</pri:text>
            </pri:prijemkaHeader>
            <pri:prijemkaDetail>
              <pri:prijemkaItem>
                <pri:text>DH PLATINUM SERIES CM.120</pri:text>
                <pri:quantity>1</pri:quantity>
                <pri:unit>PA</pri:unit>
                <pri:discountPercentage>35</pri:discountPercentage>
                <pri:homeCurrency><typ:unitPrice>94.5</typ:unitPrice></pri:homeCurrency>
                <pri:stockItem><typ:stockItem><typ:ids>7009190321200</typ:ids></typ:stockItem></pri:stockItem>
              </pri:prijemkaItem>
              <pri:prijemkaItem>
                <pri:text>STRETCH LITE CARBON LILAC</pri:text>
                <pri:quantity>20</pri:quantity>
                <pri:unit>PA</pri:unit>
                <pri:discountPercentage>35</pri:discountPercentage>
                <pri:homeCurrency><typ:unitPrice>35</typ:unitPrice></pri:homeCurrency>
                <pri:stockItem><typ:stockItem><typ:ids>7008352650000</typ:ids></typ:stockItem></pri:stockItem>
              </pri:prijemkaItem>
            </pri:prijemkaDetail>
          </lst:prijemka>
        </lst:listPrijemka>
        """;

    private static readonly ReceiptLineToWrite LineA = new("7009190321200", "DH PLATINUM SERIES CM.120", 2m, "PA", 94.5m, 35m);
    private static readonly ReceiptLineToWrite LineB = new("7008352650000", "STRETCH LITE CARBON LILAC", 20m, "PA", 35m, 35m);

    [Fact]
    public async Task ListRecentAsync_sends_a_listPrijemkaRequest_with_a_count_limit_and_a_dateFrom_filter()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""<lst:listPrijemka version="2.0" prijemkaVersion="2.0" />"""));

        var receipts = await service.ListRecentAsync(new DateOnly(2025, 9, 11), 500);

        Assert.Empty(receipts);

        var request = server.RequestPayload();
        Assert.Equal(Lst + "listPrijemkaRequest", request.Name);
        Assert.Equal("2.0", request.Attribute("version")!.Value);
        Assert.Equal("2.0", request.Attribute("prijemkaVersion")!.Value);

        // ftr:limitType is a complex type (idFrom/count), not plain text — live-verified.
        Assert.Equal("500", request.Element(Lst + "limit")!.Element(Ftr + "count")!.Value);

        // "requestPrijemka" is declared in list.xsd (lst) even though its type is from filter.xsd — live-verified.
        var filter = request.Element(Lst + "requestPrijemka")!.Element(Ftr + "filter")!;
        Assert.Equal("2025-09-11", filter.Element(Ftr + "dateFrom")!.Value);
    }

    [Fact]
    public async Task ListRecentAsync_parses_each_receipt_with_its_header_and_lines()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse(OneReceiptListing));

        var receipts = await service.ListRecentAsync(new DateOnly(2025, 1, 1), 100);

        var receipt = Assert.Single(receipts);
        Assert.Equal("42", receipt.PohodaId);
        Assert.Equal("26PR00012", receipt.Number);
        Assert.Equal("Gabel FT 2/250334", receipt.Text);
        Assert.Equal(new DateOnly(2025, 11, 20), receipt.Date);

        Assert.Equal(2, receipt.Lines.Count);
        Assert.Equal(new ExistingReceiptLine("7009190321200", "DH PLATINUM SERIES CM.120", "PA", 1m, 94.5m, 35m), receipt.Lines[0]);
        Assert.Equal(new ExistingReceiptLine("7008352650000", "STRETCH LITE CARBON LILAC", "PA", 20m, 35m, 35m), receipt.Lines[1]);
        Assert.False(receipt.Lines[0].HasUnparsedNumbers);
        Assert.False(receipt.Lines[1].HasUnparsedNumbers);
    }

    [Fact]
    public async Task ListRecentAsync_flags_lines_whose_numbers_could_not_be_parsed_and_substitutes_zero()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""
            <lst:listPrijemka version="2.0" prijemkaVersion="2.0">
              <lst:prijemka version="2.0">
                <pri:prijemkaHeader>
                  <pri:id>42</pri:id>
                  <pri:number><typ:numberRequested>26PR00012</typ:numberRequested></pri:number>
                  <pri:date>2025-11-20</pri:date>
                  <pri:text>Gabel FT 2/250334</pri:text>
                </pri:prijemkaHeader>
                <pri:prijemkaDetail>
                  <pri:prijemkaItem>
                    <pri:text>DH PLATINUM SERIES CM.120</pri:text>
                    <pri:quantity>N/A</pri:quantity>
                    <pri:unit>PA</pri:unit>
                    <pri:discountPercentage>35</pri:discountPercentage>
                    <pri:homeCurrency><typ:unitPrice>94.5</typ:unitPrice></pri:homeCurrency>
                    <pri:stockItem><typ:stockItem><typ:ids>7009190321200</typ:ids></typ:stockItem></pri:stockItem>
                  </pri:prijemkaItem>
                </pri:prijemkaDetail>
              </lst:prijemka>
            </lst:listPrijemka>
            """));

        var receipts = await service.ListRecentAsync(new DateOnly(2025, 1, 1), 100);

        var line = Assert.Single(Assert.Single(receipts).Lines);
        Assert.True(line.HasUnparsedNumbers);
        Assert.Equal(0m, line.Quantity);
        Assert.Equal(94.5m, line.UnitPrice);
    }

    [Fact]
    public async Task CreateAsync_builds_a_prijemka_with_double_nested_stock_references_and_a_freight_charge()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""
            <pri:prijemkaResponse version="2.0" state="ok">
              <pri:producedDetails><pri:id>77</pri:id><pri:number>26PR00013</pri:number></pri:producedDetails>
            </pri:prijemkaResponse>
            """));

        var number = await service.CreateAsync("Gabel FT 2/260220", new DateOnly(2026, 6, 23), [LineA, LineB], freight: 180.5m);

        Assert.Equal("26PR00013", number);

        var prijemka = server.RequestPayload();
        Assert.Equal(Prj + "prijemka", prijemka.Name);
        Assert.Equal("2.0", prijemka.Attribute("version")!.Value);

        var header = prijemka.Element(Prj + "prijemkaHeader")!;
        Assert.Null(header.Element(Prj + "id"));
        Assert.Equal("2026-06-23", header.Element(Prj + "date")!.Value);
        Assert.Equal("Gabel FT 2/260220", header.Element(Prj + "text")!.Value);

        var items = prijemka.Element(Prj + "prijemkaDetail")!.Elements(Prj + "prijemkaItem").ToList();
        Assert.Equal(2, items.Count);

        var first = items[0];
        Assert.Equal("DH PLATINUM SERIES CM.120", first.Element(Prj + "text")!.Value);
        Assert.Equal("2", first.Element(Prj + "quantity")!.Value);
        Assert.Equal("PA", first.Element(Prj + "unit")!.Value);
        Assert.Equal("false", first.Element(Prj + "payVAT")!.Value);
        Assert.Equal("none", first.Element(Prj + "rateVAT")!.Value);
        Assert.Equal("35", first.Element(Prj + "discountPercentage")!.Value);
        Assert.Equal("94.5", first.Element(Prj + "homeCurrency")!.Element(Typ + "unitPrice")!.Value);

        // Outer stockItem is prijemka.xsd's field; it wraps type.xsd's stockItem holding the typ:ids reference — live-verified.
        var stockRef = first.Element(Prj + "stockItem")!.Element(Typ + "stockItem")!.Element(Typ + "ids")!;
        Assert.Equal("7009190321200", stockRef.Value);

        var freight = Assert.Single(prijemka.Element(Prj + "prijemkaDetail")!.Elements(Prj + "prijemkaAccessoryChargesItem"));
        Assert.Equal(ReceiptService.FreightNote, freight.Element(Prj + "note")!.Value);
        Assert.Equal("1", freight.Element(Prj + "quantity")!.Value);
        Assert.Equal("false", freight.Element(Prj + "payVAT")!.Value);
        Assert.Equal("none", freight.Element(Prj + "rateVAT")!.Value);
        Assert.Equal("180.5", freight.Element(Prj + "homeCurrency")!.Element(Typ + "unitPrice")!.Value);
        // Accessory charges have no text/unit/stockItem — a different XSD type than prijemkaItem (live-verified).
        Assert.Null(freight.Element(Prj + "text"));
        Assert.Null(freight.Element(Prj + "stockItem"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task CreateAsync_omits_the_freight_charge_when_freight_is_absent_or_zero(int? freightWhole)
    {
        var (server, service) = Create();
        decimal? freight = freightWhole is null ? null : (decimal)freightWhole.Value;

        await service.CreateAsync("Gabel FT 2/260220", new DateOnly(2026, 6, 23), [LineA], freight);

        var detail = server.RequestPayload().Element(Prj + "prijemkaDetail")!;
        Assert.Single(detail.Elements(Prj + "prijemkaItem"));
        Assert.Empty(detail.Elements(Prj + "prijemkaAccessoryChargesItem"));
    }

    [Fact]
    public async Task CreateAsync_falls_back_to_the_header_text_when_the_response_carries_no_number()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse());

        var number = await service.CreateAsync("Gabel FT 2/260220", new DateOnly(2026, 6, 23), [LineA], null);

        Assert.Equal("Gabel FT 2/260220", number);
    }

    [Fact]
    public async Task UpdateAsync_puts_the_existing_id_in_the_header_and_resends_every_line()
    {
        var (server, service) = Create();

        await service.UpdateAsync("42", "Gabel FT 2/250334", [LineA, LineB], freight: null);

        var prijemka = server.RequestPayload();
        var header = prijemka.Element(Prj + "prijemkaHeader")!;
        Assert.Equal("42", header.Element(Prj + "id")!.Value);
        Assert.Equal("Gabel FT 2/250334", header.Element(Prj + "text")!.Value);

        var items = prijemka.Element(Prj + "prijemkaDetail")!.Elements(Prj + "prijemkaItem").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("7009190321200", items[0].Element(Prj + "stockItem")!.Element(Typ + "stockItem")!.Element(Typ + "ids")!.Value);
        Assert.Equal("7008352650000", items[1].Element(Prj + "stockItem")!.Element(Typ + "stockItem")!.Element(Typ + "ids")!.Value);
    }
}
