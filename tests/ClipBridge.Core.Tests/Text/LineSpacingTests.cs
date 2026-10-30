namespace ClipBridge.Core.Tests.Text;

// 줄간격 보정 (PRD 6.4, #6)
public class LineSpacingTests
{
    [Theory]
    [InlineData("a\r\nb\rc", "a\nb\nc")]                       // 줄바꿈 혼용
    [InlineData("a\n\n\n\nb", "a\n\nb")]                         // 연속 빈 줄 → 1줄
    [InlineData("a\n  \t\n\u00A0\n\u3000\nb", "a\n\nb")]        // 공백·탭·NBSP만 있는 줄은 빈 줄
    [InlineData("\n\n a\nb\n\n", " a\nb")]                       // 앞뒤 빈 줄 제거, 줄 앞 공백은 글자라 유지
    [InlineData("a  \nb", "a  \nb")]                             // 글자가 있는 줄의 공백은 그대로
    public void NormalizeText(string input, string expected)
    {
        Assert.Equal(expected, LineSpacing.NormalizeText(input));
    }

    [Theory]
    [InlineData("<p>a</p><p></p><p>&nbsp;</p><p>b</p>", true)]
    [InlineData("<p>a</p><p><br></p>\n<p> </p>", true)]
    [InlineData("<p>a<br><br>b</p>", true)]
    [InlineData("<p>a</p><p></p><p>b</p>", false)]                 // 빈 문단 하나는 S9 아님 (연속일 때만)
    [InlineData("<p>a<br>b</p>", false)]
    // 코드 블록의 빈 줄과 표 셀은 보지 않는다
    [InlineData("<table><tr><td><p>&nbsp;</p><p>&nbsp;</p></td></tr></table>", false)]
    [InlineData("<pre><code>a\n\n\nb</code></pre>", false)]
    public void HasEmptyParagraphRun(string html, bool expected)
    {
        Assert.Equal(expected, LineSpacing.HasEmptyParagraphRun(html));
    }

    [Fact]
    public void Finish_AddsS9_AndCrlfOnlyForWindows()
    {
        var result = new ConversionResult("C3", false, "<p>a</p><p></p><p></p>", "a\nb", []);

        var windows = LineSpacing.Finish(result, windows: true);
        var mac = LineSpacing.Finish(result, windows: false);

        Assert.Equal("a\r\nb", windows.PlainText);
        Assert.Equal("a\nb", mac.PlainText);
        Assert.Equal(("S9", 1), (windows.Signals.Single().Id, windows.Signals.Single().Score));
    }

    [Fact]
    public void Finish_LeavesPassthrough()
    {
        Assert.Same(ConversionResult.Passthrough, LineSpacing.Finish(ConversionResult.Passthrough, windows: true));
    }
}
