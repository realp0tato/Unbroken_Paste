namespace ClipBridge.Core;

// C8 Markdown 글 → Word (PRD 6.3.8, #12).
// Word도 한글과 같은 방식(제목은 글자 크기·굵기, Word 제목 스타일 미사용)이라 C5 결과를 그대로 쓴다
public static class MarkdownToWordConverter
{
    public const string Id = "C8";

    public static ConversionResult Convert(string markdown, MarkdownMode mode) =>
        MarkdownToHangulConverter.Convert(markdown, mode) with { ConverterId = Id };
}
