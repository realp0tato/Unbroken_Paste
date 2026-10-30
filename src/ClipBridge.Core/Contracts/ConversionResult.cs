namespace ClipBridge.Core;

// 의심 신호 S1–S9 (PRD 6.6.2, #20)
public sealed record SuspicionSignal(string Id, int Score, string Detail);

public sealed record ConversionResult(
    // "C1"…"C8", "TX", "PASS"
    string ConverterId,
    // true면 앱 껍데기가 원본 스냅샷을 그대로 기록한다 (PRD 6.5)
    bool IsPassthrough,
    // 순수 HTML 조각. CF_HTML 헤더는 Windows 앱이 CfHtml.Wrap으로 붙인다
    string? Html,
    string? PlainText,
    IReadOnlyList<SuspicionSignal> Signals)
{
    public static ConversionResult Passthrough { get; } = new("PASS", true, null, null, []);
}
