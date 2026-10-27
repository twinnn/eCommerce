using Jobs.Nop.Synch.Mappers;
using Microsoft.Extensions.Logging;
using Nop.Contract.Services;
using Pohoda.Contract.Services;
using Common.Contract.Extensions;

namespace Jobs.Nop.Synch.Processors;

public interface IAttributeProcessor
{
    Task CreateSpecificationAttributesAsync();
    Task DeleteAllSpecificationAttributesAsync();
}

public class AttributeProcessor : IAttributeProcessor
{
    private readonly IParameterService _parameterService;
    private readonly INopSpecificationAttributeService _nopSpecificationAttributeService;
    private readonly IDtoToNopAttributeMapper _toNopAttributeMapper;
    private readonly ILogger<AttributeProcessor> _logger;

    public AttributeProcessor(IParameterService parameterService, INopSpecificationAttributeService nopAttributeService, ILogger<AttributeProcessor> logger, IDtoToNopAttributeMapper toNopAttributeMapper)
    {
        _parameterService = parameterService;
        _nopSpecificationAttributeService = nopAttributeService;
        _logger = logger;
        _toNopAttributeMapper = toNopAttributeMapper;
    }

    public async Task CreateSpecificationAttributesAsync()
    {
        var parameters = await _parameterService.GetAllAsync();
        if (parameters != null)
        {
            foreach (var (parameter, index) in parameters.WithIndex())
            {
                if (parameter.Name.StartsWith("GN:"))
                {
                    try
                    {
                        var mapped = _toNopAttributeMapper.Map(parameter);
                        //mapped.specification_attribute.display_order = index+1;

                        if (mapped == null) continue;
                        _logger.LogInformation("Creating attribute: {}", mapped);
                        await _nopSpecificationAttributeService.CreateAsync(mapped);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error while processing parameter: {}", ex);
                    }
                }
            }
        }

    }

    public async Task DeleteAllSpecificationAttributesAsync()
    {
        await _nopSpecificationAttributeService.DeleteAllAsync();
    }
}