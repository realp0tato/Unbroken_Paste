using ClipBridge.Core.Markdown;

namespace ClipBridge.Core;

// C5 Markdown 글 → 한글 (PRD 6.3.5, #18)
public static class MarkdownToHangulConverter
{
    public const string Id = "C5";

    // 서식 적용: 한글용 HTML(제목은 크기·굵기, 목록·표·코드는 여백·테두리 직접 지정) + 기호를 뺀 일반 텍스트
    // 기호 제거: 일반 텍스트만
    public static ConversionResult Convert(string markdown, MarkdownMode mode)
    {
        var document = MarkdownEngine.Parse(markdown, out _);
        var stripped = MarkdownEngine.Convert(markdown, MarkdownMode.Strip);
        var html = mode == MarkdownMode.Format ? DocHtmlRenderer.Render(document) : null;
        return new ConversionResult(Id, false, html, stripped.PlainText, stripped.Signals);
    }
}
