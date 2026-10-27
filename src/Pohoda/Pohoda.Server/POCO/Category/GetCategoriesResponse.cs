#pragma warning disable CS8618
// ReSharper disable InconsistentNaming

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Category
{
    public class GetCategoriesResponse
    {
        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/response.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/response.xsd", IsNullable = false)]
        public class responsePack
        {
            private uint icoField;

            private string idField;

            private string keyField;

            private string noteField;

            private string programVersionField;

            private responsePackResponsePackItem responsePackItemField;

            private string stateField;

            private decimal versionField;

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
            private string idField;

            private listCategory listCategoryField;

            private string stateField;

            private decimal versionField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
            public listCategory listCategory
            {
                get => listCategoryField;
                set => listCategoryField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd", IsNullable = false)]
        public class listCategory
        {
            private listCategoryCategoryDetail categoryDetailField;

            private DateTime dateTimeStampField;

            private DateTime dateValidFromField;

            private string stateField;

            private decimal versionField;

            public listCategoryCategoryDetail categoryDetail
            {
                get => categoryDetailField;
                set => categoryDetailField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
        public class listCategoryCategoryDetail
        {
            private category[]? categoryField;

            private decimal versionField;

            [XmlElement("category", Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
            public category[]? category
            {
                get => categoryField;
                set => categoryField = value;
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
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/category.xsd", IsNullable = false)]
        public class category
        {
            private string descriptionField;

            private bool displayedField;

            private string idField;

            private categoryInternetParams internetParamsField;

            private bool markRecordField;

            private string nameField;

            private object noteField;

            private string pictureField;

            private string sequenceField;

            private category[]? subCategoriesField;

            public string id
            {
                get => idField;
                set => idField = value;
            }

            public string name
            {
                get => nameField;
                set => nameField = value;
            }

            public string description
            {
                get => descriptionField;
                set => descriptionField = value;
            }

            public string sequence
            {
                get => sequenceField;
                set => sequenceField = value;
            }

            public bool displayed
            {
                get => displayedField;
                set => displayedField = value;
            }

            public string picture
            {
                get => pictureField;
                set => pictureField = value;
            }

            public object note
            {
                get => noteField;
                set => noteField = value;
            }

            public bool markRecord
            {
                get => markRecordField;
                set => markRecordField = value;
            }

            public categoryInternetParams internetParams
            {
                get => internetParamsField;
                set => internetParamsField = value;
            }

            [XmlArrayItem("category", IsNullable = false)]
            public category[]? subCategories
            {
                get => subCategoriesField;
                set => subCategoriesField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
        public class categoryInternetParams
        {
            private object idInternetParamsField;

            public object idInternetParams
            {
                get => idInternetParamsField;
                set => idInternetParamsField = value;
            }
        }
    }
}