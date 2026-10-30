using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows;

// 앱을 켠 직후 첫 복사는 변환 코드가 처음 실행되며 준비 시간이 200ms를 넘었다 (실측 prepare=215.3ms, 이후 수 ms).
// 시작할 때 백그라운드에서 출처별 작은 예시를 한 번씩 변환해 둔다 (NFR-02). 예시는 코드에 있는 글이고 사용자 복사 내용이 아니다.
// Windows API를 쓰지 않으므로 ClipBridge.Windows.Tests가 이 파일을 그대로 컴파일해 시험한다
internal static class ConverterWarmup
{
    private const string Table = "<table><tr><td><b>항목</b></td><td>값</td></tr><tr><td>가</td><td>1,234.50</td></tr></table>";

    public static IReadOnlyList<ClipboardSnapshot> Samples { get; } =
    [
        // Excel 표 (C1·C2)
        Snapshot("EXCEL.EXE", (ClipFormats.WinHtml, CfHtml.Wrap(Table)), (ClipFormats.WinUnicodeText, Text("항목\t값\r\n가\t1,234.50\r\n"))),
        // 웹 글 (C3·C4·C7, C9는 실행만)
        Snapshot("chrome.exe",
            (ClipFormats.WinHtml, CfHtml.Wrap("<h2>제목</h2><p><b>굵게</b> <a href=\"https://example.com\">링크</a></p><ul><li>목록</li></ul>" + Table, "https://example.com/")),
            (ClipFormats.WinUnicodeText, Text("제목\r\n굵게 링크\r\n목록\r\n"))),
        // Markdown 글 (C5·C6·C8)
        Snapshot("Notepad.exe", (ClipFormats.WinUnicodeText, Text("# 제목\r\n\r\n**굵게**와 `코드`\r\n\r\n- 목록\r\n\r\n| A | B |\r\n|---|---|\r\n| 1 | 2 |\r\n"))),
        // 일반 텍스트 (TX)
        Snapshot("Notepad.exe", (ClipFormats.WinUnicodeText, Text("첫 문단\r\n\r\n\r\n둘째 문단\r\n"))),
    ];

    // 실패해도 앱 동작에는 영향이 없다. 첫 복사가 조금 느릴 뿐이다
    public static void Run(IClipConverter converter, ConversionOptions options)
    {
        foreach (var sample in Samples)
        {
            try
            {
                converter.Prepare(sample, options, CancellationToken.None).Dispose();
            }
            catch (Exception)
            {
            }
        }
    }

    private static byte[] Text(string text) => Encoding.Unicode.GetBytes(text + "\0");

    private static ClipboardSnapshot Snapshot(string app, params (string Name, byte[] Data)[] formats) =>
        new(formats.ToDictionary(format => format.Name, format => format.Data), app, SequenceNumber: 0);
}
