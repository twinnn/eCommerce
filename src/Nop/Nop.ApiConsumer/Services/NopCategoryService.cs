using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nop.Contract.POCO;
using Nop.Contract.Services;

namespace Nop.ApiConsumer.Services;

public class NopCategoryService : INopCategoryService
{
    private readonly ILogger<NopCategoryService> _logger;
    private readonly INopBaseApiConsumer<NopCategory, NopCategories> _apiConsumerNopCategory;
    private readonly INopBaseApiConsumer<NopCategories, object> _apiConsumerNopCategories;
    private readonly IMemoryCache _memoryCache;


    public NopCategoryService(ILogger<NopCategoryService> logger, INopBaseApiConsumer<NopCategory, NopCategories> apiConsumerNopCategory, INopBaseApiConsumer<NopCategories, object> apiConsumerNopCategories, IMemoryCache memoryCache)
    {
        _logger = logger;
        _apiConsumerNopCategory = apiConsumerNopCategory;
        _apiConsumerNopCategories = apiConsumerNopCategories;
        _memoryCache = memoryCache;
    }

    public async Task<Category?> GetCategoryByName(string pohodaCategoryName, int parentCategoryId = 0)
    {
        _memoryCache.TryGetValue("Nop_Categories", out NopCategories categoriesFromCache);

        if (categoriesFromCache == null) 
        {
            categoriesFromCache = await GetAllCategoriesAsync();
            _memoryCache.Set("Nop_Categories", categoriesFromCache);
        }

        if (parentCategoryId == 0)
        {
            return categoriesFromCache?.categories?.FirstOrDefault(c => c.name == pohodaCategoryName);
        }
        else
        {
            return categoriesFromCache?.categories?.FirstOrDefault(c => c.name == pohodaCategoryName && c.parent_category_id == parentCategoryId);
        }
    }

    public async Task<NopCategories> CreateCategoryAsync(NopCategory category)
    {
        await _apiConsumerNopCategory.InitAsync();

        return await _apiConsumerNopCategory.PostTAsync("categories", category);
    }

    public async Task DeleteAllCategoriesAsync()
    {
        await _apiConsumerNopCategories.InitAsync();
        await _apiConsumerNopCategory.InitAsync();

        var categories = await GetAllCategoriesAsync();

        var deleteTasks = categories.categories.Select(c => _apiConsumerNopCategory.DeleteAsync($"categories/{c.id}"));
        await Task.WhenAll(deleteTasks);
        _memoryCache.Remove("categories");
    }
    
    public async Task<NopCategories> GetAllCategoriesAsync()
    {
        // await _apiConsumerNopCategories.InitAsync();
        // await _apiConsumerNopCategory.InitAsync();
        // return await _apiConsumerNopCategories.GetAsync("api/categories?Limit=250");

        _logger.LogInformation("Looking for categories in cache");

        if (_memoryCache.TryGetValue("categories", out NopCategories? categories) && categories is not null)
        {
            _logger.LogInformation("Found categories in cache");
            return categories;
        }
        _logger.LogInformation("Categories not found in cache");
        await _apiConsumerNopCategories.InitAsync();
        var current =  await _apiConsumerNopCategories.GetAsync("categories?Limit=250");
        var foundAll = current.categories.Length is 0 or < 250;
        while (!foundAll)
        {
            _logger.LogInformation("Found more categories");
            var newCategories = await _apiConsumerNopCategories.GetAsync("categories?Limit=250&SinceId=" + current.categories.Last().id);
            if (newCategories.categories is null || newCategories.categories.Length == 0)
            {
                foundAll = true;
                continue;
            }

            current.categories = current.categories.Concat(newCategories.categories).ToArray();
        }

        if (current.categories.Length != 0)
            SaveToCache(current);

        return current;
        
    }

    private void SaveToCache(NopCategories current)
    {
        _logger.LogInformation("Adding categories to cache");
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(1)
        };

        _memoryCache.Set("categories", current, cacheOptions);
    }
}