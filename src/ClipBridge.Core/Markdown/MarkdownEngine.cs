using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ClipBridge.Core.Markdown;

// 기호 제거 모드는 Html이 null이다. 서식 적용 모드의 PlainText에는 기호 제거 결과를 넣는다 (PRD 6.3.5)
public sealed record MarkdownOutput(string? Html, string PlainText, IReadOnlyList<SuspicionSignal> Signals);

// Markdown 글 → 기호 제거 텍스트 / 서식 HTML (PRD 6.3.5–6.3.6, #11). C5·C6·C8이 공통으로 쓴다
public static class MarkdownEngine
{
    // 원시 HTML은 글자로 취급한다 (실행 금지).
    // AI 답변은 줄바꿈 하나로 줄을 나누는 경우가 많아 문단 안 줄바꿈도 <br>로 살린다
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    private static readonly string[] SafeLinkSchemes = ["http:", "https:", "mailto:"];

    public static MarkdownOutput Convert(string markdown, MarkdownMode mode)
    {
        var text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var document = Markdig.Markdown.Parse(text, Pipeline);

        var plainText = MarkdownStripper.Strip(document, text);
        var signals = MarkdownSignals.Find(document, text);
        var html = mode == MarkdownMode.Format ? RenderHtml(document) : null;

        return new MarkdownOutput(html, plainText, signals);
    }

    private static string RenderHtml(MarkdownDocument document)
    {
        // 이미지는 대체 글자로, 안전하지 않은 링크는 글자로 바꾼다 (외부 이미지 다운로드 금지).
        // copyChildren: false — 기본값은 자식까지 옮겨 글자가 두 번 나온다.
        // 바깥 링크가 먼저 바뀌면 안쪽 이미지는 이미 떨어져 나가 있으므로(Parent == null) 건너뛴다
        foreach (var link in document.Descendants<LinkInline>().ToList())
        {
            if (link.Parent is not null && (link.IsImage || !IsSafeLink(link.Url)))
                link.ReplaceBy(new LiteralInline(MarkdownStripper.InlineText(link)), copyChildren: false);
        }
        // <주소> 자동 링크는 LinkInline이 아니라 따로 검사한다. 이메일 자동 링크는 mailto로 본다
        foreach (var autolink in document.Descendants<AutolinkInline>().ToList())
        {
            if (!autolink.IsEmail && !IsSafeLink(autolink.Url))
                autolink.ReplaceBy(new LiteralInline(autolink.Url), copyChildren: false);
        }

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        return writer.ToString().TrimEnd('\n');
    }

    private static bool IsSafeLink(string? url) =>
        url is not null && SafeLinkSchemes.Any(scheme => url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
}
