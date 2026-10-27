using Common.Contract.Builders;
using Common.Contract.DTO.Order;
using Expando.Contract.POCO;

namespace Jobs.Expando.Orders.Builders
{
    public class OrderBuilder : IBuilder<ExpandoOrder, OrderDTO>
    {
        public OrderDTO Build(ExpandoOrder data)
        {
            throw new NotImplementedException();
        }
    }
}
