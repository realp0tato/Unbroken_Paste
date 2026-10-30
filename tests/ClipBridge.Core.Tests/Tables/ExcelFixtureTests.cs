using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Tables;

// tests/fixtures/excel/의 실제 Excel 클립보드 샘플로 확인한다
public class ExcelFixtureTests
{
    private static string Read(string sample, string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "excel", sample, file));

    private static TableModel Html(string sample) => ExcelHtmlTableParser.Parse(Read(sample, "public.html.bin"))!;
    private static TableModel Tsv(string sample) => TsvParser.Parse(Read(sample, "public.utf8-plain-text.txt"))!;

    private static string[][] Texts(TableModel table) =>
        table.Rows.Select(row => row.Select(cell => cell.Text).ToArray()).ToArray();

    [Fact]
    public void MacBasicTable()
    {
        var table = Html("mac-basic-table");

        Assert.Equal(
            [["이름", "부서", "금액"], ["홍길동", "영업", "1,234.50"], ["김철수", "개발\n2팀", "2,469.00"]],
            Texts(table));
        Assert.All(table.Rows[0], cell => Assert.True(cell.Bold));
        Assert.All(table.Rows[0], cell => Assert.Equal(CellAlign.Center, cell.Align));
        Assert.False(table.Rows[1][0].Bold);
        Assert.Equal(CellAlign.Right, table.Rows[1][2].Align);
        Assert.Equal([100, 100, 100], table.ColumnWidths);
        Assert.False(table.HasMergedCells);

        Assert.Equal(Texts(table), Texts(Tsv("mac-basic-table")));
    }

    [Fact]
    public void MacMergedTable()
    {
        var table = Html("mac-merged-table");

        Assert.True(table.HasMergedCells);
        Assert.Equal((4, 3), (table.RowCount, table.ColumnCount));
        Assert.Equal((2, 1), (table.Rows[0][0].ColSpan, table.Rows[0][0].RowSpan));
        Assert.Equal((1, 2), (table.Rows[0][1].ColSpan, table.Rows[0][1].RowSpan));
        Assert.Equal(
            [["상반기 실적", "", "비고"], ["1분기", "2분기", ""], ["100", "200", "증가"], ["150", "120", "감소"]],
            Texts(table.Unmerge()));

        // TSV는 병합 정보 없이 같은 모양의 직사각형
        Assert.Equal(Texts(table.Unmerge()), Texts(Tsv("mac-merged-table")));
    }

    [Fact]
    public void MacSingleColumn_IsText()
    {
        var table = Html("mac-single-column");

        Assert.False(table.IsTable);
        Assert.Equal(
            [["첫째 메모"], [""], ["셋째 줄\n이어지는 줄"], ["5\" 나사 \"표준\""]],
            Texts(table));
        Assert.Equal(Texts(table), Texts(Tsv("mac-single-column")));
    }

    [Fact]
    public void MacFormulaTable_UsesDisplayedValues()
    {
        var table = Html("mac-formula-table");

        Assert.Equal(
            [
                ["품목", "수량", "단가", "금액", "입고일"],
                ["사과", "3", "1,500", "4,500", "2026-10-01"],
                ["배", "2", "3,000", "6,000", "2026-10-15"],
                ["합계", "5", "", "10,500", ""],
                ["배 비율", "", "", "57.1%", ""],
            ],
            Texts(table));
        Assert.True(table.Rows[3][0].Bold);
        Assert.Equal([100, 100, 100, 100, 104], table.ColumnWidths);
        Assert.Equal(Texts(table), Texts(Tsv("mac-formula-table")));
    }
}

// Windows Excel 샘플 (#28): CF_HTML(HTML Format)과 CF_UNICODETEXT(UTF-16LE)
public class WindowsExcelFixtureTests
{
    private static byte[] Read(string sample, string file) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "excel", sample, file));

    private static TableModel Html(string sample) =>
        ExcelHtmlTableParser.Parse(CfHtml.Unwrap(Read(sample, "HTML Format.bin"))!.Value.Html)!;

    private static TableModel Tsv(string sample) =>
        TsvParser.Parse(System.Text.Encoding.Unicode.GetString(Read(sample, "CF_UNICODETEXT.txt")).TrimEnd('\0'))!;

    private static string[][] Texts(TableModel table) =>
        table.Rows.Select(row => row.Select(cell => cell.Text).ToArray()).ToArray();

    [Theory]
    [InlineData("win-basic-table", 5, 3)]
    [InlineData("win-formula-table", 6, 5)]
    [InlineData("win-merged-cells", 5, 4)]
    [InlineData("win-single-column", 6, 1)]
    public void HtmlAndTsv_HaveSameShape(string sample, int rows, int columns)
    {
        var html = Html(sample);
        var tsv = Tsv(sample);

        Assert.Equal((rows, columns), (html.RowCount, html.ColumnCount));
        Assert.Equal((rows, columns), (tsv.RowCount, tsv.ColumnCount));
    }

    [Fact]
    public void WinFormulaTable_UsesDisplayedValues()
    {
        var table = Html("win-formula-table");

        Assert.Equal(["노트북", "1,250,000", "3", "3750000", "64.7%"], Texts(table)[1]);
        Assert.All(table.Rows[0], cell => Assert.True(cell.Bold));
        Assert.Equal(CellAlign.Right, table.Rows[1][1].Align);
        Assert.Equal([70, 87, 70, 70, 70], table.ColumnWidths);
    }

    [Fact]
    public void WinMergedCells()
    {
        var table = Html("win-merged-cells");

        Assert.True(table.HasMergedCells);
        Assert.Equal((4, 1, CellAlign.Center), (table.Rows[0][0].ColSpan, table.Rows[0][0].RowSpan, table.Rows[0][0].Align));
        Assert.Equal("2026년 상반기 실적", table.Rows[0][0].Text);
        Assert.Equal(2, table.Rows[2][0].RowSpan);
        Assert.Equal(Texts(table.Unmerge()), Texts(Tsv("win-merged-cells")));
    }

    [Fact]
    public void WinSingleColumn_IsText()
    {
        var table = Html("win-single-column");

        Assert.False(table.IsTable);
        Assert.Equal("2. 하반기 목표 설정\n담당: 영업팀", table.Rows[2][0].Text);
        Assert.Equal(Texts(table), Texts(Tsv("win-single-column")));
    }
}
