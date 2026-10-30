using System.Diagnostics;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 작업 창 없이 트레이 아이콘으로만 동작한다 (FR-01, PRD 4.1·4.2·4.2.2)
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
    // PRD 4.2.2 추가 메뉴 (#44). 변환 결과에는 영향을 주지 않는다
    private readonly ToolStripMenuItem _headerItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _idleItem = new("대기");
    private readonly ToolStripMenuItem _busyItem = new("분석 중");
    private readonly ToolStripMenuItem _clipboardItem = new("클립보드");
    private readonly ToolStripMenuItem _characterItem = new("캐릭터");
    private readonly ClipboardHistory _history = new();  // UI 스레드에서만 쓴다
    // 앞 창 미리 쓰기 (#26): 앞 창이 잠깐 머문 뒤에 쓴다. Alt+Tab으로 지나가는 창마다 클립보드를 바꾸지 않도록
    private readonly System.Windows.Forms.Timer _focusTimer = new() { Interval = 150 };
    private TrayPreferences _preferences;
    private SlimeMode _mode = SlimeMode.Idle;

    // 메뉴 옵션 (PRD 4.2). 복사 때(준비)와 붙여넣기 때(결과 선택) 모두 이 값을 읽으므로 바꾸면 다음 붙여넣기부터 바로 적용된다
    private volatile ConversionOptions _options;

    public TrayApp()
    {
        _options = _settings.Load();
        _preferences = _settings.LoadTray();
        var character = TrayCharacter.Find(_preferences.Character);
        _paste = new PasteWatcher(_addresses);
        var converter = new UpClipConverter();
        _copies = new CopyTracker(converter, () => _options, log: message => DevLog.Info("convert", message));
        // 첫 복사의 준비 시간이 늦지 않게 변환 코드를 미리 한 번 돌려 둔다 (NFR-02)
        var options = _options;
        Task.Run(() =>
        {
            var started = Stopwatch.GetTimestamp();
            ConverterWarmup.Run(converter, options);
            DevLog.Info("convert", $"warmed up elapsed={Stopwatch.GetElapsedTime(started).TotalMilliseconds:0.0}ms");
        });
        BuildMenu();
        _tray = new NotifyIcon { Icon = _icon, Text = "UP", ContextMenuStrip = _menu, Visible = true };
        _animation = SlimeAnimation.Start(_tray, SystemInformation.SmallIconSize, character);
        _overlay.SetCharacter(character);
        _clipboard.Copied += OnCopied;
        // 숨김 복사는 결과를 버리기만 하고 복사 아이콘도 띄우지 않는다 (macOS와 같음)
        _clipboard.Concealed += _copies.Discard;
        _clipboard.OriginalRead += _copies.SetOriginal;
        _paste.Pasted += OnPaste;
        _writer.DataTaken += OnDataTaken;
        // 메뉴·리본·오른쪽 클릭 붙여넣기는 키 입력이 없으므로, 대상 앱이 앞에 오면 그 앱용 결과를 미리 써 둔다 (FR-22, #26)
        _focusTimer.Tick += (_, _) => WriteForForeground();
        _addresses.ForegroundChanged += ScheduleForeground;
        _addresses.AddressRead += () => _overlay.BeginInvoke(ScheduleForeground);
        _copies.ResultsReady += () => _overlay.BeginInvoke(ScheduleForeground);
        DevLog.Info("app", $"started markdown={_options.MarkdownMode} ai={_options.AiVerification} character={character.Id} react={_preferences.ReactOnCopy}");
    }

    // 종료할 때 클립보드는 건드리지 않는다 (FR-19)
    protected override void ExitThreadCore()
    {
        // 알림 영역에 아이콘이 남지 않게 메시지 루프가 끝나기 전에 숨긴다
        _focusTimer.Stop();
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
            _focusTimer.Dispose();
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

    // PRD 4.2 공통 메뉴와 4.2.2 추가 메뉴 (macOS 메뉴 막대와 같은 순서·이름)
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

        // 상태 변경: 슬라임 속도만 바뀐다. 맞춤 완료 효과 보기는 시연용이고 실제 변환과 무관하다 (PRD 4.2.2, 10)
        _idleItem.Click += (_, _) => SetMode(SlimeMode.Idle);
        _busyItem.Click += (_, _) => SetMode(SlimeMode.Busy);
        var state = new ToolStripMenuItem("상태 변경");
        state.DropDownItems.AddRange([_idleItem, _busyItem, new ToolStripSeparator(), new ToolStripMenuItem("맞춤 완료 효과 보기", null, (_, _) => Celebrate())]);

        // 최근 복사는 열 때마다 다시 만든다
        _clipboardItem.DropDownItems.Add(new ToolStripMenuItem("…") { Enabled = false });
        _clipboardItem.DropDownOpening += (_, _) => RefreshClipboard();

        foreach (var character in TrayCharacter.All)
            _characterItem.DropDownItems.Add(new ToolStripMenuItem(character.Name, null, (_, _) => SetCharacter(character)) { Tag = character.Id });

        _menu.Items.AddRange([
            _headerItem, new ToolStripSeparator(),
            markdown, ai, new ToolStripSeparator(),
            state, _clipboardItem, _characterItem, new ToolStripSeparator(),
            new ToolStripMenuItem("UP 종료", null, (_, _) => ExitThread()),
        ]);
        RefreshMenu();
    }

    private void RefreshMenu()
    {
        _formatItem.Checked = _options.MarkdownMode == MarkdownMode.Format;
        _stripItem.Checked = _options.MarkdownMode == MarkdownMode.Strip;
        _aiOnItem.Checked = _options.AiVerification;
        _aiOffItem.Checked = !_options.AiVerification;
        _headerItem.Text = _mode == SlimeMode.Busy ? "UP · 구조 분석 중" : "UP · 대기 중";
        _idleItem.Checked = _mode == SlimeMode.Idle;
        _busyItem.Checked = _mode == SlimeMode.Busy;
        foreach (ToolStripMenuItem item in _characterItem.DropDownItems)
            item.Checked = (string?)item.Tag == _preferences.Character;
    }

    private void RefreshClipboard()
    {
        var items = _clipboardItem.DropDownItems;
        items.Clear();
        if (_history.Items.Count == 0)
        {
            items.Add(new ToolStripMenuItem("아직 복사한 내용이 없어요") { Enabled = false });
        }
        else
        {
            items.Add(new ToolStripMenuItem("최근 복사 · 누르면 다시 복사") { Enabled = false });
            for (var i = 0; i < _history.Items.Count; i++)
            {
                var index = i;
                // &는 메뉴 단축키 표시로 쓰이므로 글자 그대로 보이게 한다
                var label = $"{i + 1}. {ClipboardHistory.Preview(_history.Items[i]).Replace("&", "&&")}";
                items.Add(new ToolStripMenuItem(label, null, (_, _) => Recopy(index)));
            }
            items.Add(new ToolStripSeparator());
            items.Add(new ToolStripMenuItem("기록 지우기", null, (_, _) => _history.Clear()));
        }
        items.Add(new ToolStripSeparator());
        items.Add(new ToolStripMenuItem("복사하면 슬라임 반응", null, (_, _) => SetPreferences(_preferences with { ReactOnCopy = !_preferences.ReactOnCopy }))
        {
            Checked = _preferences.ReactOnCopy,
        });
        items.Add(new ToolStripMenuItem("기록은 저장하지 않고 메모리에만 있어요") { Enabled = false });
    }

    private void SetMode(SlimeMode mode)
    {
        _mode = mode;
        if (_animation is not null)
            _animation.Mode = mode;
        RefreshMenu();
    }

    private void Celebrate()
    {
        _animation?.Celebrate();
        _tray.ShowBalloonTip(3000, "UP", "변환 결과를 붙였어요 (시뮬레이션)", ToolTipIcon.None);
    }

    private void SetCharacter(TrayCharacter character)
    {
        // 프레임을 읽지 못하면 지금 캐릭터를 그대로 둔다
        if (_animation is not null && !_animation.SetCharacter(character))
            return;
        _overlay.SetCharacter(character);
        SetPreferences(_preferences with { Character = character.Id });
    }

    private void SetPreferences(TrayPreferences preferences)
    {
        _preferences = preferences;
        RefreshMenu();
        var saved = _settings.Save(preferences);
        DevLog.Info("app", $"tray character={preferences.Character} react={preferences.ReactOnCopy} saved={saved}");
    }

    // 최근 복사 항목을 다시 복사한다. 새 복사로 보고 그 글을 원본으로 결과를 다시 준비한다 (macOS recopy와 같음)
    private void Recopy(int index)
    {
        if (index >= _history.Items.Count)
            return;
        var text = _history.Items[index];
        if (_writer.WriteText(text) is not { } sequenceNumber)
        {
            DevLog.Info("copy", "recopy skipped(clipboard busy)");
            return;
        }

        _clipboard.RememberOwnWrite(sequenceNumber);
        var snapshot = new ClipboardSnapshot(
            new Dictionary<string, byte[]> { [ClipFormats.WinUnicodeText] = PastePlan.TextBytes(text) }, SourceApp: null, sequenceNumber);
        _ = _copies.OnCopied(snapshot);
        _history.Remember(text);
        DevLog.Info("copy", $"recopy seq={sequenceNumber}");
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
        var text = ClipboardHistory.TextOf(snapshot);
        _overlay.BeginInvoke(() =>
        {
            _overlay.ShowCopy();
            if (text is not null)
                _history.Remember(text);
            if (_preferences.ReactOnCopy)
                _animation?.React();
        });
    }

    private void OnDataTaken(string format, string? requester)
    {
        DevLog.Info("paste", $"data taken format={format} by={requester ?? "unknown"}");
        _overlay.DataTaken();
    }

    // 키가 대상 앱에 가기 전, 훅 안에서 불린다. 미리 만든 결과를 기록만 한다 (NFR-03: p95 20ms, 상한 50ms)
    private void OnPaste(Paste paste)
    {
        var started = Stopwatch.GetTimestamp();
        var (result, outcome) = WriteFor(paste);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        DevLog.Info("paste", $"paste key={paste.Key} {Describe(paste)} converter={result.ConverterId} plan={outcome} elapsed={elapsed:0.0}ms");

        // 테두리는 이 대상용 변환 결과가 실제로 클립보드에 있을 때만 강조색 (같은 대상에 두 번째로 붙일 때 포함).
        // 아이콘은 키를 돌려준 뒤에 그려 붙여넣기 경로를 늦추지 않는다 (NFR-11)
        var converted = _writer.OwnsClipboard && _writer.LastWrite?.Target == paste.Target;
        var badge = PasteBadge.For(paste.Target, paste.Process, converted);
        _overlay.BeginInvoke(() => _overlay.ShowPaste(badge, paste.ProcessPath));
    }

    private void ScheduleForeground()
    {
        _focusTimer.Stop();
        _focusTimer.Start();
    }

    // 앞 창의 대상용 결과를 미리 쓰거나, 대상이 아닌 앱이면 원본으로 되돌린다. Ctrl+V와 같은 규칙(PastePlan)이라
    // 이미 이 대상용 결과가 있거나 원본이 그대로면 아무것도 쓰지 않는다. 커서 아이콘은 띄우지 않는다
    private void WriteForForeground()
    {
        _focusTimer.Stop();
        var paste = _paste.Resolve("focus");
        // 트레이 메뉴처럼 UP 자신의 창이 앞에 있으면 그대로 둔다
        if (paste.ProcessPath is null || string.Equals(paste.ProcessPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            return;

        var (result, outcome) = WriteFor(paste);
        if (outcome != "leave")
            DevLog.Info("paste", $"focus {Describe(paste)} converter={result.ConverterId} plan={outcome}");
    }

    // 붙여넣기 키와 앞 창 미리 쓰기가 같이 쓰는 기록 (FR-07, FR-16, PRD 6.5). UI 스레드
    private (ConversionResult Result, string Outcome) WriteFor(Paste paste)
    {
        var snapshot = _copies.Snapshot;
        var prepared = _copies.Prepared;
        // FR-16: 결과가 아직 없거나 다른 복사의 것이면 기다리지 않고 통과
        var result = prepared is not null && prepared.SequenceNumber == snapshot?.SequenceNumber
            ? prepared.Get(paste.Target, _options)
            : ConversionResult.Passthrough;

        var plan = PastePlan.Decide(
            snapshot?.SequenceNumber, ClipboardReader.SequenceNumber(), _writer.OwnsClipboard, _writer.LastWrite, result, paste.Target);
        var original = snapshot is null ? PastePlan.Leave.Payload : _copies.OriginalOf(snapshot);
        var written = _writer.Apply(plan, snapshot, original, paste.Target);
        if (written is { } sequenceNumber)
            _clipboard.RememberOwnWrite(sequenceNumber);

        var outcome = plan.Action == PasteAction.Leave ? "leave" : written is null ? "skipped(clipboard busy)" : plan.Action.ToString();
        return (result, outcome);
    }

    // 브라우저면 판별 근거도 남긴다. 도메인만 남기고 전체 주소·제목은 남기지 않는다
    private static string Describe(Paste paste) =>
        $"target={paste.Target} app={paste.Process ?? "unknown"}" + (paste.Via is null ? "" : $" url={paste.Host ?? "-"} via={paste.Via}");
}
