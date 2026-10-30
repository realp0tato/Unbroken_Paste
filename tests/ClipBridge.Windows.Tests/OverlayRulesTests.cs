using System.Drawing;
using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

// macOS OverlayLayoutTests와 같은 경우를 Windows 좌표(y가 아래로)로 확인한다
public class OverlayGeometryTests
{
    private static readonly Rectangle Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void BadgeSitsRightBelowThePointer() =>
        Assert.Equal(new Rectangle(116, 216, 30, 30), OverlayGeometry.BadgeFrame(new Point(100, 200), Screen));

    [Fact]
    public void FlipsLeftAtTheRightEdge() =>
        Assert.Equal(new Rectangle(1864, 216, 30, 30), OverlayGeometry.BadgeFrame(new Point(1910, 200), Screen));

    [Fact]
    public void FlipsUpAtTheBottomEdge() =>
        Assert.Equal(new Rectangle(116, 1024, 30, 30), OverlayGeometry.BadgeFrame(new Point(100, 1070), Screen));

    // 두 번째 모니터처럼 원점이 0이 아닌 화면에서도 화면 밖으로 나가지 않는다
    [Fact]
    public void StaysOnASecondScreen()
    {
        var second = new Rectangle(-1280, 0, 1280, 1024);

        var frame = OverlayGeometry.BadgeFrame(new Point(-5, 1020), second);

        Assert.True(second.Contains(frame), frame.ToString());
    }

    // 배율 150%: 지름·거리도 함께 커진다
    [Fact]
    public void ScaledSizes() =>
        Assert.Equal(new Rectangle(124, 224, 45, 45), OverlayGeometry.BadgeFrame(new Point(100, 200), Screen, diameter: 45, offset: 24));
}

public class PasteBadgeTests
{
    [Fact]
    public void ConvertedTarget_ShowsAppIconWithAccentBorder() =>
        Assert.Equal(new PasteBadge(BadgeIcon.ForegroundApp, "H", Passthrough: false), PasteBadge.For(TargetApp.Hangul, "Hwp.exe", converted: true));

    [Fact]
    public void PlainPaste_ShowsAppIconWithQuietBorder() =>
        Assert.Equal(new PasteBadge(BadgeIcon.ForegroundApp, "N", Passthrough: true), PasteBadge.For(TargetApp.Other, "notepad.exe", converted: false));

    // 변환 대상이어도 결과를 쓰지 않았으면(Excel → Word 등) 통과 테두리
    [Fact]
    public void TargetWithoutResult_IsPassthrough() =>
        Assert.True(PasteBadge.For(TargetApp.Word, "WINWORD.EXE", converted: false).Passthrough);

    // 브라우저의 Notion 웹은 브라우저가 아니라 Notion 아이콘
    [Fact]
    public void NotionWeb_ShowsNotionIcon()
    {
        Assert.Equal(new PasteBadge(BadgeIcon.NotionApp, "N", Passthrough: false), PasteBadge.For(TargetApp.Notion, "chrome.exe", converted: true));
        Assert.Equal(BadgeIcon.ForegroundApp, PasteBadge.For(TargetApp.Notion, "Notion.exe", converted: true).Icon);
    }

    [Fact]
    public void UnknownApp_ShowsQuestionMark() =>
        Assert.Equal(new PasteBadge(BadgeIcon.None, "?", Passthrough: true), PasteBadge.For(TargetApp.Other, null, converted: false));
}
