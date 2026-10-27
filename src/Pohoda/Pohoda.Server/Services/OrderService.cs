using System;
using System.Threading.Tasks;
using Common.Contract.DTO.Order;
using Common.Contract.Serialization;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Contract.Services;

namespace Pohoda.Server.Services
{
    public class OrderService : IOrderService
    {
        private readonly ILogger<OrderService> logger;
        private readonly IStorageSystem mServer;
        private readonly ISerializer serializer;

        public OrderService(ILogger<OrderService> logger, ISerializer serializer, IStorageSystem mServer)
        {
            this.logger = logger;
            this.serializer = serializer;
            this.mServer = mServer;
        }

        public async Task CreateOrder(OrderDTO order)
        {
            logger.LogDebug("Creating order: {order}", order);
            var str = serializer.Serialize(order);
            await mServer.SendRequest(str);
            logger.LogInformation("Order {order} successfully created", order);
        }

        public Task<bool> DoesOrderExists(OrderDTO order)
        {
            throw new NotImplementedException();
        }
    }
}