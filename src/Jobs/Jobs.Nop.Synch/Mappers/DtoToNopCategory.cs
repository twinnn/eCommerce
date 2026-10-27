using Common.Contract.DTO.Category;
using Nop.Contract.POCO;
using System.Text;
using System.Text.RegularExpressions;

namespace Jobs.Nop.Synch.Mappers;

public class DtoToNopCategory
{
    public static NopCategory Map(PohodaCategoryDto categoryDto, string parentSeName = "", int parentId = 0 , bool include = false)
    {

        return new NopCategory
        {
            category = new Category()
            {
                name = categoryDto.Name,
                se_name = string.IsNullOrEmpty(parentSeName) ? RemoveDiacritics(categoryDto.Name) : parentSeName + '_' + RemoveDiacritics(categoryDto.Name),
                description = categoryDto.Description,
                category_template_id = 1,
                parent_category_id = parentId,
                page_size = 0,
                include_in_top_menu = include,
                show_on_home_page = include,
                published = true,
                deleted = false,
                display_order = 0,
                id = 0                
            }
        };
    }

    public static string RemoveDiacritics(string input)
    {
        string normalized = input.Normalize(NormalizationForm.FormD);
        string pattern = @"[\p{Mn}]"; // Matches all non-spacing marks
        string result = Regex.Replace(normalized, pattern, String.Empty);

        return result.Normalize(NormalizationForm.FormC);
    }

    internal static object Map(PohodaCategoryDto subcategory, object removeDiacritics, int parentNopCategoryId, bool include)
    {
        throw new NotImplementedException();
    }
}