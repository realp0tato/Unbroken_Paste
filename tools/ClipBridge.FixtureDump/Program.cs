using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClipBridge.Windows;

// 복사 직후 클립보드의 모든 포맷을 tests/fixtures/<출처>/win-<이름>/에 저장한다 (#28)
// 사용법: dotnet run --project tools/ClipBridge.FixtureDump -- <출처> <이름> "<설명>" [--force] [--max-kb N]

string[] sources = ["excel", "web", "markdown", "plain"];
var positional = new List<string>();
var force = false;
long maxKb = 1024;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--force":
            force = true;
            break;
        case "--max-kb" when i + 1 < args.Length && long.TryParse(args[i + 1], out var kb) && kb > 0:
            maxKb = kb;
            i++;
            break;
        default:
            if (args[i].StartsWith("--"))
                return Fail($"알 수 없는 옵션: {args[i]}");
            positional.Add(args[i]);
            break;
    }
}

if (positional.Count is < 2 or > 3)
{
    Console.WriteLine("""
        사용법: dotnet run --project tools/ClipBridge.FixtureDump -- <출처> <이름> "<설명>" [--force] [--max-kb N]

          출처      excel | web | markdown | plain
          이름      영문 소문자·숫자·하이픈 (예: basic-table). 폴더는 win-<이름>으로 만든다
          설명      복사한 내용 설명 (meta.json에 기록)
          --force   같은 이름의 샘플이 있으면 지우고 다시 저장
          --max-kb  포맷 하나의 최대 크기, 넘으면 저장하지 않고 meta.json에만 기록 (기본 1024)

        먼저 원본 앱에서 복사한 뒤 실행한다.
        예) dotnet run --project tools/ClipBridge.FixtureDump -- excel basic-table "3열 5행 표, 머리글 굵게"
        """);
    return 1;
}

var source = positional[0].ToLowerInvariant();
var name = positional[1].StartsWith("win-") ? positional[1][4..] : positional[1];
var description = positional.Count > 2 ? positional[2] : "";

if (!sources.Contains(source))
    return Fail($"출처는 {string.Join(" | ", sources)} 중 하나여야 합니다: {source}");
if (!Regex.IsMatch(name, "^[a-z0-9][a-z0-9-]*$"))
    return Fail($"이름은 영문 소문자·숫자·하이픈만 씁니다: {name}");
if (!OperatingSystem.IsWindows())
    return Fail("Windows에서만 실행할 수 있습니다.");

var fixturesRoot = FindFixturesRoot();
if (fixturesRoot is null)
    return Fail("저장소 루트(ClipBridge.slnx가 있는 폴더) 안에서 실행해 주세요.");

var directory = Path.Combine(fixturesRoot, source, $"win-{name}");
if (Directory.Exists(directory) && !force)
    return Fail($"이미 있는 샘플입니다: {directory}\n다시 저장하려면 --force를 붙이세요.");

var clipboard = ClipboardReader.Read(maxBytesPerFormat: maxKb * 1024);
if (clipboard is null)
    return Fail("클립보드를 열 수 없습니다. 잠시 후 다시 실행해 주세요.");
if (clipboard.Formats.Count == 0)
    return Fail("클립보드가 비어 있습니다. 먼저 원본 앱에서 복사해 주세요.");

// 클립보드를 읽은 뒤에 지워야 실패해도 기존 샘플이 남는다
if (Directory.Exists(directory))
    Directory.Delete(directory, recursive: true);
Directory.CreateDirectory(directory);

var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "meta.json" };
var formats = new List<FormatMeta>();
foreach (var format in clipboard.Formats)
{
    if (format.Data is null)
    {
        formats.Add(new FormatMeta(format.Id, format.Name, null, null, format.SkipReason));
        continue;
    }

    var file = FileName(format, usedFiles);
    File.WriteAllBytes(Path.Combine(directory, file), format.Data);
    formats.Add(new FormatMeta(format.Id, format.Name, file, format.Data.Length, null));
}

var meta = new SampleMeta(
    Os: "windows",
    OsVersion: RuntimeInformation.OSDescription,
    SourceApp: clipboard.OwnerPath is null ? null : Path.GetFileName(clipboard.OwnerPath),
    SourceAppVersion: AppVersion(clipboard.OwnerPath),
    Description: description,
    CapturedAt: DateTimeOffset.UtcNow,
    SequenceNumber: clipboard.SequenceNumber,
    Formats: formats);

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
File.WriteAllText(Path.Combine(directory, "meta.json"), JsonSerializer.Serialize(meta, jsonOptions) + "\n");

Console.WriteLine($"저장: {directory}");
Console.WriteLine($"출처 앱: {meta.SourceApp ?? "(알 수 없음)"} {meta.SourceAppVersion}");
foreach (var format in formats)
    Console.WriteLine(format.File is null
        ? $"  - {format.Name,-40} 건너뜀: {format.Skipped}"
        : $"  - {format.Name,-40} {format.Size,10:N0} bytes");
Console.WriteLine("커밋하기 전에 개인정보가 들어 있지 않은지 확인해 주세요.");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

// 현재 폴더에서 위로 올라가며 ClipBridge.slnx를 찾는다
static string? FindFixturesRoot()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "ClipBridge.slnx")))
            return Path.Combine(dir.FullName, "tests", "fixtures");
    }
    return null;
}

// 파일 이름은 포맷 이름 그대로 쓰고, 쓸 수 없는 문자만 _로 바꾼다. 원래 이름은 meta.json에 남는다
static string FileName(ClipboardReader.Entry format, HashSet<string> used)
{
    var invalid = Path.GetInvalidFileNameChars();
    var safe = new string(format.Name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).TrimEnd('.', ' ');
    var extension = format.Name is "CF_UNICODETEXT" or "CF_TEXT" or "CF_OEMTEXT" ? ".txt" : ".bin";

    var file = safe + extension;
    if (!used.Add(file))
    {
        file = $"{safe}_{format.Id}{extension}";
        used.Add(file);
    }
    return file;
}

static string? AppVersion(string? path)
{
    if (path is null)
        return null;
    try
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return info.ProductVersion ?? info.FileVersion;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        return null;
    }
}

// meta.json 형식은 tests/fixtures/README.md 참고
internal sealed record SampleMeta(
    string Os,
    string OsVersion,
    string? SourceApp,
    string? SourceAppVersion,
    string Description,
    DateTimeOffset CapturedAt,
    long SequenceNumber,
    IReadOnlyList<FormatMeta> Formats);

internal sealed record FormatMeta(uint Id, string Name, string? File, int? Size, string? Skipped);
