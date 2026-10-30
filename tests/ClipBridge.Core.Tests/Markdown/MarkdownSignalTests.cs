using ClipBridge.Core.Markdown;

namespace ClipBridge.Core.Tests.Markdown;

// 의심 신호 S4: Markdown 문법이 애매함 (PRD 6.6.2)
public class MarkdownSignalTests
{
    private static IReadOnlyList<SuspicionSignal> Signals(string markdown) =>
        MarkdownEngine.Convert(markdown, MarkdownMode.Strip).Signals;

    [Theory]
    [InlineData("**닫히지 않은 굵게")]
    [InlineData("`닫히지 않은 코드")]
    [InlineData("*강조하려던 글")]
    [InlineData("| 이름 | 역할 |\n| 재현 | 변환기 |")]
    public void AmbiguousSyntax_RaisesS4(string markdown)
    {
        var signal = Assert.Single(Signals(markdown));

        Assert.Equal("S4", signal.Id);
        Assert.Equal(1, signal.Score);
    }

    [Theory]
    [InlineData("## 제목\n**굵게** 문장")]
    [InlineData("file_name 과 2*3")]
    [InlineData(@"\*\* 이스케이프")]
    [InlineData("| 이름 | 역할 |\n|---|---|\n| 재현 | 변환기 |")]
    public void ClearSyntax_HasNoSignal(string markdown)
    {
        Assert.Empty(Signals(markdown));
    }
}
