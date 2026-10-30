using System.Text.RegularExpressions;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 붙여넣기 대상 판별 (FR-05, PRD 4.5·6.2). 모르는 앱은 Other이고 통과한다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static partial class PasteTargets
{
    private static readonly Dictionary<string, TargetApp> Apps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hwp.exe"] = TargetApp.Hangul,
        ["Notion.exe"] = TargetApp.Notion,
        ["WINWORD.EXE"] = TargetApp.Word,
    };

    // 웹 대상(Notion 웹, 한컴독스 웹 한글)을 창 제목으로 찾는 브라우저. 어느 브라우저로 열어도 같게 인식하도록
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

    // macOS TargetResolver.webTarget의 제목 규칙에 HWP 파일 이름을 더했다. 주소는 붙여넣기 순간에 읽기엔 느려서 쓰지 않는다.
    // 한컴독스 웹 편집기(webhwp.hancomdocs.com)의 탭 제목은 "문서 이름.hwpx"뿐이라 파일 이름으로 찾는다.
    // 문서 이름에 Notion이 들어갈 수 있으므로 Notion보다 먼저 본다
    private static TargetApp WebTarget(string? title)
    {
        if (title is null)
            return TargetApp.Other;
        if (HwpFileName().IsMatch(title))
            return TargetApp.Hangul;
        if (title.Contains("Notion", StringComparison.OrdinalIgnoreCase))
            return TargetApp.Notion;
        if (title.Contains("한컴독스") || title.Contains("Hancom Docs", StringComparison.OrdinalIgnoreCase))
            return TargetApp.Hangul;
        return TargetApp.Other;
    }

    // "보고서.hwp", "문서 이름.hwpx - Chrome". ".hwpml"처럼 뒤에 글자가 더 붙은 것은 아니다
    [GeneratedRegex(@"\.hwpx?(?![\p{L}\p{N}])", RegexOptions.IgnoreCase)]
    private static partial Regex HwpFileName();
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
