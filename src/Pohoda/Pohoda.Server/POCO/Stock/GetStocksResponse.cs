using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Stock
{
    public class GetStocksResponse
    {
        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/response.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/response.xsd", IsNullable = false)]
        public class responsePack
        {

            private responsePackResponsePackItem responsePackItemField;

            private decimal versionField;

            private string idField;

            private string stateField;

            private string programVersionField;

            private uint icoField;

            private string keyField;

            private string noteField;

                public responsePackResponsePackItem responsePackItem
            {
                get => responsePackItemField;
                set => responsePackItemField = value;
            }

                [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }

                [XmlAttribute]
            public string id
            {
                get => idField;
                set => idField = value;
            }

                [XmlAttribute]
            public string state
            {
                get => stateField;
                set => stateField = value;
            }

                [XmlAttribute]
            public string programVersion
            {
                get => programVersionField;
                set => programVersionField = value;
            }

                [XmlAttribute]
            public uint ico
            {
                get => icoField;
                set => icoField = value;
            }

                [XmlAttribute]
            public string key
            {
                get => keyField;
                set => keyField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/response.xsd")]
        public class responsePackResponsePackItem
        {

            private listStock listStockField;

            private decimal versionField;

            private string idField;

            private string stateField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/list_stock.xsd")]
            public listStock listStock
            {
                get => listStockField;
                set => listStockField = value;
            }

                [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }

                [XmlAttribute]
            public string id
            {
                get => idField;
                set => idField = value;
            }

                [XmlAttribute]
            public string state
            {
                get => stateField;
                set => stateField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list_stock.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/list_stock.xsd", IsNullable = false)]
        public class listStock
        {

            private listStockStock[] stockField;

            private decimal versionField;

            private DateTime dateTimeStampField;

            private DateTime dateValidFromField;

            private string stateField;

                [XmlElement("stock")]
            public listStockStock[] stock
            {
                get => stockField;
                set => stockField = value;
            }

                [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }

                [XmlAttribute]
            public DateTime dateTimeStamp
            {
                get => dateTimeStampField;
                set => dateTimeStampField = value;
            }

                [XmlAttribute(DataType = "date")]
            public DateTime dateValidFrom
            {
                get => dateValidFromField;
                set => dateValidFromField = value;
            }

                [XmlAttribute]
            public string state
            {
                get => stateField;
                set => stateField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list_stock.xsd")]
        public class listStockStock
        {

            private stockHeader stockHeaderField;

            private stockPriceItemStockPrice[] stockPriceItemField;

            private decimal versionField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
            public stockHeader stockHeader
            {
                get => stockHeaderField;
                set => stockHeaderField = value;
            }

                [XmlArray(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
            [XmlArrayItem("stockPrice", IsNullable = false)]
            public stockPriceItemStockPrice[] stockPriceItem
            {
                get => stockPriceItemField;
                set => stockPriceItemField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd", IsNullable = false)]
        public class stockHeader
        {

            private object[] itemsField;

            private ItemsChoiceType[] itemsElementNameField;

            [XmlElement("EAN", typeof(ulong))]
            [XmlElement("PDP", typeof(bool))]
            [XmlElement("PLU", typeof(byte))]
            [XmlElement("alternativeStocks", typeof(stockHeaderAlternativeStocks))]
            [XmlElement("availability", typeof(string))]
            [XmlElement("categories", typeof(stockHeaderCategories))]
            [XmlElement("clearanceSale", typeof(bool))]
            [XmlElement("code", typeof(string))]
            [XmlElement("count", typeof(decimal))]
            [XmlElement("countIssuedOrders", typeof(decimal))]
            [XmlElement("countReceivedOrders", typeof(decimal))]
            [XmlElement("description", typeof(string))]
            [XmlElement("description2", typeof(string))]
            [XmlElement("discount", typeof(bool))]
            [XmlElement("fixation", typeof(string))]
            [XmlElement("id", typeof(ushort))]
            [XmlElement("intParameters", typeof(stockHeaderIntParameters))]
            [XmlElement("isInternet", typeof(bool))]
            [XmlElement("isSales", typeof(bool))]
            [XmlElement("markRecord", typeof(bool))]
            [XmlElement("name", typeof(string))]
            [XmlElement("nameComplement", typeof(object))]
            [XmlElement("news", typeof(bool))]
            [XmlElement("orderName", typeof(string))]
            [XmlElement("orderQuantity", typeof(decimal))]
            [XmlElement("parameters", typeof(stockHeaderParameters))]
            [XmlElement("pictures", typeof(stockHeaderPictures))]
            [XmlElement("prepare", typeof(bool))]
            [XmlElement("producer", typeof(string))]
            [XmlElement("purchasingPrice", typeof(decimal))]
            [XmlElement("purchasingRateVAT", typeof(string))]
            [XmlElement("reclamation", typeof(decimal))]
            [XmlElement("recommended", typeof(bool))]
            [XmlElement("relatedLinks", typeof(stockHeaderRelatedLinks))]
            [XmlElement("relatedStocks", typeof(stockHeaderRelatedStocks))]
            [XmlElement("reservation", typeof(decimal))]
            [XmlElement("sale", typeof(bool))]
            [XmlElement("sellingPrice", typeof(stockHeaderSellingPrice))]
            [XmlElement("sellingRateVAT", typeof(string))]
            [XmlElement("service", typeof(decimal))]
            [XmlElement("shortName", typeof(string))]
            [XmlElement("stockType", typeof(string))]
            [XmlElement("storage", typeof(stockHeaderStorage))]
            [XmlElement("supplier", typeof(stockHeaderSupplier))]
            [XmlElement("typePrice", typeof(stockHeaderTypePrice))]
            [XmlElement("unit", typeof(string))]
            [XmlElement("weightedPurchasePrice", typeof(decimal))]
            [XmlChoiceIdentifier("ItemsElementName")]
            public object[] Items
            {
                get => itemsField;
                set => itemsField = value;
            }

                [XmlElement("ItemsElementName")]
            [XmlIgnore]
            public ItemsChoiceType[] ItemsElementName
            {
                get => itemsElementNameField;
                set => itemsElementNameField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderAlternativeStocks
        {

            private stockHeaderAlternativeStocksIdStocks[] idStocksField;

                [XmlElement("idStocks")]
            public stockHeaderAlternativeStocksIdStocks[] idStocks
            {
                get => idStocksField;
                set => idStocksField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderAlternativeStocksIdStocks
        {

            private byte stockOrderField;

            private stockItem stockItemField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public byte stockOrder
            {
                get => stockOrderField;
                set => stockOrderField = value;
            }

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public stockItem stockItem
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

            private uint idField;

                public uint id
            {
                get => idField;
                set => idField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderCategories
        {

            private ushort[] idCategoryField;

                [XmlElement("idCategory")]
            public ushort[] idCategory
            {
                get => idCategoryField;
                set => idCategoryField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderIntParameters
        {

            private stockHeaderIntParametersIntParameter[] intParameterField;

                [XmlElement("intParameter")]
            public stockHeaderIntParametersIntParameter[] intParameter
            {
                get => intParameterField;
                set => intParameterField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderIntParametersIntParameter
        {

            private ushort intParameterIDField;

            private string intParameterNameField;

            private byte intParameterOrderField;

            private string intParameterTypeField;

            private stockHeaderIntParametersIntParameterIntParameterValue[] intParameterValuesField;

                public ushort intParameterID
            {
                get => intParameterIDField;
                set => intParameterIDField = value;
            }

                public string intParameterName
            {
                get => intParameterNameField;
                set => intParameterNameField = value;
            }

                public byte intParameterOrder
            {
                get => intParameterOrderField;
                set => intParameterOrderField = value;
            }

                public string intParameterType
            {
                get => intParameterTypeField;
                set => intParameterTypeField = value;
            }

                [XmlArrayItem("intParameterValue", IsNullable = false)]
            public stockHeaderIntParametersIntParameterIntParameterValue[] intParameterValues
            {
                get => intParameterValuesField;
                set => intParameterValuesField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderIntParametersIntParameterIntParameterValue
        {

            private ushort parameterValueIDField;

            private bool parameterValueIDFieldSpecified;

            private string parameterValueField;

                public ushort parameterValueID
            {
                get => parameterValueIDField;
                set => parameterValueIDField = value;
            }

                [XmlIgnore]
            public bool parameterValueIDSpecified
            {
                get => parameterValueIDFieldSpecified;
                set => parameterValueIDFieldSpecified = value;
            }

                public string parameterValue
            {
                get => parameterValueField;
                set => parameterValueField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderParameters
        {

            private parameter[] parameterField;

                [XmlElement("parameter", Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public parameter[] parameter
            {
                get => parameterField;
                set => parameterField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd", IsNullable = false)]
        public class parameter
        {

            private string nameField;

            private decimal textValueField;

            private bool textValueFieldSpecified;

            private bool booleanValueField;

            private bool booleanValueFieldSpecified;

                public string name
            {
                get => nameField;
                set => nameField = value;
            }

                public decimal textValue
            {
                get => textValueField;
                set => textValueField = value;
            }

                [XmlIgnore]
            public bool textValueSpecified
            {
                get => textValueFieldSpecified;
                set => textValueFieldSpecified = value;
            }

                public bool booleanValue
            {
                get => booleanValueField;
                set => booleanValueField = value;
            }

                [XmlIgnore]
            public bool booleanValueSpecified
            {
                get => booleanValueFieldSpecified;
                set => booleanValueFieldSpecified = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderPictures
        {

            private stockHeaderPicturesPicture[] pictureField;

                [XmlElement("picture")]
            public stockHeaderPicturesPicture[] picture
            {
                get => pictureField;
                set => pictureField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderPicturesPicture
        {

            private ushort idField;

            private string filepathField;

            private string descriptionField;

            private byte orderField;

            private bool defaultField;

                public ushort id
            {
                get => idField;
                set => idField = value;
            }

                public string filepath
            {
                get => filepathField;
                set => filepathField = value;
            }

                public string description
            {
                get => descriptionField;
                set => descriptionField = value;
            }

                public byte order
            {
                get => orderField;
                set => orderField = value;
            }

                [XmlAttribute]
            public bool @default
            {
                get => defaultField;
                set => defaultField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderRelatedLinks
        {

            private stockHeaderRelatedLinksRelatedLink relatedLinkField;

                public stockHeaderRelatedLinksRelatedLink relatedLink
            {
                get => relatedLinkField;
                set => relatedLinkField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderRelatedLinksRelatedLink
        {

            private string addressURLField;

            private string descriptionField;

            private byte orderField;

                public string addressURL
            {
                get => addressURLField;
                set => addressURLField = value;
            }

                public string description
            {
                get => descriptionField;
                set => descriptionField = value;
            }

                public byte order
            {
                get => orderField;
                set => orderField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderRelatedStocks
        {

            private stockHeaderRelatedStocksIdStocks[] idStocksField;

                [XmlElement("idStocks")]
            public stockHeaderRelatedStocksIdStocks[] idStocks
            {
                get => idStocksField;
                set => idStocksField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderRelatedStocksIdStocks
        {

            private byte stockOrderField;

            private stockItem stockItemField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public byte stockOrder
            {
                get => stockOrderField;
                set => stockOrderField = value;
            }

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public stockItem stockItem
            {
                get => stockItemField;
                set => stockItemField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderSellingPrice
        {

            private bool payVATField;

            private decimal valueField;

                [XmlAttribute]
            public bool payVAT
            {
                get => payVATField;
                set => payVATField = value;
            }

                [XmlText]
            public decimal Value
            {
                get => valueField;
                set => valueField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderStorage
        {

            private byte idField;

            private string idsField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public byte id
            {
                get => idField;
                set => idField = value;
            }

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderSupplier
        {

            private byte idField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public byte id
            {
                get => idField;
                set => idField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockHeaderTypePrice
        {

            private byte idField;

            private string idsField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public byte id
            {
                get => idField;
                set => idField = value;
            }

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }
        }

        [Serializable]
        [XmlType(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd", IncludeInSchema = false)]
        public enum ItemsChoiceType
        {

                EAN,

                PDP,

                PLU,

                alternativeStocks,

                availability,

                categories,

                clearanceSale,

                code,

                count,

                countIssuedOrders,

                countReceivedOrders,

                description,

                description2,

                discount,

                fixation,

                id,

                intParameters,

                isInternet,

                isSales,

                markRecord,

                name,

                nameComplement,

                news,

                orderName,

                orderQuantity,

                parameters,

                pictures,

                prepare,

                producer,

                purchasingPrice,

                purchasingRateVAT,

                reclamation,

                recommended,

                relatedLinks,

                relatedStocks,

                reservation,

                sale,

                sellingPrice,

                sellingRateVAT,

                service,

                shortName,

                stockType,

                storage,

                supplier,

                typePrice,

                unit,

                weightedPurchasePrice,
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockPriceItemStockPrice
        {

            private byte idField;

            private string idsField;

            private decimal priceField;

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public byte id
            {
                get => idField;
                set => idField = value;
            }

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public string ids
            {
                get => idsField;
                set => idsField = value;
            }

                [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/type.xsd")]
            public decimal price
            {
                get => priceField;
                set => priceField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd", IsNullable = false)]
        public class stockPriceItem
        {

            private stockPriceItemStockPrice[] stockPriceField;

                [XmlElement("stockPrice")]
            public stockPriceItemStockPrice[] stockPrice
            {
                get => stockPriceField;
                set => stockPriceField = value;
            }
        }

    }
}