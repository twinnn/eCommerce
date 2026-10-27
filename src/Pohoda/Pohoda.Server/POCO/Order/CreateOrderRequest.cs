#pragma warning disable CS8618
// ReSharper disable InconsistentNaming

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Order
{
    public class CreateOrderRequest
    {
        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/data.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/data.xsd", IsNullable = false)]
        public class dataPack
        {
            private string applicationField;
            private dataPackDataPackItem dataPackItemField;

            private uint icoField;

            private string idField;

            private string noteField;

            private decimal versionField;

            public dataPackDataPackItem dataPackItem
            {
                get => dataPackItemField;
                set => dataPackItemField = value;
            }

            [XmlAttribute]
            public string id
            {
                get => idField;
                set => idField = value;
            }

            [XmlAttribute]
            public uint ico
            {
                get => icoField;
                set => icoField = value;
            }

            [XmlAttribute]
            public string application
            {
                get => applicationField;
                set => applicationField = value;
            }

            [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }

            [XmlAttribute]
            public string note
            {
                get => noteField;
                set => noteField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/data.xsd")]
        public class dataPackDataPackItem
        {
            private string idField;
            private order orderField;

            private decimal versionField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
            public order order
            {
                get => orderField;
                set => orderField = value;
            }

            [XmlAttribute]
            public string id
            {
                get => idField;
                set => idField = value;
            }

            [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/order.xsd", IsNullable = false)]
        public class order
        {
            private orderOrderItem[] orderDetailField;
            private orderOrderHeader orderHeaderField;

            private orderOrderSummary orderSummaryField;

            private decimal versionField;

            public orderOrderHeader orderHeader
            {
                get => orderHeaderField;
                set => orderHeaderField = value;
            }

            [XmlArrayItem("orderItem", IsNullable = false)]
            public orderOrderItem[] orderDetail
            {
                get => orderDetailField;
                set => orderDetailField = value;
            }

            public orderOrderSummary orderSummary
            {
                get => orderSummaryField;
                set => orderSummaryField = value;
            }

            [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeader
        {
            private orderOrderHeaderCarrier carrierField;

            private DateTime dateField;

            private DateTime dateFromField;

            private DateTime dateToField;

            private orderOrderHeaderEvidentiaryResourcesMOSS evidentiaryResourcesMOSSField;

            private bool isDeliveredField;

            private bool isExecutedField;

            private orderOrderHeaderMOSS mOSSField;

            private orderOrderHeaderNumber numberField;

            private string numberOrderField;
            private string orderTypeField;

            private orderOrderHeaderPartnerIdentity partnerIdentityField;

            private orderOrderHeaderPaymentType paymentTypeField;

            private orderOrderHeaderPriceLevel priceLevelField;

            private string textField;

            public string orderType
            {
                get => orderTypeField;
                set => orderTypeField = value;
            }

            public string numberOrder
            {
                get => numberOrderField;
                set => numberOrderField = value;
            }

            [XmlElement(DataType = "date")]
            public DateTime date
            {
                get => dateField;
                set => dateField = value;
            }

            [XmlElement(DataType = "date")]
            public DateTime dateFrom
            {
                get => dateFromField;
                set => dateFromField = value;
            }

            [XmlElement(DataType = "date")]
            public DateTime dateTo
            {
                get => dateToField;
                set => dateToField = value;
            }

            public string text
            {
                get => textField;
                set => textField = value;
            }

            public orderOrderHeaderPartnerIdentity partnerIdentity
            {
                get => partnerIdentityField;
                set => partnerIdentityField = value;
            }

            public orderOrderHeaderPaymentType paymentType
            {
                get => paymentTypeField;
                set => paymentTypeField = value;
            }

            public orderOrderHeaderPriceLevel priceLevel
            {
                get => priceLevelField;
                set => priceLevelField = value;
            }

            public orderOrderHeaderMOSS MOSS
            {
                get => mOSSField;
                set => mOSSField = value;
            }

            public orderOrderHeaderEvidentiaryResourcesMOSS evidentiaryResourcesMOSS
            {
                get => evidentiaryResourcesMOSSField;
                set => evidentiaryResourcesMOSSField = value;
            }

            public bool isExecuted
            {
                get => isExecutedField;
                set => isExecutedField = value;
            }

            public orderOrderHeaderNumber number
            {
                get => numberField;
                set => numberField = value;
            }

            public bool isDelivered
            {
                get => isDeliveredField;
                set => isDeliveredField = value;
            }

            public orderOrderHeaderCarrier carrier
            {
                get => carrierField;
                set => carrierField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderPartnerIdentity
        {
            private address addressField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public address address
            {
                get => addressField;
                set => addressField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd", IsNullable = false)]
        public class address
        {
            private string cityField;
            private string? companyField;

            private addressCountry countryField;

            private string dicField;

            private string divisionField;

            private string emailField;

            private uint icoField;

            private string mobilPhoneField;

            private string nameField;

            private string streetField;

            private string zipField;

            public string? company
            {
                get => companyField;
                set => companyField = value;
            }

            public string division
            {
                get => divisionField;
                set => divisionField = value;
            }

            public string name
            {
                get => nameField;
                set => nameField = value;
            }

            public string city
            {
                get => cityField;
                set => cityField = value;
            }

            public string street
            {
                get => streetField;
                set => streetField = value;
            }

            public string zip
            {
                get => zipField;
                set => zipField = value;
            }

            public uint ico
            {
                get => icoField;
                set => icoField = value;
            }

            public string dic
            {
                get => dicField;
                set => dicField = value;
            }

            public string email
            {
                get => emailField;
                set => emailField = value;
            }

            public addressCountry country
            {
                get => countryField;
                set => countryField = value;
            }

            public string mobilPhone
            {
                get => mobilPhoneField;
                set => mobilPhoneField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
        public class addressCountry
        {
            private string idsField;

            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderPaymentType
        {
            private string idsField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderPriceLevel
        {
            private string idsField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderMOSS
        {
            private string idsField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderEvidentiaryResourcesMOSS
        {
            private string idsField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderNumber
        {
            private string numberRequestedField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string numberRequested
            {
                get => numberRequestedField;
                set => numberRequestedField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderHeaderCarrier
        {
            private string idsField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderItem
        {
            private byte deliveredField;

            private orderOrderItemHomeCurrency homeCurrencyField;

            private bool payVATField;

            private byte quantityField;

            private string rateVATField;

            private orderOrderItemStockItem stockItemField;
            private string? textField;

            private orderOrderItemTypeServiceMOSS typeServiceMOSSField;

            public string? text
            {
                get => textField;
                set => textField = value;
            }

            public byte quantity
            {
                get => quantityField;
                set => quantityField = value;
            }

            public byte delivered
            {
                get => deliveredField;
                set => deliveredField = value;
            }

            public string rateVAT
            {
                get => rateVATField;
                set => rateVATField = value;
            }

            public orderOrderItemHomeCurrency homeCurrency
            {
                get => homeCurrencyField;
                set => homeCurrencyField = value;
            }

            public orderOrderItemStockItem stockItem
            {
                get => stockItemField;
                set => stockItemField = value;
            }

            public orderOrderItemTypeServiceMOSS typeServiceMOSS
            {
                get => typeServiceMOSSField;
                set => typeServiceMOSSField = value;
            }

            public bool payVAT
            {
                get => payVATField;
                set => payVATField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderItemHomeCurrency
        {
            private decimal unitPriceField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public decimal unitPrice
            {
                get => unitPriceField;
                set => unitPriceField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderItemStockItem
        {
            private stockItem? stockItemField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public stockItem? stockItem
            {
                get => stockItemField;
                set => stockItemField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd", IsNullable = false)]
        public class stockItem
        {
            private string? eANField;

            public string? EAN
            {
                get => eANField;
                set => eANField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderItemTypeServiceMOSS
        {
            private string idsField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/order.xsd")]
        public class orderOrderSummary
        {
            private string roundingDocumentField;

            public string roundingDocument
            {
                get => roundingDocumentField;
                set => roundingDocumentField = value;
            }
        }
    }
}