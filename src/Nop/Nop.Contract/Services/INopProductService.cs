using Nop.Contract.POCO;

namespace Nop.Contract.Services;

public interface INopProductService
{
    Task<int> CreateProductAsync(NopStock stock);
    Task<NopStocks> GetAllProductsAsync();
    Task DeleteProductAsync(int id);
    Task UpdateProductAsync(NopStock nopStock);
    Task AddCategory(int id, int nopCategoryId);
    Task AddSpecification(int productId, NopSpecificationAttributeType attributeType, int optionId, string customValue);
}