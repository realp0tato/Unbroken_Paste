using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Contracts;

public class StubClipConverterTests
{
    private static ClipboardSnapshot Snapshot(string format, byte[] bytes) =>
        new(new Dictionary<string, byte[]> { [format] = bytes }, null, 7);

    [Theory]
    [InlineData(ClipFormats.WinUnicodeText)]
    [InlineData(ClipFormats.MacUtf8Text)]
    public void Prepare_TextFromEitherOs_GivesSameResult(string format)
    {
        var bytes = format == ClipFormats.WinUnicodeText
            ? Encoding.Unicode.GetBytes("a<b\0")
            : Encoding.UTF8.GetBytes("a<b");

        using var prepared = new StubClipConverter().Prepare(Snapshot(format, bytes), ConversionOptions.Default, CancellationToken.None);
        var result = prepared.Get(TargetApp.Notion, ConversionOptions.Default);

        Assert.Equal(7, prepared.SequenceNumber);
        Assert.False(result.IsPassthrough);
        Assert.Equal("[ClipBridge] a<b", result.PlainText);
        Assert.Equal("<p>[ClipBridge] a&lt;b</p>", result.Html);
        Assert.True(prepared.Get(TargetApp.Other, ConversionOptions.Default).IsPassthrough);
    }

    [Fact]
    public void Prepare_NoText_IsPassthrough()
    {
        using var prepared = new StubClipConverter().Prepare(Snapshot("public.png", [1, 2]), ConversionOptions.Default, CancellationToken.None);

        Assert.Equal(SourceKind.Other, prepared.Source);
        Assert.True(prepared.Get(TargetApp.Hangul, ConversionOptions.Default).IsPassthrough);
    }
}
