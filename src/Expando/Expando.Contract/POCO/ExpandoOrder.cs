using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Expando.Contract.POCO
{
    public class ExpandoOrder
    {
        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        [XmlRoot(Namespace = "", IsNullable = false)]
        public class orders
        {
            private ordersOrder[] orderField;


            [XmlElement("order")]
            public ordersOrder[] order
            {
                get => orderField;
                set => orderField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrder
        {
            private bool businessOrderField;

            private string currencyCodeField;

            private ordersOrderCustomer customerField;

            private object deliveryBranchIdField;

            private ordersOrderDelivery deliveryField;

            private string fulfillmentChannelField;

            private object invoicesField;

            private object invoiceUrlsField;

            private bool isCompleteField;

            private bool isPremiumOrderField;

            private bool isPrimeField;

            private bool isRefundedField;

            private ordersOrderItem[] itemsField;

            private string languageField;

            private string latestDeliveryDateField;

            private string latestShipDateField;

            private string marketplaceField;
            private string orderIdField;

            private string orderStatusField;

            private ordersOrderPayment paymentField;

            private string paymentMethodField;

            private ordersOrderPrice priceField;

            private string purchaseDateField;

            private string shippingMethodField;

            private decimal shippingPriceField;

            private string shipServiceLevelField;

            private decimal totalItemTaxField;

            private decimal totalPriceField;

            private string venueField;


            public string orderId
            {
                get => orderIdField;
                set => orderIdField = value;
            }


            public string orderStatus
            {
                get => orderStatusField;
                set => orderStatusField = value;
            }


            public string purchaseDate
            {
                get => purchaseDateField;
                set => purchaseDateField = value;
            }


            public string marketplace
            {
                get => marketplaceField;
                set => marketplaceField = value;
            }


            public string venue
            {
                get => venueField;
                set => venueField = value;
            }


            public string fulfillmentChannel
            {
                get => fulfillmentChannelField;
                set => fulfillmentChannelField = value;
            }


            public bool businessOrder
            {
                get => businessOrderField;
                set => businessOrderField = value;
            }


            public decimal totalPrice
            {
                get => totalPriceField;
                set => totalPriceField = value;
            }


            public decimal totalItemTax
            {
                get => totalItemTaxField;
                set => totalItemTaxField = value;
            }


            public string currencyCode
            {
                get => currencyCodeField;
                set => currencyCodeField = value;
            }


            public string language
            {
                get => languageField;
                set => languageField = value;
            }


            public string paymentMethod
            {
                get => paymentMethodField;
                set => paymentMethodField = value;
            }


            public string shippingMethod
            {
                get => shippingMethodField;
                set => shippingMethodField = value;
            }


            public string shipServiceLevel
            {
                get => shipServiceLevelField;
                set => shipServiceLevelField = value;
            }


            public object deliveryBranchId
            {
                get => deliveryBranchIdField;
                set => deliveryBranchIdField = value;
            }


            public decimal shippingPrice
            {
                get => shippingPriceField;
                set => shippingPriceField = value;
            }


            public string latestShipDate
            {
                get => latestShipDateField;
                set => latestShipDateField = value;
            }


            public string latestDeliveryDate
            {
                get => latestDeliveryDateField;
                set => latestDeliveryDateField = value;
            }


            public bool isPremiumOrder
            {
                get => isPremiumOrderField;
                set => isPremiumOrderField = value;
            }


            public bool isPrime
            {
                get => isPrimeField;
                set => isPrimeField = value;
            }


            public bool isComplete
            {
                get => isCompleteField;
                set => isCompleteField = value;
            }


            public bool isRefunded
            {
                get => isRefundedField;
                set => isRefundedField = value;
            }


            public object invoiceUrls
            {
                get => invoiceUrlsField;
                set => invoiceUrlsField = value;
            }


            public object invoices
            {
                get => invoicesField;
                set => invoicesField = value;
            }


            public ordersOrderCustomer customer
            {
                get => customerField;
                set => customerField = value;
            }


            [XmlArrayItem("item", IsNullable = false)]
            public ordersOrderItem[] items
            {
                get => itemsField;
                set => itemsField = value;
            }


            public ordersOrderPrice price
            {
                get => priceField;
                set => priceField = value;
            }


            public ordersOrderPayment payment
            {
                get => paymentField;
                set => paymentField = value;
            }


            public ordersOrderDelivery delivery
            {
                get => deliveryField;
                set => deliveryField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderCustomer
        {
            private ordersOrderCustomerAddress addressField;
            private string? companyNameField;

            private string emailField;

            private string firstnameField;

            private string phoneField;

            private string surnameField;

            private object taxCountryField;

            private object taxIdField;


            public string? companyName
            {
                get => companyNameField;
                set => companyNameField = value;
            }


            public string firstname
            {
                get => firstnameField;
                set => firstnameField = value;
            }


            public string surname
            {
                get => surnameField;
                set => surnameField = value;
            }


            public string email
            {
                get => emailField;
                set => emailField = value;
            }


            public string phone
            {
                get => phoneField;
                set => phoneField = value;
            }


            public object taxId
            {
                get => taxIdField;
                set => taxIdField = value;
            }


            public object taxCountry
            {
                get => taxCountryField;
                set => taxCountryField = value;
            }


            public ordersOrderCustomerAddress address
            {
                get => addressField;
                set => addressField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderCustomerAddress
        {
            private string address1Field;

            private string address2Field;

            private object address3Field;

            private string cityField;

            private string countryField;

            private string stateOrRegionField;

            private string zipField;


            public string address1
            {
                get => address1Field;
                set => address1Field = value;
            }


            public string address2
            {
                get => address2Field;
                set => address2Field = value;
            }


            public object address3
            {
                get => address3Field;
                set => address3Field = value;
            }


            public string city
            {
                get => cityField;
                set => cityField = value;
            }


            public string zip
            {
                get => zipField;
                set => zipField = value;
            }


            public string stateOrRegion
            {
                get => stateOrRegionField;
                set => stateOrRegionField = value;
            }


            public string country
            {
                get => countryField;
                set => countryField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderItem
        {
            private ordersOrderItemDeliveryDiscount deliveryDiscountField;

            private ordersOrderItemDeliveryPrice deliveryPriceField;

            private object externalIdField;
            private uint itemIdField;

            private string itemNameField;

            private decimal itemPriceField;

            private byte itemQuantityField;

            private decimal itemTaxField;

            private ordersOrderItemLineItemDiscount lineItemDiscountField;

            private ordersOrderItemLineItemPrice lineItemPriceField;

            private ordersOrderItemMarketplaceCommission marketplaceCommissionField;

            private ulong orderItemIdField;

            private byte promotionDiscountField;

            private byte promotionDiscountTaxField;

            private byte shippingDiscountField;

            private byte shippingDiscountTaxField;

            private decimal shippingPriceField;

            private decimal shippingTaxField;


            public uint itemId
            {
                get => itemIdField;
                set => itemIdField = value;
            }


            public object externalId
            {
                get => externalIdField;
                set => externalIdField = value;
            }


            public decimal itemPrice
            {
                get => itemPriceField;
                set => itemPriceField = value;
            }


            public byte itemQuantity
            {
                get => itemQuantityField;
                set => itemQuantityField = value;
            }


            public string itemName
            {
                get => itemNameField;
                set => itemNameField = value;
            }


            public ulong orderItemId
            {
                get => orderItemIdField;
                set => orderItemIdField = value;
            }


            public decimal itemTax
            {
                get => itemTaxField;
                set => itemTaxField = value;
            }


            public byte shippingDiscount
            {
                get => shippingDiscountField;
                set => shippingDiscountField = value;
            }


            public byte shippingDiscountTax
            {
                get => shippingDiscountTaxField;
                set => shippingDiscountTaxField = value;
            }


            public byte promotionDiscount
            {
                get => promotionDiscountField;
                set => promotionDiscountField = value;
            }


            public byte promotionDiscountTax
            {
                get => promotionDiscountTaxField;
                set => promotionDiscountTaxField = value;
            }


            public decimal shippingPrice
            {
                get => shippingPriceField;
                set => shippingPriceField = value;
            }


            public decimal shippingTax
            {
                get => shippingTaxField;
                set => shippingTaxField = value;
            }


            public ordersOrderItemLineItemPrice lineItemPrice
            {
                get => lineItemPriceField;
                set => lineItemPriceField = value;
            }


            public ordersOrderItemLineItemDiscount lineItemDiscount
            {
                get => lineItemDiscountField;
                set => lineItemDiscountField = value;
            }


            public ordersOrderItemDeliveryPrice deliveryPrice
            {
                get => deliveryPriceField;
                set => deliveryPriceField = value;
            }


            public ordersOrderItemDeliveryDiscount deliveryDiscount
            {
                get => deliveryDiscountField;
                set => deliveryDiscountField = value;
            }


            public ordersOrderItemMarketplaceCommission marketplaceCommission
            {
                get => marketplaceCommissionField;
                set => marketplaceCommissionField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderItemLineItemPrice
        {
            private string taxField;

            private string taxRatePercentField;

            private object withoutTaxField;

            private decimal withTaxField;


            public string tax
            {
                get => taxField;
                set => taxField = value;
            }


            public string taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public decimal withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderItemLineItemDiscount
        {
            private object taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private object withTaxField;


            public object tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public object withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderItemDeliveryPrice
        {
            private string taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private string withTaxField;


            public string tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public string withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderItemDeliveryDiscount
        {
            private object taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private object withTaxField;


            public object tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public object withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderItemMarketplaceCommission
        {
            private object taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private object withTaxField;


            public object tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public object withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPrice
        {
            private ordersOrderPriceDelivery deliveryField;

            private ordersOrderPriceItems itemsField;

            private ordersOrderPricePayment paymentField;

            private ordersOrderPriceTotalDiscount totalDiscountField;

            private ordersOrderPriceTotal totalField;


            public ordersOrderPriceDelivery delivery
            {
                get => deliveryField;
                set => deliveryField = value;
            }


            public ordersOrderPriceItems items
            {
                get => itemsField;
                set => itemsField = value;
            }


            public ordersOrderPricePayment payment
            {
                get => paymentField;
                set => paymentField = value;
            }


            public ordersOrderPriceTotal total
            {
                get => totalField;
                set => totalField = value;
            }


            public ordersOrderPriceTotalDiscount totalDiscount
            {
                get => totalDiscountField;
                set => totalDiscountField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPriceDelivery
        {
            private string taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private string withTaxField;


            public string tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public string withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPriceItems
        {
            private string taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private string withTaxField;


            public string tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public string withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPricePayment
        {
            private object taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private object withTaxField;


            public object tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public object withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPriceTotal
        {
            private object taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private string withTaxField;


            public object tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public string withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPriceTotalDiscount
        {
            private object taxField;

            private object taxRatePercentField;

            private object withoutTaxField;

            private object withTaxField;


            public object tax
            {
                get => taxField;
                set => taxField = value;
            }


            public object taxRatePercent
            {
                get => taxRatePercentField;
                set => taxRatePercentField = value;
            }


            public object withoutTax
            {
                get => withoutTaxField;
                set => withoutTaxField = value;
            }


            public object withTax
            {
                get => withTaxField;
                set => withTaxField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPayment
        {
            private ordersOrderPaymentCashOnDelivery cashOnDeliveryField;
            private string paymentMethodField;


            public string paymentMethod
            {
                get => paymentMethodField;
                set => paymentMethodField = value;
            }


            public ordersOrderPaymentCashOnDelivery cashOnDelivery
            {
                get => cashOnDeliveryField;
                set => cashOnDeliveryField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderPaymentCashOnDelivery
        {
            private object servicePriceField;
            private object toPayField;


            public object toPay
            {
                get => toPayField;
                set => toPayField = value;
            }


            public object servicePrice
            {
                get => servicePriceField;
                set => servicePriceField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true)]
        public class ordersOrderDelivery
        {
            private object shippingCarrierField;

            private object shippingCarrierServiceField;


            public object shippingCarrier
            {
                get => shippingCarrierField;
                set => shippingCarrierField = value;
            }


            public object shippingCarrierService
            {
                get => shippingCarrierServiceField;
                set => shippingCarrierServiceField = value;
            }
        }
    }
}