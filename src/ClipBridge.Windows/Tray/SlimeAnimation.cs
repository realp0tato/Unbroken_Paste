using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ClipBridge.Windows;

// 트레이 아이콘의 캐릭터 애니메이션 (#44). 캐릭터의 프레임을 모두 아이콘으로 만들어 두고 핸들만 바꾼다.
// 속도는 SlimeClock(대기·분석 중·복사 반응·완료 효과)이 정한다. UI 스레드에서만 쓴다
internal sealed class SlimeAnimation : IDisposable
{
    private const int MaxFps = 30;

    private readonly NotifyIcon _tray;
    private readonly Size _size;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 / MaxFps };
    private readonly SlimeClock _clock;
    private Icon[] _frames;
    private int _current = -1;

    private SlimeAnimation(NotifyIcon tray, Size size, Icon[] frames)
    {
        _tray = tray;
        _size = size;
        _frames = frames;
        _clock = new SlimeClock(frames.Length);
        _timer.Tick += (_, _) => ShowFrame();
        ShowFrame();
        _timer.Start();
    }

    // 프레임을 읽지 못하면 null을 돌려주고, 트레이는 기존 정지 아이콘을 그대로 쓴다
    public static SlimeAnimation? Start(NotifyIcon tray, Size size, TrayCharacter character)
    {
        var frames = LoadFrames(character, size);
        return frames.Length == 0 ? null : new SlimeAnimation(tray, size, frames);
    }

    public SlimeMode Mode
    {
        get => _clock.Mode;
        set => _clock.Mode = value;
    }

    // 복사하면 잠깐 빠르게 튄다
    public void React() => _clock.React(SlimeClock.CopyReaction, Now);

    public void Celebrate() => _clock.Celebrate(Now);

    // 프레임을 읽지 못하면 false이고 지금 캐릭터를 그대로 둔다
    public bool SetCharacter(TrayCharacter character)
    {
        var frames = LoadFrames(character, _size);
        if (frames.Length == 0)
            return false;

        var old = _frames;
        _frames = frames;
        _clock.FrameCount = frames.Length;
        _current = -1;
        ShowFrame();
        foreach (var frame in old)
            frame.Dispose();
        return true;
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Dispose();
        foreach (var frame in _frames)
            frame.Dispose();
    }

    public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private void ShowFrame()
    {
        // 경과 시간으로 프레임을 고르므로 타이머가 밀려도 속도가 일정하다
        var index = _clock.NextFrame(Now);
        if (index == _current)
            return;
        _current = index;
        _tray.Icon = _frames[index];
    }

    private static Icon[] LoadFrames(TrayCharacter character, Size size) => Load(character, image => ToIcon(image, size));

    // 캐릭터 메뉴 미리보기용 프레임. 읽지 못하면 빈 배열
    public static Bitmap[] LoadImages(TrayCharacter character) => Load(character, image => new Bitmap(image));

    private static T[] Load<T>(TrayCharacter character, Func<Image, T> convert) where T : IDisposable
    {
        var assembly = typeof(SlimeAnimation).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(character.FramePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        var frames = new List<T>();
        try
        {
            foreach (var name in names)
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var image = Image.FromStream(stream);
                frames.Add(convert(image));
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
