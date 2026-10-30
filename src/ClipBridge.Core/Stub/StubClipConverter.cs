using System.Net;

namespace ClipBridge.Core;

// 앱 껍데기 연동 개발용 가짜 변환기. Helper는 UpClipConverter(#14)를 쓰고, 이것은 테스트에서만 쓴다.
// 일반 텍스트가 있으면 한글·Notion·Word용으로 "[ClipBridge] " 표시를 붙인 결과를 주고, 그 외는 통과한다.
public sealed class StubClipConverter : IClipConverter
{
    public const string Marker = "[ClipBridge] ";

    public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var results = new Dictionary<PreparedConversions.Key, ConversionResult>();
        var text = ClipText.GetPlainText(snapshot);
        if (text is not null)
        {
            var result = new ConversionResult(
                "STUB", false,
                $"<p>{WebUtility.HtmlEncode(Marker + text)}</p>",
                Marker + text,
                []);

            foreach (var target in new[] { TargetApp.Hangul, TargetApp.Notion, TargetApp.Word })
            foreach (var mode in Enum.GetValues<MarkdownMode>())
                results[new(target, mode)] = result;
        }

        var source = text is null ? SourceKind.Other : SourceKind.PlainText;
        return new PreparedConversions(snapshot.SequenceNumber, source, ContentKind.Text, results);
    }
}
