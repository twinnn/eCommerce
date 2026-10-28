using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

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
            ["Pohoda:AcEcom:DefaultTypePriceCode"] = "0"
        });

        builder.Services.AddPohodaMServer(builder.Configuration, "StwTest");
        builder.Services.AddPohodaMServer(builder.Configuration, "AcEcom");

        return builder.Build();
    }

    [Fact]
    public void AddPohodaMServer_registers_a_keyed_MServerClient_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<MServerClient>("StwTest");
        var acEcom = host.Services.GetKeyedService<MServerClient>("AcEcom");

        Assert.NotNull(stwTest);
        Assert.NotNull(acEcom);
        Assert.NotSame(stwTest, acEcom);
    }

    [Fact]
    public void AddPohodaMServer_registers_a_keyed_MServerLauncher_per_instance()
    {
        using var host = BuildHost();

        var stwTest = host.Services.GetKeyedService<MServerLauncher>("StwTest");
        var acEcom = host.Services.GetKeyedService<MServerLauncher>("AcEcom");

        Assert.NotNull(stwTest);
        Assert.NotNull(acEcom);
        Assert.NotSame(stwTest, acEcom);
    }

    [Fact]
    public void AddPohodaMServer_configures_a_distinct_named_HttpClient_per_instance()
    {
        using var host = BuildHost();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();

        var stwTestClient = factory.CreateClient("StwTest");
        var acEcomClient = factory.CreateClient("AcEcom");

        Assert.Equal(new Uri("http://127.0.0.1:5336/"), stwTestClient.BaseAddress);
        Assert.Equal(new Uri("http://127.0.0.1:444/"), acEcomClient.BaseAddress);
    }

    [Fact]
    public void AddPohodaMServer_sets_the_STW_Authorization_header_not_the_standard_Authorization_header()
    {
        using var host = BuildHost();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient("AcEcom");

        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.True(client.DefaultRequestHeaders.Contains("STW-Authorization"));
        var expected = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("acecom-user:acecom-pass"));
        Assert.Equal(expected, client.DefaultRequestHeaders.GetValues("STW-Authorization").Single());
    }
}
