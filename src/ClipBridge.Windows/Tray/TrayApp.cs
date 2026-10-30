using System.Diagnostics;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 작업 창 없이 트레이 아이콘으로만 동작한다 (FR-01, PRD 4.1·4.2)
internal sealed class TrayApp : ApplicationContext
{
    private readonly Icon _icon = TrayIcon.Create(SystemInformation.SmallIconSize);
    private readonly ContextMenuStrip _menu = new();
    private readonly NotifyIcon _tray;
    private readonly SlimeAnimation? _animation;
    private readonly ClipboardMonitor _clipboard = new();
    private readonly PasteWatcher _paste = new();
    private readonly ClipboardWriter _writer = new();
    // TODO(#14): 실제 변환기, TODO(#22): 메뉴에서 바꾼 옵션
    private readonly CopyTracker _copies = new(
        new StubClipConverter(), () => ConversionOptions.Default, log: message => DevLog.Info("convert", message));

    public TrayApp()
    {
        // TODO(#22): Markdown 처리·AI 검증 메뉴
        _menu.Items.Add("종료", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = _icon, Text = "UP", ContextMenuStrip = _menu, Visible = true };
        _animation = SlimeAnimation.Start(_tray, SystemInformation.SmallIconSize);
        _clipboard.Copied += snapshot => _ = _copies.OnCopied(snapshot);
        _paste.Pasted += OnPaste;
        _writer.DataTaken += format => DevLog.Info("paste", $"data taken format={format}");
        DevLog.Info("app", "started");
    }

    // 종료할 때 클립보드는 건드리지 않는다 (FR-19)
    protected override void ExitThreadCore()
    {
        // 알림 영역에 아이콘이 남지 않게 메시지 루프가 끝나기 전에 숨긴다
        _animation?.Stop();
        _tray.Visible = false;
        _paste.Dispose();
        _writer.SettleBeforeExit();
        _writer.Dispose();
        _clipboard.Dispose();
        _copies.Dispose();
        DevLog.Info("app", "exited");
        DevLog.Flush();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation?.Dispose();
            _paste.Dispose();
            _writer.Dispose();
            _clipboard.Dispose();
            _copies.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    // 키가 대상 앱에 가기 전, 훅 안에서 불린다. 미리 만든 결과를 기록만 한다 (NFR-03: p95 20ms, 상한 50ms)
    private void OnPaste(Paste paste)
    {
        var started = Stopwatch.GetTimestamp();
        var snapshot = _copies.Snapshot;
        var prepared = _copies.Prepared;
        // FR-16: 결과가 아직 없거나 다른 복사의 것이면 기다리지 않고 통과
        var result = prepared is not null && prepared.SequenceNumber == snapshot?.SequenceNumber
            ? prepared.Get(paste.Target, ConversionOptions.Default)
            : ConversionResult.Passthrough;

        var plan = PastePlan.Decide(
            snapshot?.SequenceNumber, ClipboardReader.SequenceNumber(), _writer.OwnsClipboard, _writer.LastWrite, result, paste.Target);
        var written = _writer.Apply(plan, snapshot, paste.Target);
        if (written is { } sequenceNumber)
            _clipboard.RememberOwnWrite(sequenceNumber);

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var outcome = plan.Action == PasteAction.Leave ? "leave" : written is null ? "skipped(clipboard busy)" : plan.Action.ToString();
        DevLog.Info("paste", $"paste key={paste.Key} target={paste.Target} app={paste.Process ?? "unknown"} " +
            $"converter={result.ConverterId} plan={outcome} elapsed={elapsed:0.0}ms");
    }
}
