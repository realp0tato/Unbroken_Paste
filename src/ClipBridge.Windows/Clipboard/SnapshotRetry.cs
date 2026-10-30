using ClipBridge.Core;

namespace ClipBridge.Windows;

// 복사 직후 스냅샷을 다시 읽어야 하는지 정한다.
// Excel은 HTML·텍스트를 지연 렌더링으로 넣고, 복사 한 번에 클립보드를 여러 번 연달아 바꾼다.
// 다시 복사하면 먼저 클립보드를 비우고, Biff12·XML Spreadsheet를 올린 뒤에 HTML·텍스트를 올린다.
// 그 중간을 읽으면 C1·C2가 표를 읽지 못해 통과 → 한글이 원본의 그림·OLE 개체로 붙인다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class SnapshotRetry
{
    // 변환에 꼭 필요한 포맷. 이것이 목록에 있는데 데이터를 받지 못했으면 다시 읽는다
    private static readonly string[] Essential = [ClipFormats.WinHtml, ClipFormats.WinUnicodeText];

    // Excel만 넣는 포맷. 이것이 있으면 HTML·텍스트도 곧 올라오므로, 아직 목록에 없으면 기다린다
    private static readonly string[] ExcelNative = [ClipFormats.WinExcelBiff12, ClipFormats.WinExcelXml];

    public const int MaxAttempts = 5;
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(100);

    // (포맷 이름, 데이터를 읽었는지, 읽지 못한 이유). 클립보드에 있는 모든 포맷을 넘긴다
    public static bool NeedsRetry(IReadOnlyCollection<(string Name, bool HasData, string? SkipReason)> formats) =>
        Missing(formats).Length > 0;

    // 로그용: 꼭 필요한데 읽지 못한 포맷과 이유. 다시 읽을 필요가 없으면 빈 문자열
    public static string Missing(IReadOnlyCollection<(string Name, bool HasData, string? SkipReason)> formats)
    {
        // 원본 앱이 클립보드를 비우고 아직 채우지 않았다
        if (formats.Count == 0)
            return "(포맷 없음)";

        var missing = formats
            .Where(format => Essential.Contains(format.Name) && !format.HasData && format.SkipReason != "읽지 않음")
            .Select(format => $"{format.Name}({format.SkipReason})")
            .ToList();
        if (formats.Any(format => ExcelNative.Contains(format.Name)))
            missing.AddRange(Essential.Where(name => formats.All(format => format.Name != name)).Select(name => $"{name}(목록에 없음)"));
        return string.Join(",", missing);
    }
}
