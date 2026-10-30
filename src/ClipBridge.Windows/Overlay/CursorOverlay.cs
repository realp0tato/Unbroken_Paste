using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClipBridge.Windows;

// 마우스 포인터 옆 동그라미 아이콘 (FR-18, PRD 4.3, #5). macOS CursorOverlay와 같은 동작이다.
// 비활성·클릭 통과·최상위·작업 표시줄 미표시 창이라 포커스·키 입력·IME를 가져가지 않는다.
// 투명도가 있는 원을 그리려고 레이어드 창(UpdateLayeredWindow)으로 그린다
internal sealed class CursorOverlay : Form
{
    private const double CopySeconds = 0.8;
    private const double PasteTimeoutSeconds = 1.0;
    private const double FadeSeconds = 0.15;
    private const int ShadowMargin = 6;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Image? _upBadge = LoadUpBadge();
    // 실행 파일 경로 → 아이콘 (없으면 null). 같은 앱 아이콘을 다시 읽지 않는다 (NFR-11)
    private readonly Dictionary<string, Bitmap?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private Content _content = new(null, false, "UP");
    private bool _passthrough;
    private double _hideAt;
    private Bitmap? _image;
    private int _imageDpi;

    // fill: 배지 전체를 채우는 둥근 그림(복사 때 UP 캐릭터). 아니면 배지 안에 들어가는 아이콘 또는 글자
    private sealed record Content(Image? Picture, bool Fill, string Letter);

    public CursorOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        _timer.Tick += (_, _) => Tick();
        // 다른 스레드에서 BeginInvoke로 부를 수 있도록 창만 미리 만든다 (보이지는 않음)
        CreateHandle();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW
                | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    // 복사: UP 캐릭터 배지 0.8초
    public void ShowCopy() => Show(new Content(_upBadge, Fill: true, "UP"), passthrough: false, CopySeconds);

    // 붙여넣기: 대상 앱 아이콘. 대상 앱이 데이터를 가져가면(DataTaken) 바로, 아니면 1초 뒤 사라진다
    public void ShowPaste(PasteBadge badge, string? processPath)
    {
        NotionApp.Remember(processPath);
        var icon = badge.Icon switch
        {
            BadgeIcon.ForegroundApp => IconOf(processPath),
            BadgeIcon.NotionApp => IconOf(NotionApp.Find()),
            _ => null,
        };
        Show(new Content(icon, Fill: false, badge.Letter), badge.Passthrough, PasteTimeoutSeconds);
    }

    // 대상 앱이 클립보드 데이터를 가져갔다 (#3 ClipboardWriter.DataTaken): 1초를 기다리지 않고 지금 사라진다
    public void DataTaken() => _hideAt = Math.Min(_hideAt, Now);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _image?.Dispose();
            _upBadge?.Dispose();
            foreach (var icon in _icons.Values)
                icon?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private void Show(Content content, bool passthrough, double seconds)
    {
        _content = content;
        _passthrough = passthrough;
        _hideAt = Now + seconds;
        _image?.Dispose();
        _image = null;
        Follow(alpha: 1);
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        _timer.Start();
    }

    // 표시 시간이 지나면 0.15초에 걸쳐 흐려지고, 그동안 포인터를 따라간다
    private void Tick()
    {
        var now = Now;
        if (now >= _hideAt + FadeSeconds)
        {
            _timer.Stop();
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_HIDE);
            return;
        }
        Follow(now <= _hideAt ? 1 : 1 - (now - _hideAt) / FadeSeconds);
    }

    private void Follow(double alpha)
    {
        var pointer = Cursor.Position;
        var screen = Screen.FromPoint(pointer).Bounds;
        var dpi = DpiAt(pointer);
        if (_image is null || dpi != _imageDpi)
        {
            _image?.Dispose();
            _image = Render(dpi);
            _imageDpi = dpi;
        }

        var scale = dpi / 96.0;
        var margin = (int)Math.Round(ShadowMargin * scale);
        var badge = OverlayGeometry.BadgeFrame(pointer, screen,
            (int)Math.Round(OverlayGeometry.Diameter * scale), (int)Math.Round(OverlayGeometry.Offset * scale));
        Present(_image, new Point(badge.X - margin, badge.Y - margin), alpha);
    }

    // 그림자·배경 원·내용·테두리를 그린 한 장 (macOS BadgeView와 같은 구성)
    private Bitmap Render(int dpi)
    {
        var scale = dpi / 96.0;
        var diameter = (float)(OverlayGeometry.Diameter * scale);
        var margin = (float)(ShadowMargin * scale);
        var side = (int)Math.Ceiling(diameter + 2 * margin);
        var bitmap = new Bitmap(side, side, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var dark = IsDarkTheme();

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var circle = new RectangleF(margin, margin, diameter, diameter);

        // 그림자: 아래로 1px 내려 바깥으로 갈수록 옅어지는 원
        for (var i = (int)margin; i >= 1; i--)
        {
            using var shadow = new SolidBrush(Color.FromArgb((int)(60.0 / margin * (margin - i + 1) / 2), 0, 0, 0));
            var spread = RectangleF.Inflate(circle, i * 0.6f, i * 0.6f);
            spread.Offset(0, (float)scale);
            g.FillEllipse(shadow, spread);
        }

        using (var background = new SolidBrush(dark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.White))
            g.FillEllipse(background, circle);

        if (_content.Picture is { } picture && _content.Fill)
        {
            using var clip = new GraphicsPath();
            clip.AddEllipse(circle);
            g.SetClip(clip);
            g.DrawImage(picture, circle);
            g.ResetClip();
        }
        else if (_content.Picture is { } icon)
        {
            var inset = RectangleF.Inflate(circle, (float)(-5 * scale), (float)(-5 * scale));
            g.DrawImage(icon, Fit(icon.Size, inset));
        }
        else
        {
            var size = (float)((_content.Letter.Length > 1 ? 10 : 13) * scale);
            using var font = new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Pixel);
            using var text = new SolidBrush(dark ? Color.White : Color.FromArgb(0x1F, 0x1F, 0x1F));
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(_content.Letter, font, text, circle, format);
        }

        // 변환 결과를 넣었으면 강조색, 통과면 얇은 회색 테두리 (PRD 4.3)
        var border = _passthrough ? (dark ? Color.FromArgb(0x5A, 0x5A, 0x5A) : Color.FromArgb(0xC8, 0xC8, 0xC8)) : AccentColor();
        using (var pen = new Pen(border, (float)((_passthrough ? 1 : 1.5) * scale)))
            g.DrawEllipse(pen, circle);

        return bitmap;
    }

    private static RectangleF Fit(Size size, RectangleF box)
    {
        if (size.Width <= 0 || size.Height <= 0)
            return box;
        var scale = Math.Min(box.Width / size.Width, box.Height / size.Height);
        var w = size.Width * scale;
        var h = size.Height * scale;
        return new RectangleF(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
    }

    // 레이어드 창에 그림·위치·투명도를 한 번에 반영한다
    private void Present(Bitmap image, Point location, double alpha)
    {
        var screenDc = NativeMethods.GetDC(0);
        var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        var hBitmap = image.GetHbitmap(Color.FromArgb(0));
        var previous = NativeMethods.SelectObject(memoryDc, hBitmap);
        try
        {
            var size = new NativeMethods.SIZE(image.Width, image.Height);
            var source = new NativeMethods.POINT(0, 0);
            var destination = new NativeMethods.POINT(location.X, location.Y);
            var blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = 0,  // AC_SRC_OVER
                SourceConstantAlpha = (byte)Math.Clamp((int)Math.Round(alpha * 255), 0, 255),
                AlphaFormat = 1,  // AC_SRC_ALPHA
            };
            NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, NativeMethods.ULW_ALPHA);
        }
        finally
        {
            NativeMethods.SelectObject(memoryDc, previous);
            NativeMethods.DeleteObject(hBitmap);
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(0, screenDc);
        }
    }

    private Bitmap? IconOf(string? path)
    {
        if (path is null)
            return null;
        if (_icons.TryGetValue(path, out var cached))
            return cached;

        Bitmap? bitmap = null;
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            bitmap = icon?.ToBitmap();
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or ExternalException)
        {
            // 아이콘을 읽지 못하면 첫 글자 배지
        }
        _icons[path] = bitmap;
        return bitmap;
    }

    private static Image? LoadUpBadge()
    {
        using var stream = typeof(CursorOverlay).Assembly.GetManifestResourceStream("ClipBridge.Windows.badge.png");
        return stream is null ? null : new Bitmap(stream);
    }

    private static int DpiAt(Point point)
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT(point.X, point.Y), NativeMethods.MONITOR_DEFAULTTONEAREST);
        return NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0 ? (int)dpiX : 96;
    }

    // Windows 설정의 앱 모드(밝게·어둡게)
    private static bool IsDarkTheme() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is 0;

    // Windows 강조색. 읽지 못하면 기본 파랑
    private static Color AccentColor() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr
            ? Color.FromArgb(0xFF, abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF)
            : Color.FromArgb(0x00, 0x78, 0xD4);

    private static class NativeMethods
    {
        public const int WS_EX_TOPMOST = 0x00000008, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000, WS_EX_NOACTIVATE = 0x08000000;
        public const int ULW_ALPHA = 0x02, SW_HIDE = 0, MONITOR_DEFAULTTONEAREST = 2;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
        public static readonly nint HWND_TOPMOST = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT(int x, int y)
        {
            public int X = x;
            public int Y = y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE(int cx, int cy)
        {
            public int Cx = cx;
            public int Cy = cy;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(nint window, nint destinationDc, ref POINT destination, ref SIZE size,
            nint sourceDc, ref POINT source, int colorKey, ref BLENDFUNCTION blend, int flags);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(nint window, int command);

        [DllImport("user32.dll")]
        public static extern nint GetDC(nint window);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(nint window, nint dc);

        [DllImport("gdi32.dll")]
        public static extern nint CreateCompatibleDC(nint dc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(nint dc);

        [DllImport("gdi32.dll")]
        public static extern nint SelectObject(nint dc, nint gdiObject);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(nint gdiObject);

        [DllImport("user32.dll")]
        public static extern nint MonitorFromPoint(POINT point, int flags);

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}
