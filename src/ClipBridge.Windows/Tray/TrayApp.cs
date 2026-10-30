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
        DevLog.Info("app", "started");
    }

    // 종료할 때 클립보드는 건드리지 않는다 (FR-19)
    protected override void ExitThreadCore()
    {
        // 알림 영역에 아이콘이 남지 않게 메시지 루프가 끝나기 전에 숨긴다
        _animation?.Stop();
        _tray.Visible = false;
        _paste.Dispose();
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
            _clipboard.Dispose();
            _copies.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    // 키가 대상 앱에 가기 전, 훅 안에서 불린다. TODO(#3): 이 자리에서 변환 결과를 클립보드에 기록
    private void OnPaste(Paste paste)
    {
        var converter = _copies.Prepared?.Get(paste.Target, ConversionOptions.Default).ConverterId ?? "none";
        DevLog.Info("paste", $"paste key={paste.Key} target={paste.Target} app={paste.Process ?? "unknown"} converter={converter}");
    }
}
