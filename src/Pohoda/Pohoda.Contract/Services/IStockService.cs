using System.Collections.Generic;
using Common.Contract.DTO.Stock;
using System.Threading.Tasks;

namespace Pohoda.Contract.Services
{
    public interface IStockService
    {
        Task UpsertStockStockAsync(string division, StockDTO stockItem);
        Task<IEnumerable<StockDTO>> GetAllStocksAsync();
    }
}