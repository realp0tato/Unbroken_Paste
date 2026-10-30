using System.Text.RegularExpressions;

namespace ClipBridge.Core;

// 줄간격 보정 (PRD 6.4, FR-15, #6). 글자는 바꾸지 않고 빈 줄과 문단 간격만 다룬다.
// 각 변환기는 구조(코드 블록·표 셀)를 알고 있으므로 결과를 만들 때 이 규칙을 지키고,
// 매트릭스(UpClipConverter)는 마지막에 Finish로 줄바꿈 통일과 S9 검사만 한다
public static partial class LineSpacing
{
    // 일반 텍스트: 줄바꿈 \n 통일, 공백·탭·NBSP만 있는 줄은 빈 줄, 연속 빈 줄은 1줄, 앞뒤 빈 줄 제거.
    // 코드 블록·표를 모르는 글(TX)에만 쓴다
    public static string NormalizeText(string text)
    {
        var lines = BlankToEmpty(text).Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            if (line.Length == 0 && (kept.Count == 0 || kept[^1].Length == 0))
                continue;
            kept.Add(line);
        }
        if (kept.Count > 0 && kept[^1].Length == 0)
            kept.RemoveAt(kept.Count - 1);
        return string.Join('\n', kept);
    }

    // 줄바꿈을 \n으로 맞추고 공백·탭·NBSP만 있는 줄을 빈 줄로 만든다. 빈 줄 수는 그대로 둔다 (Markdown 해석 전처리)
    public static string BlankToEmpty(string text) =>
        BlankLine().Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "");

    // 매트릭스 마지막 단계: Windows 원본이면 일반 텍스트 줄바꿈을 CRLF로 (대상 앱 기준 줄바꿈),
    // HTML에 빈 문단이 연속으로 남았으면 의심 신호 S9 (PRD 6.6.2)
    public static ConversionResult Finish(ConversionResult result, bool windows)
    {
        if (result.IsPassthrough)
            return result;

        var plain = windows && result.PlainText is { } text ? text.Replace("\r\n", "\n").Replace("\n", "\r\n") : result.PlainText;
        IReadOnlyList<SuspicionSignal> signals = result.Html is { } html && HasEmptyParagraphRun(html)
            ? [.. result.Signals, new SuspicionSignal("S9", 1, "빈 문단 연속")]
            : result.Signals;
        return result with { PlainText = plain, Signals = signals };
    }

    // 표 셀·코드 블록 안은 보지 않는다 (빈 코드 줄은 &nbsp; 문단으로 나온다)
    internal static bool HasEmptyParagraphRun(string html)
    {
        var outside = Preformatted().Replace(html, "");
        return EmptyParagraphRun().IsMatch(outside) || BreakRun().IsMatch(outside);
    }

    // Windows 클립보드 포맷이 있으면 Windows 원본 (Core는 OS를 직접 묻지 않는다)
    public static bool IsWindows(ClipboardSnapshot snapshot) =>
        snapshot.Formats.Keys.Any(type => type is ClipFormats.WinUnicodeText or ClipFormats.WinHtml or ClipFormats.WinRtf);

    [GeneratedRegex(@"^[ \t\u00A0\u3000]+$", RegexOptions.Multiline)]
    private static partial Regex BlankLine();
    [GeneratedRegex(@"<table\b.*?</table>|<pre\b.*?</pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Preformatted();
    [GeneratedRegex(@"(<p\b[^>]*>(\s|&nbsp;|&#160;|\u00A0|<br\s*/?>)*</p>\s*){2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmptyParagraphRun();
    [GeneratedRegex(@"(<br\s*/?>\s*){2,}", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRun();
}
