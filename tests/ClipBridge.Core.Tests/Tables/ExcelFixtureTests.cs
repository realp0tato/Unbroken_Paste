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
