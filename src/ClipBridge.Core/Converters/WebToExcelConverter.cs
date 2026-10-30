using ClipBridge.Core.Markdown;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// C9 → Excel (P1 대상 확장, FR-21, #25). 이름과 달리 출처가 Excel이 아니면 모두 여기로 온다.
// Excel 기본 붙여넣기는 표를 잘 받지만 위키백과 각주([a])와, 표 칸에 적힌 "빈칸"까지 셀 글자로 남긴다.
// 그런 것만 고치고 나머지는 원본 그대로 넘긴다. 고칠 것이 없으면 통과
public static class WebToExcelConverter
{
    public const string Id = "C9";

    public static ConversionResult Convert(ClipboardSnapshot snapshot)
    {
        if (WebClipboard.IsTooLarge(snapshot))
            return ConversionResult.Passthrough;
        if (WebClipboard.ReadHtml(snapshot, out _) is { } html)
            return Convert(html);
        // HTML 없이 탭으로 나눈 글(메모장 등)
        return ClipText.GetPlainText(snapshot) is { } text && ClearTsvPlaceholders(text) is { } cleared
            ? new ConversionResult(Id, false, null, cleared, [])
            : ConversionResult.Passthrough;
    }

    public static ConversionResult Convert(string html)
    {
        if (WebHtmlCleaner.CleanForExcel(html) is not { } cleaned)
            return ConversionResult.Passthrough;
        // 일반 텍스트는 정리한 HTML로 C3과 같이 만든다: 표는 칸마다 탭, 그 밖은 줄마다 한 줄
        var plain = MarkdownEngine.Convert(WebHtmlCleaner.ToMarkdown(cleaned, keepLinks: false).Markdown, MarkdownMode.Strip).PlainText;
        return new ConversionResult(Id, false, cleaned, plain, []);
    }

    // 탭으로 나눈 표의 칸 중 "빈칸"뿐인 칸을 비운다. 고친 것이 없으면 null
    private static string? ClearTsvPlaceholders(string text)
    {
        if (!text.Contains('\t'))
            return null;
        var changed = false;
        var lines = text.Split('\n').Select(line => string.Join('\t', line.Split('\t').Select(field =>
        {
            if (field.Trim('\r', ' ') != WebHtmlCleaner.ExcelBlankPlaceholder)
                return field;
            changed = true;
            return field.EndsWith('\r') ? "\r" : "";
        })));
        var result = string.Join('\n', lines);
        return changed ? result : null;
    }
}
