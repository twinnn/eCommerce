using Common.Contract.DTO.Category;
using Common.Contract.DTO.Parameter;
using Common.Contract.DTO.Stock;
using Common.Contract.Extensions;
using Jobs.Nop.Synch.Helpers;
using Jobs.Nop.Synch.Mappers;
using Microsoft.Extensions.Logging;
using Nop.Contract.POCO;
using Nop.Contract.Services;
using Pohoda.Contract.Services;

namespace Jobs.Nop.Synch.Processors;

public interface IProductProcessor
{
    Task CreateAllStocksAsync();
    Task DeleteAllStocksAsync();
    Task<NopStocks> GetAllStocksAsync();
}

public class ProductProcessor : IProductProcessor
{
    private readonly ILogger<ProductProcessor> _logger;
    private readonly IStockService _stockService;
    private readonly INopProductService _nopProductService;
    private readonly IDtoToNopStockMapper _nopStockMapper;
    private readonly IImageHelper _imageHelper;
    private readonly ICategoryService _categoryService;
    private readonly INopCategoryService _nopCategoryService;
    private readonly IParameterService _parameterService;
    private readonly INopSpecificationAttributeService _nopSpecificationAttributeService;

    private IEnumerable<PohodaParameterDto> pohodaParameters;
    private NopSpecificationAttributes nopSpecificationAttributes;

    public ProductProcessor(ILogger<ProductProcessor> logger, IStockService stockService,
        INopProductService nopProductService, IDtoToNopStockMapper nopStockMapper, IImageHelper imageHelper,
        ICategoryService categoryService, INopCategoryService nopCategoryService, IParameterService parameterService, INopSpecificationAttributeService nopSpecificationAttributeService)
    {
        _logger = logger;
        _stockService = stockService;
        _nopProductService = nopProductService;
        _nopStockMapper = nopStockMapper;
        _imageHelper = imageHelper;
        _categoryService = categoryService;
        _nopCategoryService = nopCategoryService;
        _parameterService = parameterService;
        _nopSpecificationAttributeService = nopSpecificationAttributeService;
    }

    public async Task<NopStocks> GetAllStocksAsync()
    {
        var ret = await _nopProductService.GetAllProductsAsync();
        _logger.LogInformation("Found {} stocks", ret.products.Count);
        return ret;
    }

    public async Task<IEnumerable<PohodaParameterDto>> GetAllPohodaParameters()
    {
        var result = await _parameterService.GetAllAsync();
        return result ?? Array.Empty<PohodaParameterDto>();
    }

    public async Task<NopSpecificationAttributes> GetAllNopSpecificationAttributes()
    {
        return await _nopSpecificationAttributeService.GetAllAsync();
    }

    public async Task DeleteAllStocksAsync()
    {
        _logger.LogInformation("Deleting all stocks");
        var nopStocks = await _nopProductService.GetAllProductsAsync();
        if (nopStocks is null)
        {
            return;
        }
        foreach (var nopStock in nopStocks.products)
        {
            await _nopProductService.DeleteProductAsync(nopStock.id);
        }
    }

    public async Task CreateAllStocksAsync()
    {
        //get all stocks from Pohoda
        ////TODO: filtering
        var pohodaStocks = await _stockService.GetAllStocksAsync();
        _logger.LogInformation("GoutOUt");

        if (pohodaStocks is null)
        {
            _logger.LogError("Didn't get any stocks");
            return;
        }

        _logger.LogInformation("pohodaStocks count {}", pohodaStocks.Count());

        //since there is only simple products so far, let's have only one method CreateNopProduct for now

        var simpleProducts = pohodaStocks.Where(p => !(p.IsVariantMasterStock() || p.HasVariant()));
        var productWithVariants = pohodaStocks.Where(p => p.IsVariantMasterStock() || p.HasVariant());

        await CreateSimpleProducts(simpleProducts);
        await CreateProductsWithVariants(productWithVariants);
    }


    private async Task CreateSimpleProducts(IEnumerable<StockDTO> simpleProducts)
    {   
        foreach (var simpleProduct in simpleProducts)
        {
            await CreateProduct(simpleProduct);
        }
    }

    private async Task<int> CreateProduct(StockDTO product, bool isSimple = true, int parentId = default(int))
    {
        var nopStock = (isSimple) ? _nopStockMapper.MapSimple(product, parentId) : _nopStockMapper.MapGroupedProduct(product);
        var productId = await _nopProductService.CreateProductAsync(nopStock);

        if (product.AlternativeImagesUrl != null && product.AlternativeImagesUrl.Any())
        {
            _logger.LogInformation("Alternative images: {}", product.AlternativeImagesUrl);
            await UpdateImages(product.AlternativeImagesUrl, nopStock, productId);
        }

        if (product.Categories != null && product.Categories.Length > 0)
        {
            await UpdateCategories(productId, product.Categories);
        }

        if (product.Parameters != null && product.Parameters.Count() > 0) 
        {
            await UpdateSpecification(productId, product.Parameters);
        }

        return productId;
    }

    private async Task CreateProductsWithVariants(IEnumerable<StockDTO> productsWithVariants)
    {
        var parentProducts = productsWithVariants.Where(p => p.IsVariantMasterStock()).ToList();
        foreach (var parentProduct in parentProducts)
        {
            var parentId = await CreateProduct(parentProduct, false);
            var versionProducts = productsWithVariants.Where(p => parentProduct.RelatedStocks.Any(rs => rs.Equals(p.Id)));
            foreach (var versionProduct in versionProducts)
            {
                await CreateProduct(versionProduct, true, parentId);
            }
        }
    }

    private async Task UpdateCategories(int id, IEnumerable<ushort> categories)
    {
        foreach (var categoryId in categories)
        {
            List<PohodaCategoryDto> path = new List<PohodaCategoryDto>();
            var pohodaCategory = await _categoryService.GetCategoryByIdAsync(categoryId, path);

            if (pohodaCategory is null)
                continue;

            var parentCatFromPath = path.LastOrDefault();
            Category nopCategory = null;
            if (parentCatFromPath == null)
            {
                nopCategory = await _nopCategoryService.GetCategoryByName(pohodaCategory.Name);

            }
            else
            {
                var parentCategory = await _nopCategoryService.GetCategoryByName(parentCatFromPath.Name);

                nopCategory = await _nopCategoryService.GetCategoryByName(pohodaCategory.Name, parentCategory?.id ?? 0);
            }

            if (nopCategory is null)
                continue;

            _logger.LogInformation("Pohoda category id: {} Nop category id: {}", pohodaCategory.PohodaID, nopCategory.id);
            await _nopProductService.AddCategory(id, nopCategory.id);
        }
    }

    private async Task UpdateSpecification(int id, IEnumerable<StockParameterDto> specifications)
    { 
        foreach (var specification in specifications) 
        { 
            if (specification is null || specification.Values is null || !specification.Values.Any()) continue;

            var attributeType = specification.Type.Equals("listValue") ? NopSpecificationAttributeType.Option : NopSpecificationAttributeType.CustomText;
            
            foreach (var value in specification.Values)
            {
                var optionId = (attributeType == NopSpecificationAttributeType.Option) ? GetOptionId(value.Value) : 0;
                var specValue = (attributeType == NopSpecificationAttributeType.Option) ? null : value.Value;

                await _nopProductService.AddSpecification(id, attributeType, optionId, specValue);
            }
        }
    }

    private int GetOptionId(string pohodaValue )
    {

        if (nopSpecificationAttributes == null)
        { 
            nopSpecificationAttributes = GetAllNopSpecificationAttributes().Result;
        }

        var allOptions = nopSpecificationAttributes.specification_attributes.SelectMany(attr => attr.specification_attribute_options);

        return allOptions.FirstOrDefault(option => option.name.Equals(pohodaValue, StringComparison.InvariantCultureIgnoreCase))?.id ?? 0;

    }

    private async Task UpdateImages(IEnumerable<string> alternativeImages, NopStock nopStock, int id)
    {
        var image = alternativeImages?.FirstOrDefault();
        var mapped = _imageHelper.GetStockImage(image);
        if (mapped == null) return;
        nopStock.product.images = new List<StockImage> { mapped }.ToArray();
        nopStock.product.id = id;
        await _nopProductService.UpdateProductAsync(nopStock);
    }
}
