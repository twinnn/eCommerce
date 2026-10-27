namespace Nop.Contract.POCO;

public class AddCategoryRequest
{
    public Objectpropertynamevaluepairs ObjectPropertyNameValuePairs { get; set; }
        public Product_Category_Mapping product_category_mapping { get; set; }

}

public class Product_Category_Mapping
{
    public int product_id { get; set; }
    public int category_id { get; set; }
    public bool is_featured_product { get; set; }
    public int display_order { get; set; }
    public int id { get; set; }
}