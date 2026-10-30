using System.Runtime.InteropServices;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 클립보드 변경 알림(WM_CLIPBOARDUPDATE)을 받아 원본 스냅샷을 만든다 (FR-02, FR-03).
// Ctrl+C와 앱의 복사 메뉴 모두 클립보드를 바꾸므로 이 알림 하나로 감지한다
internal sealed class ClipboardMonitor : NativeWindow, IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly nint HWND_MESSAGE = -3;

    // Core가 읽는 포맷만 스냅샷에 담는다. Excel은 이미지·OLE 등 20개 넘는 포맷을 지연 렌더링으로 넣어서,
    // 전부 읽으면 복사할 때마다 Excel이 그 데이터를 모두 만들어야 한다
    private static readonly HashSet<string> SnapshotFormats =
    [
        ClipFormats.WinHtml, ClipFormats.WinUnicodeText, ClipFormats.WinRtf, ClipFormats.WinExcelXml, ClipFormats.WinExcelBiff12,
    ];

    private readonly Lock _lock = new();
    private readonly HashSet<long> _ownWrites = [];

    public ClipboardMonitor()
    {
        // 메시지 전용 창: 화면에 보이지 않고 알림만 받는다
        CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
        if (!NativeMethods.AddClipboardFormatListener(Handle))
            throw new InvalidOperationException($"클립보드 감시를 시작하지 못했습니다 (오류 {Marshal.GetLastWin32Error()}).");
    }

    // 다른 앱에서 복사했을 때 백그라운드 스레드에서 발생한다
    public event Action<ClipboardSnapshot>? Copied;

    // 숨김 표시가 된 복사(비밀번호 관리자 등, #78). 내용은 읽지 않고 시퀀스 번호만 알린다. 백그라운드 스레드
    public event Action<long>? Concealed;

    // UP이 클립보드에 기록한 뒤 호출한다 (#3). 이 번호의 변경은 새 복사로 취급하지 않는다
    public void RememberOwnWrite(long sequenceNumber)
    {
        lock (_lock)
            _ownWrites.Add(sequenceNumber);
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            NativeMethods.RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE)
            OnClipboardUpdate();
        base.WndProc(ref m);
    }

    private void OnClipboardUpdate()
    {
        var sequenceNumber = ClipboardReader.SequenceNumber();
        lock (_lock)
        {
            // FR-02: UP 자신이 기록한 변경은 무시한다. 기록은 ClipboardWriter 창이 하므로 소유 프로세스로 본다
            if (_ownWrites.Remove(sequenceNumber) || IsOwnedByThisProcess())
            {
                DevLog.Info("copy", $"ignored own write seq={sequenceNumber}");
                return;
            }
        }

        // 원본 앱이 지연 렌더링으로 데이터를 만드는 동안 이 스레드(키보드 훅이 도는 UI 스레드)를 막지 않도록
        // 클립보드는 백그라운드에서 읽는다
        Task.Run(() => ReadSnapshot(sequenceNumber));
    }

    private static bool IsOwnedByThisProcess()
    {
        var owner = NativeMethods.GetClipboardOwner();
        return owner != 0 && NativeMethods.GetWindowThreadProcessId(owner, out var processId) != 0 && processId == Environment.ProcessId;
    }

    private void ReadSnapshot(long sequenceNumber)
    {
        // 숨김 표시부터 본다. 숨김이면 복사한 본문(비밀번호 등)은 읽지 않는다 (#78)
        var marks = ClipboardReader.Read(include: ConcealedCopy.NeedsData);
        if (!IsCurrent(marks, sequenceNumber))
            return;
        if (ConcealedCopy.IsConcealed(marks!.Formats.Select(format => (format.Name, format.Data))))
        {
            DevLog.Info("copy", $"ignored concealed copy seq={sequenceNumber}");
            Concealed?.Invoke(sequenceNumber);
            return;
        }

        // 원본 앱이 아직 복사 중이면 지연 렌더링 포맷(HTML·텍스트)을 받지 못한다. 잠깐 기다렸다 다시 읽는다 (SnapshotRetry)
        ClipboardReader.Result? clipboard = null;
        for (var attempt = 1; attempt <= SnapshotRetry.MaxAttempts; attempt++)
        {
            if (attempt > 1)
                Thread.Sleep(SnapshotRetry.Delay);
            clipboard = ClipboardReader.Read(include: SnapshotFormats.Contains);
            if (!IsCurrent(clipboard, sequenceNumber))
                return;
            var formats = clipboard!.Formats.Select(f => (f.Name, f.Data is not null, f.SkipReason)).ToList();
            if (!SnapshotRetry.NeedsRetry(formats))
                break;
            DevLog.Info("copy", $"retry seq={sequenceNumber} attempt={attempt} missing={SnapshotRetry.Missing(formats)}");
        }

        var snapshot = clipboard!.ToSnapshot();
        DevLog.Info("copy", $"copy seq={snapshot.SequenceNumber} app={snapshot.SourceApp ?? "unknown"} formats={string.Join(",", snapshot.Formats.Keys)}");
        Copied?.Invoke(snapshot);
    }

    // 읽는 사이에 다시 복사됐으면 그 복사의 알림이 따로 오므로 버린다
    private static bool IsCurrent(ClipboardReader.Result? clipboard, long sequenceNumber)
    {
        if (clipboard is not null && clipboard.SequenceNumber == sequenceNumber)
            return true;
        DevLog.Info("copy", clipboard is null
            ? $"clipboard busy seq={sequenceNumber}"
            : $"skipped seq={sequenceNumber} newer={clipboard.SequenceNumber}");
        return false;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AddClipboardFormatListener(nint window);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RemoveClipboardFormatListener(nint window);

        [DllImport("user32.dll")]
        public static extern nint GetClipboardOwner();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    }
}
