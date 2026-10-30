using System.Globalization;
using System.Text;

namespace ClipBridge.Core;

// C1 Excel → 한글 (PRD 6.3.1, #7)
public static class ExcelToHangulConverter
{
    public const string Id = "C1";

    // 표로 읽을 수 없으면 통과
    public static ConversionResult Convert(ClipboardSnapshot snapshot) =>
        ExcelClipboard.ReadTable(snapshot) is { } table ? Convert(table) : ConversionResult.Passthrough;

    public static ConversionResult Convert(TableModel table) =>
        table.IsTable
            ? new ConversionResult(Id, false, TableHtml(table), ExcelClipboard.TablePlainText(table), ExcelClipboard.Signals(table))
            : new ConversionResult(Id, false, ExcelClipboard.TextHtml(table), ExcelClipboard.TextPlainText(table), ExcelClipboard.Signals(table));

    // 한글 기본 본문 폭: A4(210mm) - 좌우 여백 30mm×2 = 150mm
    public const double MaxTableWidthPt = 150 / 25.4 * 72;

    // 셀 값·굵게·기울임·가로 정렬·테두리·열 너비 비율만 남긴 단순 표.
    // Excel의 mso-*·글꼴·배경색·조건부 서식은 모델에 없으므로 저절로 빠진다
    private static string TableHtml(TableModel table)
    {
        // 비율만 주면 한글이 표를 좁게 잡아 글자가 줄바꿈되므로, Excel 너비를 pt로 넣는다
        var widths = ColumnWidthsPt(table);
        var html = new StringBuilder("<table border=\"1\" style=\"border-collapse:collapse");
        if (widths is not null)
            html.Append(";width:").Append(Pt(widths.Sum())).Append("pt");
        html.Append("\">");

        if (widths is not null)
        {
            html.Append("<colgroup>");
            foreach (var width in widths)
                html.Append("<col style=\"width:").Append(Pt(width)).Append("pt\">");
            html.Append("</colgroup>");
        }

        var positions = table.Positions().ToLookup(p => p.Row);
        foreach (var row in positions.OrderBy(group => group.Key))
        {
            html.Append("<tr>");
            foreach (var (_, column, cell) in row)
            {
                html.Append("<td");
                if (cell.ColSpan > 1)
                    html.Append(" colspan=\"").Append(cell.ColSpan).Append('"');
                if (cell.RowSpan > 1)
                    html.Append(" rowspan=\"").Append(cell.RowSpan).Append('"');
                // 여백은 한글 표 셀 기본값. 정렬은 한글 계열이 셀 안 문단에서 읽으므로 <p>에 둔다
                html.Append(" style=\"border:1px solid #000;padding:1.41pt 5.1pt");
                if (widths is not null)
                    html.Append(";width:").Append(Pt(widths.Skip(column).Take(cell.ColSpan).Sum())).Append("pt");
                html.Append("\"><p style=\"margin:0");
                if (cell.Align != CellAlign.None)
                    html.Append(";text-align:").Append(cell.Align.ToString().ToLowerInvariant());
                html.Append("\">").Append(ExcelClipboard.InlineHtml(cell)).Append("</p></td>");
            }
            html.Append("</tr>");
        }
        return html.Append("</table>").ToString();
    }

    // Excel 열 너비(px, 96dpi) → pt. 본문 폭을 넘으면 비율을 유지한 채 줄인다
    private static IReadOnlyList<double>? ColumnWidthsPt(TableModel table)
    {
        if (table.ColumnWidths is not { } widths || widths.Sum() <= 0)
            return null;

        var pt = widths.Select(px => px * 0.75).ToList();
        var scale = Math.Min(1, MaxTableWidthPt / pt.Sum());
        return pt.Select(w => w * scale).ToList();
    }

    private static string Pt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
