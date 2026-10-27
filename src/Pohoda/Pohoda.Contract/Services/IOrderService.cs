using Common.Contract.DTO.Order;
using System.Threading.Tasks;

namespace Pohoda.Contract.Services
{
    public interface IOrderService
    {
        Task CreateOrder(OrderDTO order);
        Task<bool> DoesOrderExists(OrderDTO order);
    }
}