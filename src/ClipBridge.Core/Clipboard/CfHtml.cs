using System.Globalization;
using System.Text;

namespace ClipBridge.Core;

// Windows CF_HTML 포맷. Core 결과는 순수 HTML이고, Windows 앱이 기록 직전에 Wrap으로 헤더를 붙인다
public static class CfHtml
{
    private const string StartMarker = "<!--StartFragment-->";
    private const string EndMarker = "<!--EndFragment-->";

    // 오프셋은 10자리 고정이라 헤더 길이가 값과 무관하게 일정하다
    private const string HeaderFormat =
        "Version:0.9\r\n" +
        "StartHTML:{0:D10}\r\n" +
        "EndHTML:{1:D10}\r\n" +
        "StartFragment:{2:D10}\r\n" +
        "EndFragment:{3:D10}\r\n";

    // HTML 조각을 CF_HTML 바이트(UTF-8)로 감싼다. 오프셋은 UTF-8 바이트 기준
    public static byte[] Wrap(string fragment, string? sourceUrl = null)
    {
        var sourceLine = sourceUrl is null ? "" : $"SourceURL:{sourceUrl}\r\n";
        var headerLength = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, HeaderFormat, 0, 0, 0, 0))
            + Encoding.UTF8.GetByteCount(sourceLine);

        var prefix = "<html><body>\r\n" + StartMarker;
        var suffix = EndMarker + "\r\n</body></html>";

        var startHtml = headerLength;
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);

        var header = string.Format(CultureInfo.InvariantCulture, HeaderFormat, startHtml, endHtml, startFragment, endFragment);
        return Encoding.UTF8.GetBytes(header + sourceLine + prefix + fragment + suffix);
    }
}
