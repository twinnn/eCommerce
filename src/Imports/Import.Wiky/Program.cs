using System.Text;
using Common.Contract.Serialization;
using Import.Wiky;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pohoda.Contract;
using Pohoda.Contract.Services;
using Pohoda.Server.Helpers;
using Pohoda.Server.MServer;
using Pohoda.Server.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<Worker>();

        // Add common services
        services.AddSingleton<ISerializer, Utf8Serializer>();

        // Add pohoda services
        services.AddHttpClient<IStorageSystem, MServer>(client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1:5336");
            client.DefaultRequestHeaders.Add("STW-Authorization",
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{context.Configuration["Pohoda:username"]}:{context.Configuration["Pohoda:password"]}")));
            client.DefaultRequestHeaders.Add("Accept", "text/xml");
        });
        services.AddTransient<ICategoryService, CategoryService>();
        services.AddTransient<ICategoryHelper, CategoryHelper>();
    })
    .Build();

await host.RunAsync();