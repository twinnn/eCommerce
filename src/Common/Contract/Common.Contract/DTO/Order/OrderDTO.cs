using System;
using System.Collections.Generic;

namespace Common.Contract.DTO.Order
{
    public class OrderDTO
    {
        public string OrderId { get; }
        public string Fullname { get; }
        public string City { get; }
        public string Address { get; }
        public string Zip { get; }
        public string Country { get; }
        public string CompanyName { get; }
        public string Email { get; }
        public DateTime PurchaseDate { get; }
        public string Phone { get; }
        public string OrderNumber { get; }
        public string Moss { get; }
        public string Carrier { get; }
        public List<OrderItemDTO> OrderItem { get; }
    }
}
