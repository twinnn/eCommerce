using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Common.Contract.DTO.Parameter;
using Common.Contract.DTO.Stock;
using Microsoft.Extensions.Logging;
using static Pohoda.Server.POCO.Stock.GetStocksResponse;

namespace Pohoda.Server.Mappers
{
    public interface IStockToDtoMapper
    {
        IEnumerable<StockDTO>? Map(responsePack stocks);

        StockDTO Map(listStockStock stock);
    }

    public class StockToDtoMapper : IStockToDtoMapper
    {
        private readonly ILogger<StockToDtoMapper> _logger;

        public StockToDtoMapper(ILogger<StockToDtoMapper> logger)
        {
            _logger = logger;
        }

        public IEnumerable<StockDTO>? Map(responsePack stocks)
        {
            foreach (var stock in stocks.responsePackItem.listStock.stock)
            {
                yield return Map(stock);
            };
        }

        public static T GetValueFromXml<T>(listStockStock pohodaStock, ItemsChoiceType choiceType)
        {
            var index = Array.FindIndex(pohodaStock.stockHeader.ItemsElementName,
                        v => v == choiceType);

            var value = (index > -1)
                ? pohodaStock.stockHeader.Items[index]
            : default(T);

            //return (T)Convert.ChangeType(value, typeof(T));
            return (T)value;
        }

        public static IEnumerable<T> GetListFromXml<T>(listStockStock pohodaStock, ItemsChoiceType choiceType)
        {
            var index = Array.FindIndex(pohodaStock.stockHeader.ItemsElementName,
                        v => v == choiceType);

            var value = (index > -1)
                ? pohodaStock.stockHeader.Items[index]
            : default(T);

            if (value is IEnumerable<T>)
            {
                List<T> result = new List<T>();

                foreach (T item in (IEnumerable<T>)value)
                {
                    result.Add(item);
                }

                return result;
            }

            return null;
        }

        public StockDTO Map (listStockStock stock)
        {
            var imageUrls = GetValueFromXml<stockHeaderPictures>(stock, ItemsChoiceType.pictures)?.picture.Select(image => image.filepath);
            var parameters = GetValueFromXml<stockHeaderIntParameters>(stock, ItemsChoiceType.intParameters);
            var prices = stock.stockPriceItem;
            return new StockDTO
            {
                Id = GetValueFromXml<UInt16>(stock, ItemsChoiceType.id),
                Name = GetValueFromXml<string>(stock, ItemsChoiceType.name),
                SKU = GetValueFromXml<string>(stock, ItemsChoiceType.code),
                EAN = GetValueFromXml<UInt64>(stock, ItemsChoiceType.EAN),
                Price = GetSellingPriceIncludingVat(prices),
                Manufacturer = GetValueFromXml<string>(stock, ItemsChoiceType.producer),
                ShortDescription = GetValueFromXml<string>(stock, ItemsChoiceType.description),
                LongDescription = GetValueFromXml<string>(stock, ItemsChoiceType.description2),
                Count = GetValueFromXml<decimal>(stock, ItemsChoiceType.count),
                Variant = GetValueFromXml<string>(stock, ItemsChoiceType.orderName),
                RelatedStocks = GetValueFromXml<stockHeaderRelatedStocks>(stock, ItemsChoiceType.relatedStocks)?.idStocks?.Select(stock => stock.stockItem.id) ?? Array.Empty<uint>(),
                Categories = GetValueFromXml<stockHeaderCategories>(stock, ItemsChoiceType.categories)?.idCategory ?? Array.Empty<ushort>(),
                AlternativeImagesUrl = imageUrls,
                ImgFilePath = imageUrls?.FirstOrDefault(),
                Parameters = parameters?.intParameter?.Select(parameter => new StockParameterDto 
                { 
                    Id = parameter.intParameterID,
                    Name = parameter.intParameterName,
                    Type = parameter.intParameterType,
                    Values = parameter.intParameterValues?.Select(value => new StockParameterValueDto 
                    {
                        ValueId = (ushort)((value.parameterValueIDSpecified) ? value.parameterValueID : 0),
                        Value = value.parameterValue
                    }) ?? Array.Empty<StockParameterValueDto>()
                }) ?? Array.Empty<StockParameterDto>()
            };
        }

        public decimal GetSellingPriceIncludingVat(stockPriceItemStockPrice[] prices)
        {
            return prices.FirstOrDefault(p => p.ids.Equals("Predajná")).price;
        }
    }
}