using ClipBridge.Core.Markdown;

namespace ClipBridge.Core.Tests.Markdown;

// PRD 6.3.5 기호 제거 규칙 표를 한 줄씩 확인한다
public class MarkdownStripTests
{
    private static string Strip(string markdown) => MarkdownEngine.Convert(markdown, MarkdownMode.Strip).PlainText;

    [Theory]
    // 제목 기호
    [InlineData("# 보고서 제목", "보고서 제목")]
    [InlineData("## 주요 결과", "주요 결과")]
    [InlineData("###### 여섯 번째", "여섯 번째")]
    // 굵게·기울임·취소선
    [InlineData("**중요한 내용**", "중요한 내용")]
    [InlineData("__굵게__", "굵게")]
    [InlineData("*기울임*", "기울임")]
    [InlineData("_기울임_", "기울임")]
    [InlineData("~~취소선~~", "취소선")]
    [InlineData("앞 **굵게** 뒤", "앞 굵게 뒤")]
    // 인라인 코드
    [InlineData("`코드`", "코드")]
    // 인용
    [InlineData("> 인용", "인용")]
    // 링크와 이미지
    [InlineData("[표시 글](https://example.com)", "표시 글")]
    [InlineData("![그림 설명](https://example.com/a.png)", "그림 설명")]
    // 이스케이프
    [InlineData(@"\*별표\*", "*별표*")]
    // 일반 기호는 유지
    [InlineData("file_name", "file_name")]
    [InlineData("2*3 = 6", "2*3 = 6")]
    // 원시 HTML은 글자 그대로
    [InlineData("<b>굵게</b>", "<b>굵게</b>")]
    public void Strip_RemovesSymbolsOnly(string markdown, string expected)
    {
        Assert.Equal(expected, Strip(markdown));
    }

    [Fact]
    public void Strip_CodeFence_RemovesFenceLinesOnly()
    {
        var markdown = "```csharp\nvar a = 1;\n  var b = 2;\n```";

        Assert.Equal("var a = 1;\n  var b = 2;", Strip(markdown));
    }

    [Fact]
    public void Strip_BulletList_RemovesMarkersAndKeepsIndent()
    {
        var markdown = "- 하나\n* 둘\n+ 셋\n- 바깥\n  - 안쪽";

        Assert.Equal("하나\n둘\n셋\n바깥\n  안쪽", Strip(markdown));
    }

    [Fact]
    public void Strip_OrderedList_KeepsNumbers()
    {
        var markdown = "1. 첫째\n2. **둘째**\n3) 셋째";

        Assert.Equal("1. 첫째\n2. 둘째\n3) 셋째", Strip(markdown));
    }

    [Fact]
    public void Strip_ThematicBreak_RemovesLine()
    {
        Assert.Equal("위\n\n아래", Strip("위\n\n---\n\n아래"));
        Assert.Equal("위\n\n아래", Strip("위\n\n***\n\n아래"));
    }

    [Fact]
    public void Strip_PipeTable_BecomesTabSeparated()
    {
        var markdown = "| 이름 | 역할 |\n|---|---|\n| 재현 | **변환기** |\n| 세찬 | 표 |";

        Assert.Equal("이름\t역할\n재현\t변환기\n세찬\t표", Strip(markdown));
    }

    [Fact]
    public void Strip_KeepsBlankLinesBetweenBlocksAsInSource()
    {
        var markdown = "# 제목\n본문 첫 줄\n둘째 줄\n\n## 다음";

        Assert.Equal("제목\n본문 첫 줄\n둘째 줄\n\n다음", Strip(markdown));
    }

    [Fact]
    public void Strip_KeepsKoreanEnglishAndEmoji()
    {
        Assert.Equal("한글 English 🎉 완료", Strip("**한글** English 🎉 _완료_"));
    }

    [Fact]
    public void Strip_NormalizesCrlf()
    {
        Assert.Equal("제목\n본문", Strip("# 제목\r\n본문"));
    }
}
