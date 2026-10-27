namespace Nop.Contract.POCO;

public class NopStock
{
    public Objectpropertynamevaluepairs ObjectPropertyNameValuePairs { get; set; }
    public Product product { get; set; }
}

public class NopStocks
{
    public List<Product> products { get; set; }
}

public class Objectpropertynamevaluepairs
{
}

public class Product
{
    public bool visible_individually { get; set; }
    public string name { get; set; }
    public string short_description { get; set; }
    public string full_description { get; set; }
    public bool show_on_home_page { get; set; }
    public bool allow_customer_reviews { get; set; }
    public string sku { get; set; }
    public string gtin { get; set; }
    public bool is_gift_card { get; set; }
    public bool require_other_products { get; set; }
    public bool automatically_add_required_products { get; set; }
    public bool is_download { get; set; }
    public bool unlimited_downloads { get; set; }
    public bool has_sample_download { get; set; }
    public bool has_user_agreement { get; set; }
    public bool is_recurring { get; set; }
    public bool is_rental { get; set; }
    public bool is_ship_enabled { get; set; }
    public bool is_free_shipping { get; set; }
    public bool ship_separately { get; set; }
    public decimal additional_shipping_charge { get; set; }
    public bool is_tax_exempt { get; set; }
    public int tax_category_id { get; set; }
    public bool is_telecommunications_or_broadcasting_or_electronic_services { get; set; }
    public bool use_multiple_warehouses { get; set; }
    public int stock_quantity { get; set; }
    public bool display_stock_availability { get; set; }
    public bool display_stock_quantity { get; set; }
    public int min_stock_quantity { get; set; }
    public int notify_admin_for_quantity_below { get; set; }
    public bool allow_back_in_stock_subscriptions { get; set; }
    public bool allow_adding_only_existing_attribute_combinations { get; set; }
    public bool disable_buy_button { get; set; }
    public bool disable_wishlist_button { get; set; }
    public bool available_for_pre_order { get; set; }
    public bool call_for_price { get; set; }
    public decimal price { get; set; }
    public bool customer_enters_price { get; set; }
    public bool has_tier_prices { get; set; }
    public bool has_discounts_applied { get; set; }
    public float weight { get; set; }
    public float length { get; set; }
    public float width { get; set; }
    public float height { get; set; }
    public int display_order { get; set; }
    public bool published { get; set; }
    public bool deleted { get; set; }
    public string product_type { get; set; }
    public int parent_grouped_product_id { get; set; }
    public StockImage[] images { get; set; }
    public ProductAttributeMapping[] attributes { get; set; }
    public ProductAttributeCombinations[] product_attribute_combinations { get; set; }
    public ProductSpecificationAttributes[] product_specification_attributes { get; set; }
    public string se_name { get; set; }
    public int id { get; set; }
}

public class StockImage
{
    public int id { get; set; }
    public int picture_id { get; set; }
    public int position { get; set; }
    public string src { get; set; }
    public string attachment { get; set; }
}

public class ProductAttributeMapping
{
    public int product_attribute_id { get; set; }
    public string product_attribute_name { get; set; }
    public string text_prompt { get; set; }
    public bool is_required { get; set; }
    public int attribute_control_type_id { get; set; }
    public int display_order { get; set; }
    public string default_value { get; set; }
    public Attribute_Values[] attribute_values { get; set; }
    public int id { get; set; }
}

public class Attribute_Values
{
    public int type_id { get; set; }
    public int associated_product_id { get; set; }
    public string name { get; set; }
    public string color_squares_rgb { get; set; }
    public Image_Squares_Image image_squares_image { get; set; }
    public decimal price_adjustment { get; set; }
    public decimal weight_adjustment { get; set; }
    public decimal cost { get; set; }
    public int quantity { get; set; }
    public bool is_pre_selected { get; set; }
    public int display_order { get; set; }
    public string type { get; set; }
    public int id { get; set; }
}

public class Image_Squares_Image
{
    public string src { get; set; }
    public string attachment { get; set; }
}

public class ProductAttributeCombinations
{
    public int product_id { get; set; }
    public string attributes_xml { get; set; }
    public int stock_quantity { get; set; }
    public string sku { get; set; }
    public string manufacturer_part_number { get; set; }
    public string gtin { get; set; }
    public int overridden_price { get; set; }
    public int picture_id { get; set; }
    public int id { get; set; }
}

public class ProductSpecificationAttributes
{
    public int product_id { get; set; }
    public int attribute_type_id { get; set; }
    public int specification_attribute_option_id { get; set; }
    public string custom_value { get; set; }
    public bool allow_filtering { get; set; }
    public bool show_on_product_page { get; set; }
    public int display_order { get; set; }
    public string attribute_type { get; set; }
    public SpecificationAttributeOption specification_attribute_option { get; set; }
    public int id { get; set; }
}

public class SpecificationAttributeOption
{
    public int specification_attribute_id { get; set; }
    public string name { get; set; }
    public string color_squares_rgb { get; set; }
    public int display_order { get; set; }
    public int id { get; set; }
}


