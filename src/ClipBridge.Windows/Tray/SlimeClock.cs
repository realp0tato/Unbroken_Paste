namespace ClipBridge.Windows;

internal enum SlimeMode { Idle, Busy }

// 트레이 슬라임이 보여 줄 프레임 (#44). macOS SlimeAnimator와 같은 규칙:
// 경과 시간만큼 위상을 넘기므로 속도가 바뀌어도 끊기지 않는다. 대기 1.5초, 분석 중 0.6초에 한 번 튄다.
// 복사 반응·완료 효과는 잠깐 분석 중 속도로 튄다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal sealed class SlimeClock(int frameCount)
{
    public const double IdleCycle = 1.5;
    public const double BusyCycle = 0.6;
    public const double CopyReaction = 1.2;

    private double _phase;
    private double? _last;
    private double _fastUntil = double.NegativeInfinity;

    public int FrameCount { get; set; } = frameCount;
    public SlimeMode Mode { get; set; } = SlimeMode.Idle;

    // now: 초 단위 단조 시간
    public void React(double seconds, double now) => _fastUntil = Math.Max(_fastUntil, now + seconds);

    // 맞춤 완료 효과 보기 (시연용): 빠르게 두 번 튄다
    public void Celebrate(double now) => React(2 * BusyCycle, now);

    public int NextFrame(double now)
    {
        if (FrameCount <= 0)
            return 0;
        var elapsed = _last is { } last ? now - last : 0;
        _last = now;
        _phase = (_phase + elapsed / Cycle(now)) % 1;
        return Math.Min((int)(_phase * FrameCount), FrameCount - 1);
    }

    private double Cycle(double now) => now < _fastUntil || Mode == SlimeMode.Busy ? BusyCycle : IdleCycle;
}
