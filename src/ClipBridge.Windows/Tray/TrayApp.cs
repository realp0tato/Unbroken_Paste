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
    private readonly BrowserAddresses _addresses = new();
    private readonly PasteWatcher _paste;
    private readonly ClipboardWriter _writer = new();
    private readonly CursorOverlay _overlay = new();
    private readonly AppSettings _settings = new(AppSettings.DefaultPath);
    private readonly CopyTracker _copies;
    private readonly ToolStripMenuItem _formatItem = new("서식 적용 (기본)");
    private readonly ToolStripMenuItem _stripItem = new("기호 제거");
    private readonly ToolStripMenuItem _aiOnItem = new("켜기 (기본)");
    private readonly ToolStripMenuItem _aiOffItem = new("끄기");

    // 메뉴 옵션 (PRD 4.2). 복사 때(준비)와 붙여넣기 때(결과 선택) 모두 이 값을 읽으므로 바꾸면 다음 붙여넣기부터 바로 적용된다
    private volatile ConversionOptions _options;

    public TrayApp()
    {
        _options = _settings.Load();
        _paste = new PasteWatcher(_addresses);
        _copies = new CopyTracker(new UpClipConverter(), () => _options, log: message => DevLog.Info("convert", message));
        BuildMenu();
        _tray = new NotifyIcon { Icon = _icon, Text = "UP", ContextMenuStrip = _menu, Visible = true };
        _animation = SlimeAnimation.Start(_tray, SystemInformation.SmallIconSize);
        _clipboard.Copied += OnCopied;
        // 숨김 복사는 결과를 버리기만 하고 복사 아이콘도 띄우지 않는다 (macOS와 같음)
        _clipboard.Concealed += _copies.Discard;
        _paste.Pasted += OnPaste;
        _writer.DataTaken += OnDataTaken;
        DevLog.Info("app", $"started markdown={_options.MarkdownMode} ai={_options.AiVerification}");
    }

    // 종료할 때 클립보드는 건드리지 않는다 (FR-19)
    protected override void ExitThreadCore()
    {
        // 알림 영역에 아이콘이 남지 않게 메시지 루프가 끝나기 전에 숨긴다
        _animation?.Stop();
        _tray.Visible = false;
        _paste.Dispose();
        _addresses.Dispose();
        _writer.SettleBeforeExit();
        _writer.Dispose();
        _clipboard.Dispose();
        _copies.Dispose();
        _overlay.Dispose();
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
            _addresses.Dispose();
            _writer.Dispose();
            _clipboard.Dispose();
            _copies.Dispose();
            _overlay.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    // PRD 4.2 공통 메뉴. 그 밖의 항목은 넣지 않는다 (macOS 추가 메뉴는 #44)
    private void BuildMenu()
    {
        _formatItem.Click += (_, _) => SetOptions(_options with { MarkdownMode = MarkdownMode.Format });
        _stripItem.Click += (_, _) => SetOptions(_options with { MarkdownMode = MarkdownMode.Strip });
        var markdown = new ToolStripMenuItem("Markdown 처리");
        markdown.DropDownItems.AddRange([_formatItem, _stripItem]);

        _aiOnItem.Click += (_, _) => SetOptions(_options with { AiVerification = true });
        _aiOffItem.Click += (_, _) => SetOptions(_options with { AiVerification = false });
        var ai = new ToolStripMenuItem("AI 검증");
        // 켜져 있을 때 무엇이 밖으로 나가는지 알린다 (PRD 6.6)
        ai.DropDownItems.AddRange([
            _aiOnItem, _aiOffItem, new ToolStripSeparator(),
            new ToolStripMenuItem("켜져 있으면 복사 내용 일부가 외부 AI API로 전송돼요") { Enabled = false },
        ]);

        _menu.Items.AddRange([markdown, ai, new ToolStripSeparator(), new ToolStripMenuItem("UP 종료", null, (_, _) => ExitThread())]);
        RefreshMenu();
    }

    private void RefreshMenu()
    {
        _formatItem.Checked = _options.MarkdownMode == MarkdownMode.Format;
        _stripItem.Checked = _options.MarkdownMode == MarkdownMode.Strip;
        _aiOnItem.Checked = _options.AiVerification;
        _aiOffItem.Checked = !_options.AiVerification;
    }

    private void SetOptions(ConversionOptions options)
    {
        _options = options;
        RefreshMenu();
        var saved = _settings.Save(options);
        DevLog.Info("app", $"options markdown={options.MarkdownMode} ai={options.AiVerification} saved={saved}");
    }

    // 백그라운드 스레드에서 불린다
    private void OnCopied(ClipboardSnapshot snapshot)
    {
        _ = _copies.OnCopied(snapshot);
        _overlay.BeginInvoke(_overlay.ShowCopy);
    }

    private void OnDataTaken(string format)
    {
        DevLog.Info("paste", $"data taken format={format}");
        _overlay.DataTaken();
    }

    // 키가 대상 앱에 가기 전, 훅 안에서 불린다. 미리 만든 결과를 기록만 한다 (NFR-03: p95 20ms, 상한 50ms)
    private void OnPaste(Paste paste)
    {
        var started = Stopwatch.GetTimestamp();
        var snapshot = _copies.Snapshot;
        var prepared = _copies.Prepared;
        // FR-16: 결과가 아직 없거나 다른 복사의 것이면 기다리지 않고 통과
        var result = prepared is not null && prepared.SequenceNumber == snapshot?.SequenceNumber
            ? prepared.Get(paste.Target, _options)
            : ConversionResult.Passthrough;

        var plan = PastePlan.Decide(
            snapshot?.SequenceNumber, ClipboardReader.SequenceNumber(), _writer.OwnsClipboard, _writer.LastWrite, result, paste.Target);
        var written = _writer.Apply(plan, snapshot, paste.Target);
        if (written is { } sequenceNumber)
            _clipboard.RememberOwnWrite(sequenceNumber);

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var outcome = plan.Action == PasteAction.Leave ? "leave" : written is null ? "skipped(clipboard busy)" : plan.Action.ToString();
        // 브라우저면 판별 근거도 남긴다. 도메인만 남기고 전체 주소·제목은 남기지 않는다
        var browser = paste.Via is null ? "" : $" url={paste.Host ?? "-"} via={paste.Via}";
        DevLog.Info("paste", $"paste key={paste.Key} target={paste.Target} app={paste.Process ?? "unknown"}{browser} " +
            $"converter={result.ConverterId} plan={outcome} elapsed={elapsed:0.0}ms");

        // 테두리는 이 대상용 변환 결과가 실제로 클립보드에 있을 때만 강조색 (같은 대상에 두 번째로 붙일 때 포함).
        // 아이콘은 키를 돌려준 뒤에 그려 붙여넣기 경로를 늦추지 않는다 (NFR-11)
        var converted = _writer.OwnsClipboard && _writer.LastWrite?.Target == paste.Target;
        var badge = PasteBadge.For(paste.Target, paste.Process, converted);
        _overlay.BeginInvoke(() => _overlay.ShowPaste(badge, paste.ProcessPath));
    }
}
