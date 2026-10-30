namespace ClipBridge.Core;

// 앱 껍데기가 복사 직후 만들어 넘기는 원본 스냅샷 (PRD 6.5). Core는 OS API를 호출하지 않는다.
public sealed record ClipboardSnapshot(
    // 포맷 이름 → 원본 바이트. 이름은 OS 이름 그대로 쓴다 (ClipFormats 참고). 문자열 변환은 Core가 한다
    IReadOnlyDictionary<string, byte[]> Formats,
    // 복사한 앱. Windows는 프로세스 이름("EXCEL.EXE"), macOS는 번들 ID("com.microsoft.Excel")
    string? SourceApp,
    // Windows GetClipboardSequenceNumber, macOS NSPasteboard.changeCount (FR-17 최신 복사 판단)
    long SequenceNumber);
