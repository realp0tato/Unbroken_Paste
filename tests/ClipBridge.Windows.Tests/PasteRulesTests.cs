using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

public class PasteTargetsTests
{
    [Theory]
    [InlineData("Hwp.exe", null, TargetApp.Hangul)]
    [InlineData("HWP.EXE", null, TargetApp.Hangul)]
    [InlineData("Notion.exe", null, TargetApp.Notion)]
    [InlineData("WINWORD.EXE", null, TargetApp.Word)]
    [InlineData("winword.exe", null, TargetApp.Word)]
    [InlineData("notepad.exe", null, TargetApp.Other)]
    [InlineData("EXCEL.EXE", null, TargetApp.Other)]
    [InlineData(null, null, TargetApp.Other)]
    public void DesktopApps(string? process, string? title, TargetApp expected) =>
        Assert.Equal(expected, PasteTargets.Resolve(process, title));

    // 브라우저는 창 제목으로 Notion 웹·한컴독스 웹 한글을 찾고, 모르면 통과 (macOS 제목 규칙과 같음)
    [Theory]
    [InlineData("chrome.exe", "회의록 | Notion - Chrome", TargetApp.Notion)]
    [InlineData("msedge.exe", "notion - 개인 - Microsoft Edge", TargetApp.Notion)]
    [InlineData("whale.exe", "보고서.hwp - 한컴독스 - Whale", TargetApp.Hangul)]
    [InlineData("firefox.exe", "report.hwp - Hancom Docs — Mozilla Firefox", TargetApp.Hangul)]
    [InlineData("brave.exe", "보고서.hwp - 한컴독스 - Brave", TargetApp.Hangul)]
    [InlineData("Arc.exe", "기획안 | Notion", TargetApp.Notion)]
    [InlineData("opera.exe", "Hancom Docs - Opera", TargetApp.Hangul)]
    [InlineData("vivaldi.exe", "한컴독스 - Vivaldi", TargetApp.Hangul)]
    // 한컴독스 웹 편집기 탭 제목은 문서 이름뿐이다
    [InlineData("chrome.exe", "문서 이름.hwpx - Chrome", TargetApp.Hangul)]
    [InlineData("msedge.exe", "보고서.HWP - 개인 - Microsoft Edge", TargetApp.Hangul)]
    [InlineData("chrome.exe", "Notion 정리.hwpx - Chrome", TargetApp.Hangul)]
    [InlineData("chrome.exe", "설정.hwpml - Chrome", TargetApp.Other)]
    [InlineData("notepad.exe", "보고서.hwpx - 메모장", TargetApp.Other)]
    [InlineData("chrome.exe", "네이버 - Chrome", TargetApp.Other)]
    [InlineData("chrome.exe", null, TargetApp.Other)]
    // 브라우저가 아니면 제목에 Notion이 있어도 통과
    [InlineData("notepad.exe", "Notion 메모.txt - 메모장", TargetApp.Other)]
    public void Browsers(string? process, string? title, TargetApp expected) =>
        Assert.Equal(expected, PasteTargets.Resolve(process, title));

    [Theory]
    [InlineData("chrome.exe", true)]
    [InlineData("MSEDGE.EXE", true)]
    [InlineData("Hwp.exe", false)]
    [InlineData(null, false)]
    public void OnlyBrowsersNeedTitle(string? process, bool expected) =>
        Assert.Equal(expected, PasteTargets.IsBrowser(process));
}

public class PasteKeyDetectorTests
{
    private const int VK_V = PasteKeyDetector.VK_V, VK_INSERT = PasteKeyDetector.VK_INSERT, VK_C = 0x43;

    [Theory]
    [InlineData(VK_V, true, false, false, false, "Ctrl+V")]
    [InlineData(VK_INSERT, false, true, false, false, "Shift+Insert")]
    [InlineData(VK_V, false, false, false, false, null)]        // V만
    [InlineData(VK_V, true, true, false, false, null)]          // Ctrl+Shift+V
    [InlineData(VK_V, true, false, true, false, null)]          // Ctrl+Alt+V (AltGr)
    [InlineData(VK_V, true, false, false, true, null)]          // Win 조합
    [InlineData(VK_INSERT, true, false, false, false, null)]    // Ctrl+Insert는 복사
    [InlineData(VK_C, true, false, false, false, null)]         // Ctrl+C
    public void Combinations(int vk, bool ctrl, bool shift, bool alt, bool win, string? expected) =>
        Assert.Equal(expected, new PasteKeyDetector().OnKey(vk, down: true, ctrl, shift, alt, win));

    [Fact]
    public void HeldKeyRepeat_CountsOnce()
    {
        var keys = new PasteKeyDetector();

        Assert.Equal("Ctrl+V", keys.OnKey(VK_V, true, true, false, false, false));
        Assert.Null(keys.OnKey(VK_V, true, true, false, false, false));
        Assert.Null(keys.OnKey(VK_V, false, true, false, false, false));
        Assert.Equal("Ctrl+V", keys.OnKey(VK_V, true, true, false, false, false));
    }

    // 훅이 떼어진 사이 키 떼기를 놓쳐도 다시 건 뒤 첫 붙여넣기를 놓치지 않는다
    [Fact]
    public void Reset_ForgetsHeldKeys()
    {
        var keys = new PasteKeyDetector();
        keys.OnKey(VK_V, true, true, false, false, false);

        keys.Reset();

        Assert.Equal("Ctrl+V", keys.OnKey(VK_V, true, true, false, false, false));
    }
}
