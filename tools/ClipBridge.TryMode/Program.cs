using System.Text;
using System.Text.Json;
using ClipBridge.Core;

namespace ClipBridge.TryMode;

// 개발용 UP 시험 모드의 변환 부분 (#55).
// 클립보드 형식 파일이 든 폴더를 받아 변환하고, 결과를 같은 규칙의 폴더로 쓴다.
//
//   dotnet ClipBridge.TryMode.dll <입력 폴더> <출력 폴더> --target Hangul|Notion|Word --mode Format|Strip [--source-app <번들 ID>]
//
// 종료 코드: 0 변환함, 3 통과(클립보드를 그대로 둠), 1 사용법 오류
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2 || !TryOption(args, "--target", out TargetApp target) || !TryOption(args, "--mode", out MarkdownMode mode))
        {
            Console.Error.WriteLine("사용법: <입력 폴더> <출력 폴더> --target Hangul|Notion|Word --mode Format|Strip [--source-app <번들 ID>]");
            return 1;
        }
        var input = args[0];
        var output = args[1];
        var sourceApp = Value(args, "--source-app");

        var snapshot = new ClipboardSnapshot(ReadFormats(input), sourceApp, 0);
        var (label, result) = Convert(snapshot, target, mode);
        if (result.IsPassthrough)
        {
            Console.WriteLine($"통과 ({label})");
            return 3;
        }

        WriteResult(output, result);
        Console.WriteLine($"{result.ConverterId} 변환 ({label}){SignalText(result)}");
        return 0;
    }

    // 앱과 같은 실제 변환기(UpClipConverter, #14)를 쓴다
    private static (string Label, ConversionResult Result) Convert(ClipboardSnapshot snapshot, TargetApp target, MarkdownMode mode)
    {
        using var prepared = new UpClipConverter().Prepare(snapshot, new ConversionOptions(mode, AiVerification: false), CancellationToken.None);
        var result = prepared.Get(target, new ConversionOptions(mode, AiVerification: false));
        var label = $"{prepared.Source}·{prepared.Content} → {target}, {mode}";
        return (result.IsPassthrough ? label + " — 아직 변환기 없음" : label, result);
    }

    // 파일 이름 규칙은 tests/fixtures와 같다: 타입 이름('/'만 '_')에 .txt 또는 .bin
    private static Dictionary<string, byte[]> ReadFormats(string dir) =>
        Directory.GetFiles(dir)
            .Where(path => Path.GetExtension(path) is ".txt" or ".bin")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), File.ReadAllBytes);

    // 복원 도구(restore-mac-clipboard.swift)가 그대로 읽을 수 있는 모양으로 쓴다. HTML이 앞에 와야 앱이 HTML을 고른다
    private static void WriteResult(string dir, ConversionResult result)
    {
        Directory.CreateDirectory(dir);
        var formats = new List<string>();
        if (result.Html is { } html)
        {
            File.WriteAllText(Path.Combine(dir, ClipFormats.MacHtml + ".bin"), html, new UTF8Encoding(false));
            formats.Add(ClipFormats.MacHtml);
        }
        if (result.PlainText is { } plain)
        {
            File.WriteAllText(Path.Combine(dir, ClipFormats.MacUtf8Text + ".txt"), plain, new UTF8Encoding(false));
            formats.Add(ClipFormats.MacUtf8Text);
        }
        var meta = new Dictionary<string, object> { ["formats"] = formats, ["description"] = $"UP 시험 모드 결과 ({result.ConverterId})" };
        File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(meta));
    }

    private static string SignalText(ConversionResult result) =>
        result.Signals.Count == 0 ? "" : " · 의심 신호 " + string.Join(", ", result.Signals.Select(s => $"{s.Id}({s.Detail})"));

    private static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool TryOption<T>(string[] args, string name, out T value) where T : struct, Enum =>
        Enum.TryParse(Value(args, name), ignoreCase: true, out value);
}
