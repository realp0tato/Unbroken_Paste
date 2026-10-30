using ClipBridge.Core.Markdown;

namespace ClipBridge.Core;

// C6 Markdown 글 → Notion (PRD 6.3.6, #19)
public static class MarkdownToNotionConverter
{
    public const string Id = "C6";

    // 서식 적용: 웹 HTML(h1–h3·목록·표·코드) → Notion 블록 (기준선: Claude HTML이 Notion에서 정상)
    // 기호 제거: 일반 텍스트만. Notion은 일반 텍스트의 Markdown을 스스로 서식으로 바꾸므로 기호를 반드시 뺀다
    public static ConversionResult Convert(string markdown, MarkdownMode mode)
    {
        var output = MarkdownEngine.Convert(markdown, mode);
        return new ConversionResult(Id, false, output.Html, output.PlainText, output.Signals);
    }
}
