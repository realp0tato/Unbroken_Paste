using AngleSharp.Html.Parser;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Converters;

public class WebToExcelConverterTests
{
    private static string Fixture(string sample) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "web", sample, "public.html.bin"));

    // 셀마다 화면에 보이는 글자
    private static List<string> CellTexts(string html) =>
        new HtmlParser().ParseDocument(html).QuerySelectorAll("td, th").Select(cell => cell.TextContent.Trim()).ToList();

    [Theory]
    [InlineData("mac-safari-table")]
    [InlineData("mac-chrome-table")]
    public void WikipediaTable_DropsBlankMarkersAndFootnotes(string sample)
    {
        var original = Fixture(sample);
        var result = WebToExcelConverter.Convert(original);

        Assert.Equal("C9", result.ConverterId);
        Assert.False(result.IsPassthrough);
        Assert.Contains(CellTexts(original), text => text == "빈칸");
        Assert.DoesNotContain(CellTexts(result.Html!), text => text == "빈칸");
        Assert.DoesNotContain("[a]", new HtmlParser().ParseDocument(result.Html!).Body!.TextContent);
        // 표 모양(칸 수)과 값은 그대로
        Assert.Equal(CellTexts(original).Count, CellTexts(result.Html!).Count);
        Assert.Contains("9,770,638", result.Html);
        Assert.Contains("charset", result.Html);
        Assert.DoesNotContain("빈칸", result.PlainText);
    }

    [Fact]
    public void NothingToClean_IsPassthrough()
    {
        Assert.True(WebToExcelConverter.Convert("<table><tr><td>a</td><td>b</td></tr></table>").IsPassthrough);
    }

    [Fact]
    public void Matrix_UsesC9OnlyForWebToExcel()
    {
        var web = new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.MacHtml] = System.Text.Encoding.UTF8.GetBytes(Fixture("mac-safari-table")),
        }, "com.apple.Safari", 1);

        using var prepared = new UpClipConverter().Prepare(web, ConversionOptions.Default, CancellationToken.None);

        Assert.Equal("C9", prepared.Get(TargetApp.Excel, ConversionOptions.Default).ConverterId);
    }

    // 위키백과가 아닌 웹·Word·한글 표: 칸 내용이 "빈칸"뿐이면 비운다
    [Fact]
    public void AnyHtmlTable_ClearsCellsThatOnlySayBlank()
    {
        var html = "<html><head><style>table.MsoTableGrid{border:solid windowtext 1.0pt}</style></head><body>" +
                   "<p>빈칸 채우기 문제</p><table class=MsoTableGrid><tr><td><p class=MsoNormal><span lang=KO>빈칸</span></p></td>" +
                   "<td> 빈칸 </td><td>빈칸 있음</td><td>17</td></tr></table></body></html>";

        var result = WebToExcelConverter.Convert(html);

        Assert.Equal(["", "", "빈칸 있음", "17"], CellTexts(result.Html!));
        Assert.Contains("빈칸 채우기 문제", result.Html);   // 표 밖 문장은 그대로
        Assert.Contains("MsoTableGrid{border", result.Html);  // Word 표 서식(<style>) 유지
        Assert.DoesNotContain("빈칸\t", result.PlainText);   // 비운 칸 뒤 탭 앞에 "빈칸"이 없음
        Assert.Contains("빈칸 있음", result.PlainText);
    }

    [Fact]
    public void WordSnapshot_IsNotExcelAndGetsC9()
    {
        var html = "<table><tr><td>시</td><td>군</td></tr><tr><td>빈칸</td><td>17</td></tr></table>";
        var word = new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.MacHtml] = System.Text.Encoding.UTF8.GetBytes(html),
            ["com.microsoft.appbundleid"] = System.Text.Encoding.UTF8.GetBytes("com.microsoft.Word"),
            ["com.microsoft.Link-Source"] = [1],
        }, "com.microsoft.Word", 1);

        Assert.NotEqual(SourceKind.Excel, SourceDetector.Detect(word).Source);
        using var prepared = new UpClipConverter().Prepare(word, ConversionOptions.Default, CancellationToken.None);
        Assert.Equal("C9", prepared.Get(TargetApp.Excel, ConversionOptions.Default).ConverterId);
    }

    [Fact]
    public void ExcelToExcel_StaysPassthrough()
    {
        var excel = new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.MacHtml] = System.Text.Encoding.UTF8.GetBytes("<table><tr><td>빈칸</td><td>1</td></tr></table>"),
            ["com.microsoft.appbundleid"] = System.Text.Encoding.UTF8.GetBytes("com.microsoft.Excel"),
        }, null, 1);

        Assert.Equal(SourceKind.Excel, SourceDetector.Detect(excel).Source);
        using var prepared = new UpClipConverter().Prepare(excel, ConversionOptions.Default, CancellationToken.None);
        Assert.True(prepared.Get(TargetApp.Excel, ConversionOptions.Default).IsPassthrough);
    }

    [Fact]
    public void TabSeparatedText_ClearsBlankFields()
    {
        var text = new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.MacUtf8Text] = System.Text.Encoding.UTF8.GetBytes("시\t군\r\n빈칸\t17\r\n"),
        }, "com.apple.TextEdit", 1);

        var result = WebToExcelConverter.Convert(text);

        Assert.Equal("시\t군\r\n\t17\r\n", result.PlainText);
        Assert.Null(result.Html);
    }
}
