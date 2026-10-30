using System.Text;

namespace ClipBridge.Core.Web;

// 스냅샷의 웹 HTML과 페이지 주소. 출처 판별과 C3·C4·C7이 같이 쓴다
internal static class WebClipboard
{
    // Chrome 계열이 넣는 페이지 주소. Safari는 넣지 않으므로 복사한 앱으로 판별한다 (PRD 6.1)
    public const string MacChromiumSourceUrl = "org.chromium.source-url";

    // NFR-04: 넘으면 통과
    public const int MaxHtmlBytes = 1024 * 1024;

    // HTML 본문과 페이지 주소. Windows는 CF_HTML 헤더의 SourceURL, macOS Chrome 계열은 따로 넣은 주소
    public static string? ReadHtml(ClipboardSnapshot snapshot, out string? sourceUrl)
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

    public static bool IsTooLarge(ClipboardSnapshot snapshot) =>
        (snapshot.Formats.TryGetValue(ClipFormats.MacHtml, out var mac) && mac.Length > MaxHtmlBytes)
        || (snapshot.Formats.TryGetValue(ClipFormats.WinHtml, out var win) && win.Length > MaxHtmlBytes);
}
