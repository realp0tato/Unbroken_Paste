using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C3 인터넷 → 한글 (PRD 6.3.3, #16).
// 웹 HTML을 정리(#9)한 뒤 C5와 같은 한글용 HTML(제목 크기·굵기, 여백·테두리 직접 지정)로 낸다.
// 링크는 글자만 남긴다. AI 답변을 드래그해서 복사한 경우(화면을 옮긴 HTML)도 여기로 온다
public static class WebToHangulConverter
{
    public const string Id = "C3";

    // mode는 화면에 글자로 남은 **…**만 다룬다 (WebHtmlCleaner.ToMarkdown)
    public static ConversionResult Convert(ClipboardSnapshot snapshot, MarkdownMode mode = MarkdownMode.Format) =>
        WebClipboard.IsTooLarge(snapshot) || WebClipboard.ReadHtml(snapshot, out _) is not { } html
            ? ConversionResult.Passthrough
            : Convert(html, mode);

    public static ConversionResult Convert(string html, MarkdownMode mode = MarkdownMode.Format)
    {
        var cleaned = WebHtmlCleaner.ToMarkdown(html, keepLinks: false, mode);
        if (cleaned.Markdown.Length == 0)
            return ConversionResult.Passthrough;
        var document = MarkdownEngine.Parse(cleaned.Markdown, out _);
        var plain = MarkdownEngine.Convert(cleaned.Markdown, MarkdownMode.Strip).PlainText;
        return new ConversionResult(Id, false, DocHtmlRenderer.Render(document), plain, cleaned.Signals);
    }
}
