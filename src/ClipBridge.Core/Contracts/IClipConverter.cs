namespace ClipBridge.Core;

public interface IClipConverter
{
    // 복사 직후 백그라운드 스레드에서 호출. 모든 대상·Markdown 모드의 결과 A를 준비해 반환하고,
    // options.AiVerification이면 AI 검증(B)을 백그라운드로 시작한다.
    // ct는 준비 단계만 취소한다. 준비가 끝난 뒤의 AI 검증은 PreparedConversions.Dispose로 취소한다.
    PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct);
}
