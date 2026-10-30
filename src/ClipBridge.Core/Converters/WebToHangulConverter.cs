using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C3 인터넷 → 한글 (PRD 6.3.3, #16).
// 웹 HTML을 정리(#9)한 뒤 C5와 같은 한글용 HTML(제목 크기·굵기, 여백·테두리 직접 지정)로 낸다.
// 링크는 글자만 남긴다. AI 답변을 드래그해서 복사한 경우(화면을 옮긴 HTML)도 여기로 온다
public static class WebToHangulConverter
{
    public const string Id = "C3";

    // Markdown 처리 옵션을 따른다. 서식 적용: 제목·굵게·목록·표를 한글용 HTML로.
    // 기호 제거: 서식 없이 평문으로 (C5 기호 제거와 같은 결과 — 서식 없는 문단 HTML + 일반 텍스트)
    public static ConversionResult Convert(ClipboardSnapshot snapshot, MarkdownMode mode = MarkdownMode.Format) =>
        WebClipboard.IsTooLarge(snapshot) || WebClipboard.ReadHtml(snapshot, out _) is not { } html
            ? ConversionResult.Passthrough
            : Convert(html, mode);

    public static ConversionResult Convert(string html, MarkdownMode mode = MarkdownMode.Format)
    {
        var cleaned = WebHtmlCleaner.ToMarkdown(html, keepLinks: false, mode);
        if (cleaned.Markdown.Length == 0)
            return ConversionResult.Passthrough;
        var plain = MarkdownEngine.Convert(cleaned.Markdown, MarkdownMode.Strip).PlainText;
        var output = mode == MarkdownMode.Format
            ? DocHtmlRenderer.Render(MarkdownEngine.Parse(cleaned.Markdown, out _))
            : PlainTextConverter.TextHtml(plain);
        return new ConversionResult(Id, false, output, plain, cleaned.Signals);
    }
}
