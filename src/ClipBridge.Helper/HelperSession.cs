using System.Text.Json;
using ClipBridge.Core;

namespace ClipBridge.Helper;

// 요청을 한 줄씩 순서대로 처리한다. 출력은 AI 갱신·만료 타이머와 겹치지 않게 잠금 안에서 쓴다
internal sealed class HelperSession(IClipConverter converter, TextWriter output, TimeSpan? expiry = null)
{
    public const int ProtocolVersion = 1;

    // NFR-08: 준비한 결과는 120초 후 해제
    private readonly TimeSpan _expiry = expiry ?? TimeSpan.FromSeconds(120);
    private readonly Lock _lock = new();
    private ConversionOptions _options = ConversionOptions.Default;
    private PreparedConversions? _prepared;
    private Timer? _expiryTimer;

    public async Task RunAsync(TextReader input)
    {
        lock (_lock)
            Write(new ReadyMessage("ready", ProtocolVersion), HelperJsonContext.Default.ReadyMessage);

        while (await input.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (!Handle(line))
                break;
        }

        lock (_lock)
            Release();
    }

    // false면 종료
    private bool Handle(string line)
    {
        Request? request;
        try
        {
            request = JsonSerializer.Deserialize(line, HelperJsonContext.Default.Request);
        }
        catch (JsonException ex)
        {
            SendError(null, "invalid_request", ex.Message);
            return true;
        }

        var id = request?.Id;
        try
        {
            switch (request?.Type)
            {
                case "prepare":
                    Prepare(request);
                    return true;
                case "setOptions":
                    SetOptions(request);
                    return true;
                case "shutdown":
                    lock (_lock)
                        Write(new OkMessage("ok", id), HelperJsonContext.Default.OkMessage);
                    return false;
                default:
                    SendError(id, "invalid_request", $"알 수 없는 type: {request?.Type}");
                    return true;
            }
        }
        catch (Exception ex)
        {
            // 요청 하나가 실패해도 프로세스는 계속 동작한다
            SendError(id, "failed", ex.Message);
            return true;
        }
    }

    private void Prepare(Request request)
    {
        if (request.Snapshot?.Formats is null)
        {
            SendError(request.Id, "invalid_request", "snapshot.formats가 없습니다");
            return;
        }

        ConversionOptions options;
        lock (_lock)
        {
            // FR-17: 새 복사가 들어오면 이전 결과와 진행 중인 AI 검증을 버린다
            Release();
            if (request.Options is not null)
                _options = request.Options;
            options = _options;
        }

        var prepared = converter.Prepare(request.Snapshot, options, CancellationToken.None);

        lock (_lock)
        {
            _prepared = prepared;
            _expiryTimer = new Timer(_ => Expire(prepared), null, _expiry, Timeout.InfiniteTimeSpan);
            prepared.Updated += _ => OnUpdated(prepared);
            SendResults(request.Id, prepared);
        }
    }

    private void SetOptions(Request request)
    {
        if (request.Options is null)
        {
            SendError(request.Id, "invalid_request", "options가 없습니다");
            return;
        }

        lock (_lock)
        {
            _options = request.Options;
            if (_prepared is null)
                Write(new OkMessage("ok", request.Id), HelperJsonContext.Default.OkMessage);
            else
                SendResults(request.Id, _prepared);
        }
    }

    private void OnUpdated(PreparedConversions prepared)
    {
        lock (_lock)
        {
            if (ReferenceEquals(prepared, _prepared))
                SendResults(null, prepared);
        }
    }

    private void Expire(PreparedConversions prepared)
    {
        lock (_lock)
        {
            if (ReferenceEquals(prepared, _prepared))
                Release();
        }
    }

    // 잠금 안에서 호출
    private void Release()
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;
        _prepared?.Dispose();
        _prepared = null;
    }

    // 잠금 안에서 호출. 앱은 붙여넣기 순간 Helper를 부르지 않으므로 모든 대상의 결과를 한 번에 보낸다
    private void SendResults(long? id, PreparedConversions prepared)
    {
        var results = Enum.GetValues<TargetApp>().ToDictionary(target => target.ToString(), target => prepared.Get(target, _options));
        Write(new ResultsMessage("results", id, prepared.SequenceNumber, prepared.Source, prepared.Content, results),
            HelperJsonContext.Default.ResultsMessage);
    }

    private void SendError(long? id, string code, string message)
    {
        lock (_lock)
            Write(new ErrorMessage("error", id, code, message), HelperJsonContext.Default.ErrorMessage);
    }

    private void Write<T>(T message, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        output.WriteLine(JsonSerializer.Serialize(message, typeInfo));
        output.Flush();
    }
}
