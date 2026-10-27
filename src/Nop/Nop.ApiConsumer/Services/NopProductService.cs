using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nop.Contract.POCO;
using Nop.Contract.Services;

namespace Nop.ApiConsumer.Services;

public class NopProductService : INopProductService
{
    private readonly ILogger<NopProductService> _logger;
    private readonly INopBaseApiConsumer<NopStock, NopStocks> _apiConsumerNopCreateStock;
    private readonly INopBaseApiConsumer<NopStocks, object> _apiConsumerNopGetStocks;
    private readonly INopBaseApiConsumer<AddCategoryRequest, object> _apiConsumerNopUpdateCategory;
    private readonly INopBaseApiConsumer<AddSpecificationRequest, object> _apiConsumerNopUpdateSpecification;
    private readonly IMemoryCache _memoryCache;


    public NopProductService(ILogger<NopProductService> logger, INopBaseApiConsumer<NopStock, NopStocks> apiConsumerNopCreateStock, INopBaseApiConsumer<NopStocks, object> apiConsumerNopGetStocks, IMemoryCache memoryCache, INopBaseApiConsumer<AddCategoryRequest, object> apiConsumerNopUpdateCategory, INopBaseApiConsumer<AddSpecificationRequest, object> apiConsumerNopUpdateSpecification)
    {
        _logger = logger;
        _apiConsumerNopCreateStock = apiConsumerNopCreateStock;
        _apiConsumerNopGetStocks = apiConsumerNopGetStocks;
        _memoryCache = memoryCache;
        _apiConsumerNopUpdateCategory = apiConsumerNopUpdateCategory;
        _apiConsumerNopUpdateSpecification = apiConsumerNopUpdateSpecification;
    }

    public async Task<int> CreateProductAsync(NopStock stock)
    {
        await _apiConsumerNopCreateStock.InitAsync();
        var response = await _apiConsumerNopCreateStock.PostTAsync("products", stock);
        _logger.LogInformation("Got response: {}", response.products[0].id);
        return response.products[0].id;
    }

    public async Task<NopStocks> GetAllProductsAsync()
    {

        try
        {
            _logger.LogInformation("Looking for products in cache");
            _memoryCache.TryGetValue("products", out NopStocks? stocks);
            if (stocks is not null)
            {
                _logger.LogInformation("Found products in cache");
                return stocks;
            }

            await _apiConsumerNopGetStocks.InitAsync();
            var current =  await _apiConsumerNopGetStocks.GetAsync("products?Limit=250");
            var foundAll = current.products.Count < 250;
            while (!foundAll)
            {
                var newStocks = await _apiConsumerNopGetStocks.GetAsync("products?Limit=250&SinceId=" + current.products.Last().id);
                if (newStocks.products.Count == 0)
                {
                    foundAll = true;
                    continue;
                }

                current.products.AddRange(newStocks.products);
            }

            SaveToCache(current);

            return current;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while getting all attributes {}", ex);
            throw;
        }
    }

    private void SaveToCache(NopStocks current)
    {
        _logger.LogInformation("Adding products to cache");
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        _memoryCache.Set("products", current, cacheOptions);
    }

    public async Task DeleteProductAsync(int id)
    {
        var found = _memoryCache.TryGetValue("products", out NopStocks? stocks);

        try
        {
            _logger.LogInformation("Deleting stock with id {}", id);
            await _apiConsumerNopGetStocks.DeleteAsync($"products/{id}");
            if (found)
            {
                //stocks!.products.RemoveAll(x => x.id == id);
                //SaveToCache(stocks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("error while deleting stock {}", ex);
        }
    }

    public async Task UpdateProductAsync(NopStock nopStock)
    {
        try
        {
            await _apiConsumerNopCreateStock.PutAsync($"products/{nopStock.product.id}", nopStock);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Somehitngs wrong up {}", ex.Data);
        }
    }

    public async Task AddCategory(int id, int nopCategoryId)
    {
        try
        {
            await _apiConsumerNopUpdateCategory.InitAsync();
            await _apiConsumerNopUpdateCategory.PostAsync(@"product_category_mappings/", new AddCategoryRequest
            {
                product_category_mapping = new Product_Category_Mapping
                {
                    product_id = id,
                    category_id = nopCategoryId,
                    is_featured_product = false,
                    display_order = 0,
                    id = 0
                }
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Something wrong up {} {}", id, nopCategoryId);
        }
    }

    public async Task AddSpecification(int productId, NopSpecificationAttributeType attributeType, int optionId, string customValue)
    { 
        try
        {
            await _apiConsumerNopUpdateSpecification.InitAsync();
            await _apiConsumerNopUpdateSpecification.PostAsync(@"productspecificationattributes/", new AddSpecificationRequest
            {
                product_specification_attribute = new Product_Specification_Attribute
                {
                    product_id= productId,
                    attribute_type_id = (int)attributeType,
                    specification_attribute_option_id= optionId,
                    custom_value = customValue,
                    attribute_type = attributeType.ToString(),
                    show_on_product_page = true,
                    id = 0
                }
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Something wrong up {} {}", productId, optionId);
        }
    }
}