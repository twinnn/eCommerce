
namespace Nop.Contract.POCO
{
    public class AddSpecificationRequest
    {
        public Objectpropertynamevaluepairs ObjectPropertyNameValuePairs { get; set; }
        public Product_Specification_Attribute product_specification_attribute { get; set; }
    }

    public class Product_Specification_Attribute
    {
        public int product_id { get; set; }
        public int attribute_type_id { get; set; }
        public int specification_attribute_option_id { get; set; }
        public string custom_value { get; set; }
        public string attribute_type { get; set; }
        public bool show_on_product_page { get; set; }
        public int id { get; set; }
    }
}
