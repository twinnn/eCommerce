using System.Collections.Generic;
using System.Linq;
using Common.Contract.DTO.Parameter;
using Pohoda.Server.POCO.Parameter;

namespace Pohoda.Server.Mappers
{
    public interface IAttributeToDtoMapper
    {
        IEnumerable<PohodaParameterDto> Map(GetParametersResponse.responsePack attributes);
    }

    public class AttributeToDtoMapper : IAttributeToDtoMapper
    {
        public IEnumerable<PohodaParameterDto> Map(GetParametersResponse.responsePack attributes)
        {
            return attributes?.responsePackItem?.listIntParam.intParamDetail.Select(pohodaAttribute => new PohodaParameterDto
                {
                    Name = pohodaAttribute.intParam.name,
                    Type = pohodaAttribute.intParam.parameterType,
                    SubParameters = pohodaAttribute.intParam.parameterType == "listValue" ?
                        pohodaAttribute.intParam.parameterSettings.parameterList!.Select(pohodaAtt => new PohodaParameterDto
                        {
                            Name = pohodaAtt.name,
                        }).ToList() : null,
                    Value = pohodaAttribute.intParam.parameterType != "listValue" ? pohodaAttribute.intParam.parameterSettings.unit ?? pohodaAttribute.intParam.parameterSettings.length ?? null : null,
                });
        }
    }


}