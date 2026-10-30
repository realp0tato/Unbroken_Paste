using System.Text.RegularExpressions;

namespace ClipBridge.Core.Tests.Markdown;

// C5 Markdown → 한글 (PRD 6.3.5 대상별 서식 적용 방식, #18)
public class MarkdownToHangulTests
{
    private static string Html(string markdown) => MarkdownToHangulConverter.Convert(markdown, MarkdownMode.Format).Html!;

    [Theory]
    [InlineData("# 제목", "font-weight:bold;font-size:20pt\">제목</p>")]
    [InlineData("## 제목", "font-weight:bold;font-size:16pt\">제목</p>")]
    [InlineData("### 제목", "font-weight:bold;font-size:13pt\">제목</p>")]
    [InlineData("#### 제목", "font-weight:bold\">제목</p>")]
    public void Heading_IsSizedBoldParagraph(string markdown, string expected)
    {
        var html = Html(markdown);

        Assert.Contains(expected, html);
        Assert.DoesNotMatch("<h[1-6]", html);
    }

    // 일반 텍스트만 주면 한글이 커서 자리의 글자 모양(검은 배경 등)을 이어받는다
    [Fact]
    public void Strip_GivesUnformattedParagraphHtml()
    {
        var result = MarkdownToHangulConverter.Convert("## 제목\n\n**강조 내용**과 본문", MarkdownMode.Strip);

        Assert.Equal("<p style=\"margin:0 0 10pt\">제목</p><p style=\"margin:0 0 0\">강조 내용과 본문</p>", result.Html);
        Assert.Equal("제목\n\n강조 내용과 본문", result.PlainText);
    }

    [Fact]
    public void BoldBeforeParticle_IsBold()
    {
        Assert.Contains("<b>복사했는지(출처)</b>와", Html("**복사했는지(출처)**와"));
    }

    [Fact]
    public void Lists_UseMarkersAndExplicitIndent_NoListTags()
    {
        var html = Html("- 하나\n  - 안쪽\n\n1. 첫째\n2. 둘째");

        Assert.DoesNotMatch("<(ul|ol|li)[ >]", html);
        Assert.Contains("margin-left:20pt\">•&nbsp;하나</p>", html);
        Assert.Contains("margin-left:40pt\">◦&nbsp;안쪽</p>", html);
        Assert.Contains("margin-left:20pt\">1.&nbsp;첫째</p>", html);
        Assert.Contains("margin-left:20pt\">2.&nbsp;둘째</p>", html);
    }

    [Fact]
    public void Table_HasBordersPointWidthsAndBoldHeader()
    {
        var html = Html("| 이름 | 역할 |\n|---|:---:|\n| 재현 | 변환기 |");

        Assert.Contains("<table border=\"1\"", html);
        Assert.Matches(@"<col width=""\d+"" style=""width:\d+(\.\d)?pt"">", html);
        Assert.Contains("border:1px solid #000", html);
        Assert.Contains("font-weight:bold\">이름</p>", html);
        Assert.Contains("text-align:center\">변환기</p>", html);
    }

    [Fact]
    public void WideTable_FitsBodyWidth()
    {
        var cell = new string('가', 40);
        var html = Html($"| {cell} | {cell} | {cell} |\n|---|---|---|\n| a | b | c |");

        var width = double.Parse(Regex.Match(html, @"<table[^>]*width:([\d.]+)pt").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(width <= 150 / 25.4 * 72 + 0.1, $"표 너비 {width}pt가 본문 폭을 넘음");
    }

    private static double TableWidthPt(string html, int index = 0) =>
        double.Parse(Regex.Matches(html, @"<table[^>]*width:([\d.]+)pt")[index].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void SmallTable_IsMinimalWidthWithoutWrapping()
    {
        var html = Html("| 출처 | 대상 | 변환기 |\n|---|---|---|\n| 인터넷 | 한글 | C3 |\n| Markdown | Notion | C6 |");

        // 가장 긴 칸(한글 3자, Markdown 8자)이 줄바꿈되지 않는 너비이면서 본문 폭보다 훨씬 좁다
        Assert.InRange(TableWidthPt(html), 120, 250);
        Assert.Contains(" nowrap style=", html);
        Assert.Contains("white-space:nowrap", html);
        Assert.Matches(@"<table border=""1"" width=""\d+""", html);
    }

    [Fact]
    public void WiderCell_GetsWiderColumn()
    {
        var html = Html("| 짧음 | 아주 긴 칸 내용입니다 |\n|---|---|\n| a | b |");
        var widths = Regex.Matches(html, @"<col width=""\d+"" style=""width:([\d.]+)pt"">")
            .Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.True(widths[1] > widths[0] * 2);
    }

    [Fact]
    public void TooWideTable_ShrinksAndAllowsWrapping()
    {
        var cell = new string('가', 40);
        var html = Html($"| {cell} | {cell} |\n|---|---|\n| a | b |");

        Assert.DoesNotContain("nowrap", html);
    }

    [Fact]
    public void CodeBlock_IsAsWideAsLongestLine()
    {
        var shortCode = Html("```\nx = 1\n```");
        var longCode = Html("```\n" + new string('x', 50) + "\n```");

        Assert.True(TableWidthPt(shortCode) < TableWidthPt(longCode));
        Assert.True(TableWidthPt(longCode) >= 50 * 6.2);
        Assert.Contains("white-space:nowrap", longCode);
    }

    [Fact]
    public void CodeBlock_IsBorderedMonospaceCell_KeepsIndent()
    {
        var html = Html("```python\ndef greet(name):\n    print(name)\n```");

        Assert.DoesNotContain("<pre", html);
        Assert.Contains("<table border=\"1\"", html);
        Assert.Contains("monospace\">def greet(name):</p>", html);
        Assert.Contains("monospace\">&nbsp;&nbsp;&nbsp;&nbsp;print(name)</p>", html);
    }

    [Theory]
    [InlineData("**굵게**", "<b>굵게</b>")]
    [InlineData("*기울임*", "<i>기울임</i>")]
    [InlineData("~~취소~~", "<s>취소</s>")]
    [InlineData("`코드`", "monospace\">코드</span>")]
    [InlineData("[안전](https://example.com)", "<a href=\"https://example.com\">안전</a>")]
    public void Inline_Formatting(string markdown, string expected)
    {
        Assert.Contains(expected, Html(markdown));
    }

    [Theory]
    [InlineData("[위험](javascript:alert(1))", "위험")]
    [InlineData("![그림](https://example.com/a.png)", "그림")]
    [InlineData("<javascript:alert(1)>", "javascript:alert(1)")]
    [InlineData("<script>x</script>", "&lt;script&gt;x&lt;/script&gt;")]
    public void Unsafe_BecomesText(string markdown, string text)
    {
        var html = Html(markdown);

        Assert.DoesNotContain("<a ", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains(text, html);
    }

    [Fact]
    public void Quote_IsIndentedGrey()
    {
        Assert.Contains("margin-left:20pt;color:#555\">인용</p>", Html("> 인용"));
    }

    [Fact]
    public void Format_PlainTextIsStripResult_AndIdIsC5()
    {
        var result = MarkdownToHangulConverter.Convert("## 주요 결과\n**중요**", MarkdownMode.Format);

        Assert.Equal("C5", result.ConverterId);
        Assert.False(result.IsPassthrough);
        Assert.Equal("주요 결과\n중요", result.PlainText);
    }

    [Fact]
    public void Signals_ArePassedThrough()
    {
        var result = MarkdownToHangulConverter.Convert("**닫히지 않은 굵게", MarkdownMode.Format);

        Assert.Equal("S4", Assert.Single(result.Signals).Id);
    }
}
