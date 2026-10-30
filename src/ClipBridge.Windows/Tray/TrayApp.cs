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
    // TODO(#14): 실제 변환기, TODO(#22): 메뉴에서 바꾼 옵션
    private readonly CopyTracker _copies = new(new StubClipConverter(), () => ConversionOptions.Default);
#if DEBUG
    private readonly System.Windows.Forms.Timer _debugStatus = new() { Interval = 500 };
#endif

    public TrayApp()
    {
        // TODO(#22): Markdown 처리·AI 검증 메뉴
        _menu.Items.Add("종료", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = _icon, Text = "UP", ContextMenuStrip = _menu, Visible = true };
        _animation = SlimeAnimation.Start(_tray, SystemInformation.SmallIconSize);
        _clipboard.Copied += snapshot => _ = _copies.OnCopied(snapshot);
#if DEBUG
        _debugStatus.Tick += (_, _) => ShowDebugStatus();
        _debugStatus.Start();
#endif
    }

    // 종료할 때 클립보드는 건드리지 않는다 (FR-19)
    protected override void ExitThreadCore()
    {
        // 알림 영역에 아이콘이 남지 않게 메시지 루프가 끝나기 전에 숨긴다
        _animation?.Stop();
        _tray.Visible = false;
        _clipboard.Dispose();
        _copies.Dispose();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
#if DEBUG
            _debugStatus.Dispose();
#endif
            _animation?.Dispose();
            _clipboard.Dispose();
            _copies.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

#if DEBUG
    // 개발 빌드에서만 툴팁에 마지막 복사를 보여 준다. 붙여넣기(#3) 전까지 복사 감지를 눈으로 확인하는 용도
    private void ShowDebugStatus()
    {
        var snapshot = _copies.Snapshot;
        if (snapshot is null)
            return;

        var converter = _copies.Prepared?.Get(TargetApp.Hangul, ConversionOptions.Default).ConverterId ?? "없음";
        var text = $"UP (개발)\n복사 #{snapshot.SequenceNumber}: {snapshot.SourceApp ?? "?"}\n포맷 {snapshot.Formats.Count}개 · 한글용 결과 {converter}";
        _tray.Text = text.Length <= 127 ? text : text[..127];
    }
#endif
}
