namespace ClipBridge.Core.Tests.Converters;

// TX 일반 텍스트 줄간격 정리 (PRD 6.3.9, #6)
public class PlainTextConverterTests
{
    private const string Text = "회의 메모\n\n\n\n안건 1\n  들여쓴 줄\n \u00A0\n- 목록처럼 보이는 줄\n";

    [Fact]
    public void Text_KeepsCharacters_FixesBlankLines()
    {
        var result = PlainTextConverter.Convert(Text, TargetApp.Hangul);

        Assert.Equal("TX", result.ConverterId);
        Assert.Equal("회의 메모\n\n안건 1\n  들여쓴 줄\n\n- 목록처럼 보이는 줄", result.PlainText);
    }

    [Fact]
    public void Text_Html_HasNoEmptyParagraphs_GapIsMargin()
    {
        var html = PlainTextConverter.Convert(Text, TargetApp.Word).Html!;

        Assert.Equal(
            "<p style=\"margin:0 0 10pt\">회의 메모</p>" +
            "<p style=\"margin:0 0 0\">안건 1</p>" +
            "<p style=\"margin:0 0 10pt\">&nbsp;&nbsp;들여쓴 줄</p>" +
            "<p style=\"margin:0 0 0\">- 목록처럼 보이는 줄</p>",
            html);
        Assert.False(LineSpacing.HasEmptyParagraphRun(html));
    }

    [Fact]
    public void Text_Html_EncodesAndKeepsInnerSpaces()
    {
        Assert.Equal("<p style=\"margin:0 0 0\">a &lt;b&gt; &nbsp;c</p>", PlainTextConverter.Convert("a <b>  c", TargetApp.Notion).Html);
    }

    [Fact]
    public void Tsv_ToHangulAndWord_UsesC1BorderedTable()
    {
        foreach (var target in new[] { TargetApp.Hangul, TargetApp.Word })
        {
            var result = PlainTextConverter.Convert("이름\t역할\n재현\t변환기\n", target);

            Assert.Equal("TX", result.ConverterId);
            Assert.Contains("<table border=\"1\"", result.Html);
            Assert.Equal("이름\t역할\n재현\t변환기", result.PlainText);
        }
    }

    [Fact]
    public void Tsv_ToNotion_UsesC2SimpleTable()
    {
        var result = PlainTextConverter.Convert("이름\t역할\n재현\t변환기", TargetApp.Notion);

        Assert.StartsWith("<table><thead><tr><th>이름</th>", result.Html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n \n\u00A0\n")]
    public void BlankOnly_Passes(string text)
    {
        Assert.True(PlainTextConverter.Convert(text, TargetApp.Hangul).IsPassthrough);
    }
}
