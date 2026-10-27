using Microsoft.Extensions.Logging;
using Nop.Contract.Poco;
using Nop.Contract.POCO;
using Nop.Contract.Services;

namespace Nop.ApiConsumer.Services;

public class NopSpecificationAttributeService : INopSpecificationAttributeService
{
    private readonly ILogger<NopSpecificationAttributeService> _logger;
    private readonly INopBaseApiConsumer<NopSpecificationAttribute, NopSpecificationAttributes> _apiConsumerNopSpecificationAttribute;
    private readonly INopBaseApiConsumer<NopSpecificationAttributes, object> _apiConsumerNopSpecificationAttributes;

    public NopSpecificationAttributeService(ILogger<NopSpecificationAttributeService> logger, INopBaseApiConsumer<NopSpecificationAttribute, NopSpecificationAttributes> apiConsumer, INopBaseApiConsumer<NopSpecificationAttributes, object> apiConsumerNopSpecificationAttributes)
    {
        _logger = logger;
        _apiConsumerNopSpecificationAttribute = apiConsumer;
        _apiConsumerNopSpecificationAttributes = apiConsumerNopSpecificationAttributes;
    }

    public async Task DeleteAllAsync()
    {
        try
        {
            await _apiConsumerNopSpecificationAttribute.InitAsync();
            await _apiConsumerNopSpecificationAttributes.InitAsync();

            var attributes = await GetAllAsync();

            var tasks = attributes.specification_attributes.Select(async item =>
            {
                await _apiConsumerNopSpecificationAttribute.DeleteAsync($"specificationattributes/{item.id}");
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while deleting all attributes {}", ex);
            throw;
        }
    }

    public async Task CreateAsync(IEnumerable<NopSpecificationAttribute> attributes)
    {
        try
        {
            await _apiConsumerNopSpecificationAttribute.InitAsync();
            await _apiConsumerNopSpecificationAttributes.InitAsync();


            var tasks = attributes.Select(async item =>
            {
                await CreateAsync(item);
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while deleting all attributes {}", ex);
            throw;
        }
    }

    public async Task CreateAsync(NopSpecificationAttribute attribute)
    {
        try
        {
            _logger.LogDebug("Creating a new attribute");
            await _apiConsumerNopSpecificationAttribute.InitAsync();

            var attributeWithNoOptions = await _apiConsumerNopSpecificationAttribute.PostTAsync("specificationattributes", attribute, true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while creating new attribute: {}", ex);
            throw;
        }
    }

    public async Task<NopSpecificationAttributes> GetAllAsync()
    {
        
        try
        {
            _logger.LogDebug("Getting all attributes");

            await _apiConsumerNopSpecificationAttributes.InitAsync();
            return await _apiConsumerNopSpecificationAttributes.GetAsync("specificationattributes?Limit=250");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while getting all attributes {}", ex);
            throw;
        }
    }
}