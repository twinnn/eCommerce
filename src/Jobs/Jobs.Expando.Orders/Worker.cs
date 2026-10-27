using Expando.Contract.Services;
using Jobs.Expando.Orders.Processors;

namespace Jobs.Expando.Orders;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IExpandoOrderService expando;
    private readonly IOrderProcessor orderProcessor;

    public Worker(ILogger<Worker> logger, IExpandoOrderService expando, IOrderProcessor orderProcessor)
    {
        _logger = logger;
        this.expando = expando;
        this.orderProcessor = orderProcessor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            //get unshipped expando orders for processing
            var expandoOrders = expando.GetExpandoOrders();

            //process each unshipped expando order
            await foreach (var order in expandoOrders) await orderProcessor.CreateOrderAsync(order);
        }
    }
}