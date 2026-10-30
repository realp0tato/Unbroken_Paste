namespace ClipBridge.Core;

// 실제 변환기: 출처를 판별하고 [출처 → 대상] 매트릭스(PRD 6.2, #14)대로 모든 대상·모드의 결과를 미리 만든다.
// 변환기가 없는 칸은 통과로 둔다 (FR-16). 변환기 하나가 실패해도 그 칸만 통과한다.
// 모든 결과는 마지막에 줄간격 보정(PRD 6.4)의 공통 단계를 거친다
public sealed class UpClipConverter : IClipConverter
{
    private static readonly TargetApp[] Targets = [TargetApp.Hangul, TargetApp.Notion, TargetApp.Word, TargetApp.Excel];

    public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (source, content) = SourceDetector.Detect(snapshot);
        var windows = LineSpacing.IsWindows(snapshot);

        var results = new Dictionary<PreparedConversions.Key, ConversionResult>();
        foreach (var target in Targets)
        foreach (var mode in Enum.GetValues<MarkdownMode>())
        {
            ct.ThrowIfCancellationRequested();
            ConversionResult result;
            try
            {
                result = LineSpacing.Finish(Convert(snapshot, source, target, mode), windows);
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
            (SourceKind.Markdown, TargetApp.Word) => Markdown(snapshot, text => MarkdownToWordConverter.Convert(text, mode)),   // C8
            (SourceKind.Web, TargetApp.Hangul) => WebToHangulConverter.Convert(snapshot, mode),   // C3
            (SourceKind.Web, TargetApp.Notion) => WebToNotionConverter.Convert(snapshot, mode),   // C4
            (SourceKind.Web, TargetApp.Word) => WebToWordConverter.Convert(snapshot, mode),   // C7
            (SourceKind.PlainText, TargetApp.Hangul or TargetApp.Notion or TargetApp.Word) => PlainTextConverter.Convert(snapshot, target),   // TX
            (SourceKind.Web, TargetApp.Excel) => WebToExcelConverter.Convert(snapshot),   // C9 (P1, #25). 그 밖의 → Excel은 Excel 기본 붙여넣기
            _ => ConversionResult.Passthrough,
        };

    private static ConversionResult Markdown(ClipboardSnapshot snapshot, Func<string, ConversionResult> convert) =>
        ClipText.GetPlainText(snapshot) is { } text ? convert(text) : ConversionResult.Passthrough;
}
