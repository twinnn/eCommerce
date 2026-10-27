using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Pohoda.Server.POCO.Parameter
{
    public class GetParametersResponse
    {

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/response.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/response.xsd", IsNullable = false)]
        public class responsePack
        {

            private responsePackResponsePackItem responsePackItemField;

            private decimal versionField;

            private int idField;

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
            public int id
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

            private listIntParam listIntParamField;

            private decimal versionField;

            private string idField;

            private string? stateField;
            
            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
            public listIntParam listIntParam
            {
                get => listIntParamField;
                set => listIntParamField = value;
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
            public string? state
            {
                get => stateField;
                set => stateField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/list.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/list.xsd", IsNullable = false)]
        public class listIntParam
        {

            private listIntParamIntParamDetail[] intParamDetailField;


            private string stateField;
            
            [XmlElement("intParamDetail")]
            public listIntParamIntParamDetail[] intParamDetail
            {
                get => intParamDetailField;
                set => intParamDetailField = value;
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
        public class listIntParamIntParamDetail
        {

            private intParam intParamField;

            [XmlElement(Namespace = "http://www.stormware.cz/schema/version_2/intParam.xsd")]
            public intParam intParam
            {
                get => intParamField;
                set => intParamField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/intParam.xsd")]
        [XmlRoot(Namespace = "http://www.stormware.cz/schema/version_2/intParam.xsd", IsNullable = false)]
        public class intParam
        {

            private uint idField;

            private string nameField;

            private string parameterTypeField;

            private intParamParameterSettings parameterSettingsField;

            public uint id
            {
                get => idField;
                set => idField = value;
            }

            public string name
            {
                get => nameField;
                set => nameField = value;
            }

            public string parameterType
            {
                get => parameterTypeField;
                set => parameterTypeField = value;
            }

            public intParamParameterSettings parameterSettings
            {
                get => parameterSettingsField;
                set => parameterSettingsField = value;
            }

        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/intParam.xsd")]
        public class intParamParameterSettings
        {

            private string? unitField;

            private string? lengthField;

            private intParamParameterSettingsParameterListItem[] parameterListField;
            public string? unit
            {
                get => unitField;
                set => unitField = value;
            }

            public string? length
            {
                get => lengthField;
                set => lengthField = value;
            }

            [XmlArrayItem("parameterListItem", IsNullable = true)]
            public intParamParameterSettingsParameterListItem[] parameterList
            {
                get => parameterListField;
                set => parameterListField = value;
            }
        }

        [Serializable]
        [DesignerCategory("code")]
        [XmlType(AnonymousType = true, Namespace = "http://www.stormware.cz/schema/version_2/intParam.xsd")]
        public class intParamParameterSettingsParameterListItem
        {

            private uint idField;

            private string nameField;

            private decimal sequenceField;
            public uint id
            {
                get => idField;
                set => idField = value;
            }

            public string name
            {
                get => nameField;
                set => nameField = value;
            }

            public decimal sequence
            {
                get => sequenceField;
                set => sequenceField = value;
            }
        }


    }
}