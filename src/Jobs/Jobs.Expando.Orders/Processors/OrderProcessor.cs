using Common.Contract.Builders;
using Common.Contract.DTO.Order;
using Expando.Contract.POCO;
using Pohoda.Contract.Services;

namespace Jobs.Expando.Orders.Processors;

public interface IOrderProcessor
{
    Task CreateOrderAsync(ExpandoOrder expandoOrder);
}

public class OrderProcessor : IOrderProcessor
{
    private readonly IBuilder<ExpandoOrder, OrderDTO> orderBuilder;
    private readonly IOrderService orderService;

    public OrderProcessor(IBuilder<ExpandoOrder, OrderDTO> orderBuilder,
        IOrderService orderService)
    {
        this.orderBuilder = orderBuilder;
        this.orderService = orderService;
    }

    public async Task CreateOrderAsync(ExpandoOrder expandoOrder)
    {
        var order = orderBuilder.Build(expandoOrder);
        await orderService.CreateOrder(order);
    }
}