using System.Collections.Frozen;
using System.Collections.Immutable;

namespace ClipBridge.Core;

// 복사 1건에 대해 미리 준비한 대상별 결과.
// 앱 껍데기는 다음 복사가 들어오거나 120초가 지나면 Dispose한다 (FR-17, NFR-08).
public sealed class PreparedConversions : IDisposable
{
    internal readonly record struct Key(TargetApp Target, MarkdownMode Mode);

    private readonly FrozenDictionary<Key, ConversionResult> _results;
    private ImmutableDictionary<Key, ConversionResult> _verified = ImmutableDictionary<Key, ConversionResult>.Empty;
    private readonly CancellationTokenSource _aiCts = new();
    private volatile bool _disposed;

    // results: 대상·모드별 결과 A. 없는 조합은 통과로 취급한다
    internal PreparedConversions(
        long sequenceNumber, SourceKind source, ContentKind content,
        IReadOnlyDictionary<Key, ConversionResult> results)
    {
        SequenceNumber = sequenceNumber;
        Source = source;
        Content = content;
        _results = results.ToFrozenDictionary();
    }

    public long SequenceNumber { get; }
    public SourceKind Source { get; }
    public ContentKind Content { get; }

    // AI 검증 결과 B가 채택되면 백그라운드 스레드에서 발생. Helper는 이를 갱신 메시지로 보낸다
    public event Action<TargetApp>? Updated;

    internal CancellationToken AiCancellation => _aiCts.Token;

    // 붙여넣기 순간(키보드 훅 안)에서 호출. 잠금·계산 없이 미리 만든 결과만 돌려준다 (NFR-03)
    public ConversionResult Get(TargetApp target, ConversionOptions options)
    {
        if (_disposed)
            return ConversionResult.Passthrough;

        var key = new Key(target, options.MarkdownMode);
        if (options.AiVerification && _verified.TryGetValue(key, out var verified))
            return verified;
        return _results.TryGetValue(key, out var result) ? result : ConversionResult.Passthrough;
    }

    // AI 검증을 통과한 결과 B 등록 (PRD 6.6.4). A는 덮어쓰지 않는다
    internal void SetVerified(TargetApp target, MarkdownMode mode, ConversionResult result)
    {
        if (_disposed)
            return;

        ImmutableInterlocked.AddOrUpdate(ref _verified, new Key(target, mode), result, (_, _) => result);
        Updated?.Invoke(target);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        // 진행 중인 AI 작업이 토큰을 계속 볼 수 있도록 CTS는 Dispose하지 않고 취소만 한다
        _aiCts.Cancel();
    }
}
