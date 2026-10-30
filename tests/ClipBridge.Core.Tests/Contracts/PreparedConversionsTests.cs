using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Contracts;

public class PreparedConversionsTests
{
    private static readonly ConversionResult ResultA = new("C5", false, "<p>A</p>", "A", []);
    private static readonly ConversionResult ResultB = new("C5", false, "<h1>B</h1>", "B", []);

    private static PreparedConversions Create() =>
        new(1, SourceKind.Markdown, ContentKind.Text, new Dictionary<PreparedConversions.Key, ConversionResult>
        {
            [new(TargetApp.Hangul, MarkdownMode.Strip)] = ResultA,
        });

    [Fact]
    public void Get_ReturnsPreparedResult()
    {
        using var prepared = Create();

        Assert.Same(ResultA, prepared.Get(TargetApp.Hangul, new(MarkdownMode.Strip, false)));
    }

    [Fact]
    public void Get_MissingCombination_IsPassthrough()
    {
        using var prepared = Create();

        Assert.True(prepared.Get(TargetApp.Other, ConversionOptions.Default).IsPassthrough);
        Assert.True(prepared.Get(TargetApp.Hangul, new(MarkdownMode.Format, false)).IsPassthrough);
    }

    [Fact]
    public void Get_UsesVerifiedResultOnlyWhenAiEnabled()
    {
        using var prepared = Create();
        TargetApp? updated = null;
        prepared.Updated += target => updated = target;

        prepared.SetVerified(TargetApp.Hangul, MarkdownMode.Strip, ResultB);

        Assert.Equal(TargetApp.Hangul, updated);
        Assert.Same(ResultB, prepared.Get(TargetApp.Hangul, new(MarkdownMode.Strip, true)));
        Assert.Same(ResultA, prepared.Get(TargetApp.Hangul, new(MarkdownMode.Strip, false)));
    }

    [Fact]
    public void Dispose_CancelsAiAndReturnsPassthrough()
    {
        var prepared = Create();
        var aiToken = prepared.AiCancellation;

        prepared.Dispose();
        prepared.SetVerified(TargetApp.Hangul, MarkdownMode.Strip, ResultB);

        Assert.True(aiToken.IsCancellationRequested);
        Assert.True(prepared.Get(TargetApp.Hangul, ConversionOptions.Default).IsPassthrough);
    }
}
