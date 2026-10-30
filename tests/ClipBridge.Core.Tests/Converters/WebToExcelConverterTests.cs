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
        Assert.StartsWith("<meta charset=\"utf-8\">", result.Html);
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
}
