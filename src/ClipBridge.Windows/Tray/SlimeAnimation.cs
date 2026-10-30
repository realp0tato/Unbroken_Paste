using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ClipBridge.Windows;

// 트레이 아이콘의 슬라임 애니메이션 (#44). 시작할 때 프레임을 모두 아이콘으로 만들어 두고 핸들만 바꾼다
internal sealed class SlimeAnimation : IDisposable
{
    private const string ResourcePrefix = "ClipBridge.Windows.Slime.";
    private const double CycleSeconds = 1.5;  // macOS SlimeAnimator.idleCycle과 같은 속도
    private const int MaxFps = 30;

    private readonly NotifyIcon _tray;
    private readonly Icon[] _frames;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 / MaxFps };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _current = -1;

    private SlimeAnimation(NotifyIcon tray, Icon[] frames)
    {
        _tray = tray;
        _frames = frames;
        _timer.Tick += (_, _) => ShowFrame();
        ShowFrame();
        _timer.Start();
    }

    // 프레임을 읽지 못하면 null을 돌려주고, 트레이는 기존 정지 아이콘을 그대로 쓴다
    public static SlimeAnimation? Start(NotifyIcon tray, Size size)
    {
        var frames = LoadFrames(size);
        return frames.Length == 0 ? null : new SlimeAnimation(tray, frames);
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Dispose();
        foreach (var frame in _frames)
            frame.Dispose();
    }

    private void ShowFrame()
    {
        // 경과 시간으로 프레임을 고르므로 타이머가 밀려도 속도가 일정하다
        var phase = _clock.Elapsed.TotalSeconds / CycleSeconds % 1;
        var index = Math.Min((int)(phase * _frames.Length), _frames.Length - 1);
        if (index == _current)
            return;
        _current = index;
        _tray.Icon = _frames[index];
    }

    private static Icon[] LoadFrames(Size size)
    {
        var assembly = typeof(SlimeAnimation).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        var frames = new List<Icon>();
        try
        {
            foreach (var name in names)
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var image = Image.FromStream(stream);
                frames.Add(ToIcon(image, size));
            }
            return [.. frames];
        }
        catch (Exception e) when (e is ArgumentException or ExternalException or OutOfMemoryException)
        {
            foreach (var frame in frames)
                frame.Dispose();
            return [];
        }
    }

    // 32px 프레임을 화면 배율에 맞는 트레이 크기로 줄여 아이콘으로 만든다
    private static Icon ToIcon(Image image, Size size)
    {
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, 0, 0, size.Width, size.Height);
        }

        // Clone은 핸들을 복사하므로 GetHicon이 만든 핸들은 바로 해제한다
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
}
