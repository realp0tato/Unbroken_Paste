namespace ClipBridge.Core;

// 메뉴 앱 옵션 (PRD 4.2). Get마다 넘기므로 다시 복사하지 않아도 바뀐 옵션이 반영된다
public sealed record ConversionOptions(MarkdownMode MarkdownMode, bool AiVerification)
{
    public static ConversionOptions Default { get; } = new(MarkdownMode.Format, AiVerification: true);
}
