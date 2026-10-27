#pragma warning disable CS8618
// ReSharper disable InconsistentNaming

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Stock
{
    public class CreateStockRequest
    {
        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/data.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/data.xsd", IsNullable = false)]
        public class dataPack
        {
            private string applicationField;
            private dataPackDataPackItem[] dataPackItemField;

            private uint icoField;

            private string idField;

            private string noteField;

            private decimal versionField;

            [XmlElement("dataPackItem")]
            public dataPackDataPackItem[] dataPackItem
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
            private stock stockField;

            private decimal versionField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
            public stock stock
            {
                get => stockField;
                set => stockField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd", IsNullable = false)]
        public class stock
        {
            private stockStockHeader stockHeaderField;

            private decimal versionField;

            public stockStockHeader stockHeader
            {
                get => stockHeaderField;
                set => stockHeaderField = value;
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
        public class stockStockHeader
        {
            private uint codeField;

            private string descriptionField;

            private string eANField;

            private bool isBatchField;

            private bool isInternetField;

            private bool isSalesField;

            private int limitMaxField;

            private byte limitMinField;

            private decimal massField;

            private string nameComplementField;

            private string nameField;

            private string noteField;

            private stockStockHeaderPictures picturesField;

            private byte pLUField;

            private string producerField;

            private decimal purchasingPriceField;

            private string purchasingRateVATField;

            private stockStockHeaderRelatedLinks relatedLinksField;

            private decimal sellingPriceField;

            private string sellingRateVATField;
            private string stockTypeField;

            private stockStockHeaderStorage storageField;

            private stockStockHeaderSupplier supplierField;

            private stockStockHeaderTypePrice typePriceField;

            private string unitField;

            public string stockType
            {
                get => stockTypeField;
                set => stockTypeField = value;
            }

            public uint code
            {
                get => codeField;
                set => codeField = value;
            }

            public string EAN
            {
                get => eANField;
                set => eANField = value;
            }

            public byte PLU
            {
                get => pLUField;
                set => pLUField = value;
            }

            public bool isSales
            {
                get => isSalesField;
                set => isSalesField = value;
            }

            public bool isInternet
            {
                get => isInternetField;
                set => isInternetField = value;
            }

            public bool isBatch
            {
                get => isBatchField;
                set => isBatchField = value;
            }

            public string purchasingRateVAT
            {
                get => purchasingRateVATField;
                set => purchasingRateVATField = value;
            }

            public string sellingRateVAT
            {
                get => sellingRateVATField;
                set => sellingRateVATField = value;
            }

            public string name
            {
                get => nameField;
                set => nameField = value;
            }

            public string nameComplement
            {
                get => nameComplementField;
                set => nameComplementField = value;
            }

            public string unit
            {
                get => unitField;
                set => unitField = value;
            }

            public stockStockHeaderStorage storage
            {
                get => storageField;
                set => storageField = value;
            }

            public stockStockHeaderTypePrice typePrice
            {
                get => typePriceField;
                set => typePriceField = value;
            }

            public decimal purchasingPrice
            {
                get => purchasingPriceField;
                set => purchasingPriceField = value;
            }

            public decimal sellingPrice
            {
                get => sellingPriceField;
                set => sellingPriceField = value;
            }

            public byte limitMin
            {
                get => limitMinField;
                set => limitMinField = value;
            }

            public int limitMax
            {
                get => limitMaxField;
                set => limitMaxField = value;
            }

            public decimal mass
            {
                get => massField;
                set => massField = value;
            }

            public stockStockHeaderSupplier supplier
            {
                get => supplierField;
                set => supplierField = value;
            }

            public string producer
            {
                get => producerField;
                set => producerField = value;
            }

            public string description
            {
                get => descriptionField;
                set => descriptionField = value;
            }

            public stockStockHeaderPictures pictures
            {
                get => picturesField;
                set => picturesField = value;
            }

            public string note
            {
                get => noteField;
                set => noteField = value;
            }

            public stockStockHeaderRelatedLinks relatedLinks
            {
                get => relatedLinksField;
                set => relatedLinksField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockStockHeaderStorage
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockStockHeaderTypePrice
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockStockHeaderSupplier
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
        public class stockStockHeaderPictures
        {
            private stockStockHeaderPicturesPicture pictureField;

            public stockStockHeaderPicturesPicture picture
            {
                get => pictureField;
                set => pictureField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockStockHeaderPicturesPicture
        {
            private bool defaultField;

            private string descriptionField;
            private string filepathField;

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
        public class stockStockHeaderRelatedLinks
        {
            private stockStockHeaderRelatedLinksRelatedLink relatedLinkField;

            public stockStockHeaderRelatedLinksRelatedLink relatedLink
            {
                get => relatedLinkField;
                set => relatedLinkField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/stock.xsd")]
        public class stockStockHeaderRelatedLinksRelatedLink
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
    }
}