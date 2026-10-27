using Pohoda.MServer;
using Pohoda.Server.Services;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Tests;

public class StockCardServiceTests
{
    private static (FakeMServer Server, StockCardService Service) Create()
    {
        var server = new FakeMServer();
        return (server, new StockCardService(server.Client, server.Config));
    }

    private const string OneStockListing = """
        <lStk:listStock version="2.0">
          <lStk:stock version="2.0">
            <stk:stockHeader>
              <stk:id>1</stk:id>
              <stk:code>7009190321200</stk:code>
              <stk:EAN>8030515107460</stk:EAN>
              <stk:name>Gabel DH PLATINUM SERIES 120 cm</stk:name>
            </stk:stockHeader>
          </lStk:stock>
        </lStk:listStock>
        """;

    [Fact]
    public async Task FindBySupplierCodeAsync_sends_a_listStockRequest_filtered_by_code_and_returns_the_matching_code()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse(OneStockListing));

        var code = await service.FindBySupplierCodeAsync("7009190321200");

        Assert.Equal("7009190321200", code);

        var request = server.RequestPayload();
        Assert.Equal(ListStk + "listStockRequest", request.Name);
        Assert.Equal("2.0", request.Attribute("version")!.Value);
        Assert.Equal("2.0", request.Attribute("stockVersion")!.Value);

        // requestStock is declared in list_stock.xsd (lStk), its filter children in filter.xsd (ftr) — live-verified.
        var filter = request.Element(ListStk + "requestStock")!.Element(Ftr + "filter")!;
        Assert.Equal("7009190321200", filter.Element(Ftr + "code")!.Value);

        Assert.Equal("53870441", server.Requests[0].Root!.Attribute("ico")!.Value);
    }

    [Fact]
    public async Task FindBySupplierCodeAsync_returns_null_when_no_card_matches()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""<lStk:listStock version="2.0" />"""));

        Assert.Null(await service.FindBySupplierCodeAsync("0000000000000"));
    }

    [Fact]
    public async Task SearchByNameContainingAsync_uses_a_queryFilter_LIKE_condition_with_escaped_quotes()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.OkResponse("""
            <lStk:listStock version="2.0">
              <lStk:stock version="2.0"><stk:stockHeader><stk:code>7009181280000</stk:code><stk:name>Gabel SLK-R - pretekárska palica</stk:name></stk:stockHeader></lStk:stock>
              <lStk:stock version="2.0"><stk:stockHeader><stk:code>7009181281250</stk:code><stk:name>Gabel SLK-R - pretekárska palica 125 cm</stk:name></stk:stockHeader></lStk:stock>
              <lStk:stock version="2.0"><stk:stockHeader><stk:name>bez kódu — musí byť vynechaná</stk:name></stk:stockHeader></lStk:stock>
            </lStk:listStock>
            """));

        var matches = await service.SearchByNameContainingAsync("SLK'R");

        Assert.Equal(2, matches.Count);
        Assert.Equal("7009181280000", matches[0].Code);
        Assert.Equal("Gabel SLK-R - pretekárska palica", matches[0].Name);
        Assert.Equal("7009181281250", matches[1].Code);

        var request = server.RequestPayload();
        Assert.Equal(ListStk + "listStockRequest", request.Name);
        var queryFilter = request.Element(ListStk + "requestStock")!.Element(Ftr + "queryFilter")!;
        Assert.Equal("Nazev LIKE '%SLK''R%'", queryFilter.Element(Ftr + "filter")!.Value);
    }

    [Fact]
    public async Task CreateAsync_sends_a_card_with_stockType_code_name_storage_typePrice_and_orderName()
    {
        var (server, service) = Create();

        var code = await service.CreateAsync("7009190321200", "DH PLATINUM SERIES CM.120", "120 cm");

        Assert.Equal("7009190321200", code);

        var stock = server.RequestPayload();
        Assert.Equal(Stk + "stock", stock.Name);
        Assert.Equal("2.0", stock.Attribute("version")!.Value);

        var header = stock.Element(Stk + "stockHeader")!;
        Assert.Equal("card", header.Element(Stk + "stockType")!.Value);
        Assert.Equal("7009190321200", header.Element(Stk + "code")!.Value);
        Assert.Equal("DH PLATINUM SERIES CM.120", header.Element(Stk + "name")!.Value);
        Assert.Equal("GoNoow", header.Element(Stk + "storage")!.Element(Typ + "ids")!.Value);
        Assert.Equal("Cenova skupina 1", header.Element(Stk + "typePrice")!.Element(Typ + "ids")!.Value);
        Assert.Equal("120 cm", header.Element(Stk + "orderName")!.Value);

        // The supplier code is NOT a real EAN — it must never be written to stk:EAN.
        Assert.Null(header.Element(Stk + "EAN"));
    }

    [Fact]
    public async Task CreateAsync_omits_orderName_when_none_is_supplied()
    {
        var (server, service) = Create();

        await service.CreateAsync("7008352650000", "STRETCH LITE CARBON LILAC", orderName: null);

        var header = server.RequestPayload().Element(Stk + "stockHeader")!;
        Assert.Null(header.Element(Stk + "orderName"));
    }

    [Fact]
    public async Task CreateAsync_throws_PohodaResponseException_when_mServer_reports_an_error()
    {
        var (server, service) = Create();
        server.Enqueue(FakeMServer.ErrorResponse("Typ zásoby. Tento element je vyžadovaný pri vytvorení dokladu."));

        var ex = await Assert.ThrowsAsync<PohodaResponseException>(() => service.CreateAsync("1", "x", null));

        Assert.Contains("Typ zásoby", ex.Message);
    }
}
