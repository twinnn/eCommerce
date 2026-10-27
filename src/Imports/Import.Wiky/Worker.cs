using Common.Contract.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pohoda.Contract.Services;

namespace Import.Wiky;

public class Worker : BackgroundService
{
    private readonly ICategoryService categoryService;
    private readonly IConfiguration configuration;
    private readonly ILogger logger;
    private readonly ISerializer serializer;

    public Worker(ILogger<Worker> logger, ISerializer serializer, IConfiguration configuration,
        ICategoryService categoryService)
    {
        this.logger = logger;
        this.serializer = serializer;
        this.configuration = configuration;
        this.categoryService = categoryService;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //string hrackysFeedUrl = @"https://www.wiky.sk/be3159ad04564bfb90db9e32851ebf9c/customExportForWholesalers_export.xml";

        //var items = await new BaseXmlReader<SHOP>(hrackysFeedUrl).GetNodes().FirstAsync(cancellationToken: stoppingToken);

        //if (items is null)
        //{
        //    logger.LogWarning("Couldn't fetch any items");
        //    return;
        //}

        //foreach (var item in items.SHOPITEM)
        //{
        //    Console.WriteLine(item.PRICE);
        //    //using var db = new LiteDatabase(configuration["Database:Path"]);
        //    //var col = db.GetCollection<Data>("customers");
        //}

        // Myslel som ze mServer v metode Send skontroluje ci bezi mServer instancia, mozno by to bolo dobre pridat, ze by to bola automaticka aktivacia 
        Console.WriteLine(await categoryService.Exists("Doplnky Rukavice", storage: "abc"));
    }
}