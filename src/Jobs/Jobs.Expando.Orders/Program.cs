using System.Text;
using Common.Contract.Builders;
using Common.Contract.DTO.Order;
using Common.Contract.Serialization;
using Expando.Connector.Configuration;
using Expando.Connector.Services;
using Expando.Contract.Configuration;
using Expando.Contract.POCO;
using Expando.Contract.Services;
using Jobs.Expando.Orders;
using Jobs.Expando.Orders.Builders;
using Jobs.Expando.Orders.Processors;
using Pohoda.Contract;
using Pohoda.Contract.Services;
using Pohoda.Server.MServer;
using Pohoda.Server.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<Worker>();

        // Add common services
        services.AddSingleton<ISerializer, Utf8Serializer>();

        // Add expando services
        services.AddTransient<IExpandoOrderService, ExpandoOrderService>();
        services.AddTransient<IExpandoOrderServiceConfiguration, ExpandoOrderServiceConfiguration>();
        services.AddTransient<IOrderProcessor, OrderProcessor>();
        services.AddTransient<IBuilder<ExpandoOrder, OrderDTO>, OrderBuilder>();

        // Add pohoda services
        services.AddHttpClient<IStorageSystem, MServer>(client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1:5336");
            client.DefaultRequestHeaders.Add("STW-Authorization",
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{context.Configuration["Pohoda:username"]}:{context.Configuration["Pohoda:password"]}")));
            client.DefaultRequestHeaders.Add("Accept", "text/xml");
        });
        services.AddTransient<IOrderService, OrderService>();
    })
    .Build();

await host.RunAsync();