namespace ClipBridge.Windows;

// 비밀번호 관리자 등이 "기록하지 말 것"이라고 표시한 복사인지 본다 (#78, PRD 4.2.2·6.6).
// macOS ClipboardMonitor.concealedTypes와 같은 역할이다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class ConcealedCopy
{
    // 포맷이 있기만 하면 숨김 (Windows 표준 표시와 예전 클립보드 뷰어용 표시)
    public const string ExcludeFromMonitor = "ExcludeClipboardContentFromMonitorProcessing";
    public const string ViewerIgnore = "Clipboard Viewer Ignore";

    // DWORD 값이 0이면 숨김 (Windows 클립보드 기록·클라우드 동기화에서 빼 달라는 표시)
    public const string CanIncludeInHistory = "CanIncludeInClipboardHistory";
    public const string CanUploadToCloud = "CanUploadToCloudClipboard";

    // 판별에 값이 필요한 포맷. 나머지 포맷은 이름만 본다
    public static bool NeedsData(string format) => format is CanIncludeInHistory or CanUploadToCloud;

    public static bool IsConcealed(IEnumerable<(string Name, byte[]? Data)> formats) =>
        formats.Any(format => format.Name is ExcludeFromMonitor or ViewerIgnore
            || (NeedsData(format.Name) && format.Data is { Length: >= 4 } data && BitConverter.ToUInt32(data, 0) == 0));
}
