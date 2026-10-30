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

    // 실제 로그 (#80 확인 중): Excel에서 다시 복사하면 먼저 클립보드를 비운다 → copy formats=
    [Fact]
    public void EmptyClipboard_NeedsRetry()
    {
        Assert.True(SnapshotRetry.NeedsRetry([]));
        Assert.Equal("(포맷 없음)", SnapshotRetry.Missing([]));
    }

    // 실제 로그: copy seq=2369 app=EXCEL.EXE formats=Biff12,XML Spreadsheet (HTML·텍스트가 아직 목록에 없음)
    [Fact]
    public void ExcelBeforeHtmlIsListed_NeedsRetry()
    {
        (string, bool, string?)[] formats = [("Biff12", true, null), ("XML Spreadsheet", true, null), ("CF_ENHMETAFILE", false, "읽지 않음")];

        Assert.True(SnapshotRetry.NeedsRetry(formats));
        Assert.Equal("HTML Format(목록에 없음),CF_UNICODETEXT(목록에 없음)", SnapshotRetry.Missing(formats));
    }

    // Excel 포맷이 없는 앱은 HTML·텍스트가 없어도 기다리지 않는다
    [Fact]
    public void OtherAppWithoutHtml_NoRetry()
    {
        (string, bool, string?)[] formats = [("CF_UNICODETEXT", true, null), ("CF_LOCALE", false, "읽지 않음")];

        Assert.False(SnapshotRetry.NeedsRetry(formats));
        Assert.Equal("", SnapshotRetry.Missing(formats));
    }
}
