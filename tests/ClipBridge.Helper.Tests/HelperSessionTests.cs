using System.Text.Json;
using ClipBridge.Core;
using ClipBridge.Helper;

namespace ClipBridge.Helper.Tests;

public class HelperSessionTests
{
    // "a<b"를 UTF-8 Base64로
    private const string Prepare = """{"type":"prepare","id":1,"snapshot":{"formats":{"public.utf8-plain-text":"YTxi"},"sourceApp":"com.apple.Safari","sequenceNumber":42},"options":{"markdownMode":"Strip","aiVerification":true}}""";

    private static async Task<List<JsonElement>> Run(IClipConverter converter, params string[] lines)
    {
        var output = new StringWriter();
        await new HelperSession(converter, output).RunAsync(new StringReader(string.Join('\n', lines)));
        return Parse(output);
    }

    private static List<JsonElement> Parse(StringWriter output) =>
        output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement).ToList();

    private static string Type(JsonElement message) => message.GetProperty("type").GetString()!;

    [Fact]
    public async Task StartsWithReady()
    {
        var messages = await Run(new StubClipConverter());

        Assert.Equal("ready", Type(messages[0]));
        Assert.Equal(HelperSession.ProtocolVersion, messages[0].GetProperty("protocolVersion").GetInt32());
    }

    [Fact]
    public async Task Prepare_SendsResultsForEveryTarget()
    {
        var messages = await Run(new StubClipConverter(), Prepare);
        var results = messages[1];

        Assert.Equal("results", Type(results));
        Assert.Equal(1, results.GetProperty("id").GetInt64());
        Assert.Equal(42, results.GetProperty("sequenceNumber").GetInt64());
        Assert.Equal("PlainText", results.GetProperty("source").GetString());

        var byTarget = results.GetProperty("results");
        Assert.Equal(["Hangul", "Notion", "Word", "Excel", "Other"], byTarget.EnumerateObject().Select(p => p.Name));
        Assert.Equal("[ClipBridge] a<b", byTarget.GetProperty("Hangul").GetProperty("plainText").GetString());
        Assert.True(byTarget.GetProperty("Other").GetProperty("isPassthrough").GetBoolean());
    }

    [Fact]
    public async Task SetOptions_BeforePrepareIsOk_AfterPrepareResendsResults()
    {
        const string setOptions = """{"type":"setOptions","id":2,"options":{"markdownMode":"Format","aiVerification":false}}""";

        var messages = await Run(new StubClipConverter(), setOptions, Prepare, setOptions);

        Assert.Equal(["ready", "ok", "results", "results"], messages.Select(Type));
        Assert.Equal(2, messages[3].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task BadRequests_FailOnlyThatRequest()
    {
        var messages = await Run(new StubClipConverter(),
            "{not json",
            """{"type":"paste","id":5}""",
            """{"type":"prepare","id":6}""",
            Prepare);

        Assert.Equal(["ready", "error", "error", "error", "results"], messages.Select(Type));
        Assert.Equal(JsonValueKind.Null, messages[1].GetProperty("id").ValueKind);
        Assert.Equal("invalid_request", messages[2].GetProperty("code").GetString());
        Assert.Equal(5, messages[2].GetProperty("id").GetInt64());
        Assert.Equal(6, messages[3].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task ConverterException_IsReportedAndSessionContinues()
    {
        var messages = await Run(new ThrowingConverter(), Prepare, """{"type":"shutdown","id":9}""");

        Assert.Equal(["ready", "error", "ok"], messages.Select(Type));
        Assert.Equal("failed", messages[1].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Shutdown_StopsReading()
    {
        var messages = await Run(new StubClipConverter(), """{"type":"shutdown","id":3}""", Prepare);

        Assert.Equal(["ready", "ok"], messages.Select(Type));
    }

    [Fact]
    public async Task VerifiedResult_SendsUpdateOnlyForLatestCopy()
    {
        var converter = new ManualConverter();
        var output = new StringWriter();
        var session = new HelperSession(converter, output);
        var input = new BlockingReader();
        var run = session.RunAsync(input);

        input.Send(Prepare);
        var first = await converter.Prepared.Reader.ReadAsync(TestContext.Current.CancellationToken);
        input.Send(Prepare.Replace("\"id\":1", "\"id\":2"));
        var second = await converter.Prepared.Reader.ReadAsync(TestContext.Current.CancellationToken);

        var verified = new ConversionResult("C5", false, "<h1>B</h1>", "B", []);
        first.SetVerified(TargetApp.Hangul, MarkdownMode.Strip, verified);  // 이전 복사: 무시
        second.SetVerified(TargetApp.Hangul, MarkdownMode.Strip, verified);
        input.Complete();
        await run;

        var messages = Parse(output);
        Assert.Equal(["ready", "results", "results", "results"], messages.Select(Type));
        Assert.Equal(JsonValueKind.Null, messages[3].GetProperty("id").ValueKind);
        Assert.Equal("B", messages[3].GetProperty("results").GetProperty("Hangul").GetProperty("plainText").GetString());
    }

    [Fact]
    public async Task PreparedResults_ExpireAfterTimeout()
    {
        var output = new StringWriter();
        var input = new BlockingReader();
        var run = new HelperSession(new StubClipConverter(), output, TimeSpan.FromMilliseconds(50)).RunAsync(input);

        input.Send(Prepare);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        input.Send("""{"type":"setOptions","id":2,"options":{"markdownMode":"Strip","aiVerification":true}}""");
        input.Complete();
        await run;

        Assert.Equal(["ready", "results", "ok"], Parse(output).Select(Type));
    }

    private sealed class ThrowingConverter : IClipConverter
    {
        public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct) =>
            throw new InvalidOperationException("변환 실패");
    }

    // 만든 PreparedConversions를 테스트가 받아 AI 결과를 넣을 수 있게 한다
    private sealed class ManualConverter : IClipConverter
    {
        public System.Threading.Channels.Channel<PreparedConversions> Prepared { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<PreparedConversions>();

        public PreparedConversions Prepare(ClipboardSnapshot snapshot, ConversionOptions options, CancellationToken ct)
        {
            var prepared = new StubClipConverter().Prepare(snapshot, options, ct);
            Prepared.Writer.TryWrite(prepared);
            return prepared;
        }
    }

    // 테스트가 줄을 하나씩 넣는 표준 입력
    private sealed class BlockingReader : TextReader
    {
        private readonly System.Threading.Channels.Channel<string> _lines =
            System.Threading.Channels.Channel.CreateUnbounded<string>();

        public void Send(string line) => _lines.Writer.TryWrite(line);
        public void Complete() => _lines.Writer.Complete();

        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            await _lines.Reader.WaitToReadAsync(cancellationToken) && _lines.Reader.TryRead(out var line) ? line : null;

        public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();
    }
}
