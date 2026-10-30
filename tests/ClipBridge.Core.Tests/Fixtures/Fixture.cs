using System.Text.Json;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Fixtures;

// tests/fixtures 샘플을 ClipboardSnapshot으로 읽는다. 형식은 tests/fixtures/README.md
public static class Fixture
{
    // 빌드 시 tests/fixtures가 출력 폴더의 fixtures/로 복사된다
    public static string Root => Path.Combine(AppContext.BaseDirectory, "fixtures");

    // 예) Fixture.Load("excel/win-basic-table")
    public static ClipboardSnapshot Load(string sample) => LoadFrom(Path.Combine(Root, sample));

    public static ClipboardSnapshot LoadFrom(string directory)
    {
        var metaPath = Path.Combine(directory, "meta.json");
        var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(metaPath), JsonOptions)
            ?? throw new InvalidDataException($"meta.json이 비어 있습니다: {metaPath}");

        var formats = meta.Formats
            .Where(f => f.File is not null)
            .ToDictionary(f => f.Name, f => File.ReadAllBytes(Path.Combine(directory, f.File!)));
        return new ClipboardSnapshot(formats, meta.SourceApp, meta.SequenceNumber);
    }

    // 저장소에 있는 모든 샘플 (예: "excel/win-basic-table")
    public static IEnumerable<string> All() =>
        Directory.Exists(Root)
            ? Directory.EnumerateFiles(Root, "meta.json", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, Path.GetDirectoryName(path)!).Replace('\\', '/'))
                .Order()
            : [];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record Meta(string? SourceApp, long SequenceNumber, IReadOnlyList<MetaFormat> Formats);

    private sealed record MetaFormat(string Name, string? File);
}
