

using Common.Contract.DTO.Stock;
using Pohoda.Server.POCO.Stock;
using System.Threading.Tasks;

namespace Pohoda.Server.Builders.Stock
{
    public interface IStockRequestBuilder
    {
        Task<CreateStockRequest.dataPack> CreateStockRequest(StockDTO stockItem);
    }
    public class StockRequestBuilder : IStockRequestBuilder
    {
        public async Task<CreateStockRequest.dataPack> CreateStockRequest(StockDTO stockItem)
        {
            return new CreateStockRequest.dataPack
            { };
        }
    }
}
