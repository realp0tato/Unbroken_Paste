using System.Runtime.InteropServices;

namespace ClipBridge.Windows;

// 동작 기록. 터미널에서 실행했을 때(dotnet run 등)만 그 터미널에 한 줄씩 출력한다.
// macOS 앱이 시스템 로그(log stream)에 남기는 것과 같은 용도·형식이고, 복사한 본문은 남기지 않는다.
// 파일로 저장하지 않는다 (디스크에는 옵션만 저장)
internal static class DevLog
{
    private const int AttachParentProcess = -1;

    // 트레이 앱은 콘솔 창이 없으므로 실행한 터미널의 콘솔에 붙는다. 탐색기에서 실행하면 붙을 콘솔이 없어 출력하지 않는다
    private static readonly bool HasConsole = AttachConsole(AttachParentProcess);
    private static readonly Lock WriteLock = new();

    public static void Info(string category, string message)
    {
        if (!HasConsole)
            return;
        lock (WriteLock)
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{category}] {message}");
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
