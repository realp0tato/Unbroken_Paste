using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Converters;

public class ExcelToNotionConverterTests
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

    [Fact]
    public void Table_IsUnstyledWithHeaderRow()
    {
        var result = ExcelToNotionConverter.Convert(Fixture("mac-basic-table", ClipFormats.MacHtml, ClipFormats.MacUtf8Text));

        Assert.Equal("C2", result.ConverterId);
        Assert.False(result.IsPassthrough);
        Assert.Equal(
            "<table><thead><tr><th><b>이름</b></th><th><b>부서</b></th><th><b>금액</b></th></tr></thead>" +
            "<tbody><tr><td>홍길동</td><td>영업</td><td>1,234.50</td></tr>" +
            "<tr><td>김철수</td><td>개발<br>2팀</td><td>2,469.00</td></tr></tbody></table>",
            result.Html);
        Assert.Equal("이름\t부서\t금액\n홍길동\t영업\t1,234.50\n김철수\t개발 2팀\t2,469.00", result.PlainText);
    }

    [Fact]
    public void Table_DropsAlignmentBordersAndWidths()
    {
        var html = ExcelToNotionConverter.Convert(Fixture("mac-formula-table", ClipFormats.MacHtml)).Html!;

        Assert.DoesNotContain("style", html);
        Assert.DoesNotContain("align", html);
        Assert.DoesNotContain("border", html);
        Assert.DoesNotContain("<col", html);
        Assert.Contains("<td><b>합계</b></td>", html);
        Assert.Contains("<td>57.1%</td>", html);
    }

    [Fact]
    public void MergedCells_AreUnmergedWithValueInFirstCell()
    {
        var result = ExcelToNotionConverter.Convert(Fixture("mac-merged-table", ClipFormats.MacHtml));

        Assert.DoesNotContain("span", result.Html);
        Assert.StartsWith(
            "<table><thead><tr><th><b>상반기 실적</b></th><th></th><th><b>비고</b></th></tr></thead>" +
            "<tbody><tr><td><b>1분기</b></td><td><b>2분기</b></td><td></td></tr>",
            result.Html);
        Assert.Equal("상반기 실적\t\t비고\n1분기\t2분기\t\n100\t200\t증가\n150\t120\t감소", result.PlainText);
        Assert.Contains(result.Signals, signal => signal.Id == "S2");
    }

    [Fact]
    public void SingleColumn_IsOneBlockPerCell()
    {
        var result = ExcelToNotionConverter.Convert(Fixture("mac-single-column", ClipFormats.MacUtf8Text));

        Assert.Equal("<p>첫째 메모</p><p>셋째 줄<br>이어지는 줄</p><p>5&quot; 나사 &quot;표준&quot;</p>", result.Html);
        Assert.Equal("첫째 메모\n셋째 줄\n이어지는 줄\n5\" 나사 \"표준\"", result.PlainText);
    }

    [Fact]
    public void Unreadable_IsPassthrough()
    {
        var snapshot = new ClipboardSnapshot(
            new Dictionary<string, byte[]> { [ClipFormats.MacUtf8Text] = Encoding.UTF8.GetBytes("a\tb\nc") }, null, 1);

        Assert.True(ExcelToNotionConverter.Convert(snapshot).IsPassthrough);
    }
}

public class WindowsExcelConverterTests
{
    private static ClipboardSnapshot Windows(string sample)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "excel", sample);
        return new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.WinHtml] = File.ReadAllBytes(Path.Combine(dir, "HTML Format.bin")),
            [ClipFormats.WinUnicodeText] = File.ReadAllBytes(Path.Combine(dir, "CF_UNICODETEXT.txt")),
        }, "EXCEL.EXE", 1);
    }

    [Fact]
    public void C1_WinMergedCells_KeepsMergesAndCenter()
    {
        var html = ExcelToHangulConverter.Convert(Windows("win-merged-cells")).Html!;

        Assert.Contains("<td colspan=\"4\" style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:210pt\"><p style=\"margin:0;text-align:center\"><b>2026년 상반기 실적</b></p></td>", html);
        Assert.Contains("rowspan=\"2\"", html);
    }

    [Fact]
    public void C2_WinMergedCells_Unmerges()
    {
        var html = ExcelToNotionConverter.Convert(Windows("win-merged-cells")).Html!;

        Assert.StartsWith("<table><thead><tr><th><b>2026년 상반기 실적</b></th><th></th><th></th><th></th></tr></thead>", html);
        Assert.DoesNotContain("span", html);
    }

    [Fact]
    public void C2_WinSingleColumn_IsOneBlockPerCell()
    {
        var result = ExcelToNotionConverter.Convert(Windows("win-single-column"));

        Assert.Equal(
            "<p>회의 안건</p><p>1. 상반기 실적 검토</p><p>2. 하반기 목표 설정<br>담당: 영업팀</p><p>3. 기타 논의 사항</p><p>다음 회의 11월 둘째 주</p>",
            result.Html);
    }
}
