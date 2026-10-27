using System.Xml.Linq;
using Pohoda.MServer;

namespace Pohoda.MServer.Tests;

public class PohodaResponseParserTests
{
    [Fact]
    public void Parse_returns_success_when_no_element_has_an_error_or_warning_state()
    {
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="ok" />
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_reports_failure_from_an_outer_error_state_with_a_note_attribute()
    {
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="error" note="Missing required element stk:stockType" />
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.False(result.Success);
        Assert.Contains("Missing required element stk:stockType", result.Errors);
    }

    [Fact]
    public void Parse_reports_failure_from_a_nested_error_state_even_when_the_outer_state_is_ok()
    {
        // Confirmed live against mServer "GoNoow" (see Gabel.PrijemkaSync docs/technicka-dokumentacia.md
        // section 3.2): the outer responsePackItem can say state="ok" while a nested element says
        // state="error" — this is the exact silent-failure gap the old eCommerce client never caught.
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem state="ok">
                <stk:stockItemResponse xmlns:stk="http://www.stormware.cz/schema/version_2/stock.xsd" state="error">
                  <rdc:importDetails xmlns:rdc="http://www.stormware.cz/schema/version_2/responseDoc.xsd">
                    <rdc:detail>
                      <rdc:errno>137</rdc:errno>
                      <rdc:note>Invalid column name 'Name'</rdc:note>
                      <rdc:XPath>/stk:stock/stk:stockHeader</rdc:XPath>
                    </rdc:detail>
                  </rdc:importDetails>
                </stk:stockItemResponse>
              </rsp:responsePackItem>
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("[137]") && e.Contains("Invalid column name 'Name'"));
    }

    [Fact]
    public void Parse_collects_warnings_without_failing_the_result()
    {
        var doc = XDocument.Parse("""
            <rsp:responsePack xmlns:rsp="http://www.stormware.cz/schema/version_2/response.xsd" state="ok">
              <rsp:responsePackItem id="item1" state="warning" />
            </rsp:responsePack>
            """);

        var result = PohodaResponseParser.Parse(doc);

        Assert.True(result.Success);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Parse_returns_failure_for_a_document_with_no_root_element_state()
    {
        var doc = new XDocument();

        var result = PohodaResponseParser.Parse(doc);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
