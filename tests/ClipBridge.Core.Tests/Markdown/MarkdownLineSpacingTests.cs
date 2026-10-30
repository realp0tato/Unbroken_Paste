using ClipBridge.Core.Markdown;

namespace ClipBridge.Core.Tests.Markdown;

// Markdown 변환기(C5·C6)의 줄간격 보정 (PRD 6.4, #6)
public class MarkdownLineSpacingTests
{
    [Fact]
    public void LooseList_NotionHtml_IsTight()
    {
        // 항목 사이 빈 줄 → <li><p>…</p></li>가 되면 한글·Word에서 항목 간격이 벌어진다 (#51 리뷰)
        var html = MarkdownEngine.Convert("- 하나\n\n- 둘\n\n- 셋", MarkdownMode.Format).Html!;

        Assert.Contains("<li>하나</li>", html);
        Assert.DoesNotContain("<li><p>", html);
    }

    [Fact]
    public void LooseList_WithTwoParagraphsInItem_KeepsParagraphs()
    {
        var html = MarkdownEngine.Convert("- 하나\n\n  이어지는 문단\n- 둘", MarkdownMode.Format).Html!;

        Assert.Contains("<li><p>하나</p>", html);
        Assert.Contains("<p>이어지는 문단</p>", html);
    }

    [Fact]
    public void LooseList_StripText_IsTight()
    {
        Assert.Equal("하나\n둘", MarkdownEngine.Convert("- 하나\n\n\n- 둘", MarkdownMode.Strip).PlainText);
    }

    [Fact]
    public void NbspLine_IsBlankLine_NotEmptyParagraph()
    {
        var output = MarkdownEngine.Convert("# 제목\n \n \n본문", MarkdownMode.Format);

        Assert.Equal("제목\n\n본문", output.PlainText);
        Assert.DoesNotMatch(@"<p>(\s|&nbsp;| )*</p>", output.Html!);
    }

    [Theory]
    [InlineData("## 표\n| a | b |\n|---|---|\n| 1 | 2 |\n## 다음", "표\n\na\tb\n1\t2\n\n다음")]   // 빈 줄 없이 붙은 표
    [InlineData("앞 문단\n\n\n\n| a | b |\n|---|---|\n| 1 | 2 |", "앞 문단\n\na\tb\n1\t2")]
    public void Table_StripText_HasOneBlankLineAround(string markdown, string expected)
    {
        Assert.Equal(expected, MarkdownEngine.Convert(markdown, MarkdownMode.Strip).PlainText);
    }

    [Fact]
    public void ManyBlankLines_StripText_CollapseToOne()
    {
        Assert.Equal("가\n\n나", MarkdownEngine.Convert("\n\n가\n\n\n\n\n나\n\n", MarkdownMode.Strip).PlainText);
    }

    [Fact]
    public void CodeBlock_BlankLines_AreKept()
    {
        var markdown = "```\na\n\n\nb\n```";

        Assert.Equal("a\n\n\nb", MarkdownEngine.Convert(markdown, MarkdownMode.Strip).PlainText);
        Assert.Contains("a\n\n\nb", MarkdownEngine.Convert(markdown, MarkdownMode.Format).Html);
    }

    [Fact]
    public void HangulHtml_SkipsEmptyParagraph()
    {
        // 대체 글자가 없는 이미지만 있는 문단은 글자가 없다
        var html = MarkdownToHangulConverter.Convert("가\n\n![](https://example.com/a.png)\n\n나", MarkdownMode.Format).Html!;

        Assert.Equal(2, html.Split("<p ").Length - 1);
        Assert.False(LineSpacing.HasEmptyParagraphRun(html));
    }
}
