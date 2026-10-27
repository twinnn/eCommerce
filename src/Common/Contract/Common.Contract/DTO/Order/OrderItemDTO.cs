
namespace Common.Contract.DTO.Order
{
    public class OrderItemDTO
    {
        public byte Quantity { get; }
        public string? Ean { get; }
        public decimal Price { get; }
        public string? Text { get; }
    }
}
