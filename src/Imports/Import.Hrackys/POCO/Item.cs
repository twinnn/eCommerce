using System.ComponentModel;
using System.Xml.Serialization;

namespace Import.Hrackys.POCO;

// NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
/// <remarks />
[SerializableAttribute]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public class SHOPITEM
{
    private object aGEField;

    private string cATEGORYTEXTField;

    private string dESCRIPTIONField;

    private byte dPHField;

    private ulong eANField;

    private string[] iMGURL_ALTERNATIVEField;

    private string iMGURLField;

    private string iTEMIDField;

    private string mANUFACTURERField;

    private decimal pRICE_VAT_RECOMMENDField;

    private decimal pRICEField;

    private string pRODUCTField;

    private object sIZEField;

    private ushort sTOCKField;

    private string uRLField;

    /// <remarks />
    [XmlElement("ITEM-ID")]
    public string ITEMID
    {
        get => iTEMIDField;
        set => iTEMIDField = value;
    }

    /// <remarks />
    public string PRODUCT
    {
        get => pRODUCTField;
        set => pRODUCTField = value;
    }

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
    [XmlElement("IMGURL_ALTERNATIVE")]
    public string[] IMGURL_ALTERNATIVE
    {
        get => iMGURL_ALTERNATIVEField;
        set => iMGURL_ALTERNATIVEField = value;
    }

    /// <remarks />
    public object AGE
    {
        get => aGEField;
        set => aGEField = value;
    }

    /// <remarks />
    public object SIZE
    {
        get => sIZEField;
        set => sIZEField = value;
    }

    /// <remarks />
    public ushort STOCK
    {
        get => sTOCKField;
        set => sTOCKField = value;
    }

    /// <remarks />
    public byte DPH
    {
        get => dPHField;
        set => dPHField = value;
    }

    /// <remarks />
    public ulong EAN
    {
        get => eANField;
        set => eANField = value;
    }

    /// <remarks />
    public decimal PRICE
    {
        get => pRICEField;
        set => pRICEField = value;
    }

    /// <remarks />
    public string URL
    {
        get => uRLField;
        set => uRLField = value;
    }

    /// <remarks />
    public string MANUFACTURER
    {
        get => mANUFACTURERField;
        set => mANUFACTURERField = value;
    }

    /// <remarks />
    public decimal PRICE_VAT_RECOMMEND
    {
        get => pRICE_VAT_RECOMMENDField;
        set => pRICE_VAT_RECOMMENDField = value;
    }

    /// <remarks />
    public string CATEGORYTEXT
    {
        get => cATEGORYTEXTField;
        set => cATEGORYTEXTField = value;
    }
}