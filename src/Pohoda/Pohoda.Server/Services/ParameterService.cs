using Common.Contract.DTO.Parameter;
using Pohoda.Contract.Services;
using Pohoda.Server.Storages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pohoda.Server.Services
{
    public class ParameterService : IParameterService
    {
        //public const string parametersCacheKey = "Pohoda_Parameters";
        private readonly IParameterStorage parameterStorage;

        public ParameterService(IParameterStorage parameterStorage)
        {
            this.parameterStorage = parameterStorage;
        }

        public async Task<IEnumerable<PohodaParameterDto>?> GetAllAsync()
        {
            return await parameterStorage.GetAllAsync(string.Empty);
        }
    }
}
