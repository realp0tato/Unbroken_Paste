using ClipBridge.Core.Markdown;

namespace ClipBridge.Core.Tests.Markdown;

// 서식 적용 모드 (PRD 6.3.5)
public class MarkdownFormatTests
{
    private static MarkdownOutput Format(string markdown) => MarkdownEngine.Convert(markdown, MarkdownMode.Format);

    [Theory]
    [InlineData("# 보고서 제목", "<h1>보고서 제목</h1>")]
    [InlineData("## 주요 결과", "<h2>주요 결과</h2>")]
    [InlineData("**중요한 내용**", "<strong>중요한 내용</strong>")]
    [InlineData("~~취소선~~", "<del>취소선</del>")]
    [InlineData("**복사했는지(출처)**와", "<strong>복사했는지(출처)</strong>와")]
    public void Format_AppliesFormatting(string markdown, string expectedFragment)
    {
        Assert.Contains(expectedFragment, Format(markdown).Html);
    }

    [Fact]
    public void Format_Lists()
    {
        var html = Format("- 하나\n- 둘\n\n1. 첫째\n2. 둘째").Html;

        Assert.Contains("<ul>", html);
        Assert.Contains("<li>하나</li>", html);
        Assert.Contains("<ol>", html);
        Assert.Contains("<li>첫째</li>", html);
    }

    [Fact]
    public void Format_PipeTable()
    {
        var html = Format("| 이름 | 역할 |\n|---|---|\n| 재현 | 변환기 |").Html;

        Assert.Contains("<table>", html);
        Assert.Contains("<th>이름</th>", html);
        Assert.Contains("<td>재현</td>", html);
    }

    [Fact]
    public void Format_PlainTextIsStripResult()
    {
        var markdown = "## 주요 결과\n**중요한 내용**";

        var output = Format(markdown);

        Assert.Equal("주요 결과\n중요한 내용", output.PlainText);
        Assert.Equal(MarkdownEngine.Convert(markdown, MarkdownMode.Strip).PlainText, output.PlainText);
    }

    [Fact]
    public void Strip_HasNoHtml()
    {
        Assert.Null(MarkdownEngine.Convert("**굵게**", MarkdownMode.Strip).Html);
    }

    [Fact]
    public void Format_RawHtmlIsEscaped()
    {
        var html = Format("<script>alert(1)</script> <b>굵게</b>").Html!;

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Format_ImageBecomesAltText()
    {
        var html = Format("![그림 설명](https://example.com/a.png)").Html!;

        Assert.DoesNotContain("<img", html);
        Assert.Contains("그림 설명", html);
    }

    [Fact]
    public void Format_UnsafeLink_TextAppearsOnce()
    {
        Assert.Equal("<p>위험</p>", Format("[위험](javascript:alert(1))").Html);
    }

    [Fact]
    public void Format_ImageInsideLink_TextAppearsOnce()
    {
        Assert.Equal("<p><a href=\"https://x.com\">alt 글</a></p>", Format("[![alt](a.png) 글](https://x.com)").Html);
    }

    [Fact]
    public void Format_ImageInsideUnsafeLink_TextAppearsOnce()
    {
        Assert.Equal("<p>alt 글</p>", Format("[![alt](a.png) 글](javascript:x)").Html);
    }

    [Theory]
    [InlineData("<javascript:alert(1)>", "<p>javascript:alert(1)</p>")]
    [InlineData("<data:text/html,x>", "<p>data:text/html,x</p>")]
    [InlineData("<https://example.com>", "<p><a href=\"https://example.com\">https://example.com</a></p>")]
    [InlineData("<me@example.com>", "<p><a href=\"mailto:me@example.com\">me@example.com</a></p>")]
    public void Format_Autolink_KeepsSafeOnly(string markdown, string expected)
    {
        Assert.Equal(expected, Format(markdown).Html);
    }

    [Fact]
    public void Format_SingleLineBreak_IsKept()
    {
        Assert.Equal("<p>첫 줄<br />\n둘째 줄</p>", Format("첫 줄\n둘째 줄").Html);
    }

    [Fact]
    public void Format_KeepsSafeLinksOnly()
    {
        var html = Format("[안전](https://example.com) [위험](javascript:alert(1))").Html!;

        Assert.Contains("<a href=\"https://example.com\">안전</a>", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.Contains("위험", html);
    }
}
