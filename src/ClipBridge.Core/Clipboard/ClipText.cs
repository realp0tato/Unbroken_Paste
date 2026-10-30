using System.Text;

namespace ClipBridge.Core;

// 포맷별 바이트 → 문자열 변환. 인코딩 처리는 Core 책임이다
internal static class ClipText
{
    public static string? GetPlainText(ClipboardSnapshot snapshot)
    {
        if (snapshot.Formats.TryGetValue(ClipFormats.WinUnicodeText, out var utf16))
            return Encoding.Unicode.GetString(utf16).TrimEnd('\0');
        if (snapshot.Formats.TryGetValue(ClipFormats.MacUtf8Text, out var utf8))
            return Encoding.UTF8.GetString(utf8);
        return null;
    }
}
