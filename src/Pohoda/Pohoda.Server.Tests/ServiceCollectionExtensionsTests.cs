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

            ["Pohoda:GoNoow:BaseUrl"] = "http://127.0.0.1",
            ["Pohoda:GoNoow:Port"] = "444",
            ["Pohoda:GoNoow:ConfigName"] = "GoNoow",
            ["Pohoda:GoNoow:Username"] = "gonoow-user",
            ["Pohoda:GoNoow:Password"] = "gonoow-pass",
            ["Pohoda:GoNoow:Ico"] = "53870441",
            ["Pohoda:GoNoow:DefaultStorageCode"] = "GoNoow",
            ["Pohoda:GoNoow:DefaultTypePriceCode"] = "GABEL"
        });

        builder.Services.AddPohodaMServer(builder.Configuration, "StwTest");
        builder.Services.AddPohodaMServer(builder.Configuration, "GoNoow");
        builder.Services.AddPohodaAgendaServices("StwTest");
        builder.Services.AddPohodaAgendaServices("GoNoow");

        return builder.Build();
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_a_keyed_IReceiptService_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<IReceiptService>("StwTest");
        var goNoow = host.Services.GetKeyedService<IReceiptService>("GoNoow");

        Assert.IsType<ReceiptService>(stwTest);
        Assert.IsType<ReceiptService>(goNoow);
        Assert.NotSame(stwTest, goNoow);
    }

    [Fact]
    public void AddPohodaAgendaServices_registers_a_keyed_IStockCardService_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<IStockCardService>("StwTest");
        var goNoow = host.Services.GetKeyedService<IStockCardService>("GoNoow");

        Assert.IsType<StockCardService>(stwTest);
        Assert.IsType<StockCardService>(goNoow);
        Assert.NotSame(stwTest, goNoow);
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
        var goNoowFirst = host.Services.GetRequiredKeyedService<IReceiptService>("GoNoow");
        var goNoowSecond = host.Services.GetRequiredKeyedService<IReceiptService>("GoNoow");
        var stwTest = host.Services.GetRequiredKeyedService<IReceiptService>("StwTest");

        Assert.Same(goNoowFirst, goNoowSecond);
        Assert.NotSame(goNoowFirst, stwTest);

        var goNoowClient = host.Services.GetRequiredKeyedService<MServerClient>("GoNoow");
        var stwTestClient = host.Services.GetRequiredKeyedService<MServerClient>("StwTest");
        Assert.NotSame(goNoowClient, stwTestClient);
    }
}
