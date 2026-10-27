using Common.Documents.PdfReader;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Common.Documents.PdfReader.Tests;

public class PdfRowReaderTests
{
    private static byte[] BuildTwoRowPdf()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        // Same row: two words placed on the same horizontal line, left to right.
        page.AddText("Hello", 12, new PdfPoint(25, 700), font);
        page.AddText("World", 12, new PdfPoint(70, 700), font);

        // A second, lower row.
        page.AddText("Second", 12, new PdfPoint(25, 650), font);
        page.AddText("Row", 12, new PdfPoint(80, 650), font);

        return builder.Build();
    }

    [Fact]
    public void ReadRows_from_bytes_groups_words_on_the_same_line_into_one_row()
    {
        var rows = PdfRowReader.ReadRows(BuildTwoRowPdf());

        Assert.Equal(["Hello World", "Second Row"], rows);
    }

    [Fact]
    public void ReadRows_from_a_file_path_matches_ReadRows_from_bytes()
    {
        var bytes = BuildTwoRowPdf();
        var path = Path.Combine(Path.GetTempPath(), $"pdf-row-reader-test-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(path, bytes);

            var rowsFromPath = PdfRowReader.ReadRows(path);
            var rowsFromBytes = PdfRowReader.ReadRows(bytes);

            Assert.Equal(rowsFromBytes, rowsFromPath);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
