using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 트레이 메뉴의 최근 복사 (#44, PRD 4.2.2). macOS ClipboardMonitor.history와 같은 규칙:
// 텍스트가 있는 복사만, 최신이 위로, 같은 글은 위로 옮기고, 10개까지. 메모리에만 두고 디스크에 쓰지 않는다 (NFR-07).
// 숨김 복사와 UP 자신의 기록은 ClipboardMonitor가 Copied로 올리지 않으므로 여기에 오지 않는다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal sealed class ClipboardHistory
{
    public const int MaxItems = 10;

    private readonly List<string> _items = [];

    public IReadOnlyList<string> Items => _items;

    public void Remember(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        _items.Remove(text);
        _items.Insert(0, text);
        if (_items.Count > MaxItems)
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
    }

    public void Clear() => _items.Clear();

    // 복사 스냅샷의 텍스트 (CF_UNICODETEXT, 끝의 \0 제외)
    public static string? TextOf(ClipboardSnapshot snapshot) =>
        snapshot.Formats.TryGetValue(ClipFormats.WinUnicodeText, out var bytes) ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : null;

    // 메뉴에 보일 한 줄: 공백·줄바꿈을 한 칸으로 줄이고 28자에서 자른다 (macOS preview와 같음)
    public static string Preview(string text, int limit = 28)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var chars = line.EnumerateRunes().ToList();
        return chars.Count <= limit ? line : string.Concat(chars.Take(limit)) + "…";
    }
}
