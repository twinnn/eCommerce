using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Contract.DTO.Category;
using Common.Contract.Serialization;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Server.Mappers;
using Pohoda.Server.POCO.Category;
using static Pohoda.Server.POCO.Category.GetCategoriesRequest;

namespace Pohoda.Server.Storages
{
    public interface ICategoryStorage
    {
        Task<IEnumerable<PohodaCategoryDto>?> GetAllAsync(string storage);
        Task CreateCategory();
    }

    public class CategoryStorage : ICategoryStorage
    {
        private readonly ILogger<ParametersStorage> _logger;
        private readonly IStorageSystem _mServer;
        private readonly ISerializer _serializer;
        private readonly ICategoryToDtoMapper _mapper;

        public CategoryStorage(ILogger<ParametersStorage> logger, IStorageSystem mServer, ISerializer serializer, ICategoryToDtoMapper mapper)
        {
            _logger = logger;
            _mServer = mServer;
            _serializer = serializer;
            _mapper = mapper;
        }

        public async Task<IEnumerable<PohodaCategoryDto>?> GetAllAsync(string storage)
        {
            try
            {
                await _mServer.StartServer();
                _logger.LogInformation("Loading categories from Pohoda");
                var pohodaCategories = await GetCategoriesFromPohoda();
                var mapped = pohodaCategories is { responsePackItem: { } } ? _mapper.Map(pohodaCategories) : null;
                _logger.LogInformation("Found categories: mapped:{} / found:{}", mapped.Count(), pohodaCategories.responsePackItem.listCategory.categoryDetail.category.Length);
                return mapped;
            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, message: "Error while getting categories from Pohoda");
                return null;
            }
        }

        private async Task<GetCategoriesResponse.responsePack> GetCategoriesFromPohoda()
        {
            var request = new dataPack
            {
                id = "002",
                ico = 53870441,
                application = "klient",
                version = 2.0m,
                note = "Export kategorii internetového obchodu",
                dataPackItem = new dataPackDataPackItem
                {
                    id = "002",
                    version = 2.0m,
                    listCategoryRequest = new listCategoryRequest
                    {
                        version = 2.0m,
                        requestCategory = new requestCategory()
                    }
                }
            };

            _logger.LogInformation("Creating request");
            var body = _serializer.Serialize(request);
            var response = await _mServer.SendRequest(body);

            _logger.LogInformation("Deserializing request");
            var responseDeserialized = _serializer.Deserialize<GetCategoriesResponse.responsePack>(response);
            return responseDeserialized;
        }

        public async Task CreateCategory()
        {

        }
    }
}