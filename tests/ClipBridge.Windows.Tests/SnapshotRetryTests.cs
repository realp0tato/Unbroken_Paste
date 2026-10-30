using ClipBridge.Windows;

namespace ClipBridge.Windows.Tests;

public class SnapshotRetryTests
{
    [Fact]
    public void ExcelBeforeRendering_NeedsRetry()
    {
        // 실제 로그: copy app=EXCEL.EXE formats=Biff12,XML Spreadsheet (HTML·텍스트는 데이터 없음)
        (string, bool, string?)[] formats =
        [
            ("Biff12", true, null), ("XML Spreadsheet", true, null),
            ("HTML Format", false, "데이터 없음"), ("CF_UNICODETEXT", false, "데이터 없음"),
            ("CF_ENHMETAFILE", false, "읽지 않음"),
        ];

        Assert.True(SnapshotRetry.NeedsRetry(formats));
        Assert.Equal("HTML Format(데이터 없음),CF_UNICODETEXT(데이터 없음)", SnapshotRetry.Missing(formats));
    }

    [Fact]
    public void HtmlAndTextRead_NoRetry()
    {
        (string, bool, string?)[] formats = [("HTML Format", true, null), ("CF_UNICODETEXT", true, null), ("Biff12", true, null)];

        Assert.False(SnapshotRetry.NeedsRetry(formats));
    }

    [Fact]
    public void FormatsNotOnClipboard_NoRetry()
    {
        // 이미지만 복사 등: HTML·텍스트가 아예 없으면 기다려도 오지 않는다
        (string, bool, string?)[] formats = [("PNG", false, "읽지 않음"), ("CF_DIB", false, "GDI 핸들 포맷")];

        Assert.False(SnapshotRetry.NeedsRetry(formats));
    }
}
