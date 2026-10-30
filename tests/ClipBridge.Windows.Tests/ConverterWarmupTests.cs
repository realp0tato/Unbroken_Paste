using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

// 시작할 때 미리 돌리는 예시가 실제 변환기 경로를 모두 지나는지 (첫 복사 준비 시간, NFR-02)
public class ConverterWarmupTests
{
    [Fact]
    public void Samples_CoverEverySourceAndConverter()
    {
        var converter = new UpClipConverter();
        var converters = new HashSet<string>();
        var sources = new HashSet<SourceKind>();
        foreach (var sample in ConverterWarmup.Samples)
        {
            using var prepared = converter.Prepare(sample, ConversionOptions.Default, CancellationToken.None);
            sources.Add(prepared.Source);
            foreach (var target in Enum.GetValues<TargetApp>())
                converters.Add(prepared.Get(target, ConversionOptions.Default).ConverterId);
        }

        Assert.Equal([SourceKind.Excel, SourceKind.Web, SourceKind.Markdown, SourceKind.PlainText], sources.Order());
        // C9(인터넷 → Excel)는 지울 찌꺼기가 없으면 통과를 돌려주지만 코드는 웹 예시에서 실행된다
        foreach (var id in new[] { "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "TX" })
            Assert.Contains(id, converters);
    }

    [Fact]
    public void Run_DoesNotThrow() => ConverterWarmup.Run(new UpClipConverter(), ConversionOptions.Default);
}
