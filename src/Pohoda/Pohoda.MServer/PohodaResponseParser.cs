using System.Xml.Linq;

namespace Pohoda.MServer;

public sealed record PohodaResult(bool Success, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, XDocument Document);

/// <summary>
/// Parses a POHODA mServer `responsePack` XML into a success/error result. Matches elements by
/// local name only (namespace-agnostic) since mServer error structures are not fully documented
/// publicly and prefixes are not guaranteed — this is intentionally defensive rather than strict.
///
/// mServer nests a `state` attribute at MULTIPLE levels, and an outer level being "ok" does NOT
/// guarantee the operation succeeded (confirmed live against mServer "GoNoow" — a `createStockRequest`
/// missing a required element came back with `responsePackItem state="ok"` while a nested
/// `stk:stockItemResponse state="error"` carried the real error). This parser therefore walks every
/// element in the document and checks every `state` attribute it finds, rather than only the top level.
/// </summary>
public static class PohodaResponseParser
{
    public static PohodaResult Parse(XDocument doc)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var root = doc.Root;

        if (root is null)
        {
            return new PohodaResult(false, ["Prázdna alebo neplatná odpoveď z mServer (chýba koreňový element)."], warnings, doc);
        }

        var success = true;

        foreach (var el in root.DescendantsAndSelf())
        {
            var stateAttr = el.Attribute("state");
            if (stateAttr is null)
            {
                continue;
            }

            if (IsErrorState(stateAttr))
            {
                success = false;
                errors.AddRange(ExtractErrorMessages(el));
            }
            else if (IsWarningState(stateAttr))
            {
                warnings.Add(DescribeItem(el));
            }
        }

        errors = errors.Distinct().ToList();
        if (errors.Count > 0)
        {
            success = false;
        }

        return new PohodaResult(success, errors, warnings, doc);
    }

    private static bool IsErrorState(XAttribute state) =>
        string.Equals(state.Value, "error", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningState(XAttribute state) =>
        string.Equals(state.Value, "warning", StringComparison.OrdinalIgnoreCase);

    private static string DescribeItem(XElement item)
    {
        var id = item.Attribute("id")?.Value ?? item.Name.LocalName;
        return $"{item.Name.LocalName} '{id}': warning";
    }

    /// <summary>
    /// Extracts human-readable error text from an element whose `state` attribute is "error".
    /// Handles the two shapes confirmed live: (1) the message directly in this element's own
    /// `note` attribute (schema-validation failures), and (2) a nested `rdc:importDetails/rdc:detail`
    /// with `errno`/`note`/`XPath` child elements (business-rule validation failures). Falls back to
    /// a dedicated `&lt;error&gt;` child (documented shape, not yet observed live) for robustness.
    /// </summary>
    private static IEnumerable<string> ExtractErrorMessages(XElement el)
    {
        var ownNote = el.Attribute("note")?.Value;
        if (!string.IsNullOrWhiteSpace(ownNote))
        {
            yield return ownNote;
        }

        foreach (var detail in el.Descendants().Where(d => d.Name.LocalName == "detail"))
        {
            var errno = detail.Elements().FirstOrDefault(c => c.Name.LocalName == "errno")?.Value;
            var note = detail.Elements().FirstOrDefault(c => c.Name.LocalName == "note")?.Value;
            var xPath = detail.Elements().FirstOrDefault(c => c.Name.LocalName == "XPath")?.Value;

            if (string.IsNullOrWhiteSpace(note))
            {
                continue;
            }

            var message = string.IsNullOrWhiteSpace(errno) ? note : $"[{errno}] {note}";
            yield return string.IsNullOrWhiteSpace(xPath) ? message : $"{message} ({xPath})";
        }

        foreach (var errorEl in el.Elements().Where(e => e.Name.LocalName == "error"))
        {
            var number = errorEl.Elements().FirstOrDefault(e => e.Name.LocalName == "errorNumber")?.Value;
            var note = errorEl.Elements().FirstOrDefault(e => e.Name.LocalName is "note" or "text")?.Value ?? errorEl.Value.Trim();
            yield return string.IsNullOrWhiteSpace(number) ? note : $"[{number}] {note}";
        }
    }
}
