using System.Collections.Generic;
using Expando.Contract.POCO;

namespace Expando.Contract.Services
{
    public interface IExpandoOrderService
    {
        public IAsyncEnumerable<ExpandoOrder> GetExpandoOrders();
    }
}