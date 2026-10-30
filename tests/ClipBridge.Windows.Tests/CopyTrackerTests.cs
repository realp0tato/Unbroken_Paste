using System.Collections.Concurrent;
using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

public class CopyTrackerTests
{
    private static ClipboardSnapshot Copy(long sequenceNumber, string text) =>
        new(new Dictionary<string, byte[]> { [ClipFormats.WinUnicodeText] = Encoding.Unicode.GetBytes(text + "\0") }, "notepad.exe", sequenceNumber);

    private static string? HangulText(CopyTracker tracker) =>
        tracker.Prepared?.Get(TargetApp.Hangul, ConversionOptions.Default).PlainText;

    [Fact]
    public async Task OnCopied_PreparesResultsInBackground()
    {
        using var tracker = new CopyTracker(new StubClipConverter(), () => ConversionOptions.Default);

        await tracker.OnCopied(Copy(1, "A"));

        Assert.Equal(1, tracker.Snapshot?.SequenceNumber);
        Assert.Equal(StubClipConverter.Marker + "A", HangulText(tracker));
    }

    // 9.1 빠른 연속 복사: A의 준비가 B보다 늦게 끝나도 B의 결과만 남는다
    [Fact]
    public async Task LateResultOfEarlierCopy_IsDiscarded()
    {
        var converter = new GatedConverter();
        using var tracker = new CopyTracker(converter, () => ConversionOptions.Default);

        var a = tracker.OnCopied(Copy(1, "A"));
        converter.WaitStarted(1);
        var b = tracker.OnCopied(Copy(2, "B"));
        converter.Release(2);
        await b;
        converter.Release(1);
        await a;

        Assert.Equal(2, tracker.Snapshot?.SequenceNumber);
        Assert.Equal(StubClipConverter.Marker + "B", HangulText(tracker));
        Assert.True(converter.Prepared[1].Get(TargetApp.Hangul, ConversionOptions.Default).IsPassthrough);
    }

    // 스냅샷을 백그라운드에서 읽으므로 이전 복사가 늦게 도착할 수 있다
    [Fact]
    public async Task OlderCopyArrivingLate_IsIgnored()
    {
        using var tracker = new CopyTracker(new StubClipConverter(), () => ConversionOptions.Default);

        await tracker.OnCopied(Copy(2, "B"));
        await tracker.OnCopied(Copy(1, "A"));

        Assert.Equal(2, tracker.Snapshot?.SequenceNumber);
        Assert.Equal(StubClipConverter.Marker + "B", HangulText(tracker));
    }

    // NFR-08: 결과는 만료되고 원본 스냅샷은 다음 복사까지 남는다
    [Fact]
    public async Task Expiry_ReleasesResultsButKeepsSnapshot()
    {
        using var tracker = new CopyTracker(new StubClipConverter(), () => ConversionOptions.Default, TimeSpan.FromMilliseconds(50));

        await tracker.OnCopied(Copy(1, "A"));
        var prepared = tracker.Prepared!;
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Null(tracker.Prepared);
        Assert.True(prepared.Get(TargetApp.Hangul, ConversionOptions.Default).IsPassthrough);
        Assert.Equal(1, tracker.Snapshot?.SequenceNumber);
    }

    // FR-16: 변환기가 실패하면 결과 없이 원본으로 동작한다
    [Fact]
    public async Task ConverterFailure_LeavesNoResult()
    {
        using var tracker = new CopyTracker(new ThrowingConverter(), () => ConversionOptions.Default);

        await tracker.OnCopied(Copy(1, "A"));

        Assert.Null(tracker.Prepared);
        Assert.Equal(1, tracker.Snapshot?.SequenceNumber);
    }

    // 로그에는 번호·변환기 ID만 남고 복사한 본문은 남지 않는다 (macOS 로그와 같은 규칙)
    [Fact]
    public async Task Log_HasConverterIdsButNoCopiedText()
    {
        var log = new ConcurrentQueue<string>();
        using var tracker = new CopyTracker(new StubClipConverter(), () => ConversionOptions.Default, log: log.Enqueue);

        await tracker.OnCopied(Copy(7, "비밀번호1234"));

        var line = Assert.Single(log);
        Assert.Equal("results seq=7 source=PlainText Hangul=STUB Notion=STUB Word=STUB Other=PASS", line);
        Assert.DoesNotContain("비밀번호", line);
    }

    // 복사마다 Release를 부를 때까지 준비를 끝내지 않는 변환기
    private sealed class GatedConverter : IClipConverter
    {
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _started = new();
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _gates = new();
        public ConcurrentDictionary<long, PreparedConversions> Prepared { get; } = new();

        public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct)
        {
            Signal(_started, snapshot.SequenceNumber).Release();
            Assert.True(Signal(_gates, snapshot.SequenceNumber).Wait(TimeSpan.FromSeconds(5)));
            // 취소돼도 결과를 돌려줘서 CopyTracker가 늦은 결과를 버리는지 확인한다
            return Prepared[snapshot.SequenceNumber] = new StubClipConverter().Prepare(snapshot, options, CancellationToken.None);
        }

        public void WaitStarted(long sequenceNumber) => Assert.True(Signal(_started, sequenceNumber).Wait(TimeSpan.FromSeconds(5)));

        public void Release(long sequenceNumber) => Signal(_gates, sequenceNumber).Release();

        private static SemaphoreSlim Signal(ConcurrentDictionary<long, SemaphoreSlim> map, long key) =>
            map.GetOrAdd(key, _ => new SemaphoreSlim(0));
    }

    private sealed class ThrowingConverter : IClipConverter
    {
        public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct) =>
            throw new InvalidOperationException("변환 실패");
    }
}
