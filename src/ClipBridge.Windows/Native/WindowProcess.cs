using System.Runtime.InteropServices;
using System.Text;

namespace ClipBridge.Windows;

// 창을 만든 프로세스의 실행 파일 경로와 창 제목. 복사한 앱(클립보드 소유 창)과 붙여넣는 앱(앞 창) 판별에 쓴다
internal static class WindowProcess
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    // 알 수 없으면 null (권한이 더 높은 프로세스 등)
    public static string? ImagePath(nint window)
    {
        if (window == 0 || GetWindowThreadProcessId(window, out var processId) == 0)
            return null;

        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
            return null;

        try
        {
            var buffer = new StringBuilder(1024);
            var length = buffer.Capacity;
            return QueryFullProcessImageName(process, 0, buffer, ref length) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    // 창 제목. 문서 이름이 들어 있을 수 있으므로 로그에 남기지 않는다
    public static string? Title(nint window)
    {
        var buffer = new StringBuilder(512);
        return GetWindowText(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);
}
