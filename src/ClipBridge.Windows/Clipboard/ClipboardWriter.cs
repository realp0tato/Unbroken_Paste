using System.Runtime.InteropServices;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 변환 결과나 원본을 클립보드에 쓴다 (FR-07, PRD 6.5).
// 지연 렌더링: 포맷 목록만 먼저 올리고, 대상 앱이 데이터를 달라고 할 때(WM_RENDERFORMAT) 바이트를 넘긴다.
// 그래서 붙여넣기 경로가 짧고(NFR-03), 대상 앱이 데이터를 가져간 순간을 알 수 있다(커서 아이콘 #5)
internal sealed class ClipboardWriter : NativeWindow, IDisposable
{
    private const int WM_RENDERFORMAT = 0x0305, WM_RENDERALLFORMATS = 0x0306, WM_DESTROYCLIPBOARD = 0x0307;
    private const uint GMEM_MOVEABLE = 0x0002;
    private static readonly nint HWND_MESSAGE = -3;

    // 지금 클립보드에 약속한 포맷 → 바이트. UI 스레드에서만 읽고 쓴다
    private Dictionary<uint, byte[]> _pending = [];
    // UP이 마지막으로 쓴 복사의 원본 포맷 (종료할 때 되돌린다)
    private IReadOnlyDictionary<string, byte[]>? _writtenFor;
    private bool _taken;

    public ClipboardWriter()
    {
        // 메시지 전용 창: 클립보드 소유자로서 데이터 요청을 받는다
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
    }

    // 대상 앱이 쓴 데이터를 처음 가져갈 때 UI 스레드에서 발생한다 (커서 아이콘이 사라지는 신호, #5)
    public event Action<string>? DataTaken;

    public LastWrite? LastWrite { get; private set; }

    public bool OwnsClipboard => NativeMethods.GetClipboardOwner() == Handle;

    // 붙여넣기 키를 처리하는 중(키보드 훅 안)에 부른다. 썼으면 쓴 뒤의 클립보드 시퀀스 번호, 아니면 null.
    // original: 원본을 되돌릴 때 쓸 포맷, 복사한 순서대로 (#80, CopyTracker.OriginalOf)
    public long? Apply(PastePlan plan, ClipboardSnapshot? snapshot, IReadOnlyDictionary<string, byte[]> original, TargetApp target)
    {
        if (snapshot is null || plan.Action == PasteAction.Leave)
            return null;

        var payload = plan.Action == PasteAction.WriteResult ? plan.Payload : original;
        if (!Promise(payload))
            return null;

        LastWrite = new LastWrite(snapshot.SequenceNumber, plan.Action == PasteAction.WriteResult ? target : null);
        _writtenFor = original;
        return ClipboardReader.SequenceNumber();
    }

    // 종료하면 약속한 데이터가 창과 함께 사라지므로, UP이 쓴 것이 아직 클립보드에 있으면
    // 원본을 실제 데이터로 되돌려 둔다 (macOS settleBeforeQuit과 같음, FR-19)
    public void SettleBeforeExit()
    {
        if (_writtenFor is null || !OwnsClipboard || !NativeMethods.OpenClipboard(Handle))
            return;

        try
        {
            NativeMethods.EmptyClipboard();
            foreach (var (name, data) in _writtenFor)
            {
                var id = ClipboardReader.FormatId(name);
                if (id != 0)
                    SetData(id, data);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }

        _pending = [];
        _writtenFor = null;
        LastWrite = null;
    }

    public void Dispose()
    {
        if (Handle != 0)
            DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_RENDERFORMAT:
                // 요청한 앱이 이미 클립보드를 열어 두었으므로 여기서는 열지 않는다
                var format = (uint)m.WParam;
                if (_pending.TryGetValue(format, out var data))
                    SetData(format, data);
                if (!_taken)
                {
                    _taken = true;
                    DataTaken?.Invoke(ClipboardReader.FormatName(format));
                }
                m.Result = 0;
                return;
            case WM_RENDERALLFORMATS:
                // SettleBeforeExit 없이 창이 사라질 때. 약속한 데이터를 모두 실제로 넣어 둔다
                if (NativeMethods.OpenClipboard(Handle))
                {
                    try
                    {
                        if (OwnsClipboard)
                            foreach (var (id, bytes) in _pending)
                                SetData(id, bytes);
                    }
                    finally
                    {
                        NativeMethods.CloseClipboard();
                    }
                }
                m.Result = 0;
                return;
            case WM_DESTROYCLIPBOARD:
                // 다른 앱(또는 UP 자신)이 클립보드를 비웠다
                _pending = [];
                break;
        }
        base.WndProc(ref m);
    }

    private bool Promise(IReadOnlyDictionary<string, byte[]> payload)
    {
        // FR-16, 4.4: 다른 앱이 클립보드를 열고 있으면 기다리지 않고 쓰지 않는다
        if (!NativeMethods.OpenClipboard(Handle))
            return false;

        try
        {
            if (!NativeMethods.EmptyClipboard())
                return false;

            var pending = new Dictionary<uint, byte[]>();
            foreach (var (name, data) in payload)
            {
                var id = ClipboardReader.FormatId(name);
                if (id == 0 || pending.ContainsKey(id))
                    continue;
                pending[id] = data;
                NativeMethods.SetClipboardData(id, 0);  // 데이터는 WM_RENDERFORMAT에서
            }
            _pending = pending;
            _taken = false;
            return true;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    // 클립보드가 열려 있는 상태에서 호출한다
    private static void SetData(uint format, byte[] data)
    {
        var memory = NativeMethods.GlobalAlloc(GMEM_MOVEABLE, (nuint)Math.Max(data.Length, 1));
        if (memory == 0)
            return;

        var pointer = NativeMethods.GlobalLock(memory);
        if (pointer == 0)
        {
            NativeMethods.GlobalFree(memory);
            return;
        }
        Marshal.Copy(data, 0, pointer, data.Length);
        NativeMethods.GlobalUnlock(memory);

        // 성공하면 메모리는 시스템 소유가 된다
        if (NativeMethods.SetClipboardData(format, memory) == 0)
            NativeMethods.GlobalFree(memory);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool OpenClipboard(nint newOwner);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint SetClipboardData(uint format, nint memory);

        [DllImport("user32.dll")]
        public static extern nint GetClipboardOwner();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint GlobalAlloc(uint flags, nuint bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint GlobalLock(nint memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalUnlock(nint memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern nint GlobalFree(nint memory);
    }
}
