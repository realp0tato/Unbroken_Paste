using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace ClipBridge.Windows;

// 브라우저 창의 주소(도메인)를 미리 읽어 둔다 (#79). macOS는 붙여넣기 때 주소를 30ms 안에서 읽지만,
// Windows의 UI Automation은 그보다 느려 붙여넣기 순간(키보드 훅 안, NFR-03)에 쓸 수 없다.
// 그래서 브라우저 창이 앞으로 오거나 제목이 바뀔 때(탭 전환·페이지 이동) 백그라운드에서 읽어 창·제목별로 저장한다.
// 이 객체는 UI 스레드에서 만든다 (WinEvent 알림이 그 스레드의 메시지 루프로 온다)
internal sealed class BrowserAddresses : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_OBJECT_NAMECHANGE = 0x800C;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000, WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int OBJID_WINDOW = 0, CHILDID_SELF = 0;

    private readonly WinEventProc _callback;  // 네이티브 코드가 들고 있으므로 필드로 붙잡아 둔다
    private readonly nint _foregroundHook;
    private readonly nint _titleHook;
    // 창 → (주소를 읽었을 때의 제목, 도메인). 도메인을 못 읽었으면 null
    private readonly ConcurrentDictionary<nint, (string Title, string? Host)> _known = new();
    private readonly BlockingCollection<(nint Window, string Title)> _requests = new(boundedCapacity: 16);

    public BrowserAddresses()
    {
        _callback = OnWinEvent;
        _foregroundHook = NativeMethods.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, _callback, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _titleHook = NativeMethods.SetWinEventHook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE, 0, _callback, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        // UI Automation은 자기 창이 있는 UI 스레드에서 부르면 멈출 수 있어 전용 스레드에서 차례로 부른다
        new Thread(ReadLoop) { IsBackground = true, Name = "UP browser address" }.Start();
        Request(NativeMethods.GetForegroundWindow());
    }

    // 앞 창이 바뀌거나 앞 창의 제목이 바뀌었을 때 (UI 스레드). 앞 창 미리 쓰기가 쓴다 (#26)
    public event Action? ForegroundChanged;

    // 주소를 새로 읽었을 때 (백그라운드 스레드). 앞 창이 브라우저면 대상이 바뀔 수 있다 (#26)
    public event Action? AddressRead;

    // 붙여넣기 순간 부른다. 이 창이 지금 제목일 때 읽어 둔 주소가 있으면 true.
    // 아직 못 읽었거나 그 뒤 제목이 바뀌었으면 false이고, 그때는 창 제목 규칙으로 판단한다
    public bool TryGetHost(nint window, string title, out string? host)
    {
        if (_known.TryGetValue(window, out var entry) && entry.Title == title)
        {
            host = entry.Host;
            return true;
        }
        host = null;
        return false;
    }

    public void Dispose()
    {
        NativeMethods.UnhookWinEvent(_foregroundHook);
        NativeMethods.UnhookWinEvent(_titleHook);
        _requests.CompleteAdding();
    }

    private void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (objectId != OBJID_WINDOW || childId != CHILDID_SELF)
            return;
        // 제목 바뀜은 다른 창에서도 수없이 오므로 앞에 있는 창만 본다
        if (eventType == EVENT_OBJECT_NAMECHANGE && window != NativeMethods.GetForegroundWindow())
            return;
        Request(window);
        ForegroundChanged?.Invoke();
    }

    private void Request(nint window)
    {
        if (window == 0 || WindowProcess.Title(window) is not { } title)
            return;
        if (_known.TryGetValue(window, out var entry) && entry.Title == title)
            return;
        if (!PasteTargets.IsBrowser(Path.GetFileName(WindowProcess.ImagePath(window))))
            return;
        _requests.TryAdd((window, title));
    }

    private void ReadLoop()
    {
        foreach (var (window, title) in _requests.GetConsumingEnumerable())
        {
            // 페이지가 열리는 동안 제목이 여러 번 바뀐다. 이미 다른 제목이 됐으면 그 요청이 따로 오므로 건너뛴다
            if (WindowProcess.Title(window) != title)
                continue;

            var host = PasteTargets.HostOf(ReadAddressBar(window));
            _known[window] = (title, host);
            // 도메인만 남기고 전체 주소·제목은 남기지 않는다 (macOS 로그와 같음)
            DevLog.Info("paste", $"browser url={host ?? "-"} via=uia");
            AddressRead?.Invoke();

            if (_known.Count > 64)
            {
                foreach (var closed in _known.Keys.Where(key => !NativeMethods.IsWindow(key)))
                    _known.TryRemove(closed, out _);
            }
        }
    }

    // 브라우저 창 안의 첫 입력란이 주소창이다 (Chrome·Edge·Whale·Firefox 모두 도구 모음이 페이지보다 앞에 있음)
    private static string? ReadAddressBar(nint window)
    {
        try
        {
            var edit = AutomationElement.FromHandle(window)
                .FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            return edit?.GetCurrentPropertyValue(ValuePattern.ValueProperty) as string;
        }
        catch (Exception e) when (e is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException or TimeoutException)
        {
            return null;
        }
    }

    private delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time);

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(nint hook);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindow(nint window);
    }
}
