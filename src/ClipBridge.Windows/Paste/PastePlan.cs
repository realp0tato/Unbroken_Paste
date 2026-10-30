using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows;

internal enum PasteAction { Leave, WriteResult, RestoreOriginal }

// UP이 마지막으로 클립보드에 쓴 것. Target이 null이면 원본을 되돌려 쓴 것이다
internal sealed record LastWrite(long CopySequence, TargetApp? Target);

// 붙여넣기 순간 클립보드에 무엇을 쓸지 정한다 (FR-07, FR-16, PRD 6.5). macOS PastePlan.decide와 같은 규칙이다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal sealed record PastePlan(PasteAction Action, IReadOnlyDictionary<string, byte[]> Payload)
{
    private static readonly IReadOnlyDictionary<string, byte[]> Empty = new Dictionary<string, byte[]>();

    // 아무것도 쓰지 않는다. 키는 그대로 가고 지금 클립보드 내용이 붙는다
    public static PastePlan Leave { get; } = new(PasteAction.Leave, Empty);

    // 원본 스냅샷을 되돌려 쓴다 (앞서 다른 대상용 결과를 쓴 뒤 통과 대상에 붙일 때)
    public static PastePlan RestoreOriginal { get; } = new(PasteAction.RestoreOriginal, Empty);

    // copySequence: 현재 복사의 시퀀스 번호, 쓸 수 있는 복사가 없으면 null
    // clipboardSequence: 지금 클립보드의 시퀀스 번호
    // ownsClipboard: 지금 클립보드를 마지막으로 쓴 것이 UP인지 (클립보드 소유 창)
    public static PastePlan Decide(
        long? copySequence, long clipboardSequence, bool ownsClipboard, LastWrite? lastWrite, ConversionResult result, TargetApp target)
    {
        if (copySequence is not { } sequence)
            return Leave;

        var holdsOurWrite = ownsClipboard && lastWrite?.CopySequence == sequence;
        var holdsOriginal = clipboardSequence == sequence;
        // 스냅샷 뒤에 다른 앱이 클립보드를 바꿨다(아직 감지하지 못한 새 복사): 절대 덮어쓰지 않는다
        if (!holdsOurWrite && !holdsOriginal)
            return Leave;

        var payload = PayloadOf(result);
        if (payload.Count > 0)
        {
            // 이미 이 대상용 결과가 들어 있다
            if (holdsOurWrite && lastWrite!.Target == target)
                return Leave;
            return new PastePlan(PasteAction.WriteResult, payload);
        }

        // PRD 6.5: 다른 대상용 결과가 남아 있으면 원본을 되돌려 놓는다. 원본이 그대로면 건드리지 않는다
        return holdsOurWrite && lastWrite!.Target is not null ? RestoreOriginal : Leave;
    }

    // Windows 포맷으로 바꾼 변환 결과. HTML은 CF_HTML 헤더를 붙이고(Core 계약), 텍스트는 UTF-16LE + \0
    public static IReadOnlyDictionary<string, byte[]> PayloadOf(ConversionResult result)
    {
        if (result.IsPassthrough)
            return Empty;

        // 앱은 앞에 있는 포맷을 먼저 고르므로 HTML을 먼저 넣는다
        var payload = new Dictionary<string, byte[]>();
        if (result.Html is not null)
            payload[ClipFormats.WinHtml] = CfHtml.Wrap(result.Html);
        if (result.PlainText is not null)
            payload[ClipFormats.WinUnicodeText] = TextBytes(result.PlainText);
        return payload;
    }

    // CF_UNICODETEXT 바이트: UTF-16LE + \0
    public static byte[] TextBytes(string text) => Encoding.Unicode.GetBytes(text + "\0");
}
