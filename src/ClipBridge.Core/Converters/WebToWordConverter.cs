using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C7 인터넷 → Word (PRD 6.3.7, #10).
// C3과 같은 구조: 웹 HTML을 정리(#9)한 뒤 Word·한글용 HTML(제목은 글자 크기·굵기, Word 제목 스타일 미사용)로 낸다.
// 원본 HTML을 그대로 붙이면 Word가 <thead>를 "제목 행 반복"으로 받아 페이지마다 머리글이 다시 나오고,
// 위키백과가 화면에서만 숨기는 "빈칸" 표시 글자가 보인다. 정리한 표는 <thead> 없이 머리글을 굵게만 한다.
// C3과 달리 링크는 유지한다 (Word는 링크를 지원)
public static class WebToWordConverter
{
    public const string Id = "C7";

    public static ConversionResult Convert(ClipboardSnapshot snapshot, MarkdownMode mode = MarkdownMode.Format) =>
        WebClipboard.IsTooLarge(snapshot) || WebClipboard.ReadHtml(snapshot, out _) is not { } html
            ? ConversionResult.Passthrough
            : Convert(html, mode);

    public static ConversionResult Convert(string html, MarkdownMode mode = MarkdownMode.Format)
    {
        var cleaned = WebHtmlCleaner.ToMarkdown(html, keepLinks: true, mode);
        if (cleaned.Markdown.Length == 0)
            return ConversionResult.Passthrough;
        var plain = MarkdownEngine.Convert(cleaned.Markdown, MarkdownMode.Strip).PlainText;
        var output = mode == MarkdownMode.Format
            ? DocHtmlRenderer.Render(MarkdownEngine.Parse(cleaned.Markdown, out _))
            : PlainTextConverter.TextHtml(plain);
        return new ConversionResult(Id, false, output, plain, cleaned.Signals);
    }
}
