using System.Collections.Generic;
using System.Linq;
using Common.Contract.DTO.Category;
using static Pohoda.Server.POCO.Category.GetCategoriesResponse;

namespace Pohoda.Server.Mappers
{
    public interface ICategoryToDtoMapper
    {
        IEnumerable<PohodaCategoryDto> Map(responsePack categories);
    }

    public class CategoryToDtoMapper : ICategoryToDtoMapper
    {
        public IEnumerable<PohodaCategoryDto> Map(responsePack categories)
        {
            return categories?.responsePackItem?.listCategory.categoryDetail.category.Select(pohodaCategory => new PohodaCategoryDto
            {
                Name = pohodaCategory.name,
                Subcategories = CreateSubcategories(pohodaCategory.subCategories),
                Image = pohodaCategory.picture,
                Description = pohodaCategory.description,
                PohodaID = pohodaCategory.id
            });
        }
        
        private List<PohodaCategoryDto>? CreateSubcategories(IEnumerable<category>? subcategories)
        {
            if (subcategories is null)
            {
                return null;
            }

            return subcategories.Select(subcategory => new PohodaCategoryDto
            {
                Name = subcategory.name,
                Subcategories = CreateSubcategories(subcategory.subCategories),
                Image = subcategory.picture,
                Description = subcategory.description,
                PohodaID = subcategory.id
            }).ToList();
        }
    }
}