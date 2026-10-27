using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Contract.DTO.Stock;
using Common.Contract.Serialization;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Contract.Services;
using Pohoda.Server.Builders.Stock;
using Pohoda.Server.Storages;

namespace Pohoda.Server.Services
{
    public class StockService : IStockService
    {
        private readonly ILogger<StockService> _logger;
        private readonly IStockRequestBuilder _stockBuilder;
        private readonly IStorageSystem _mServer;
        private readonly ISerializer _serializer;
        private readonly IStockStorage _stockStorage;

        public StockService(ILogger<StockService> logger, ISerializer serializer, IStorageSystem mServer, IStockRequestBuilder stockBuilder, IStockStorage stockStorage)
        {
            _logger = logger;
            _serializer = serializer;
            _mServer = mServer;
            _stockBuilder = stockBuilder;
            _stockStorage = stockStorage;
        }

        public async Task UpsertStockStockAsync(string division, StockDTO stockItem)
        {
            //var imageFileName = stock.ImgUrl.Split("/").Last();
            //var path = $"{stock.ImgFilePath}{imageFileName}";
            // TODO: chceme mat aj viac obrazkov, ako ich chceme posielat?

            //logger.LogDebug("Downloading picture with url {url} and destination {destination}. StockData ImgFilePath is {ImgFilePath}", stockData.ImgUrl, path, stockData.ImgFilePath);
            //// _imageDownloader.Download(stockData.ImgUrl, path).Wait();
            //logger.LogInformation("Downloaded image, path: {path}", path);

            var createStockRequest = _stockBuilder.CreateStockRequest(stockItem);

            var serRequest = _serializer.Serialize(createStockRequest);
            _logger.LogDebug("Creating stock: {stock}", stockItem);
            await _mServer.SendRequest(serRequest);
        }

        public async Task<IEnumerable<StockDTO>?> GetAllStocksAsync()
        {
            return await _stockStorage.GetAllAsync("GoNoow/Palice");
        }
    }
}