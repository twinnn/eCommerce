using Common.Contract.DTO.Stock;
using Common.Contract.Serialization;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Server.Mappers;
using Pohoda.Server.POCO.Stock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pohoda.Server.Storages
{
	public interface IStockStorage
	{
		Task<IEnumerable<StockDTO>?> GetAllAsync(string storage);
	}
	public class StockStorage : IStockStorage
	{
		private readonly ILogger<ParametersStorage> _logger;
		private readonly IStorageSystem _mServer;
		private readonly ISerializer _serializer;
		private readonly IStockToDtoMapper _mapper;

		public StockStorage(ILogger<ParametersStorage> logger, IStorageSystem mServer, ISerializer serializer, IStockToDtoMapper mapper)
		{
			_logger = logger;
			_mServer = mServer;
			_serializer = serializer;
			_mapper = mapper;
		}

		public async Task<IEnumerable<StockDTO>?> GetAllAsync(string storage)
		{
			try
			{
				await _mServer.StartServer();
				var mapped = new List<StockDTO>();
				var pohodaStocks = await GetStocksFromPohoda(storage);
                //_logger.LogInformation("Pohoda stocks: {stocks}", pohodaStocks.responsePackItem.listStock.stock.Length);

                if (pohodaStocks is { responsePackItem: { } })
				{
					mapped = _mapper.Map(pohodaStocks).ToList();
				}

				return mapped;
			}
			catch (Exception ex)
			{
				_logger.LogError(exception: ex, message: "Error while getting stocks from Pohoda");
				return null;
			}

		}

		private async Task<GetStocksResponse.responsePack> GetStocksFromPohoda(string storageSection)
		{
			if (string.IsNullOrWhiteSpace(storageSection))
			{
				throw new ArgumentException(storageSection, nameof(storageSection));
			}

			var response = await _mServer.SendRequest(GetRequestWithFilterByStorage(storageSection));

			//_logger.LogInformation("Deserializing response: {}", response);
			var responseDeserialized = _serializer.Deserialize<GetStocksResponse.responsePack>(response);
            //_logger.LogInformation("What is going on {}", responseDeserialized.responsePackItem.listStock.stock.Length);

            return responseDeserialized;
        }

		private string GetRequestWithFilterByStorage(string storageSection) 
		{
			return @"<dat:dataPack xmlns:dat=""http://www.stormware.cz/schema/version_2/data.xsd"" xmlns:stk=""http://www.stormware.cz/schema/version_2/stock.xsd"" xmlns:ftr=""http://www.stormware.cz/schema/version_2/filter.xsd"" xmlns:lStk=""http://www.stormware.cz/schema/version_2/list_stock.xsd"" xmlns:typ=""http://www.stormware.cz/schema/version_2/type.xsd"" id=""Za001"" ico=""53870441"" application=""StwTest"" version=""2.0"" note=""Export zásob"">
						<dat:dataPackItem id=""a55"" version=""2.0"">
							<!--  export zásoby  -->
							<lStk:listStockRequest version=""2.0"" stockVersion=""2.0"">
								<lStk:requestStock>
									<ftr:filter>
										<ftr:storage>
											<typ:ids>##storageSection##</typ:ids>
										</ftr:storage>
									</ftr:filter>
								</lStk:requestStock>
							</lStk:listStockRequest>
						</dat:dataPackItem>
					</dat:dataPack>".Replace("##storageSection##", storageSection);
		}
	}
}
