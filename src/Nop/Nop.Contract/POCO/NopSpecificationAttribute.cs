using System.Text.Json.Serialization;

namespace Nop.Contract.POCO;


public enum NopSpecificationAttributeType
{
    Option = 0,
    CustomText = 10

}
public class NopSpecificationAttribute
{

    public Specification_Attribute specification_attribute { get; set; }

}

public class NopSpecificationAttributes
{
    public List<Specification_Attribute> specification_attributes { get; set; }
}

public class Specification_Attribute
{
    public string name { get; set; }
    public int display_order { get; set; }
    public Specification_Attribute_Options[] specification_attribute_options { get; set; }
    public int id { get; set; }
}

public class Specification_Attribute_Options
{
    public int specification_attribute_id { get; set; }
    public string name { get; set; }
    public string color_squares_rgb { get; set; }
    public int display_order { get; set; }
    public int id { get; set; }
}