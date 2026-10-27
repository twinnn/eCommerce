namespace Nop.Contract.POCO;

public class NopCategories
{
    public Category[] categories { get; set; }
}

public class NopCategory
{
    public Objectpropertynamevaluepairs ObjectPropertyNameValuePairs { get; set; }
    public Category category { get; set; }
}

public class Category
{
    public string name { get; set; }
    public string description { get; set; }
    public int category_template_id { get; set; }
    // public object meta_keywords { get; set; }
    // public object meta_description { get; set; }
    // public string meta_title { get; set; }
    public int parent_category_id { get; set; }
    public int page_size { get; set; }
    // public string page_size_options { get; set; }
    // public object price_ranges { get; set; }
    public object show_on_home_page { get; set; }
    public bool include_in_top_menu { get; set; }
    // public bool has_discounts_applied { get; set; }
    public bool published { get; set; }
    public bool deleted { get; set; }
    public int display_order { get; set; }
    // public DateTime created_on_utc { get; set; }
    // public DateTime updated_on_utc { get; set; }
    // public object[] role_ids { get; set; }
    // public object[] discount_ids { get; set; }
    // public object[] store_ids { get; set; }
    public Image image { get; set; }
    public string se_name { get; set; }
    public int id { get; set; }
}

public class Image
{
    public string src { get; set; }
    public object attachment { get; set; }
}