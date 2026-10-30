using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows;

internal sealed record Paste(string Key, TargetApp Target, string? Process);

// 붙여넣기 키(Ctrl+V, Shift+Insert)를 감지하고 대상 앱을 판별한다 (FR-05).
// 저수준 키보드 훅은 키를 막거나 다시 보내지 않고 그대로 넘긴다. 훅은 이 객체를 만든 UI 스레드에서 돈다.
// 훅 콜백이 느리면 Windows가 알리지 않고 훅을 떼어 내므로, Raw Input으로 키 입력이 있었는데
// 훅이 불리지 않았으면 다시 건다
internal sealed class PasteWatcher : NativeWindow, IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_INPUT = 0x00FF;
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private const uint RIDEV_REMOVE = 0x01, RIDEV_INPUTSINK = 0x100;
    private static readonly nint HWND_MESSAGE = -3;

    // 키 입력 뒤 이 시간 안에 훅이 불리지 않았으면 떼어 내진 것으로 본다
    private const long HookSilenceMs = 1000;

    private readonly HookProc _callback;  // 네이티브 코드가 들고 있으므로 필드로 붙잡아 둔다
    private readonly PasteKeyDetector _keys = new();
    private readonly System.Windows.Forms.Timer _watchdog = new() { Interval = 2000 };
    private nint _hook;
    private long _lastHookTick;
    private long _lastKeyTick;

    public PasteWatcher()
    {
        _callback = OnHook;
        // 메시지 전용 창: Raw Input을 받는 용도
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
        RegisterRawKeyboard(RIDEV_INPUTSINK, Handle);
        Install();
        _watchdog.Tick += (_, _) => CheckHook();
        _watchdog.Start();
    }

    // UI 스레드에서, 키가 대상 앱에 전달되기 전에 발생한다. 오래 걸리는 일을 하면 안 된다
    public event Action<Paste>? Pasted;

    public void Dispose()
    {
        _watchdog.Dispose();
        Uninstall();
        if (Handle != 0)
        {
            RegisterRawKeyboard(RIDEV_REMOVE, 0);
            DestroyHandle();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
            _lastKeyTick = Environment.TickCount64;
        base.WndProc(ref m);
    }

    private void Install()
    {
        _hook = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _callback, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "키보드 감시를 시작하지 못했습니다.");
        _lastHookTick = Environment.TickCount64;
    }

    private void Uninstall()
    {
        if (_hook != 0)
            NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = 0;
    }

    private void CheckHook()
    {
        if (_lastKeyTick - _lastHookTick <= HookSilenceMs)
            return;

        Uninstall();
        _keys.Reset();
        Install();
        DevLog.Info("paste", "keyboard hook reinstalled");
    }

    private nint OnHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            _lastHookTick = Environment.TickCount64;
            try
            {
                var message = (int)wParam;
                var down = message is WM_KEYDOWN or WM_SYSKEYDOWN;
                if (down || message is WM_KEYUP or WM_SYSKEYUP)
                {
                    var vk = Marshal.ReadInt32(lParam);  // KBDLLHOOKSTRUCT.vkCode
                    var key = _keys.OnKey(vk, down, IsDown(VK_CONTROL), IsDown(VK_SHIFT), IsDown(VK_MENU), IsDown(VK_LWIN) || IsDown(VK_RWIN));
                    if (key is not null)
                        Pasted?.Invoke(Resolve(key));
                }
            }
            catch (Exception ex)
            {
                // 예외가 훅 밖으로 나가면 앱이 죽는다. 키는 아래에서 그대로 넘긴다
                DevLog.Info("paste", $"hook error {ex.GetType().Name}");
            }
        }
        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static Paste Resolve(string key)
    {
        var window = NativeMethods.GetForegroundWindow();
        var process = Path.GetFileName(WindowProcess.ImagePath(window));
        // 창 제목에는 문서 이름이 들어 있으므로 브라우저일 때만 읽고 로그에도 남기지 않는다
        var title = PasteTargets.IsBrowser(process) ? WindowTitle(window) : null;
        return new Paste(key, PasteTargets.Resolve(process, title), process);
    }

    private static string? WindowTitle(nint window)
    {
        var buffer = new StringBuilder(512);
        return NativeMethods.GetWindowText(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : null;
    }

    private static bool IsDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static void RegisterRawKeyboard(uint flags, nint target)
    {
        // 사용 페이지 1(일반 데스크톱), 사용 6(키보드). INPUTSINK면 앞 창이 아니어도 받는다
        var device = new NativeMethods.RawInputDevice { UsagePage = 0x01, Usage = 0x06, Flags = flags, Target = target };
        NativeMethods.RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<NativeMethods.RawInputDevice>());
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public nint Target;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint SetWindowsHookEx(int hookId, HookProc callback, nint module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(nint hook);

        [DllImport("user32.dll")]
        public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern nint GetModuleHandle(string? moduleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vk);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(nint window, StringBuilder text, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    }
}
