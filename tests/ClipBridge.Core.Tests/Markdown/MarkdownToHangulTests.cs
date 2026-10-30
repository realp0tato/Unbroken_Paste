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
        Assert.Matches(@"<col style=""width:\d+(\.\d)?pt"">", html);
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
    public void Strip_HasNoHtml()
    {
        var result = MarkdownToHangulConverter.Convert("## 주요 결과", MarkdownMode.Strip);

        Assert.Null(result.Html);
        Assert.Equal("주요 결과", result.PlainText);
    }

    [Fact]
    public void Signals_ArePassedThrough()
    {
        var result = MarkdownToHangulConverter.Convert("**닫히지 않은 굵게", MarkdownMode.Format);

        Assert.Equal("S4", Assert.Single(result.Signals).Id);
    }
}
