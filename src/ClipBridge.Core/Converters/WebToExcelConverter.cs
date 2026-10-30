using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C9 인터넷 → Excel (P1 대상 확장, FR-21, #25).
// Excel 기본 붙여넣기는 웹 표를 잘 받지만 위키백과 "빈칸" 표시·각주([a])까지 셀 글자로 남긴다.
// 그런 찌꺼기만 지우고 나머지는 원본 HTML 그대로 넘긴다. 지울 것이 없으면 통과
public static class WebToExcelConverter
{
    public const string Id = "C9";

    public static ConversionResult Convert(ClipboardSnapshot snapshot) =>
        WebClipboard.IsTooLarge(snapshot) || WebClipboard.ReadHtml(snapshot, out _) is not { } html
            ? ConversionResult.Passthrough
            : Convert(html);

    public static ConversionResult Convert(string html)
    {
        if (WebHtmlCleaner.CleanForExcel(html) is not { } cleaned)
            return ConversionResult.Passthrough;
        // 일반 텍스트는 C3과 같다: 표는 칸마다 탭, 그 밖은 줄마다 한 줄
        var plain = MarkdownEngine.Convert(WebHtmlCleaner.ToMarkdown(html, keepLinks: false).Markdown, MarkdownMode.Strip).PlainText;
        return new ConversionResult(Id, false, cleaned, plain, []);
    }
}
