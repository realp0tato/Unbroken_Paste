using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Tables;

public class ExcelHtmlTableParserTests
{
    // Excel이 넣는 HTML 모양: 서식은 .xlNN 클래스, <table>은 StartFragment 밖에 있다
    private const string Style = """
        <style>
        <!--table
        	{mso-displayed-decimal-separator:"\.";}
        .xl65
        	{mso-style-parent:style0;
        	font-weight:700;
        	text-align:center;}
        .xl66
        	{mso-style-parent:style0;
        	font-style:italic;
        	text-align:right;}
        -->
        </style>
        """;

    private static string ExcelHtml(string body) => $"""
        <html xmlns:o="urn:schemas-microsoft-com:office:office">
        <head><meta charset=utf-8>{Style}</head>
        <body link="#0563C1">
        <table border=0 cellpadding=0 cellspacing=0 width=144 style='border-collapse:collapse;width:108pt'>
        <!--StartFragment-->
        {body}
        <!--EndFragment-->
        </table>
        </body></html>
        """;

    [Fact]
    public void Parse_ReadsValuesAndClassStyles()
    {
        var table = ExcelHtmlTableParser.Parse(ExcelHtml("""
            <col width=72 style='width:54pt'>
            <col width=96 style='width:72pt'>
             <tr height=19 style='height:14.25pt'>
              <td height=19 class=xl65 width=72 style='height:14.25pt;width:54pt'>이름</td>
              <td class=xl65 width=96 style='width:72pt'>금액</td>
             </tr>
             <tr height=19 style='height:14.25pt'>
              <td height=19 style='height:14.25pt'>홍길동</td>
              <td class=xl66 align=right x:num="1234.5" x:fmla="=A1*2">1,234.50</td>
             </tr>
            """))!;

        Assert.True(table.IsTable);
        Assert.Equal((2, 2), (table.RowCount, table.ColumnCount));
        Assert.Equal([72, 96], table.ColumnWidths);

        var header = table.Rows[0][0];
        Assert.Equal("이름", header.Text);
        Assert.True(header.Bold);
        Assert.Equal(CellAlign.Center, header.Align);

        var amount = table.Rows[1][1];
        Assert.Equal("1,234.50", amount.Text); // 수식이 아니라 화면 표시 값
        Assert.True(amount.Italic);
        Assert.Equal(CellAlign.Right, amount.Align);

        Assert.False(table.Rows[1][0].Bold);
        Assert.False(table.HasMergedCells);
    }

    [Fact]
    public void Parse_CellLineBreaksAndWhitespace()
    {
        var table = ExcelHtmlTableParser.Parse(ExcelHtml("""
             <tr>
              <td>첫 줄<br style='mso-data-placement:same-cell;' />둘째
              줄</td>
              <td><b>굵게</b></td>
             </tr>
            """))!;

        Assert.Equal("첫 줄\n둘째 줄", table.Rows[0][0].Text);
        Assert.True(table.Rows[0][1].Bold);
    }

    [Fact]
    public void Parse_MergedCells_AndUnmerge()
    {
        var table = ExcelHtmlTableParser.Parse(ExcelHtml("""
             <tr><td colspan=2 class=xl65>제목</td><td rowspan=2>세로</td></tr>
             <tr><td>a</td><td>b</td></tr>
            """))!;

        Assert.True(table.HasMergedCells);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal(
            [(0, 0, "제목"), (0, 2, "세로"), (1, 0, "a"), (1, 1, "b")],
            table.Positions().Select(p => (p.Row, p.Column, p.Cell.Text)));

        var flat = table.Unmerge();
        Assert.False(flat.HasMergedCells);
        Assert.Equal(
            [["제목", "", "세로"], ["a", "b", ""]],
            flat.Rows.Select(row => row.Select(cell => cell.Text).ToArray()).ToArray());
        Assert.True(flat.Rows[0][0].Bold);
    }

    [Fact]
    public void Parse_SingleCell_IsText()
    {
        var table = ExcelHtmlTableParser.Parse(ExcelHtml("<tr><td>메모</td></tr>"))!;

        Assert.False(table.IsTable);
    }

    [Theory]
    [InlineData("<tr><td>a</td><td>b</td></tr><tr><td>c</td></tr>")]                    // 행마다 열 수가 다름
    [InlineData("<tr><td colspan=2>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr>")] // 병합 후 폭이 다름
    [InlineData("<tr><td rowspan=3>a</td><td>b</td></tr><tr><td>c</td></tr>")]          // rowspan이 표 밖으로
    [InlineData("<tr><td><table><tr><td>x</td></tr></table></td><td>b</td></tr>")]      // 중첩 표
    public void Parse_Irregular_IsPassthrough(string body)
    {
        Assert.Null(ExcelHtmlTableParser.Parse(ExcelHtml(body)));
    }

    [Fact]
    public void Parse_NoTable_IsPassthrough()
    {
        Assert.Null(ExcelHtmlTableParser.Parse("<html><body><p>글</p></body></html>"));
    }

    [Fact]
    public void Parse_FromCfHtml()
    {
        var html = ExcelHtml("<tr><td>a</td><td>b</td></tr>");
        var header = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:0000000000\r\nEndFragment:0000000000\r\n";
        var headerLength = string.Format(header, 0, 0).Length;
        var data = Encoding.UTF8.GetBytes(string.Format(header, headerLength, headerLength + Encoding.UTF8.GetByteCount(html)) + html);

        var unwrapped = CfHtml.Unwrap(data)!.Value;
        var table = ExcelHtmlTableParser.Parse(unwrapped.Html)!;

        Assert.Equal(html, unwrapped.Html);
        Assert.Null(unwrapped.SourceUrl);
        Assert.Equal("b", table.Rows[0][1].Text);
    }
}
