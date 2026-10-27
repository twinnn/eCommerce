using Common.Contract.DTO.Category;
using Jobs.Nop.Synch.Helpers;
using Jobs.Nop.Synch.Mappers;
using Microsoft.Extensions.Logging;
using Nop.Contract.Services;
using Pohoda.Contract.Services;
using Common.Contract.Extensions;

namespace Jobs.Nop.Synch.Processors;

public interface ICategoryProcessor
{
    Task CreateCategoriesAsync();
    Task DeleteAllCategoriesAsync();
    Task GetAllCategoriesAsync();
    Task<PohodaCategoryDto> GetCategoryByIdAsync(int id);
}

public class CategoryProcessor : ICategoryProcessor
{
    private readonly ICategoryService _categoryService;
    private readonly IImageHelper _imageHelper;
    private readonly INopCategoryService _nopCategoryService;
    private readonly ILogger<CategoryProcessor> _logger;

    public static List<string> WhiteListedCategories = new List<string> { "Palice" };

    public CategoryProcessor(ICategoryService categoryService, INopCategoryService nopCategoryService, ILogger<CategoryProcessor> logger, IImageHelper imageHelper)
    {
        _categoryService = categoryService;
        _nopCategoryService = nopCategoryService;
        _logger = logger;
        _imageHelper = imageHelper;
    }

    public async Task CreateCategoriesAsync()
    {
        var categories = await _categoryService.GetAllCategories();

        var whiteListed = categories.SelectMany(c => c.Subcategories).Where(sc => WhiteListedCategories.Contains(sc.Name));
        
        foreach (var (category, index) in whiteListed.WithIndex())
        {
            var categoryDto = DtoToNopCategory.Map(category);

            if (string.IsNullOrEmpty(category.Name)) continue;

            if (!string.IsNullOrEmpty(category.Image))
            {
                var mapped = _imageHelper.GetImage(category.Image);

                if (mapped != null)
                {
                    categoryDto.category.image = mapped;
                }
            }

            categoryDto.category.display_order = index + 1;
            var nopCategories = await _nopCategoryService.CreateCategoryAsync(categoryDto);

            if (category.Subcategories is null) continue;

            var nopCategoryId = nopCategories.categories.FirstOrDefault()?.id ?? -1;

            if (nopCategoryId > -1)
            {
                await CreateSubcategories(category, categoryDto.category.se_name, nopCategoryId);
            }
        }
    }

    private async Task CreateSubcategories(PohodaCategoryDto parentCategory, string parentCategorySeName, int parentNopCategoryId)
    {
        if (parentCategory != null && parentCategory.Subcategories != null)
        {
            foreach (var (subcategory, index) in parentCategory.Subcategories.WithIndex())
            {
                var include = parentCategory.Name.Equals("Palice", StringComparison.InvariantCultureIgnoreCase);
                var subcategoryDto = DtoToNopCategory.Map(subcategory, parentCategorySeName, parentNopCategoryId, include);

                if (string.IsNullOrEmpty(subcategoryDto.category.name)) continue;

                if (!string.IsNullOrEmpty(subcategory.Image))
                {
                    var mapped = _imageHelper.GetImage(subcategory.Image);

                    if (mapped != null)
                    {
                        subcategoryDto.category.image = mapped;
                    }
                }

                subcategoryDto.category.display_order = index + 1;
                var nopCategories = await _nopCategoryService.CreateCategoryAsync(subcategoryDto);

                if (subcategory.Subcategories is null) continue;

                var nopCategoryId = nopCategories.categories.FirstOrDefault()?.id ?? -1;

                if (nopCategoryId > -1)
                {
                    await CreateSubcategories(subcategory, subcategoryDto.category.se_name, nopCategoryId);
                }
            }
        }
    }

    public async Task GetAllCategoriesAsync()
    {
        var res = await _categoryService.GetAllCategories();
        _logger.LogInformation("Categories: {0}", res.Count());
    }

    public Task<PohodaCategoryDto?> GetCategoryByIdAsync(int id)
    {
        return _categoryService.GetCategoryByIdAsync(id);
    }

    public async Task DeleteAllCategoriesAsync()
    {
        await _nopCategoryService.DeleteAllCategoriesAsync();
    }

}