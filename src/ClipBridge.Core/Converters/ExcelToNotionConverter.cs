using System.Text;

namespace ClipBridge.Core;

// C2 Excel → Notion (PRD 6.3.2, #8)
public static class ExcelToNotionConverter
{
    public const string Id = "C2";

    // 표로 읽을 수 없으면 통과
    public static ConversionResult Convert(ClipboardSnapshot snapshot) =>
        ExcelClipboard.ReadTable(snapshot) is { } table ? Convert(table) : ConversionResult.Passthrough;

    public static ConversionResult Convert(TableModel table) =>
        table.IsTable
            ? new ConversionResult(Id, false, TableHtml(table), ExcelClipboard.TablePlainText(table.Unmerge()), ExcelClipboard.Signals(table))
            : new ConversionResult(Id, false, ExcelClipboard.TextHtml(table), ExcelClipboard.TextPlainText(table), ExcelClipboard.Signals(table));

    // Notion 단순 표는 병합·색·테두리·너비·정렬을 지원하지 않으므로 스타일 없는 표로 쓴다.
    // 병합은 풀어서 첫 칸에 값을 두고, 첫 행은 머리글로 한다
    private static string TableHtml(TableModel table)
    {
        var rows = table.Unmerge().Rows;
        var html = new StringBuilder("<table><thead>");
        AppendRow(html, rows[0], "th");
        html.Append("</thead><tbody>");
        foreach (var row in rows.Skip(1))
            AppendRow(html, row, "td");
        return html.Append("</tbody></table>").ToString();
    }

    private static void AppendRow(StringBuilder html, IReadOnlyList<TableCell> row, string tag)
    {
        html.Append("<tr>");
        foreach (var cell in row)
            html.Append('<').Append(tag).Append('>').Append(ExcelClipboard.InlineHtml(cell)).Append("</").Append(tag).Append('>');
        html.Append("</tr>");
    }
}
