
using Common.Contract.DTO.Stock;
using System.Linq;

namespace Common.Contract.Extensions
{
    public static class StockDtoExtensions
    {
        public static bool HasVariant(this StockDTO stock) 
        {
            return !string.IsNullOrWhiteSpace(stock.Variant);
        }

        public static bool IsVariantMasterStock(this StockDTO stock)
        {
            return string.IsNullOrWhiteSpace(stock.Variant) && stock.RelatedStocks != null && stock.RelatedStocks.Any();
        }
    }
}
