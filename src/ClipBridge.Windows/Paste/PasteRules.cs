using ClipBridge.Core;

namespace ClipBridge.Windows;

// 붙여넣기 대상 판별 (FR-05, PRD 4.5·6.2). 모르는 앱은 Other이고 통과한다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class PasteTargets
{
    private static readonly Dictionary<string, TargetApp> Apps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hwp.exe"] = TargetApp.Hangul,
        ["Notion.exe"] = TargetApp.Notion,
        ["WINWORD.EXE"] = TargetApp.Word,
        ["EXCEL.EXE"] = TargetApp.Excel,   // P1 대상 확장 (#25)
    };

    // Notion 웹을 창 제목으로 찾는 브라우저. 어느 브라우저로 열어도 같게 인식하도록
    // PRD 6.1의 4종에 macOS 목록의 Arc와 Windows에서 많이 쓰는 Chromium 계열 브라우저를 더했다
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe", "whale.exe",
        "arc.exe", "brave.exe", "opera.exe", "vivaldi.exe",
    };

    // 창 제목은 브라우저일 때만 필요하다
    public static bool IsBrowser(string? process) => process is not null && Browsers.Contains(process);

    public static TargetApp Resolve(string? process, string? windowTitle)
    {
        if (process is null)
            return TargetApp.Other;
        if (Apps.TryGetValue(process, out var target))
            return target;
        return IsBrowser(process) ? WebTarget(windowTitle) : TargetApp.Other;
    }

    // macOS TargetResolver.webTarget의 Notion 제목 규칙과 같다. 주소는 붙여넣기 순간에 읽기엔 느려서 쓰지 않는다.
    // 한컴독스 웹 한글은 macOS만 지원한다 (Windows는 데스크톱 한글 Hwp.exe로 충분)
    private static TargetApp WebTarget(string? title) =>
        title is not null && title.Contains("Notion", StringComparison.OrdinalIgnoreCase) ? TargetApp.Notion : TargetApp.Other;
}

// 키 입력에서 붙여넣기(Ctrl+V, Shift+Insert)를 찾는다 (FR-05)
internal sealed class PasteKeyDetector
{
    public const int VK_V = 0x56;
    public const int VK_INSERT = 0x2D;

    private readonly HashSet<int> _held = [];

    // 키를 누르거나 뗄 때마다 호출한다. 붙여넣기 키를 처음 누른 순간이면 키 이름, 아니면 null
    public string? OnKey(int vk, bool down, bool ctrl, bool shift, bool alt, bool win)
    {
        if (vk is not (VK_V or VK_INSERT))
            return null;
        if (!down)
        {
            _held.Remove(vk);
            return null;
        }
        // 길게 눌러 반복되는 입력은 처음 한 번만 센다 (macOS와 같음)
        if (!_held.Add(vk))
            return null;

        // Ctrl+Shift+V(서식 없이 붙여넣기 등)와 Ctrl+Alt(AltGr) 조합은 붙여넣기로 보지 않는다
        if (vk == VK_V && ctrl && !shift && !alt && !win)
            return "Ctrl+V";
        if (vk == VK_INSERT && shift && !ctrl && !alt && !win)
            return "Shift+Insert";
        return null;
    }

    // 키보드 훅을 다시 걸 때 놓친 키 떼기가 있을 수 있으므로 비운다
    public void Reset() => _held.Clear();
}
