using System.Xml.Linq;
using Pohoda.Server.Xml;

namespace Pohoda.Server.Tests;

public class DataPackBuilderTests
{
    private static readonly XNamespace Dat = "http://www.stormware.cz/schema/version_2/data.xsd";

    [Fact]
    public void Wrap_builds_a_dataPack_envelope_with_one_numbered_item_per_element()
    {
        var doc = DataPackBuilder.Wrap("53870441", "test note", new XElement("first"), new XElement("second"));

        var root = doc.Root!;
        Assert.Equal(Dat + "dataPack", root.Name);
        Assert.Equal("53870441", root.Attribute("ico")!.Value);
        Assert.Equal("2.0", root.Attribute("version")!.Value);
        Assert.Equal("test note", root.Attribute("note")!.Value);
        Assert.Equal(DataPackBuilder.ApplicationName, root.Attribute("application")!.Value);
        Assert.False(string.IsNullOrEmpty(root.Attribute("id")?.Value));

        var items = root.Elements(Dat + "dataPackItem").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("item1", items[0].Attribute("id")!.Value);
        Assert.Equal("item2", items[1].Attribute("id")!.Value);
        Assert.Equal("2.0", items[0].Attribute("version")!.Value);
        Assert.Equal("first", items[0].Elements().Single().Name.LocalName);
        Assert.Equal("second", items[1].Elements().Single().Name.LocalName);
    }
}
