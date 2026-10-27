using System.ComponentModel;
using System.Xml.Serialization;

namespace Import.Wiky.POCO;

// NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
/// <remarks />
[SerializableAttribute]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public class SHOP
{
    private SHOPSHOPITEM[] sHOPITEMField;

    /// <remarks />
    [XmlElement("SHOPITEM")]
    public SHOPSHOPITEM[] SHOPITEM
    {
        get => sHOPITEMField;
        set => sHOPITEMField = value;
    }
}

/// <remarks />
[SerializableAttribute]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class SHOPSHOPITEM
{
    private string cATEGORYTEXTField;

    private string dELIVERY_DATEField;

    private SHOPSHOPITEMDELIVERY[] dELIVERYField;

    private string dESCRIPTIONField;

    private string eANField;

    private string iMGURLField;

    private string iTEM_IDField;

    private string mANUFACTURERField;

    private SHOPSHOPITEMPARAMETER[] pARAMETERSField;

    private string pRICE_VATField;

    private string pRICEField;

    private string pRODUCTField;

    private ulong sTOCKField;

    private string uRLField;

    private string vATField;

    private string wHOLESALE_PRICEField;

    /// <remarks />
    public string DESCRIPTION
    {
        get => dESCRIPTIONField;
        set => dESCRIPTIONField = value;
    }

    /// <remarks />
    public string IMGURL
    {
        get => iMGURLField;
        set => iMGURLField = value;
    }

    /// <remarks />
    public string VAT
    {
        get => vATField;
        set => vATField = value;
    }

    /// <remarks />
    public string MANUFACTURER
    {
        get => mANUFACTURERField;
        set => mANUFACTURERField = value;
    }

    /// <remarks />
    public string CATEGORYTEXT
    {
        get => cATEGORYTEXTField;
        set => cATEGORYTEXTField = value;
    }

    /// <remarks />
    [XmlElement("DELIVERY")]
    public SHOPSHOPITEMDELIVERY[] DELIVERY
    {
        get => dELIVERYField;
        set => dELIVERYField = value;
    }

    /// <remarks />
    public string ITEM_ID
    {
        get => iTEM_IDField;
        set => iTEM_IDField = value;
    }

    /// <remarks />
    public string PRODUCT
    {
        get => pRODUCTField;
        set => pRODUCTField = value;
    }

    /// <remarks />
    public string PRICE
    {
        get => pRICEField;
        set => pRICEField = value;
    }

    /// <remarks />
    public string PRICE_VAT
    {
        get => pRICE_VATField;
        set => pRICE_VATField = value;
    }

    /// <remarks />
    [XmlArrayItem("PARAMETER", IsNullable = false)]
    public SHOPSHOPITEMPARAMETER[] PARAMETERS
    {
        get => pARAMETERSField;
        set => pARAMETERSField = value;
    }

    /// <remarks />
    public string WHOLESALE_PRICE
    {
        get => wHOLESALE_PRICEField;
        set => wHOLESALE_PRICEField = value;
    }

    /// <remarks />
    public string EAN
    {
        get => eANField;
        set => eANField = value;
    }

    /// <remarks />
    public string DELIVERY_DATE
    {
        get => dELIVERY_DATEField;
        set => dELIVERY_DATEField = value;
    }

    /// <remarks />
    public string URL
    {
        get => uRLField;
        set => uRLField = value;
    }

    /// <remarks />
    public ulong STOCK
    {
        get => sTOCKField;
        set => sTOCKField = value;
    }
}

/// <remarks />
[SerializableAttribute]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class SHOPSHOPITEMDELIVERY
{
    private string dELIVERY_IDField;

    private decimal dELIVERY_PRICE_CODField;

    private decimal dELIVERY_PRICEField;

    /// <remarks />
    public string DELIVERY_ID
    {
        get => dELIVERY_IDField;
        set => dELIVERY_IDField = value;
    }

    /// <remarks />
    public decimal DELIVERY_PRICE
    {
        get => dELIVERY_PRICEField;
        set => dELIVERY_PRICEField = value;
    }

    /// <remarks />
    public decimal DELIVERY_PRICE_COD
    {
        get => dELIVERY_PRICE_CODField;
        set => dELIVERY_PRICE_CODField = value;
    }
}

/// <remarks />
[SerializableAttribute]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class SHOPSHOPITEMPARAMETER
{
    private string pARAMETER_NAMEField;

    private string pARAMETER_VALUEField;

    /// <remarks />
    public string PARAMETER_NAME
    {
        get => pARAMETER_NAMEField;
        set => pARAMETER_NAMEField = value;
    }

    /// <remarks />
    public string PARAMETER_VALUE
    {
        get => pARAMETER_VALUEField;
        set => pARAMETER_VALUEField = value;
    }
}