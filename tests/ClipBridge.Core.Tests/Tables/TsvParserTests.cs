using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Tables;

public class TsvParserTests
{
    private static string[][] Texts(TableModel table) =>
        table.Rows.Select(row => row.Select(cell => cell.Text).ToArray()).ToArray();

    [Fact]
    public void Parse_BasicTable()
    {
        var table = TsvParser.Parse("이름\t점수\r\n홍길동\t90\r\n")!;

        Assert.True(table.IsTable);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal([["이름", "점수"], ["홍길동", "90"]], Texts(table));
    }

    [Theory]
    [InlineData("a\tb\nc\td")]
    [InlineData("a\tb\rc\td\r")]
    public void Parse_AcceptsLfAndCr(string text)
    {
        Assert.Equal([["a", "b"], ["c", "d"]], Texts(TsvParser.Parse(text)!));
    }

    [Fact]
    public void Parse_QuotedCellWithNewlineAndQuotes()
    {
        var table = TsvParser.Parse("\"첫 줄\n둘째 \"\"줄\"\"\"\tb\r\n")!;

        Assert.Equal([["첫 줄\n둘째 \"줄\"", "b"]], Texts(table));
    }

    [Fact]
    public void Parse_QuoteInsideUnquotedCell_IsKept()
    {
        Assert.Equal([["5\" 나사", "\"x\" y"]], Texts(TsvParser.Parse("5\" 나사\t\"x\" y")!));
    }

    [Fact]
    public void Parse_EmptyCells()
    {
        var table = TsvParser.Parse("a\t\tc\r\n\t\t\r\n")!;

        Assert.Equal([["a", "", "c"], ["", "", ""]], Texts(table));
    }

    [Fact]
    public void Parse_SingleColumn_IsText()
    {
        var table = TsvParser.Parse("첫째\r\n둘째\r\n")!;

        Assert.False(table.IsTable);
        Assert.Equal(1, table.ColumnCount);
    }

    [Fact]
    public void Parse_IrregularRows_IsPassthrough()
    {
        Assert.Null(TsvParser.Parse("a\tb\r\nc\r\n"));
    }

    [Fact]
    public void Parse_Empty_IsPassthrough()
    {
        Assert.Null(TsvParser.Parse(""));
    }

    [Theory]
    [InlineData(33, 1)]   // 열 초과
    [InlineData(2, 501)]  // 행 초과
    [InlineData(20, 251)] // 셀 초과 (5,020)
    public void Parse_TooLarge_IsPassthrough(int columns, int rows)
    {
        var line = string.Join('\t', Enumerable.Repeat("x", columns));
        Assert.Null(TsvParser.Parse(string.Concat(Enumerable.Repeat(line + "\r\n", rows))));
    }

    [Fact]
    public void Parse_AtLimits_IsTable()
    {
        var line = string.Join('\t', Enumerable.Repeat("x", 10));
        Assert.NotNull(TsvParser.Parse(string.Concat(Enumerable.Repeat(line + "\r\n", 500))));
    }
}
