using System.Xml.Linq;

namespace Pohoda.Server.Xml;

/// <summary>Stormware XSD namespaces used across dataPack requests, confirmed against the published schemas at stormware.cz/xml/schema/version_2/.</summary>
public static class PohodaNamespaces
{
    public static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";
    public static readonly XNamespace Stk = "http://www.stormware.cz/schema/version_2/stock.xsd";
    public static readonly XNamespace ListStk = "http://www.stormware.cz/schema/version_2/list_stock.xsd";
    public static readonly XNamespace Prj = "http://www.stormware.cz/schema/version_2/prijemka.xsd";
    public static readonly XNamespace Lst = "http://www.stormware.cz/schema/version_2/list.xsd";
    public static readonly XNamespace Ftr = "http://www.stormware.cz/schema/version_2/filter.xsd";
    public static readonly XNamespace Typ = "http://www.stormware.cz/schema/version_2/type.xsd";
    public static readonly XNamespace Rsp = "http://www.stormware.cz/schema/version_2/response.xsd";
}
