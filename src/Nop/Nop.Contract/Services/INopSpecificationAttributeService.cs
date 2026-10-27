using Nop.Contract.POCO;

namespace Nop.Contract.Services;

public interface INopSpecificationAttributeService
{
    Task<NopSpecificationAttributes> GetAllAsync();
    Task CreateAsync(NopSpecificationAttribute attribute);
    Task CreateAsync(IEnumerable<NopSpecificationAttribute> attributes);
    Task DeleteAllAsync();
}