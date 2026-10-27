using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Common.Documents.PdfReader;

/// <summary>
/// Reconstructs a PDF's text as visual rows (top-to-bottom, left-to-right) by clustering words with
/// PdfPig's per-word bounding boxes. More reliable than trusting the raw content-stream text order for
/// simple single-column tables (e.g. invoice line items), which does not always follow reading order.
/// Generalized from Gabel.PrijemkaSync.Extraction's (internal, Gabel-specific) PdfRowReader — this
/// version has no knowledge of any particular document layout.
/// </summary>
public static class PdfRowReader
{
    private const double SameRowTolerance = 2.5;

    public static IReadOnlyList<string> ReadRows(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return ReadRows(document);
    }

    public static IReadOnlyList<string> ReadRows(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        return ReadRows(document);
    }

    private static IReadOnlyList<string> ReadRows(PdfDocument document)
    {
        var rows = new List<string>();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0)
            {
                continue;
            }

            var sorted = words
                .OrderByDescending(w => w.BoundingBox.Top)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var currentRow = new List<Word> { sorted[0] };
            var currentTop = sorted[0].BoundingBox.Top;

            for (var i = 1; i < sorted.Count; i++)
            {
                var word = sorted[i];
                if (Math.Abs(word.BoundingBox.Top - currentTop) <= SameRowTolerance)
                {
                    currentRow.Add(word);
                }
                else
                {
                    rows.Add(BuildRowText(currentRow));
                    currentRow = [word];
                    currentTop = word.BoundingBox.Top;
                }
            }

            rows.Add(BuildRowText(currentRow));
        }

        return rows;
    }

    private static string BuildRowText(List<Word> row) =>
        string.Join(' ', row.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
}
