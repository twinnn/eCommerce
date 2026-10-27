using System.Text;
using Common.Contract.Serialization;
using Jobs.Nop.Synch;
using Jobs.Nop.Synch.Helpers;
using Jobs.Nop.Synch.Mappers;
using Jobs.Nop.Synch.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nop.ApiConsumer;
using Nop.ApiConsumer.Services;
using Nop.Contract.Configuration;
using Nop.Contract.POCO;
using Nop.Contract.Services;
using Pohoda.Contract;
using Pohoda.Contract.Services;
using Pohoda.Server.Builders.Stock;
using Pohoda.Server.Helpers;
using Pohoda.Server.Mappers;
using Pohoda.Server.MServer;
using Pohoda.Server.Services;
using Pohoda.Server.Storages;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {

        services.AddHostedService<Worker>();

        // Add common services
        services.AddSingleton<ISerializer, Utf8Serializer>();

        // Add pohoda services
        services.AddHttpClient<IStorageSystem, MServer>("mServer", httpClient =>
        {
            httpClient.BaseAddress = new Uri("http://127.0.0.1:5336");
            httpClient.DefaultRequestHeaders.Add("STW-Authorization",
                "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{context.Configuration["Pohoda:username"]}:{context.Configuration["Pohoda:password"]}")));
            httpClient.DefaultRequestHeaders.Add("Accept", "text/xml");
            httpClient.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddTransient<IParameterService, ParameterService>();
        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<IStockService, StockService>();
        services.AddTransient<ICategoryService, CategoryService>();

        // Add Storage
        services.AddTransient<IParameterStorage, ParametersStorage>();
        services.AddTransient<IStockStorage, StockStorage>();
        services.AddTransient<ICategoryStorage, CategoryStorage>();

        services.AddTransient<ICategoryHelper, CategoryHelper>();

        services.AddTransient<IStockRequestBuilder, StockRequestBuilder>();

        // Add NopBaseApiConsumer
        services.AddTransient<INopBaseApiConsumer<NopSpecificationAttribute, NopSpecificationAttributes>, NopBaseApiConsumer<NopSpecificationAttribute, NopSpecificationAttributes>>();
        services.AddTransient<INopBaseApiConsumer<NopSpecificationAttributes, object>, NopBaseApiConsumer<NopSpecificationAttributes, object>>();
        services.AddTransient<INopBaseApiConsumer<NopStock, NopStocks>, NopBaseApiConsumer<NopStock, NopStocks>>();
        services.AddTransient<INopBaseApiConsumer<NopStocks, object>, NopBaseApiConsumer<NopStocks, object>>();
        services.AddTransient<INopBaseApiConsumer<AddCategoryRequest, object>, NopBaseApiConsumer<AddCategoryRequest, object>>();
        services.AddTransient<INopBaseApiConsumer<AddSpecificationRequest, object>, NopBaseApiConsumer<AddSpecificationRequest, object>>();
        services.AddTransient<INopBaseApiConsumer<NopCategory, NopCategories>, NopBaseApiConsumer<NopCategory, NopCategories>>();
        services.AddTransient<INopBaseApiConsumer<NopCategories, object>, NopBaseApiConsumer<NopCategories, object>>();



        // Add NopCommerce services
        services.AddTransient<INopCategoryService, NopCategoryService>();
        services.AddTransient<INopSpecificationAttributeService, NopSpecificationAttributeService>();
        services.AddTransient<INopProductService, NopProductService>();

        // Add mappers
        services.AddTransient<IDtoToNopAttributeMapper, DtoToNopAttributeMapper>();
        services.AddTransient<IAttributeToDtoMapper, AttributeToDtoMapper>();
        services.AddTransient<IStockToDtoMapper,StockToDtoMapper>();
        services.AddTransient<IDtoToNopStockMapper, DtoToNopStockMapper>();
        services.AddTransient<ICategoryToDtoMapper, CategoryToDtoMapper>();

        // Get NopCommerce settings
        var settings = new NopCommerceSettings();
        new ConfigureFromConfigurationOptions<NopCommerceSettings>(
            context.Configuration.GetSection("NopCommerce")).Configure(settings);
        services.AddSingleton(settings);

        // Add Processors
        services.AddTransient<ICategoryProcessor, CategoryProcessor>();
        services.AddTransient<IAttributeProcessor, AttributeProcessor>();
        services.AddTransient<IProductProcessor, ProductProcessor>();
        
        // Configure HttpClient Factory
        services.AddHttpClient("NopCommerce", httpClient =>
        {
            httpClient.BaseAddress = new Uri(settings.BaseUrl);
        });

        // Add Helpers
        services.AddSingleton<IImageHelper, ImageHelper>();

        // Add Cache
        services.AddMemoryCache();

    })
    .Build();

await host.RunAsync();