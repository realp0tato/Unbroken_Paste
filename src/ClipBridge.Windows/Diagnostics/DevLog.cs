using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipBridge.Windows;

// 동작 기록. 터미널에서 실행했을 때(dotnet run 등)만 그 터미널에 한 줄씩 출력한다.
// macOS 앱이 시스템 로그(log stream)에 남기는 것과 같은 용도·형식이고, 복사한 본문은 남기지 않는다.
// 기본으로는 파일로 저장하지 않는다 (디스크에는 옵션만 저장). 검증할 때만 --log <파일>로 켠다 (#24)
internal static class DevLog
{
    private const int AttachParentProcess = -1;

    // 트레이 앱은 콘솔 창이 없으므로 실행한 터미널의 콘솔에 붙는다. 탐색기에서 실행하면 붙을 콘솔이 없어 출력하지 않는다
    private static readonly bool HasConsole = AttachConsole(AttachParentProcess);

    // 키보드 훅 안에서도 부르므로 콘솔·파일 쓰기는 백그라운드 스레드에서 한다
    private static readonly BlockingCollection<string> Lines = [];
    private static volatile StreamWriter? _file;
    private static bool _started;

    static DevLog()
    {
        if (HasConsole)
            Start();
    }

    // 같은 줄을 파일 끝에 덧붙인다. 앱 시작 때 한 번 부른다. 열지 못하면 null, 열었으면 전체 경로
    public static string? ToFile(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            _file = new StreamWriter(fullPath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
            Start();
            return fullPath;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static void Info(string category, string message)
    {
        if (HasConsole || _file is not null)
            Lines.Add($"{DateTime.Now:HH:mm:ss.fff} [{category}] {message}");
    }

    // 종료 직전에 남은 줄을 내보낸다
    public static void Flush()
    {
        SpinWait.SpinUntil(() => Lines.Count == 0, TimeSpan.FromMilliseconds(500));
        _file?.Flush();
    }

    private static void Start()
    {
        if (_started)
            return;
        _started = true;
        new Thread(() =>
        {
            foreach (var line in Lines.GetConsumingEnumerable())
            {
                if (HasConsole)
                    Console.WriteLine(line);
                _file?.WriteLine(line);
            }
        }) { IsBackground = true }.Start();
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
}
