using Nop.Contract.POCO;

namespace Nop.Contract.Services;

public interface INopCategoryService
{
    Task<NopCategories> GetAllCategoriesAsync();
    Task<Category?> GetCategoryByName(string pohodaCategoryName, int parentCategoryId = 0);
    Task<NopCategories> CreateCategoryAsync(NopCategory category);
    Task DeleteAllCategoriesAsync();

}