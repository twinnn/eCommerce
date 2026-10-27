using Common.Contract.DTO.Parameter;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pohoda.Contract.Services
{
    public interface IParameterService
    {
        Task<IEnumerable<PohodaParameterDto>?> GetAllAsync();
    }
}
