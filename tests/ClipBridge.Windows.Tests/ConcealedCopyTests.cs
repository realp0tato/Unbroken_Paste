namespace ClipBridge.Windows.Tests;

public class ConcealedCopyTests
{
    private static readonly byte[] Zero = BitConverter.GetBytes(0u);
    private static readonly byte[] One = BitConverter.GetBytes(1u);

    private static bool Concealed(params (string Name, byte[]? Data)[] formats) => ConcealedCopy.IsConcealed(formats);

    [Fact]
    public void OrdinaryCopy_IsNotConcealed() =>
        Assert.False(Concealed(("CF_UNICODETEXT", null), ("HTML Format", null), ("CF_LOCALE", null)));

    // KeePass·Bitwarden 등이 넣는 표시
    [Theory]
    [InlineData(ConcealedCopy.ExcludeFromMonitor)]
    [InlineData(ConcealedCopy.ViewerIgnore)]
    public void MarkerFormat_IsConcealed(string marker) =>
        Assert.True(Concealed(("CF_UNICODETEXT", null), (marker, null)));

    // Windows 클립보드 기록·클라우드 동기화에서 빼 달라는 표시는 값이 0일 때만
    [Theory]
    [InlineData(ConcealedCopy.CanIncludeInHistory)]
    [InlineData(ConcealedCopy.CanUploadToCloud)]
    public void ZeroFlag_IsConcealed(string flag)
    {
        Assert.True(Concealed(("CF_UNICODETEXT", null), (flag, Zero)));
        Assert.False(Concealed(("CF_UNICODETEXT", null), (flag, One)));
    }

    // 값을 읽지 못했으면 숨김으로 보지 않는다 (평소대로 변환)
    [Fact]
    public void UnreadableFlag_IsNotConcealed()
    {
        Assert.False(Concealed((ConcealedCopy.CanIncludeInHistory, null)));
        Assert.False(Concealed((ConcealedCopy.CanIncludeInHistory, [0, 0])));
    }

    // 본문 포맷은 읽지 않고 이름만 본다
    [Theory]
    [InlineData(ConcealedCopy.CanIncludeInHistory, true)]
    [InlineData(ConcealedCopy.CanUploadToCloud, true)]
    [InlineData(ConcealedCopy.ExcludeFromMonitor, false)]
    [InlineData("CF_UNICODETEXT", false)]
    [InlineData("HTML Format", false)]
    public void OnlyFlagsNeedData(string format, bool expected) =>
        Assert.Equal(expected, ConcealedCopy.NeedsData(format));
}
