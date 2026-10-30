namespace ClipBridge.Windows;

internal static class Program
{
    // 사용자 세션마다 하나만 실행한다 (FR-01)
    private const string SingleInstanceName = @"Local\UnbrokenPaste.Windows";

    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(initiallyOwned: true, SingleInstanceName, out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show(
                "UP이 이미 실행 중입니다.\n작업 표시줄 알림 영역의 UP 아이콘을 확인하세요.",
                "UP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
