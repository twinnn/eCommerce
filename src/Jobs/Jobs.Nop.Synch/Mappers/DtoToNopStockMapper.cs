using Common.Contract.DTO.Stock;
using Nop.Contract.POCO;

namespace Jobs.Nop.Synch.Mappers;

public interface IDtoToNopStockMapper
{
    public NopStock MapSimple(StockDTO stock, int parentId = default(int));
    public NopStock MapGroupedProduct(StockDTO stock);
    public IEnumerable<StockImage> MapImages(StockDTO stock);
}

public class DtoToNopStockMapper : IDtoToNopStockMapper
{
    private const string SimpleProduct = "SimpleProduct";
    private const string GroupedProduct = "GroupedProduct";

    public NopStock MapSimple(StockDTO stock, int parentId = default(int))
    {
        var result = Map(stock);
        result.product.product_type = SimpleProduct;
        result.product.parent_grouped_product_id = parentId;
        if (parentId != default(int)) 
        {
            result.product.visible_individually = false;
        }

        return result;
    }

    public NopStock MapGroupedProduct(StockDTO stock)
    {
        var result = Map(stock);
        result.product.product_type = GroupedProduct;

        return result;
    }


    protected NopStock Map(StockDTO stock)
    {
        return new NopStock
        {
            ObjectPropertyNameValuePairs = null,
            product = new Product
            {
                visible_individually = true,
                name = stock.Name,
                short_description= stock.ShortDescription,
                full_description = stock.LongDescription,
                show_on_home_page = false,
                allow_customer_reviews = false,
                sku = stock.SKU.ToString(),
                gtin = stock.EAN.ToString(),
                is_gift_card = false,
                stock_quantity = Convert.ToInt32(stock.Count),
                require_other_products = false,
                automatically_add_required_products = true,
                is_download = false,
                is_recurring = false,
                is_rental = false,
                is_ship_enabled = true,
                is_free_shipping = false,
                ship_separately = false,
                additional_shipping_charge = 5,
                is_tax_exempt = false,
                tax_category_id = 0,
                is_telecommunications_or_broadcasting_or_electronic_services = false,
                use_multiple_warehouses = false,
                display_stock_availability = true,
                display_stock_quantity = false,
                allow_back_in_stock_subscriptions = false,
                allow_adding_only_existing_attribute_combinations = false,
                disable_buy_button = false,
                disable_wishlist_button = false,
                available_for_pre_order = false,
                call_for_price = false,
                price = stock.Price,
                customer_enters_price = false,
                has_tier_prices = false,
                has_discounts_applied = false,
                weight = stock.Weight,
                length = stock.Lenght,
                width = stock.Width,
                height = stock.Height,
                display_order = 0,
                published = true,
                deleted = false,
                id = 0                
            }
        };
    }

    public IEnumerable<StockImage> MapImages(StockDTO stock)
    {
        var images = new List<StockImage>();
        if (stock == null)
        {


        }
        var altImages = stock.AlternativeImagesUrl?.Select(x => new StockImage
        {
            id = 0,
            picture_id = 0,
            position = 0,
            src = @$"\\AzetCool-Pohoda\POHODA_SK_E1_DATA\Dokumenty\ACecom\Obrázky\{x.Split('\\').Last()}",
        }).ToList();

        images.Add(new StockImage
        {
            id = 0,
            picture_id = 0,
            position = 0,
            src = @$"\\AzetCool-Pohoda\POHODA_SK_E1_DATA\Dokumenty\ACecom\Obrázky\{stock.ImgFilePath}",
        });

        if (altImages != null)
        {
            images.AddRange(altImages);
        }

        return images;
    }
}