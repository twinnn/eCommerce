using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.Contract.Services;
using Pohoda.MServer;
using Pohoda.Server.Services;

namespace Pohoda.Server.Tests;

public class ServiceCollectionExtensionsTests
{
    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pohoda:StwTest:BaseUrl"] = "http://127.0.0.1",
            ["Pohoda:StwTest:Port"] = "5336",
            ["Pohoda:StwTest:ConfigName"] = "StwTest",
            ["Pohoda:StwTest:Username"] = "stwtest-user",
            ["Pohoda:StwTest:Password"] = "stwtest-pass",
            ["Pohoda:StwTest:Ico"] = "53870441",
            ["Pohoda:StwTest:DefaultStorageCode"] = "AzetCool",
            ["Pohoda:StwTest:DefaultTypePriceCode"] = "0",

            ["Pohoda:AcEcom:BaseUrl"] = "http://127.0.0.1",
            ["Pohoda:AcEcom:Port"] = "444",
            ["Pohoda:AcEcom:ConfigName"] = "GoNoow",
            ["Pohoda:AcEcom:Username"] = "acecom-user",
            ["Pohoda:AcEcom:Password"] = "acecom-pass",
            ["Pohoda:AcEcom:Ico"] = "53870441",
            ["Pohoda:AcEcom:DefaultStorageCode"] = "GoNoow",
            ["Pohoda:AcEcom:DefaultTypePriceCode"] = "GABEL"
        });

        builder.Services.AddPohodaMServer(builder.Configuration, "StwTest");
        builder.Services.AddPohodaMServer(builder.Configuration, "AcEcom");
        builder.Services.AddPohodaAgendaServices("StwTest");
        builder.Services.AddPohodaAgendaServices("AcEcom");

        return builder.Build();
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_a_keyed_IReceiptService_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<IReceiptService>("StwTest");
        var acEcom = host.Services.GetKeyedService<IReceiptService>("AcEcom");

        Assert.IsType<ReceiptService>(stwTest);
        Assert.IsType<ReceiptService>(acEcom);
        Assert.NotSame(stwTest, acEcom);
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_a_keyed_IStockCardService_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<IStockCardService>("StwTest");
        var acEcom = host.Services.GetKeyedService<IStockCardService>("AcEcom");

        Assert.IsType<StockCardService>(stwTest);
        Assert.IsType<StockCardService>(acEcom);
        Assert.NotSame(stwTest, acEcom);
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_nothing_under_the_default_unkeyed_registration()
    {
        using var host = BuildHost();

        Assert.Null(host.Services.GetService<IReceiptService>());
        Assert.Null(host.Services.GetService<IStockCardService>());
    }

    [Fact]
    public void Each_keyed_agenda_service_talks_to_its_own_mServer_instance()
    {
        using var host = BuildHost();

        // The service is constructed from the keyed MServerClient, so resolving the same key twice
        // must hand back the one singleton, and the two keys must never share it.
        var acEcomFirst = host.Services.GetRequiredKeyedService<IReceiptService>("AcEcom");
        var acEcomSecond = host.Services.GetRequiredKeyedService<IReceiptService>("AcEcom");
        var stwTest = host.Services.GetRequiredKeyedService<IReceiptService>("StwTest");

        Assert.Same(acEcomFirst, acEcomSecond);
        Assert.NotSame(acEcomFirst, stwTest);

        var acEcomClient = host.Services.GetRequiredKeyedService<MServerClient>("AcEcom");
        var stwTestClient = host.Services.GetRequiredKeyedService<MServerClient>("StwTest");
        Assert.NotSame(acEcomClient, stwTestClient);
    }
}
