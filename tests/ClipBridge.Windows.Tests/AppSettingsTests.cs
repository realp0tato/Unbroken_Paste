using System.Text.Json.Nodes;
using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("up-settings-");

    private string FilePath => Path.Combine(_directory.FullName, "UnbrokenPaste", "settings.json");

    public void Dispose() => _directory.Delete(recursive: true);

    // PRD 4.2: 처음 실행하면 서식 적용 · AI 검증 켜기
    [Fact]
    public void NoFile_LoadsDefaults() =>
        Assert.Equal(new ConversionOptions(MarkdownMode.Format, AiVerification: true), new AppSettings(FilePath).Load());

    // 재실행 후에도 유지
    [Fact]
    public void SavedOptions_LoadAgain()
    {
        var options = new ConversionOptions(MarkdownMode.Strip, AiVerification: false);

        Assert.True(new AppSettings(FilePath).Save(options));

        Assert.Equal(options, new AppSettings(FilePath).Load());
    }

    // 옵션 값만 저장한다 (NFR-07). 값 이름은 Core 계약과 같다
    [Fact]
    public void FileHoldsOnlyOptionValues()
    {
        new AppSettings(FilePath).Save(new ConversionOptions(MarkdownMode.Strip, AiVerification: false));

        var json = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();

        Assert.Equal(["markdownMode", "aiVerification"], json.Select(pair => pair.Key));
        Assert.Equal("Strip", json["markdownMode"]!.GetValue<string>());
        Assert.False(json["aiVerification"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("""{"markdownMode":"strip","aiVerification":false}""", MarkdownMode.Strip, false)]
    [InlineData("""{"aiVerification":false}""", MarkdownMode.Format, false)]
    [InlineData("""{"markdownMode":"Bold","aiVerification":"yes"}""", MarkdownMode.Format, true)]
    [InlineData("""{"markdownMode":"7"}""", MarkdownMode.Format, true)]
    [InlineData("깨진 파일", MarkdownMode.Format, true)]
    [InlineData("[]", MarkdownMode.Format, true)]
    public void UnreadableValues_FallBackToDefaults(string content, MarkdownMode mode, bool ai)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, content);

        Assert.Equal(new ConversionOptions(mode, ai), new AppSettings(FilePath).Load());
    }

    // 나중에 추가될 값(#44 캐릭터 등)은 옵션을 저장해도 지우지 않는다
    [Fact]
    public void Save_KeepsOtherValues()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{"character":"slime"}""");

        new AppSettings(FilePath).Save(ConversionOptions.Default);

        Assert.Equal("slime", JsonNode.Parse(File.ReadAllText(FilePath))!["character"]!.GetValue<string>());
    }
}
