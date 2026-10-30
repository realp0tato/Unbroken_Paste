using System.Diagnostics;
using System.Reflection;

namespace ClipBridge.Windows;

// "Unbroken Paste 정보" 창 (#23, PRD 4.2.2). macOS About.swift와 같은 내용·순서:
// 앱 아이콘·이름·버전, 한 줄 소개, 만든 사람(UP 팀 4명과 역할), 해커톤, 저장소 주소, 저작권
internal sealed class AboutWindow : Form
{
    public static readonly (string Name, string Role)[] Team =
    [
        ("장세찬", "Excel 변환기 · macOS 보조 프로그램"),
        ("박재현", "인터넷·Markdown 변환기 · 출처 판별 · AI 검증"),
        ("이창민", "Windows 앱"),
        ("윤승희", "macOS 앱"),
    ];
    private const string Repository = "https://github.com/realp0tato/donga_hackathon";

    private static AboutWindow? _open;

    private readonly Icon _icon = TrayIcon.Create(new Size(64, 64));

    // 이미 열려 있으면 그 창을 앞으로 가져온다
    public static void ShowOrActivate()
    {
        if (_open is { IsDisposed: false })
        {
            _open.Activate();
            return;
        }
        _open = new AboutWindow();
        _open.FormClosed += (_, _) => _open = null;
        _open.Show();
        // 트레이 앱은 앞 창이 아니므로 직접 앞으로 가져온다
        _open.Activate();
    }

    private AboutWindow()
    {
        Text = "Unbroken Paste 정보";
        Icon = TrayIcon.Create(SystemInformation.SmallIconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Padding = new Padding(28, 20, 28, 16);

        // 위에서 아래로 한 열. 열 안에서 Anchor가 None인 항목은 가운데에 놓인다
        var body = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
        };

        body.Controls.Add(new PictureBox { Image = _icon.ToBitmap(), SizeMode = PictureBoxSizeMode.AutoSize, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, 8) });
        body.Controls.Add(Line("Unbroken Paste", bold: true, size: 1.4f));
        body.Controls.Add(Line($"버전 {Version()}", muted: true, gap: 12));
        body.Controls.Add(Line("복사한 내용을 붙여넣는 앱에 맞게\n정리해서 붙여 주는 앱", gap: 12));
        body.Controls.Add(Line("만든 사람 · UP 팀", bold: true, gap: 4));
        foreach (var (name, role) in Team)
            body.Controls.Add(Member(name, role));
        body.Controls.Add(Line("대학 AX 해커톤 2026", muted: true, gap: 4, top: 12));

        var link = new LinkLabel { Text = "github.com/realp0tato/donga_hackathon", AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, 12) };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(Repository) { UseShellExecute = true });
        body.Controls.Add(link);

        body.Controls.Add(Line(Copyright(), muted: true, gap: 16));

        var ok = new Button { Text = "확인", AutoSize = true, Anchor = AnchorStyles.None };
        ok.Click += (_, _) => Close();
        AcceptButton = ok;
        CancelButton = ok;
        body.Controls.Add(ok);

        Controls.Add(body);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Dispose();
            Icon?.Dispose();
        }
        base.Dispose(disposing);
    }

    // 파일 속성의 제품 버전 (ClipBridge.Windows.csproj의 Version)
    private static string Version() => Application.ProductVersion;

    // 파일 속성의 저작권 (ClipBridge.Windows.csproj의 Copyright)
    private static string Copyright() =>
        typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    private Label Line(string text, bool bold = false, bool muted = false, float size = 1f, int gap = 0, int top = 0) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.None,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font(Font.FontFamily, Font.Size * size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = muted ? SystemColors.GrayText : SystemColors.ControlText,
        Margin = new Padding(0, top, 0, gap),
    };

    // "장세찬  Excel 변환기 · macOS 보조 프로그램" (이름은 굵게, 역할은 흐리게)
    private Control Member(string name, string role)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, 2) };
        row.Controls.Add(Line(name, bold: true));
        row.Controls.Add(Line(role, muted: true));
        return row;
    }
}
