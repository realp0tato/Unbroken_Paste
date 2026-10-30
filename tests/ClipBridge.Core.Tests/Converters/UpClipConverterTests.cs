using System.Text;
using ClipBridge.Core.Tests.Fixtures;

namespace ClipBridge.Core.Tests.Converters;

// 출처 판별(PRD 6.1, #13)과 [출처 → 대상] 매트릭스(PRD 6.2, #14)
public class UpClipConverterTests
{
    private const string Markdown = "## 주요 결과\n**중요한 내용**\n- 항목";

    private static ClipboardSnapshot Snapshot(string? sourceApp, params (string Type, string Value)[] formats) =>
        new(formats.ToDictionary(f => f.Type, f => Encoding.UTF8.GetBytes(f.Value)), sourceApp, 7);

    private static ClipboardSnapshot MacText(string text, string? app = "com.apple.TextEdit") =>
        Snapshot(app, (ClipFormats.MacUtf8Text, text));

    // ---- 출처 판별 ----

    [Theory]
    [InlineData("excel/mac-basic-table", SourceKind.Excel, ContentKind.Table)]
    [InlineData("excel/mac-single-column", SourceKind.Excel, ContentKind.Text)]
    [InlineData("excel/win-basic-table", SourceKind.Excel, ContentKind.Table)]
    [InlineData("excel/win-single-column", SourceKind.Excel, ContentKind.Text)]
    public void Detect_ExcelFixtures(string fixture, SourceKind source, ContentKind content)
    {
        Assert.Equal((source, content), SourceDetector.Detect(Fixture.Load(fixture)));
    }

    [Fact]
    public void Detect_ClaudeAppCopy_IsMarkdownEvenWithHtml()
    {
        var snapshot = Snapshot("com.anthropic.claudefordesktop",
            (ClipFormats.MacHtml, "<h2>주요 결과</h2>"), (ClipFormats.MacUtf8Text, Markdown));

        Assert.Equal((SourceKind.Markdown, ContentKind.Text), SourceDetector.Detect(snapshot));
    }

    [Fact]
    public void Detect_ClaudeWebCopy_IsMarkdownByUrl()
    {
        var snapshot = Snapshot("com.google.Chrome",
            (ClipFormats.MacHtml, "<h2>주요 결과</h2>"), (ClipFormats.MacUtf8Text, Markdown),
            (SourceDetector.MacChromiumSourceUrl, "https://claude.ai/chat/abc"));

        Assert.Equal(SourceKind.Markdown, SourceDetector.Detect(snapshot).Source);
    }

    [Fact]
    public void Detect_AiCopyWithoutMarkdown_IsNotMarkdown()
    {
        var snapshot = Snapshot("com.anthropic.claudefordesktop",
            (ClipFormats.MacHtml, "<p>그냥 문장</p>"), (ClipFormats.MacUtf8Text, "그냥 문장"));

        Assert.NotEqual(SourceKind.Markdown, SourceDetector.Detect(snapshot).Source);
    }

    [Theory]
    [InlineData("com.apple.Safari", null)]
    [InlineData("com.google.Chrome", "https://ko.wikipedia.org/wiki/x")]
    [InlineData(null, "https://example.com/")]
    public void Detect_Web(string? app, string? url)
    {
        var formats = new List<(string, string)> { (ClipFormats.MacHtml, "<p>글</p><table><tr><td>a</td></tr></table>"), (ClipFormats.MacUtf8Text, "글") };
        if (url is not null)
            formats.Add((SourceDetector.MacChromiumSourceUrl, url));

        Assert.Equal((SourceKind.Web, ContentKind.Text), SourceDetector.Detect(Snapshot(app, formats.ToArray())));
    }

    [Fact]
    public void Detect_WebTableOnly_IsTable()
    {
        var snapshot = Snapshot("com.apple.Safari",
            (ClipFormats.MacHtml, "<meta charset='utf-8'><table><tr><td>a</td><td>b</td></tr></table>"), (ClipFormats.MacUtf8Text, "a\tb"));

        Assert.Equal((SourceKind.Web, ContentKind.Table), SourceDetector.Detect(snapshot));
    }

    [Fact]
    public void Detect_WindowsCfHtmlSourceUrl_IsWeb()
    {
        var snapshot = new ClipboardSnapshot(new Dictionary<string, byte[]>
        {
            [ClipFormats.WinHtml] = CfHtml.Wrap("<p>글</p>", "https://example.com/"),
            [ClipFormats.WinUnicodeText] = Encoding.Unicode.GetBytes("글\0"),
        }, "notepad.exe", 1);

        Assert.Equal(SourceKind.Web, SourceDetector.Detect(snapshot).Source);
    }

    [Fact]
    public void Detect_HtmlFromNonBrowserWithoutUrl_IsOther()
    {
        var snapshot = Snapshot("com.microsoft.Word", (ClipFormats.MacHtml, "<p>글</p>"), (ClipFormats.MacUtf8Text, "글"));

        Assert.Equal(SourceKind.Other, SourceDetector.Detect(snapshot).Source);
    }

    [Theory]
    [InlineData("# 제목", SourceKind.Markdown)]
    [InlineData("**굵게** 그리고\n- 목록", SourceKind.Markdown)]
    [InlineData("| a | b |\n|---|---|\n| 1 | 2 |", SourceKind.Markdown)]
    [InlineData("**굵게**만 하나", SourceKind.PlainText)]
    [InlineData("그냥 메모\n두 번째 줄", SourceKind.PlainText)]
    [InlineData("file_name 과 2*3", SourceKind.PlainText)]
    public void Detect_PlainTextOnly(string text, SourceKind expected)
    {
        Assert.Equal(expected, SourceDetector.Detect(MacText(text)).Source);
    }

    [Fact]
    public void Detect_MarkdownPipeTableOnly_IsTable()
    {
        Assert.Equal(ContentKind.Table, SourceDetector.Detect(MacText("| a | b |\n|---|---|\n| 1 | 2 |")).Content);
    }

    [Fact]
    public void Detect_NoText_IsOther()
    {
        Assert.Equal(SourceKind.Other, SourceDetector.Detect(Snapshot(null, ("public.png", "x"))).Source);
    }

    // ---- 매트릭스: 모든 출처 × 대상 × 모드 ----

    public static TheoryData<string, TargetApp, MarkdownMode, string> Matrix()
    {
        var data = new TheoryData<string, TargetApp, MarkdownMode, string>();
        foreach (var mode in Enum.GetValues<MarkdownMode>())
        {
            data.Add("excel", TargetApp.Hangul, mode, "C1");
            data.Add("excel", TargetApp.Notion, mode, "C2");
            data.Add("excel", TargetApp.Word, mode, "PASS");
            data.Add("excel", TargetApp.Other, mode, "PASS");
            data.Add("markdown", TargetApp.Hangul, mode, "C5");
            data.Add("markdown", TargetApp.Notion, mode, "C6");
            data.Add("markdown", TargetApp.Word, mode, "PASS");   // C8 #12 전
            data.Add("markdown", TargetApp.Other, mode, "PASS");
            data.Add("web", TargetApp.Hangul, mode, "C3");
            data.Add("web", TargetApp.Notion, mode, "C4");
            data.Add("web", TargetApp.Word, mode, "PASS");        // C7 #10 전
            data.Add("plain", TargetApp.Hangul, mode, "PASS");    // TX #6 전
            data.Add("other", TargetApp.Notion, mode, "PASS");
        }
        return data;
    }

    private static ClipboardSnapshot Sample(string kind) => kind switch
    {
        "excel" => Fixture.Load("excel/mac-basic-table"),
        "markdown" => MacText(Markdown),
        "web" => Snapshot("com.apple.Safari", (ClipFormats.MacHtml, "<p>글</p>"), (ClipFormats.MacUtf8Text, "글")),
        "plain" => MacText("그냥 메모"),
        _ => Snapshot(null, ("public.png", "x")),
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Prepare_UsesMatrix(string kind, TargetApp target, MarkdownMode mode, string converterId)
    {
        var options = new ConversionOptions(mode, AiVerification: false);
        using var prepared = new UpClipConverter().Prepare(Sample(kind), options, CancellationToken.None);

        Assert.Equal(converterId, prepared.Get(target, options).ConverterId);
    }

    [Fact]
    public void Prepare_MarkdownModeChange_NeedsNoNewCopy()
    {
        using var prepared = new UpClipConverter().Prepare(MacText(Markdown), ConversionOptions.Default, CancellationToken.None);

        var format = prepared.Get(TargetApp.Hangul, new(MarkdownMode.Format, false));
        var strip = prepared.Get(TargetApp.Hangul, new(MarkdownMode.Strip, false));

        Assert.NotNull(format.Html);
        Assert.Null(strip.Html);
        Assert.Equal("주요 결과\n중요한 내용\n항목", strip.PlainText);
    }

    [Fact]
    public void Prepare_KeepsSequenceAndSource()
    {
        using var prepared = new UpClipConverter().Prepare(MacText(Markdown), ConversionOptions.Default, CancellationToken.None);

        Assert.Equal(7, prepared.SequenceNumber);
        Assert.Equal(SourceKind.Markdown, prepared.Source);
    }

    [Fact]
    public void Prepare_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new UpClipConverter().Prepare(MacText(Markdown), ConversionOptions.Default, cts.Token));
    }

    // ---- C3·C4 ----

    [Theory]
    [InlineData("web/mac-safari-blog")]
    [InlineData("web/mac-chrome-table")]
    public void WebFixtures_ToHangul_UseDocHtml(string fixture)
    {
        var result = WebToHangulConverter.Convert(Fixture.Load(fixture));

        Assert.Equal("C3", result.ConverterId);
        Assert.DoesNotMatch("<(h[1-6]|ul|ol|li|pre|a)[ >]", result.Html!);
        Assert.Contains("<table border=\"1\"", result.Html);
        Assert.DoesNotContain("빈칸", result.Html);
    }

    [Fact]
    public void WebFixture_ToNotion_KeepsHeadingsCodeAndLinks()
    {
        var result = WebToNotionConverter.Convert(Fixture.Load("web/mac-chrome-blog"));

        Assert.Equal("C4", result.ConverterId);
        Assert.Contains("<h2>클립보드 접근</h2>", result.Html);
        Assert.Contains("<pre><code>navigator.clipboard", result.Html);
        Assert.Contains("<a href=\"https://developer.mozilla.org/", result.Html);
        Assert.DoesNotContain("jsCopy", result.Html);
    }

    [Fact]
    public void ClaudeDragCopy_IsWeb_AndGetsC3()
    {
        // 복사 버튼이 아니라 드래그로 복사하면 Markdown 기호가 없는 글자 + 화면 HTML이 온다 (#16 댓글)
        var snapshot = Snapshot("com.anthropic.claudefordesktop",
            (ClipFormats.MacHtml, "<h2 aria-level=\"4\">제목</h2><ul><li>항목</li></ul>"),
            (ClipFormats.MacUtf8Text, "제목\n\n항목"),
            (SourceDetector.MacChromiumSourceUrl, "https://claude.ai/chat/x"));
        using var prepared = new UpClipConverter().Prepare(snapshot, ConversionOptions.Default, CancellationToken.None);

        Assert.Equal(SourceKind.Web, prepared.Source);
        Assert.Equal("C3", prepared.Get(TargetApp.Hangul, ConversionOptions.Default).ConverterId);
    }
}
