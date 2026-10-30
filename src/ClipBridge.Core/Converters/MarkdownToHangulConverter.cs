using ClipBridge.Core.Markdown;

namespace ClipBridge.Core;

// C5 Markdown 글 → 한글 (PRD 6.3.5, #18)
public static class MarkdownToHangulConverter
{
    public const string Id = "C5";

    // 서식 적용: 한글용 HTML(제목은 크기·굵기, 목록·표·코드는 여백·테두리 직접 지정) + 기호를 뺀 일반 텍스트
    // 기호 제거: 기호를 뺀 글자를 서식 없는 문단 HTML로도 준다. 일반 텍스트만 주면 한글이 커서 자리의
    // 글자 모양(배경색 등)을 이어받아 붙이기 때문이다. 굵게·크기는 넣지 않아 보이는 결과는 평문과 같다
    public static ConversionResult Convert(string markdown, MarkdownMode mode)
    {
        var document = MarkdownEngine.Parse(markdown, out _);
        var stripped = MarkdownEngine.Convert(markdown, MarkdownMode.Strip);
        var html = mode == MarkdownMode.Format ? DocHtmlRenderer.Render(document) : PlainTextConverter.TextHtml(stripped.PlainText);
        return new ConversionResult(Id, false, html, stripped.PlainText, stripped.Signals);
    }
}
