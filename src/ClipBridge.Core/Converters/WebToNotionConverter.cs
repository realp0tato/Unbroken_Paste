using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C4 인터넷 → Notion (PRD 6.3.4, #17).
// 웹 HTML을 정리(#9)한 뒤 사이트 서식 없는 HTML(제목·목록·표·코드·링크)로 낸다. 링크와 코드는 유지
public static class WebToNotionConverter
{
    public const string Id = "C4";

    // Markdown 처리 옵션을 따른다. 서식 적용: 제목·목록·표·코드·링크를 Notion 블록용 HTML로.
    // 기호 제거: 일반 텍스트만 (C6 기호 제거와 같음)
    public static ConversionResult Convert(ClipboardSnapshot snapshot, MarkdownMode mode = MarkdownMode.Format) =>
        WebClipboard.IsTooLarge(snapshot) || WebClipboard.ReadHtml(snapshot, out _) is not { } html
            ? ConversionResult.Passthrough
            : Convert(html, mode);

    public static ConversionResult Convert(string html, MarkdownMode mode = MarkdownMode.Format)
    {
        var cleaned = WebHtmlCleaner.ToMarkdown(html, keepLinks: true, mode);
        if (cleaned.Markdown.Length == 0)
            return ConversionResult.Passthrough;
        var output = MarkdownEngine.Convert(cleaned.Markdown, mode);
        return new ConversionResult(Id, false, output.Html, output.PlainText, cleaned.Signals);
    }
}
