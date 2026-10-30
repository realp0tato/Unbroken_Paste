using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClipBridge.Core;
using ClipBridge.Core.Markdown;

namespace ClipBridge.TryMode;

// 개발용 UP 시험 모드의 변환 부분 (#55).
// 클립보드 형식 파일이 든 폴더를 받아 변환하고, 결과를 같은 규칙의 폴더로 쓴다.
// 진짜 출처 판별(#13)·변환기 선택(#14)이 들어오기 전까지 쓰는 임시 선택 규칙이다.
//
//   dotnet ClipBridge.TryMode.dll <입력 폴더> <출력 폴더> --target Hangul|Notion|Word --mode Format|Strip [--source-app <번들 ID>]
//
// 종료 코드: 0 변환함, 3 통과(클립보드를 그대로 둠), 1 사용법 오류
internal static class Program
{
    private static readonly string[] AiServiceApps = ["com.anthropic.claudefordesktop"];

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

    private static (string Label, ConversionResult Result) Convert(ClipboardSnapshot snapshot, TargetApp target, MarkdownMode mode)
    {
        var hasHtml = snapshot.Formats.ContainsKey(ClipFormats.MacHtml);
        var text = snapshot.Formats.TryGetValue(ClipFormats.MacUtf8Text, out var utf8) ? Encoding.UTF8.GetString(utf8) : null;

        if (IsExcel(snapshot))
            return target == TargetApp.Hangul
                ? ("Excel → 한글", ExcelToHangulConverter.Convert(snapshot))
                : ("Excel → 아직 변환기 없음", ConversionResult.Passthrough);

        // AI 서비스 복사는 HTML이 있어도 Markdown 글로 본다 (PRD 6.1 순서 2)
        var fromAi = snapshot.SourceApp is { } app && AiServiceApps.Contains(app);
        if (text is not null && (!hasHtml || fromAi) && LooksLikeMarkdown(text))
        {
            if (target == TargetApp.Hangul)
                return ($"Markdown 글 → 한글, {mode}", MarkdownToHangulConverter.Convert(text, mode));
            var converted = MarkdownEngine.Convert(text, mode);
            var id = target == TargetApp.Notion ? "C6" : "C8";
            return ($"Markdown 글 → {target}, {mode} (공통 엔진)", new ConversionResult(id, false, converted.Html, converted.PlainText, converted.Signals));
        }

        return (hasHtml ? "인터넷 등 → 아직 변환기 없음" : "일반 텍스트 → 아직 변환기 없음", ConversionResult.Passthrough);
    }

    private static bool IsExcel(ClipboardSnapshot snapshot) =>
        snapshot.SourceApp == "com.microsoft.Excel" || snapshot.Formats.Keys.Any(type => type.StartsWith("com.microsoft.", StringComparison.Ordinal));

    // PRD 6.1의 Markdown 판별을 단순하게 흉내 낸다: 제목 문법 1개 이상, 또는 문법 2개 이상
    private static bool LooksLikeMarkdown(string text)
    {
        var lines = text.Split('\n');
        if (lines.Any(line => Regex.IsMatch(line, @"^#{1,6} \S")))
            return true;
        var hits = 0;
        if (Regex.IsMatch(text, @"\*\*\S.*?\S\*\*")) hits++;
        if (lines.Any(line => Regex.IsMatch(line, @"^\s*([-*+]|\d+[.)]) \S"))) hits++;
        if (lines.Any(line => Regex.IsMatch(line, @"^\s*\|.*\|\s*$"))) hits++;
        if (text.Contains("```", StringComparison.Ordinal)) hits++;
        return hits >= 2;
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
