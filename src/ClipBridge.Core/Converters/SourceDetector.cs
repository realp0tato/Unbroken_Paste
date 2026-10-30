using System.Text;
using System.Text.RegularExpressions;

namespace ClipBridge.Core;

// 출처·내용 판별 (PRD 6.1, #13). 위에서부터 먼저 맞는 규칙을 쓴다
public static partial class SourceDetector
{
    // Chrome 계열이 넣는 페이지 주소. Safari는 넣지 않으므로 복사한 앱으로 판별한다 (PRD 6.1)
    public const string MacChromiumSourceUrl = "org.chromium.source-url";

    private static readonly string[] ExcelApps = ["EXCEL.EXE", "com.microsoft.Excel"];
    private static readonly string[] ExcelFormats = [ClipFormats.WinExcelXml, ClipFormats.WinExcelBiff12];

    // 확인된 AI 서비스만 넣는다. ChatGPT·Gemini는 실제 샘플로 확인한 뒤 추가 (PRD 6.1)
    private static readonly string[] AiApps = ["com.anthropic.claudefordesktop"];
    private static readonly string[] AiHosts = ["claude.ai"];

    private static readonly string[] Browsers =
    [
        "chrome.exe", "msedge.exe", "firefox.exe", "whale.exe",
        "com.apple.Safari", "com.google.Chrome", "com.microsoft.edgemac", "org.mozilla.firefox", "com.naver.Whale",
    ];

    public static (SourceKind Source, ContentKind Content) Detect(ClipboardSnapshot snapshot)
    {
        if (IsExcel(snapshot))
            return (SourceKind.Excel, ExcelClipboard.ReadTable(snapshot) is { IsTable: false } ? ContentKind.Text : ContentKind.Table);

        var text = ClipText.GetPlainText(snapshot);
        var html = Html(snapshot, out var sourceUrl);

        // AI 서비스 복사는 Markdown 원문과 서식 HTML을 함께 넣는다 → HTML이 있어도 Markdown 글 (순서 2)
        if (text is not null && IsAiService(snapshot.SourceApp, sourceUrl) && LooksLikeMarkdown(text))
            return (SourceKind.Markdown, MarkdownContent(text));

        if (html is not null && (IsWebUrl(sourceUrl) || IsBrowser(snapshot.SourceApp)))
            return (SourceKind.Web, IsTableOnly(html) ? ContentKind.Table : ContentKind.Text);

        if (html is null && text is not null)
            return LooksLikeMarkdown(text)
                ? (SourceKind.Markdown, MarkdownContent(text))
                : (SourceKind.PlainText, ContentKind.Text);

        return (SourceKind.Other, ContentKind.Text);
    }

    // PRD 6.1: 제목 문법 1개 이상, 또는 Markdown 문법 2개 이상.
    // 구분선(|---|)까지 갖춘 파이프 표는 머리글 줄 + 구분선 줄이라 그것만으로 Markdown으로 본다
    public static bool LooksLikeMarkdown(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Any(line => HeadingLine().IsMatch(line)) || lines.Any(line => TableSeparator().IsMatch(line)))
            return true;
        var hits = 0;
        if (BoldText().IsMatch(text)) hits++;
        if (lines.Any(line => ListLine().IsMatch(line))) hits++;
        if (lines.Any(line => PipeLine().IsMatch(line))) hits++;
        if (text.Contains("```", StringComparison.Ordinal)) hits++;
        return hits >= 2;
    }

    private static bool IsExcel(ClipboardSnapshot snapshot) =>
        ExcelApps.Contains(snapshot.SourceApp)
        || snapshot.Formats.Keys.Any(type => ExcelFormats.Contains(type) || type.StartsWith("com.microsoft.", StringComparison.Ordinal));

    private static bool IsAiService(string? sourceApp, string? sourceUrl) =>
        AiApps.Contains(sourceApp)
        || (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && AiHosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.Ordinal)));

    private static bool IsBrowser(string? sourceApp) => Browsers.Contains(sourceApp, StringComparer.OrdinalIgnoreCase);

    private static bool IsWebUrl(string? url) =>
        url is not null && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    // HTML 본문과 페이지 주소. Windows는 CF_HTML 헤더의 SourceURL, macOS Chrome 계열은 따로 넣은 주소
    private static string? Html(ClipboardSnapshot snapshot, out string? sourceUrl)
    {
        sourceUrl = snapshot.Formats.TryGetValue(MacChromiumSourceUrl, out var url) ? Encoding.UTF8.GetString(url).Trim() : null;
        if (snapshot.Formats.TryGetValue(ClipFormats.MacHtml, out var macHtml))
            return Encoding.UTF8.GetString(macHtml);
        if (snapshot.Formats.TryGetValue(ClipFormats.WinHtml, out var cfHtml) && CfHtml.Unwrap(cfHtml) is { } unwrapped)
        {
            sourceUrl ??= unwrapped.SourceUrl;
            return unwrapped.Html;
        }
        return null;
    }

    // 표 하나만 있으면 표, 표 밖에 글자가 있으면 글 (글 안의 표는 변환기가 표로 바꾼다)
    private static bool IsTableOnly(string html)
    {
        var tables = TableBlock().Matches(html);
        if (tables.Count != 1)
            return false;
        var outside = TableBlock().Replace(html, "");
        return Tag().Replace(outside, "").Replace("&nbsp;", " ").Trim().Length == 0;
    }

    private static ContentKind MarkdownContent(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Where(line => line.Trim().Length > 0).ToList();
        return lines.Count > 0 && lines.All(line => PipeLine().IsMatch(line)) ? ContentKind.Table : ContentKind.Text;
    }

    [GeneratedRegex(@"^#{1,6} \S")]
    private static partial Regex HeadingLine();
    [GeneratedRegex(@"\*\*\S(.*?\S)?\*\*")]
    private static partial Regex BoldText();
    [GeneratedRegex(@"^\s*([-*+]|\d+[.)]) \S")]
    private static partial Regex ListLine();
    [GeneratedRegex(@"^\s*\|.*\|\s*$")]
    private static partial Regex PipeLine();
    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$")]
    private static partial Regex TableSeparator();
    [GeneratedRegex(@"<table\b.*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TableBlock();
    [GeneratedRegex(@"<[^>]*>|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Tag();
}
