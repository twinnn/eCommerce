using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Cache;
using System.Threading.Tasks;
using Common.Contract.DTO.Category;
using Common.Contract.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Contract.Services;
using Pohoda.Server.Helpers;
using Pohoda.Server.POCO.Category;
using Pohoda.Server.Storages;

namespace Pohoda.Server.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryHelper categoryHelper;
        private readonly ICategoryStorage _categoryStorage;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CategoryService> logger;
        private readonly IStorageSystem mServer;
        private readonly ISerializer serializer;

        public CategoryService(IStorageSystem mServer, ISerializer serializer, ILogger<CategoryService> logger,
            ICategoryHelper categoryHelper, ICategoryStorage categoryStorage, IMemoryCache memoryCache)
        {
            this.mServer = mServer;
            this.serializer = serializer;
            this.logger = logger;
            this.categoryHelper = categoryHelper;
            _categoryStorage = categoryStorage;
            _memoryCache = memoryCache;
        }

        public async Task<bool> Exists(string categoryString, string delimiter = " ", string storage = "")
        {
            if (!VerifyCategoryString(categoryString))
                return false;

            var categoriesList = categoryString.Split(delimiter).ToList();

            if (categoriesList.Count == 0)
                return false;

            var categories = _memoryCache.TryGetValue("Pohoda_Categories", out List<PohodaCategoryDto> categoriesFromCache)
                ? categoriesFromCache
                : await _categoryStorage.GetAllAsync(storage) as List<PohodaCategoryDto>;

            var rootCategory = categoriesList[0];
            categoriesList.Remove(rootCategory);

            return categories.Any(category =>
                // Check if we searching right storage, if storage is not set we searching whole data
                (category.Name == storage || string.IsNullOrEmpty(storage)) &&
                categoryHelper.SearchMainCategory(rootCategory, categoriesList, category));
        }

        public async Task CreateCategory(string categoryString, string storage, string delimiter = " ", string startingPath = "")
        {
            throw new NotImplementedException();

            if (!VerifyCategoryString(categoryString))
            {
                logger.LogError($"Invalid category path. Path: {categoryString}");
                return;
            }

            if (!await Exists(storage))
            {
                logger.LogError($"Storage doesn't exists. Storage: {storage}");
                return;
            }

            var fullPath = (startingPath + delimiter + categoryString).Split(delimiter).ToList();

            // Rewrite this, it doesn't work in any way imaginable
            // var lastFoundCategory = cache!.responsePackItem.listCategory.categoryDetail.category?.FirstOrDefault(c => c.name == storage);
            // foreach (var directory in fullPath)
            // {
            //     var found = false;
            //     
            //     foreach (var subcategory in lastFoundCategory.subCategories)
            //     {
            //         if (subcategory.name != directory) continue;
            //         lastFoundCategory = subcategory;
            //         found = true;
            //         fullPath.Remove(directory);
            //     }
            //
            //     if (!found)
            //     {
            //         break;
            //     }
            // }

            var request = new CreateCategoryRequest.dataPack()
            {
                application = "SWT",
                ico = 53870441,
                id = 1,
                note = "Import kategorie",
                version = 2.0m,
                dataPackItem = new CreateCategoryRequest.dataPackDataPackItem()
                {
                    version = 2.0m,
                    id = "a56",
                    categoryDetail = new CreateCategoryRequest.categoryDetail()
                    {
                        version = 2.0m,
                        category = new CreateCategoryRequest.categoryDetailCategory
                        {
                            name = fullPath[0],
                            //parentId = lastFoundCategory!.id,
                            displayed = true,
                            internetParams = new CreateCategoryRequest.categoryDetailCategoryInternetParams(),
                            subCategories = CreateSubcategories(fullPath.Skip(1).ToList()) as CreateCategoryRequest.categoryDetailSubcategory[],
                        }
                    }
                }
            };

            var body = serializer.Serialize(request);
            await mServer.SendRequest(body);
        }

        public async Task<IEnumerable<PohodaCategoryDto>?> GetAllCategories()
        {
            var result =  _memoryCache.TryGetValue("Pohoda_Categories", out IEnumerable<PohodaCategoryDto> categoriesFromCache);
            if (result) return categoriesFromCache;
            var categories = await _categoryStorage.GetAllAsync("");
            _memoryCache.Set("Pohoda_Categories", categories);
            return categories;

        }

        public async Task<PohodaCategoryDto?> GetCategoryByIdAsync(int id, List<PohodaCategoryDto> path = null)
        {
            _memoryCache.TryGetValue("Pohoda_Categories", out IEnumerable<PohodaCategoryDto> categoriesFromCache);
             
            if (categoriesFromCache == null || !categoriesFromCache.Any())
            {
                categoriesFromCache = await _categoryStorage.GetAllAsync("");
                _memoryCache.Set("Pohoda_Categories", categoriesFromCache);
            }            
            
            var result = categoryHelper.FindById(id.ToString(), categoriesFromCache, path);
            return result;
        }

        private IEnumerable<CreateCategoryRequest.categoryDetailSubcategory> CreateSubcategories(IReadOnlyCollection<string> categories)
        {
            var category = new CreateCategoryRequest.categoryDetailSubcategory
            {
                name = categories.Last(),
                subCategories = Array.Empty<CreateCategoryRequest.categoryDetailSubcategory>(),
                internetParams = new CreateCategoryRequest.categoryDetailCategoryInternetParams(),
                displayed = true
            };

            for (var i = categories.Count; i > 0; i--)
            {
                var newCat = new CreateCategoryRequest.categoryDetailSubcategory
                {
                    name = categories.Last(),
                    subCategories = new []{ category },
                    internetParams = new CreateCategoryRequest.categoryDetailCategoryInternetParams(),
                    displayed = true
                };

                category = newCat;
            }

            return new [] { category };
        }

        private bool VerifyCategoryString(string categoryString)
        {
            return !string.IsNullOrEmpty(categoryString);
        }
    }
}