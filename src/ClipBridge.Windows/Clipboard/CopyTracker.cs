using System.Diagnostics;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 가장 최근 복사 1건의 원본 스냅샷과 미리 준비한 변환 결과를 관리한다 (FR-03, FR-17, NFR-08, PRD 6.5).
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다.
// log에는 번호·변환기 ID만 넘기고 복사한 본문은 넘기지 않는다
internal sealed class CopyTracker(
    IClipConverter converter, Func<ConversionOptions> options, TimeSpan? expiry = null, Action<string>? log = null) : IDisposable
{
    // NFR-08: 준비한 결과는 120초 후 해제하고, 스냅샷은 다음 복사까지 둔다
    private readonly TimeSpan _expiry = expiry ?? TimeSpan.FromSeconds(120);
    private readonly Lock _lock = new();
    private ClipboardSnapshot? _snapshot;
    // _snapshot의 모든 포맷 (원본 되돌리기용, #80). 아직 읽는 중이면 null
    private IReadOnlyDictionary<string, byte[]>? _original;
    private PreparedConversions? _prepared;
    private CancellationTokenSource? _preparing;
    // 지금까지 본 가장 최근 복사의 번호 (숨김 복사 포함)
    private long _latest = long.MinValue;
    private System.Threading.Timer? _expiryTimer;

    // 가장 최근 복사의 원본. 통과할 때 되돌려 기록한다 (#3)
    public ClipboardSnapshot? Snapshot
    {
        get { lock (_lock) return _snapshot; }
    }

    // 결과 준비가 끝나 Prepared가 바뀌었을 때. 백그라운드 스레드에서 발생한다 (앞 창 미리 쓰기, #26)
    public event Action? ResultsReady;

    // 원본을 되돌릴 때 쓸 포맷. 모든 포맷을 아직 읽지 못했거나 snapshot이 지금 복사가 아니면 snapshot의 포맷 (#80)
    public IReadOnlyDictionary<string, byte[]> OriginalOf(ClipboardSnapshot snapshot)
    {
        lock (_lock)
            return ReferenceEquals(snapshot, _snapshot) && _original is not null ? _original : snapshot.Formats;
    }

    // ClipboardMonitor가 Copied 뒤에 읽은 모든 포맷. 그사이 새 복사가 왔으면 버린다.
    // 메모리에만 두고 다음 복사 때 버린다 (NFR-07)
    public void SetOriginal(long sequenceNumber, IReadOnlyDictionary<string, byte[]> formats)
    {
        lock (_lock)
        {
            if (_snapshot?.SequenceNumber == sequenceNumber)
                _original = formats;
        }
    }

    // 붙여넣기 때 쓸 결과. 아직 준비 중이거나 만료됐으면 null이고, 그때는 원본으로 동작한다 (FR-16)
    public PreparedConversions? Prepared
    {
        get { lock (_lock) return _prepared; }
    }

    // 새 복사. 이전 결과와 진행 중인 준비를 버리고 백그라운드에서 준비를 시작한다.
    // 반환한 Task는 준비가 끝나면 완료된다 (시험용)
    public Task OnCopied(ClipboardSnapshot snapshot)
    {
        CancellationTokenSource preparing;
        lock (_lock)
        {
            // 스냅샷을 백그라운드에서 읽으므로 이전 복사가 늦게 도착할 수 있다.
            // Excel은 번호가 같은 채로 포맷을 더 올리기도 하므로, 같은 번호면 포맷이 더 많은 스냅샷으로 바꾼다
            if (snapshot.SequenceNumber < _latest
                || (snapshot.SequenceNumber == _latest && snapshot.Formats.Count <= (_snapshot?.Formats.Count ?? int.MaxValue)))
            {
                log?.Invoke($"ignored older copy seq={snapshot.SequenceNumber} current={_latest}");
                return Task.CompletedTask;
            }

            _latest = snapshot.SequenceNumber;
            ReleasePrepared();
            _preparing?.Cancel();
            _snapshot = snapshot;
            _original = null;
            _preparing = preparing = new CancellationTokenSource();
        }
        return Task.Run(() => Prepare(snapshot, preparing.Token));
    }

    // 숨김 표시가 된 복사(#78). 이전 복사의 스냅샷과 결과를 버려, 그 뒤 붙여넣기는 클립보드 내용이 그대로 붙는다
    // (macOS ClipboardMonitor가 snapshot을 nil로 두는 것과 같음)
    public void Discard(long sequenceNumber)
    {
        lock (_lock)
        {
            if (sequenceNumber <= _latest)
                return;

            _latest = sequenceNumber;
            _preparing?.Cancel();
            _preparing = null;
            ReleasePrepared();
            _snapshot = null;
            _original = null;
        }
        log?.Invoke($"discarded seq={sequenceNumber}");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _preparing?.Cancel();
            ReleasePrepared();
            _snapshot = null;
            _original = null;
        }
    }

    private void Prepare(ClipboardSnapshot snapshot, CancellationToken ct)
    {
        PreparedConversions prepared;
        // 결과 준비 시간 (NFR-02 측정용)
        var started = Stopwatch.GetTimestamp();
        try
        {
            prepared = converter.Prepare(snapshot, options(), ct);
        }
        catch (Exception ex)
        {
            // 취소됐거나 변환기가 실패하면 결과를 두지 않는다. 붙여넣기는 원본으로 동작한다 (FR-16)
            log?.Invoke(ex is OperationCanceledException
                ? $"prepare canceled seq={snapshot.SequenceNumber}"
                : $"prepare failed seq={snapshot.SequenceNumber} error={ex.GetType().Name}");
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        lock (_lock)
        {
            // FR-17: A→B 연속 복사에서 늦게 끝난 A의 결과는 버린다
            if (!ReferenceEquals(snapshot, _snapshot) || ct.IsCancellationRequested)
            {
                prepared.Dispose();
                log?.Invoke($"dropped late results seq={snapshot.SequenceNumber} current={_snapshot?.SequenceNumber}");
                return;
            }

            _prepared = prepared;
            _expiryTimer = new System.Threading.Timer(_ => Expire(prepared), null, _expiry, Timeout.InfiniteTimeSpan);
            if (log is not null)
            {
                var current = options();
                var summary = string.Join(" ", Enum.GetValues<TargetApp>().Select(target => $"{target}={prepared.Get(target, current).ConverterId}"));
                log($"results seq={prepared.SequenceNumber} source={prepared.Source} {summary} prepare={elapsed:0.0}ms");
            }
        }
        ResultsReady?.Invoke();
    }

    private void Expire(PreparedConversions prepared)
    {
        lock (_lock)
        {
            if (ReferenceEquals(prepared, _prepared))
            {
                ReleasePrepared();
                log?.Invoke($"results expired seq={prepared.SequenceNumber}");
            }
        }
    }

    // 잠금 안에서 호출
    private void ReleasePrepared()
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;
        _prepared?.Dispose();
        _prepared = null;
    }
}
