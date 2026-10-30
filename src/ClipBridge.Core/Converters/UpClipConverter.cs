namespace ClipBridge.Core;

// 실제 변환기: 출처를 판별하고 [출처 → 대상] 매트릭스(PRD 6.2, #14)대로 모든 대상·모드의 결과를 미리 만든다.
// 아직 없는 변환기 칸은 통과로 둔다 (FR-16). 변환기 하나가 실패해도 그 칸만 통과한다
public sealed class UpClipConverter : IClipConverter
{
    private static readonly TargetApp[] Targets = [TargetApp.Hangul, TargetApp.Notion, TargetApp.Word];

    public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (source, content) = SourceDetector.Detect(snapshot);

        var results = new Dictionary<PreparedConversions.Key, ConversionResult>();
        foreach (var target in Targets)
        foreach (var mode in Enum.GetValues<MarkdownMode>())
        {
            ct.ThrowIfCancellationRequested();
            ConversionResult result;
            try
            {
                result = Convert(snapshot, source, target, mode);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                result = ConversionResult.Passthrough;
            }
            if (!result.IsPassthrough)
                results[new(target, mode)] = result;
        }

        // TODO(#20): options.AiVerification이면 의심 신호가 있는 결과를 AI로 검증
        return new PreparedConversions(snapshot.SequenceNumber, source, content, results);
    }

    // PRD 6.2 매트릭스. 없는 조합은 통과
    internal static ConversionResult Convert(ClipboardSnapshot snapshot, SourceKind source, TargetApp target, MarkdownMode mode) =>
        (source, target) switch
        {
            (SourceKind.Excel, TargetApp.Hangul) => ExcelToHangulConverter.Convert(snapshot),   // C1
            (SourceKind.Excel, TargetApp.Notion) => ExcelToNotionConverter.Convert(snapshot),   // C2
            // Excel → Word는 Office 기본 붙여넣기 (PRD 6.2)
            (SourceKind.Markdown, TargetApp.Hangul) => Markdown(snapshot, text => MarkdownToHangulConverter.Convert(text, mode)),   // C5
            (SourceKind.Markdown, TargetApp.Notion) => Markdown(snapshot, text => MarkdownToNotionConverter.Convert(text, mode)),   // C6
            (SourceKind.Web, TargetApp.Hangul) => WebToHangulConverter.Convert(snapshot),   // C3
            (SourceKind.Web, TargetApp.Notion) => WebToNotionConverter.Convert(snapshot),   // C4
            // 아직 없음: C7 인터넷 → Word(#10), C8 Markdown → Word(#12), TX 일반 텍스트(#6)
            _ => ConversionResult.Passthrough,
        };

    private static ConversionResult Markdown(ClipboardSnapshot snapshot, Func<string, ConversionResult> convert) =>
        ClipText.GetPlainText(snapshot) is { } text ? convert(text) : ConversionResult.Passthrough;
}
