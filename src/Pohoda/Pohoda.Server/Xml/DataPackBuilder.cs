using System;
using System.Linq;
using System.Xml.Linq;
using static Pohoda.Server.Xml.PohodaNamespaces;

namespace Pohoda.Server.Xml;

/// <summary>Builds the outer `dat:dataPack` / `dat:dataPackItem` envelope shared by every mServer request.</summary>
public static class DataPackBuilder
{
    /// <summary>Value of the dataPack `application` attribute — informational only, shown in Pohoda's XML log.</summary>
    public const string ApplicationName = "eCommerce.Pohoda.Server";

    public static XDocument Wrap(string ico, string note, params XElement[] items)
    {
        var root = new XElement(Dat + "dataPack",
            new XAttribute("id", DateTime.UtcNow.Ticks.ToString("x")),
            new XAttribute("ico", ico),
            new XAttribute("application", ApplicationName),
            new XAttribute("version", "2.0"),
            new XAttribute("note", note),
            items.Select((item, idx) => WrapItem(item, idx)));

        return new XDocument(root);
    }

    private static XElement WrapItem(XElement content, int index) =>
        new(Dat + "dataPackItem",
            new XAttribute("id", $"item{index + 1}"),
            new XAttribute("version", "2.0"),
            content);
}
