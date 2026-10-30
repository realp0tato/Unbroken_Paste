using System.Text.Json;
using System.Text.Json.Nodes;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 트레이 메뉴의 캐릭터·복사 반응 (#44, PRD 4.2.2). macOS Settings와 같은 이름("character", "reactOnCopy")
internal sealed record TrayPreferences(string Character, bool ReactOnCopy)
{
    public static TrayPreferences Default { get; } = new(TrayCharacter.DefaultId, ReactOnCopy: true);
}

// 메뉴 옵션을 %APPDATA%\UnbrokenPaste\settings.json에 저장한다 (PRD 4.2, 4.2.2, FR-14, FR-27).
// 옵션 값만 저장하고 복사 본문은 저장하지 않는다 (NFR-07). 값은 Core 계약과 같은 이름("Format"/"Strip")이다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal sealed class AppSettings(string path)
{
    private const string MarkdownModeKey = "markdownMode";
    private const string AiVerificationKey = "aiVerification";
    private const string CharacterKey = "character";
    private const string ReactOnCopyKey = "reactOnCopy";

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnbrokenPaste", "settings.json");

    public string FilePath { get; } = path;

    // 파일이 없거나 읽을 수 없는 값은 기본값(서식 적용, AI 검증 켜기)으로 둔다
    public ConversionOptions Load()
    {
        var defaults = ConversionOptions.Default;
        var json = ReadObject();
        if (json is null)
            return defaults;

        // 대소문자를 가리지 않는다 (macOS Settings와 같음)
        var markdown = json[MarkdownModeKey] is JsonValue mode && mode.TryGetValue(out string? name)
            && Enum.TryParse<MarkdownMode>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : defaults.MarkdownMode;
        var ai = json[AiVerificationKey] is JsonValue value && value.TryGetValue(out bool on) ? on : defaults.AiVerification;
        return new ConversionOptions(markdown, ai);
    }

    // 저장에 실패하면 false. 옵션은 메모리에서 계속 쓰이고 다음 실행 때만 기본값으로 돌아간다
    public bool Save(ConversionOptions options) => Write(json =>
    {
        json[MarkdownModeKey] = options.MarkdownMode.ToString();
        json[AiVerificationKey] = options.AiVerification;
    });

    // 모르는 캐릭터는 기본 슬라임 (TrayCharacter.Find)
    public TrayPreferences LoadTray()
    {
        var defaults = TrayPreferences.Default;
        var json = ReadObject();
        if (json is null)
            return defaults;

        var character = json[CharacterKey] is JsonValue id && id.TryGetValue(out string? name) ? TrayCharacter.Find(name).Id : defaults.Character;
        var react = json[ReactOnCopyKey] is JsonValue value && value.TryGetValue(out bool on) ? on : defaults.ReactOnCopy;
        return new TrayPreferences(character, react);
    }

    public bool Save(TrayPreferences preferences) => Write(json =>
    {
        json[CharacterKey] = preferences.Character;
        json[ReactOnCopyKey] = preferences.ReactOnCopy;
    });

    // 다른 값을 지우지 않도록 기존 내용 위에 바꿀 값만 바꾼다
    private bool Write(Action<JsonObject> change)
    {
        var json = ReadObject() ?? [];
        change(json);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // 쓰는 도중 앱이 꺼져도 파일이 깨지지 않게 임시 파일에 쓴 뒤 바꿔 넣는다
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private JsonObject? ReadObject()
    {
        try
        {
            return File.Exists(FilePath) ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
