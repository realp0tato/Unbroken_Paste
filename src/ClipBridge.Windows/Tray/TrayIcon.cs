namespace ClipBridge.Windows;

// UP 앱 아이콘(Resources/app.ico, 16–96px)에서 화면 배율에 맞는 크기를 고른다. 슬라임 프레임을 읽지 못하면 이 아이콘을 쓴다
internal static class TrayIcon
{
    public static Icon Create(Size size)
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("ClipBridge.Windows.app.ico")
            ?? throw new InvalidOperationException("app.ico 리소스가 없습니다.");
        return new Icon(stream, size);
    }
}
