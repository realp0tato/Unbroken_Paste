using System.Drawing;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 포인터 옆 배지의 위치 (PRD 4.3). 화면 좌표(왼쪽 위가 원점, y는 아래로). macOS OverlayGeometry와 같은 규칙이다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class OverlayGeometry
{
    // 96 DPI 기준. 모니터 배율에 맞춰 키운다
    public const int Diameter = 30;
    public const int Offset = 16;

    // 포인터 오른쪽 아래. 화면 가장자리에 걸리면 반대쪽으로 뒤집고, 화면 밖으로 나가지 않게 한다
    public static Rectangle BadgeFrame(Point pointer, Rectangle screen, int diameter = Diameter, int offset = Offset)
    {
        var x = pointer.X + offset;
        var y = pointer.Y + offset;
        if (x + diameter > screen.Right)
            x = pointer.X - offset - diameter;
        if (y + diameter > screen.Bottom)
            y = pointer.Y - offset - diameter;
        x = Math.Clamp(x, screen.Left, screen.Right - diameter);
        y = Math.Clamp(y, screen.Top, screen.Bottom - diameter);
        return new Rectangle(x, y, diameter, diameter);
    }
}

// 배지 안에 그릴 아이콘
internal enum BadgeIcon
{
    // 아이콘 없이 글자만
    None,
    // 붙여넣는 앱(앞 창 프로세스)의 아이콘
    ForegroundApp,
    // 설치된 Notion 앱의 아이콘 (브라우저의 Notion 웹)
    NotionApp,
}

// 붙여넣기 때 보여 줄 배지 (PRD 4.3). 대상 서비스의 아이콘만 보여 주고 복사한 글이나 창 제목은 보여 주지 않는다.
// macOS OverlayBadge.forPaste와 같은 규칙이다
internal sealed record PasteBadge(BadgeIcon Icon, string Letter, bool Passthrough)
{
    // converted: 이 대상용 변환 결과가 실제로 클립보드에 있는지. 테두리 색을 정한다
    public static PasteBadge For(TargetApp target, string? process, bool converted)
    {
        if (process is null)
            return new PasteBadge(BadgeIcon.None, "?", Passthrough: true);

        var passthrough = !converted;
        // 브라우저 안의 Notion 웹은 브라우저가 아니라 Notion 아이콘
        if (target == TargetApp.Notion && !process.Equals("Notion.exe", StringComparison.OrdinalIgnoreCase))
            return new PasteBadge(BadgeIcon.NotionApp, "N", passthrough);
        return new PasteBadge(BadgeIcon.ForegroundApp, LetterOf(process), passthrough);
    }

    // 아이콘을 찾지 못했을 때 보여 줄 첫 글자 ("WINWORD.EXE" → "W")
    private static string LetterOf(string process)
    {
        var name = Path.GetFileNameWithoutExtension(process).Trim();
        return name.Length == 0 ? "?" : char.ToUpperInvariant(name[0]).ToString();
    }
}
