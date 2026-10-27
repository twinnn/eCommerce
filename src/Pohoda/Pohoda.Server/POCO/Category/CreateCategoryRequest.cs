#pragma warning disable CS8618
// ReSharper disable InconsistentNaming

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Category
{
    public class CreateCategoryRequest
    {

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/data.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/data.xsd", IsNullable = false)]
        public class dataPack
        {

            private dataPackDataPackItem dataPackItemField;

            private uint icoField;

            private decimal versionField;

            private byte idField;

            private string noteField;

            private string applicationField;

            public dataPackDataPackItem dataPackItem
            {
                get => dataPackItemField;
                set => dataPackItemField = value;
            }

            [XmlAttribute]
            public uint ico
            {
                get => icoField;
                set => icoField = value;
            }

            [XmlAttribute]
            public decimal version
            {
                get => versionField;
                set => versionField = value;
            }

            [XmlAttribute]
            public byte id
            {
                get => idField;
                set => idField = value;
            }

            [XmlAttribute]
            public string note
            {
                get => noteField;
                set => noteField = value;
            }

            [XmlAttribute]
            public string application
            {
                get => applicationField;
                set => applicationField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/data.xsd")]
        public class dataPackDataPackItem
        {

            private categoryDetail categoryDetailField;

            private decimal versionField;

            private string idField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
            public categoryDetail categoryDetail
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
            public string id
            {
                get => idField;
                set => idField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/category.xsd", IsNullable = false)]
        public class categoryDetail
        {

            private categoryDetailCategory categoryField;

            private decimal versionField;

            public categoryDetailCategory category
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
        public class categoryDetailCategory
        {

            private string nameField;

            private string descriptionField;

            private string sequenceField;

            private bool displayedField;

            private string pictureField;

            private string noteField;

            private string _parentId;

            private categoryDetailCategoryInternetParams internetParamsField;

            private categoryDetailSubcategory[] subCategoriesField;

            public string name
            {
                get => nameField;
                set => nameField = value;
            }

            public string parentId
            {
                get => _parentId;
                set => _parentId = value;
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

            public string note
            {
                get => noteField;
                set => noteField = value;
            }

            public categoryDetailCategoryInternetParams internetParams
            {
                get => internetParamsField;
                set => internetParamsField = value;
            }

            [XmlArrayItem("category", IsNullable = false)]
            public categoryDetailSubcategory[] subCategories
            {
                get => subCategoriesField;
                set => subCategoriesField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
        public class categoryDetailSubcategory
        {

            private string nameField;

            private string descriptionField;

            private string sequenceField;

            private bool displayedField;

            private string pictureField;

            private string noteField;

            private categoryDetailCategoryInternetParams internetParamsField;

            private categoryDetailSubcategory[] subCategoriesField;

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

            public string note
            {
                get => noteField;
                set => noteField = value;
            }

            public categoryDetailCategoryInternetParams internetParams
            {
                get => internetParamsField;
                set => internetParamsField = value;
            }

            [XmlArrayItem("category", IsNullable = false)]
            public categoryDetailSubcategory[] subCategories
            {
                get => subCategoriesField;
                set => subCategoriesField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/category.xsd")]
        public class categoryDetailCategoryInternetParams
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