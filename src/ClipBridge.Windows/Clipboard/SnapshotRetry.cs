using ClipBridge.Core;

namespace ClipBridge.Windows;

// 복사 직후 스냅샷을 다시 읽어야 하는지 정한다.
// Excel은 HTML·텍스트를 지연 렌더링으로 넣고, 복사 한 번에 클립보드를 여러 번 연달아 바꾼다.
// 첫 알림 직후 읽으면 Excel이 아직 복사 중이라 HTML·텍스트를 만들어 주지 못해 "데이터 없음"이 되고,
// 그러면 C1·C2가 표를 읽지 못해 통과 → 한글이 원본의 그림·OLE 개체로 붙인다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class SnapshotRetry
{
    // 변환에 꼭 필요한 포맷. 이것이 목록에 있는데 데이터를 받지 못했으면 다시 읽는다
    private static readonly string[] Essential = [ClipFormats.WinHtml, ClipFormats.WinUnicodeText];

    public const int MaxAttempts = 5;
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(100);

    // (포맷 이름, 데이터를 읽었는지, 읽지 못한 이유)
    public static bool NeedsRetry(IEnumerable<(string Name, bool HasData, string? SkipReason)> formats) =>
        formats.Any(format => Essential.Contains(format.Name) && !format.HasData && format.SkipReason != "읽지 않음");

    // 로그용: 꼭 필요한데 읽지 못한 포맷과 이유
    public static string Missing(IEnumerable<(string Name, bool HasData, string? SkipReason)> formats) =>
        string.Join(",", formats
            .Where(format => Essential.Contains(format.Name) && !format.HasData)
            .Select(format => $"{format.Name}({format.SkipReason})"));
}
