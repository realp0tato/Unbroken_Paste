using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Converters;

public class ExcelToHangulConverterTests
{
    private static ClipboardSnapshot Fixture(string sample, params string[] formats)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "excel", sample);
        var files = new Dictionary<string, string>
        {
            [ClipFormats.MacHtml] = "public.html.bin",
            [ClipFormats.MacUtf8Text] = "public.utf8-plain-text.txt",
        };
        return new ClipboardSnapshot(
            formats.ToDictionary(format => format, format => File.ReadAllBytes(Path.Combine(dir, files[format]))),
            "com.microsoft.Excel", 1);
    }

    private static ClipboardSnapshot Text(string text) =>
        new(new Dictionary<string, byte[]> { [ClipFormats.MacUtf8Text] = Encoding.UTF8.GetBytes(text) }, null, 1);

    [Fact]
    public void Table_KeepsValuesStyleAlignmentAndWidths()
    {
        var result = ExcelToHangulConverter.Convert(Fixture("mac-basic-table", ClipFormats.MacHtml, ClipFormats.MacUtf8Text));

        Assert.Equal("C1", result.ConverterId);
        Assert.False(result.IsPassthrough);
        Assert.Equal(
            "<table border=\"1\" style=\"border-collapse:collapse;width:225pt\">" +
            "<colgroup><col style=\"width:75pt\"><col style=\"width:75pt\"><col style=\"width:75pt\"></colgroup>" +
            "<tr><td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0;text-align:center\"><b>이름</b></p></td>" +
            "<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0;text-align:center\"><b>부서</b></p></td>" +
            "<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0;text-align:center\"><b>금액</b></p></td></tr>" +
            "<tr><td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0\">홍길동</p></td>" +
            "<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0\">영업</p></td>" +
            "<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0;text-align:right\">1,234.50</p></td></tr>" +
            "<tr><td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0\">김철수</p></td>" +
            "<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0\">개발<br>2팀</p></td>" +
            "<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0;text-align:right\">2,469.00</p></td></tr>" +
            "</table>",
            result.Html);
        Assert.Equal("이름\t부서\t금액\n홍길동\t영업\t1,234.50\n김철수\t개발 2팀\t2,469.00", result.PlainText);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void Table_DropsExcelStyling()
    {
        var html = ExcelToHangulConverter.Convert(Fixture("mac-formula-table", ClipFormats.MacHtml)).Html!;

        Assert.DoesNotContain("mso-", html);
        Assert.DoesNotContain("class=", html);
        Assert.DoesNotContain("font-family", html);
        Assert.Contains(">57.1%<", html); // 수식은 결과값
        Assert.Contains("<b>합계</b>", html);
    }

    [Fact]
    public void Table_KeepsMergedCells()
    {
        var result = ExcelToHangulConverter.Convert(Fixture("mac-merged-table", ClipFormats.MacHtml));

        Assert.Contains("<td colspan=\"2\" style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:150pt\"><p style=\"margin:0;text-align:center\"><b>상반기 실적</b></p></td>", result.Html);
        Assert.Contains("<td rowspan=\"2\" style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:75pt\"><p style=\"margin:0;text-align:center\"><b>비고</b></p></td>", result.Html);
        Assert.Contains(result.Signals, signal => signal.Id == "S2");
    }

    [Fact]
    public void WideTable_IsScaledToPageWidth()
    {
        var cells = string.Concat(Enumerable.Repeat("<td>x</td>", 10));
        var html = $"<table><col width=100 span=10><tr>{cells}</tr><tr>{cells}</tr></table>";
        var snapshot = new ClipboardSnapshot(
            new Dictionary<string, byte[]> { [ClipFormats.MacHtml] = Encoding.UTF8.GetBytes(html) }, null, 1);

        // 10열 × 75pt = 750pt → 본문 폭 425.2pt에 맞춰 열마다 42.52pt
        var result = ExcelToHangulConverter.Convert(snapshot).Html!;

        Assert.Contains("border-collapse:collapse;width:425.2pt", result);
        Assert.Contains("<col style=\"width:42.52pt\">", result);
    }

    [Fact]
    public void SingleColumn_IsOneParagraphPerCell()
    {
        var result = ExcelToHangulConverter.Convert(Fixture("mac-single-column", ClipFormats.MacHtml, ClipFormats.MacUtf8Text));

        // 빈 셀은 문단을 만들지 않고, 셀 안 줄바꿈은 유지, 따옴표는 원래 글자만
        Assert.Equal("<p>첫째 메모</p><p>셋째 줄<br>이어지는 줄</p><p>5&quot; 나사 &quot;표준&quot;</p>", result.Html);
        Assert.Equal("첫째 메모\n셋째 줄\n이어지는 줄\n5\" 나사 \"표준\"", result.PlainText);
    }

    [Fact]
    public void SingleColumn_FromTsvRemovesExcelQuotes()
    {
        var result = ExcelToHangulConverter.Convert(Fixture("mac-single-column", ClipFormats.MacUtf8Text));

        Assert.Equal("<p>첫째 메모</p><p>셋째 줄<br>이어지는 줄</p><p>5&quot; 나사 &quot;표준&quot;</p>", result.Html);
    }

    [Fact]
    public void TsvOnly_ConvertsValuesWithoutStyle()
    {
        var result = ExcelToHangulConverter.Convert(Text("a\tb\r\n1\t<2>\r\n"));

        Assert.Equal(
            "<table border=\"1\" style=\"border-collapse:collapse\">" +
            "<tr><td style=\"border:1px solid #000;padding:1.41pt 5.1pt\"><p style=\"margin:0\">a</p></td><td style=\"border:1px solid #000;padding:1.41pt 5.1pt\"><p style=\"margin:0\">b</p></td></tr>" +
            "<tr><td style=\"border:1px solid #000;padding:1.41pt 5.1pt\"><p style=\"margin:0\">1</p></td><td style=\"border:1px solid #000;padding:1.41pt 5.1pt\"><p style=\"margin:0\">&lt;2&gt;</p></td></tr>" +
            "</table>",
            result.Html);
    }

    [Fact]
    public void WindowsCfHtml_IsRead()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "excel", "mac-basic-table", "public.html.bin"));
        var snapshot = new ClipboardSnapshot(
            new Dictionary<string, byte[]> { [ClipFormats.WinHtml] = CfHtml.Wrap(html) }, "EXCEL.EXE", 1);

        Assert.Contains("<b>이름</b>", ExcelToHangulConverter.Convert(snapshot).Html);
    }

    [Theory]
    [InlineData("a\tb\r\nc\r\n")] // 불규칙 표
    [InlineData("")]
    public void Unreadable_IsPassthrough(string text)
    {
        Assert.True(ExcelToHangulConverter.Convert(Text(text)).IsPassthrough);
    }

    [Fact]
    public void IrregularHtml_DoesNotFallBackToTsv()
    {
        var snapshot = new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.MacHtml] = Encoding.UTF8.GetBytes("<table><tr><td><table><tr><td>x</td></tr></table></td></tr></table>"),
            [ClipFormats.MacUtf8Text] = Encoding.UTF8.GetBytes("x\ty"),
        }, null, 1);

        Assert.True(ExcelToHangulConverter.Convert(snapshot).IsPassthrough);
    }

    [Fact]
    public void TooLarge_IsPassthrough()
    {
        Assert.True(ExcelToHangulConverter.Convert(Text(new string('x', ExcelClipboard.MaxTextLength + 1))).IsPassthrough);
    }

    [Fact]
    public void ManyEmptyCells_SignalsS7()
    {
        var result = ExcelToHangulConverter.Convert(Text("a\t\t\r\n\t\tb\r\n"));

        Assert.Contains(result.Signals, signal => signal.Id == "S7");
    }
}
