using System.Collections.Generic;
using System.Linq;
using Common.Contract.DTO.Category;

namespace Pohoda.Server.Helpers
{
    public interface ICategoryHelper
    {
        bool SearchMainCategory(string categoryToFind, IEnumerable<string>? subCategoriesToFind,
            PohodaCategoryDto currentCategory);

        PohodaCategoryDto? FindById(string id, IEnumerable<PohodaCategoryDto> categories, List<PohodaCategoryDto> path = null);
    }

    public class CategoryHelper : ICategoryHelper
    {
        public PohodaCategoryDto? SearchSubCategory(string categoryToFind,
            PohodaCategoryDto currentCategory)
        {
            if (categoryToFind == currentCategory.Name) return currentCategory;

            // If currentCategory.subCategories is null or empty then return null otherwise search subcategories
            return currentCategory.Subcategories is { Count: 0 }
                ? null
                : currentCategory.Subcategories?.Select(category => SearchSubCategory(categoryToFind, category))
                    .FirstOrDefault(result => result != null);
        }

        public bool SearchForDirectSubCategories(IEnumerable<string>? subCategoriesToFind,
            PohodaCategoryDto category)
        {
            // Recursively search every subcategory until we find subcategory in subCategories list that doesn't exist in pohoda
            foreach (var subCategory in subCategoriesToFind)
            {
                var contains = SearchSubCategory(subCategory, category);

                if (contains == null) return false;

                category = contains;
            }

            return true;
        }

        public bool SearchMainCategory(string categoryToFind, IEnumerable<string>? subCategoriesToFind,
            PohodaCategoryDto currentCategory)
        {
            if (currentCategory.Name == categoryToFind)
                return SearchForDirectSubCategories(subCategoriesToFind, currentCategory);

            // Return null if category wasn't found, if it was return subcategory that contained desired category
            return currentCategory.Subcategories != null && currentCategory.Subcategories.Count != 0 &&
                   currentCategory.Subcategories.Any(category =>
                   {
                       var cat = SearchSubCategory(categoryToFind, category);
                       return cat != null && SearchForDirectSubCategories(subCategoriesToFind, cat);
                   });
        }

        public PohodaCategoryDto? FindById(string id, IEnumerable<PohodaCategoryDto> categories, List<PohodaCategoryDto> path = null)
        {
            // write searching algorithm here categories can have subcategories and subcategories can also have subcategories

            if (path == null)
            {
                path = new List<PohodaCategoryDto>();
            }

            foreach (var category in categories)
            {
                if (category.PohodaID == id)
                {
                    return category;
                }                

                if (category.Subcategories == null || category.Subcategories.Count == 0)
                {
                    continue;
                }


                path.Add(category);

                var found = FindById(id, category.Subcategories, path);

                if (found != null)
                {
                    return found;
                }
                path.RemoveAt(path.Count - 1);
            }

            
            return null;
        }

    }
}