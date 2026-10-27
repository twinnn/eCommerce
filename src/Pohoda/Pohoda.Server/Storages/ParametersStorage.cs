using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Contract.DTO.Parameter;
using Common.Contract.Serialization;
using Microsoft.Extensions.Logging;
using Pohoda.Contract;
using Pohoda.Server.Mappers;
using Pohoda.Server.POCO.Parameter;

namespace Pohoda.Server.Storages
{
    public interface IParameterStorage
    {
        Task<IEnumerable<PohodaParameterDto>?> GetAllAsync(string storage);
    }

    public class ParametersStorage : IParameterStorage
    {
        private readonly ILogger<ParametersStorage> _logger;
        private readonly IStorageSystem _mServer;
        private readonly IAttributeToDtoMapper _mapper;
        private readonly ISerializer _serializer;

        public ParametersStorage(ILogger<ParametersStorage> logger, IStorageSystem mServer, ISerializer serializer, IAttributeToDtoMapper mapper)
        {
            _logger = logger;
            _mServer = mServer;
            _serializer = serializer;
            _mapper = mapper;
        }

        public async Task<IEnumerable<PohodaParameterDto>?> GetAllAsync(string storage)
        {

            try
            {
                await _mServer.StartServer();
                var mapped = new List<PohodaParameterDto>();
                var pohodaParameters = await GetParametersFromPohoda();
                if (pohodaParameters is { responsePackItem: { } })
                {
                    mapped = _mapper.Map(pohodaParameters).ToList();
                }

                return mapped;

            }
            catch (Exception ex)
            {
                _logger.LogError(exception: ex, message: "Error while getting parameters from Pohoda");
                return null;
            }

        }

        private async Task<GetParametersResponse.responsePack> GetParametersFromPohoda()
        {
            string request = @"
                <dat:dataPack xmlns:dat=""http://www.stormware.cz/schema/version_2/data.xsd"" xmlns:lst=""http://www.stormware.cz/schema/version_2/list.xsd"" id=""002"" ico=""53870441"" application=""klient"" version=""2.0"" note=""Export kategorii internetového obchodu"">
                    <dat:dataPackItem id=""a56"" version=""2.0"">
                        <lst:listIntParamRequest version=""2.0"">
                            <lst:requestIntParam/>
                        </lst:listIntParamRequest>
                    </dat:dataPackItem>
                </dat:dataPack>";

            var response = await _mServer.SendRequest(request);

            _logger.LogInformation("Deserializing response: {Response}", response);
            var responseDeserialized = _serializer.Deserialize<GetParametersResponse.responsePack>(response);

            return responseDeserialized;
        }
    }
}
