using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Contract.DTO.Category;

namespace Pohoda.Contract.Services
{
    public interface ICategoryService
    {
        Task<bool> Exists(string categoryString, string delimiter = " ", string storage = "");
        Task CreateCategory(string categoryString, string storage, string delimiter = " ", string startingPath = "");
        Task<IEnumerable<PohodaCategoryDto>?> GetAllCategories();
        Task<PohodaCategoryDto?> GetCategoryByIdAsync(int id, List<PohodaCategoryDto> path = null);
    }
}