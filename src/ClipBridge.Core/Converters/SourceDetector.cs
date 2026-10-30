using System.Text.RegularExpressions;
using ClipBridge.Core.Web;

namespace ClipBridge.Core;

// 출처·내용 판별 (PRD 6.1, #13). 위에서부터 먼저 맞는 규칙을 쓴다
public static partial class SourceDetector
{
    public const string MacChromiumSourceUrl = WebClipboard.MacChromiumSourceUrl;

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
        var html = WebClipboard.ReadHtml(snapshot, out var sourceUrl);

        // AI 서비스 복사는 Markdown 원문과 서식 HTML을 함께 넣는다 → HTML이 있어도 Markdown 글 (순서 2)
        if (text is not null && IsAiService(snapshot.SourceApp, sourceUrl) && LooksLikeMarkdown(text))
            return (SourceKind.Markdown, MarkdownContent(text));

        if (html is not null && (IsWebUrl(sourceUrl) || IsBrowser(snapshot.SourceApp)))
        {
            // 화면에 Markdown 원문이 글자 그대로 보이는 경우(GitHub 코드·Raw 보기, 굵게를 해석하지 못한 문단 등):
            // HTML에 서식 태그가 없고 글자가 Markdown이면 Markdown 글로 보고 Markdown 처리 옵션을 따른다
            if (text is not null && !FormattingTag().IsMatch(html) && LooksLikeMarkdown(text))
                return (SourceKind.Markdown, MarkdownContent(text));
            return (SourceKind.Web, IsTableOnly(html) ? ContentKind.Table : ContentKind.Text);
        }

        if (html is null && text is not null)
            return LooksLikeMarkdown(text)
                ? (SourceKind.Markdown, MarkdownContent(text))
                : (SourceKind.PlainText, PlainTextConverter.ReadTable(text) is not null ? ContentKind.Table : ContentKind.Text);

        return (SourceKind.Other, ContentKind.Text);
    }

    // PRD 6.1: 제목 문법 1개 이상, 짝이 맞는 **굵게** 1개 이상, 또는 Markdown 문법 2개 이상.
    // 구분선(|---|)까지 갖춘 파이프 표는 머리글 줄 + 구분선 줄이라 그것만으로 Markdown으로 본다
    public static bool LooksLikeMarkdown(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Any(line => HeadingLine().IsMatch(line)) || lines.Any(line => TableSeparator().IsMatch(line)) || BoldText().IsMatch(text))
            return true;
        var hits = 0;
        if (BoldText().IsMatch(text)) hits++;
        if (lines.Any(line => ListLine().IsMatch(line))) hits++;
        if (lines.Any(line => PipeLine().IsMatch(line))) hits++;
        if (text.Contains("```", StringComparison.Ordinal)) hits++;
        return hits >= 2;
    }

    // macOS의 com.microsoft.* 포맷은 Word·PowerPoint도 넣으므로, 그중 복사한 앱 번들 ID(appbundleid)로 Excel만 고른다
    public const string MacOfficeAppBundleId = "com.microsoft.appbundleid";

    internal static bool IsExcel(ClipboardSnapshot snapshot) =>
        ExcelApps.Contains(snapshot.SourceApp)
        || snapshot.Formats.Keys.Any(type => ExcelFormats.Contains(type))
        || (snapshot.Formats.TryGetValue(MacOfficeAppBundleId, out var app)
            && System.Text.Encoding.UTF8.GetString(app).Trim('\0', ' ') == "com.microsoft.Excel");

    private static bool IsAiService(string? sourceApp, string? sourceUrl) =>
        AiApps.Contains(sourceApp)
        || (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && AiHosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.Ordinal)));

    private static bool IsBrowser(string? sourceApp) => Browsers.Contains(sourceApp, StringComparer.OrdinalIgnoreCase);

    private static bool IsWebUrl(string? url) =>
        url is not null && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

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
    // 사이트가 서식을 입힌 HTML이면 웹 글로 다룬다 (이 태그가 하나도 없으면 글자만 있는 HTML)
    [GeneratedRegex(@"<(h[1-6]|strong|b|em|i|del|s|ul|ol|li|table|a|img|blockquote)[\s>/]", RegexOptions.IgnoreCase)]
    private static partial Regex FormattingTag();
}
