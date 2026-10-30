namespace ClipBridge.Windows.Tests;

// #44: macOS SlimeAnimator와 같은 속도 규칙
public class SlimeClockTests
{
    [Fact]
    public void Idle_OneBounceEveryOnePointFiveSeconds()
    {
        var clock = new SlimeClock(48);
        clock.NextFrame(0);

        Assert.Equal(24, clock.NextFrame(0.75));  // 반 바퀴
        Assert.Equal(0, clock.NextFrame(1.5));
    }

    [Fact]
    public void Busy_FasterCycle()
    {
        var clock = new SlimeClock(48) { Mode = SlimeMode.Busy };
        clock.NextFrame(0);

        Assert.Equal(24, clock.NextFrame(0.3));
    }

    [Fact]
    public void React_UsesBusySpeedOnlyForAWhile()
    {
        var clock = new SlimeClock(48);
        clock.NextFrame(0);
        clock.React(SlimeClock.CopyReaction, now: 0);

        Assert.Equal(24, clock.NextFrame(0.3));   // 반응 중: 0.6초 주기
        clock.NextFrame(1.2);                      // 반응 끝
        var before = clock.NextFrame(1.2);
        Assert.Equal((before + 24) % 48, clock.NextFrame(1.95));  // 다시 1.5초 주기
    }

    [Fact]
    public void NoFrames_ReturnsZero() => Assert.Equal(0, new SlimeClock(0).NextFrame(1));
}
