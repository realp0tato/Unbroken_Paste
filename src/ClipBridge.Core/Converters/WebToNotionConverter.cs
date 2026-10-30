using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C4 인터넷 → Notion (PRD 6.3.4, #17).
// 웹 HTML을 정리(#9)한 뒤 사이트 서식 없는 HTML(제목·목록·표·코드·링크)로 낸다. 링크와 코드는 유지
public static class WebToNotionConverter
{
    public const string Id = "C4";

    public static ConversionResult Convert(ClipboardSnapshot snapshot) =>
        WebClipboard.IsTooLarge(snapshot) || WebClipboard.ReadHtml(snapshot, out _) is not { } html
            ? ConversionResult.Passthrough
            : Convert(html);

    public static ConversionResult Convert(string html)
    {
        var cleaned = WebHtmlCleaner.ToMarkdown(html, keepLinks: true);
        if (cleaned.Markdown.Length == 0)
            return ConversionResult.Passthrough;
        var output = MarkdownEngine.Convert(cleaned.Markdown, MarkdownMode.Format);
        return new ConversionResult(Id, false, output.Html, output.PlainText, cleaned.Signals);
    }
}
