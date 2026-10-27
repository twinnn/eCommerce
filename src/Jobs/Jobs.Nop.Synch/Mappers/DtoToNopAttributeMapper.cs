using Common.Contract.DTO.Parameter;
using Nop.Contract.POCO;

namespace Jobs.Nop.Synch.Mappers;

public interface IDtoToNopAttributeMapper   
{
    NopSpecificationAttribute? Map(PohodaParameterDto? other);
}

public class DtoToNopAttributeMapper : IDtoToNopAttributeMapper
{
    public NopSpecificationAttribute? Map(PohodaParameterDto? other)
    {
        if (other == null)
        {
            return null;
        }    

        return new NopSpecificationAttribute()
        {
            specification_attribute = new Specification_Attribute()
            {
                name = other.Name.Split(':').Last(),
                display_order = 0,
                specification_attribute_options = other.Type == "listValue" ? GetSpecificationAttributeList(other.SubParameters!) : null,
            }
        };
    }

    private Specification_Attribute_Options[] GetSpecificationAttributeList(IEnumerable<PohodaParameterDto> values)
    {
        return values.Select((x, index) => new Specification_Attribute_Options()
        {
            specification_attribute_id = index + 10,
            name = x.Name,
            color_squares_rgb = "",
            display_order = index + 1
        }).ToArray();
    }
}

