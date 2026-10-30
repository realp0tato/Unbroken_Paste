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
        using var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "meta.json")));
        var root = meta.RootElement;

        var formats = new Dictionary<string, byte[]>();
        foreach (var format in root.GetProperty("formats").EnumerateArray())
        {
            // Windows 도구는 {name, file} 객체로, macOS 스크립트는 타입 이름 문자열로 기록한다
            var (name, file) = format.ValueKind == JsonValueKind.String
                ? (format.GetString()!, MacFileName(directory, format.GetString()!))
                : (format.GetProperty("name").GetString()!, format.TryGetProperty("file", out var f) ? f.GetString() : null);
            if (file is not null)
                formats[name] = File.ReadAllBytes(Path.Combine(directory, file));
        }

        var sourceApp = root.TryGetProperty("sourceApp", out var app) ? app.GetString() : null;
        var sequenceNumber = root.TryGetProperty("sequenceNumber", out var seq) ? seq.GetInt64() : 0;
        return new ClipboardSnapshot(formats, sourceApp, sequenceNumber);
    }

    // 저장소에 있는 모든 샘플 (예: "excel/win-basic-table")
    public static IEnumerable<string> All() =>
        Directory.Exists(Root)
            ? Directory.EnumerateFiles(Root, "meta.json", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, Path.GetDirectoryName(path)!).Replace('\\', '/'))
                .Order()
            : [];

    // dump-mac-clipboard.swift 규칙: '/'만 '_'로 바꾸고 텍스트 타입은 .txt, 나머지는 .bin
    private static string MacFileName(string directory, string type)
    {
        var stem = type.Replace('/', '_');
        return new[] { stem + ".txt", stem + ".bin" }.FirstOrDefault(file => File.Exists(Path.Combine(directory, file)))
            ?? throw new FileNotFoundException($"{type} 파일이 없습니다", Path.Combine(directory, stem + ".bin"));
    }
}
