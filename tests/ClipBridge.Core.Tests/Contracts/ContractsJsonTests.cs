using System.Text.Json;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Contracts;

public class ContractsJsonTests
{
    [Fact]
    public void Snapshot_RoundTripsWithBase64Bytes()
    {
        var snapshot = new ClipboardSnapshot(
            new Dictionary<string, byte[]> { [ClipFormats.MacHtml] = [0x3C, 0x70, 0x3E] },
            "com.microsoft.Excel",
            42);

        var json = JsonSerializer.Serialize(snapshot, ContractsJsonContext.Default.ClipboardSnapshot);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.ClipboardSnapshot)!;

        Assert.Contains("\"public.html\":\"PHA+\"", json);
        Assert.Contains("\"sourceApp\":\"com.microsoft.Excel\"", json);
        Assert.Equal(snapshot.Formats[ClipFormats.MacHtml], back.Formats[ClipFormats.MacHtml]);
        Assert.Equal(42, back.SequenceNumber);
    }

    [Fact]
    public void Options_UseStringEnums()
    {
        var json = JsonSerializer.Serialize(new ConversionOptions(MarkdownMode.Format, false), ContractsJsonContext.Default.ConversionOptions);

        Assert.Equal("""{"markdownMode":"Format","aiVerification":false}""", json);
    }

    [Fact]
    public void Result_RoundTrips()
    {
        var result = new ConversionResult("C1", false, "<table></table>", "x", [new("S2", 0, "병합 셀")]);

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.ConversionResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.ConversionResult)!;

        Assert.Equal(result.Html, back.Html);
        Assert.Equal(result.Signals, back.Signals);
    }
}
