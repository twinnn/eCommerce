#pragma warning disable CS8618
// ReSharper disable InconsistentNaming

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Category
{
    public class GetCategoriesRequest
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

            private listCategoryRequest listCategoryRequestField;

            private decimal versionField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
            public listCategoryRequest listCategoryRequest
            {
                get => listCategoryRequestField;
                set => listCategoryRequestField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd", IsNullable = false)]
        public class listCategoryRequest
        {
            private requestCategory requestCategoryField;

            private decimal versionField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
            public requestCategory requestCategory
            {
                get => requestCategoryField;
                set => requestCategoryField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd", IsNullable = false)]
        public class requestCategory
        {
        }
    }
}