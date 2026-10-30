using System.Net;
using System.Text;

namespace ClipBridge.Core;

// Excel 스냅샷 → 표 모델. C1·C2가 같이 쓴다
internal static class ExcelClipboard
{
    // NFR-04: 넘으면 통과
    public const int MaxHtmlBytes = 1024 * 1024;
    public const int MaxTextLength = 100_000;

    // Excel HTML이 있으면 서식까지 읽고, 없으면 TSV로 값만 읽는다.
    // HTML이 있는데 표로 읽을 수 없으면(불규칙·중첩·크기 초과) TSV로 바꾸지 않고 통과한다 (PRD 6.3.10)
    public static TableModel? ReadTable(ClipboardSnapshot snapshot) =>
        Parse(snapshot) is { } table ? ClearBlankPlaceholders(table) : null;

    private static TableModel? Parse(ClipboardSnapshot snapshot)
    {
        if (snapshot.Formats.TryGetValue(ClipFormats.MacHtml, out var macHtml))
            return macHtml.Length > MaxHtmlBytes ? null : ExcelHtmlTableParser.Parse(Encoding.UTF8.GetString(macHtml));

        if (snapshot.Formats.TryGetValue(ClipFormats.WinHtml, out var cfHtml))
            return cfHtml.Length > MaxHtmlBytes || CfHtml.Unwrap(cfHtml) is not { } unwrapped
                ? null
                : ExcelHtmlTableParser.Parse(unwrapped.Html);

        var text = ClipText.GetPlainText(snapshot);
        return text is null || text.Length > MaxTextLength ? null : TsvParser.Parse(text);
    }

    // 위키백과 등에서 복사한 표를 Excel에 붙이면 빈 칸 표시 글자("빈칸")가 셀 값으로 남는다.
    // 웹 정리(#9)가 지우는 것과 맞춰, 값이 정확히 "빈칸"인 셀만 비운다
    public const string BlankPlaceholder = "빈칸";

    private static TableModel ClearBlankPlaceholders(TableModel table)
    {
        if (!table.Rows.Any(row => row.Any(IsBlankPlaceholder)))
            return table;

        var rows = table.Rows
            .Select(row => (IReadOnlyList<TableCell>)row.Select(cell => IsBlankPlaceholder(cell) ? cell with { Text = "" } : cell).ToList())
            .ToList();
        return TableModel.Create(rows, table.ColumnWidths)!;
    }

    private static bool IsBlankPlaceholder(TableCell cell) => cell.Text.Trim() == BlankPlaceholder;

    // 글(단일 열): 셀마다 한 문단. 빈 셀은 문단을 만들지 않는다 (C1·C2)
    public static string TextHtml(TableModel table) =>
        string.Concat(Cells(table).Where(cell => cell.Text.Length > 0).Select(cell => $"<p>{InlineHtml(cell)}</p>"));

    // 일반 텍스트 표: 칸은 탭, 행은 줄바꿈. 셀 안 줄바꿈은 표 모양이 깨지지 않게 공백으로
    public static string TablePlainText(TableModel table) =>
        string.Join('\n', table.Rows.Select(row => string.Join('\t', row.Select(cell => cell.Text.Replace('\n', ' ')))));

    public static string TextPlainText(TableModel table) =>
        string.Join('\n', Cells(table).Where(cell => cell.Text.Length > 0).Select(cell => cell.Text));

    // 셀 안 줄바꿈은 <br>, 굵게·기울임은 태그로
    public static string InlineHtml(TableCell cell)
    {
        var text = string.Join("<br>", cell.Text.Split('\n').Select(WebUtility.HtmlEncode));
        if (text.Length == 0)
            return text;
        if (cell.Italic)
            text = $"<i>{text}</i>";
        if (cell.Bold)
            text = $"<b>{text}</b>";
        return text;
    }

    private static IEnumerable<TableCell> Cells(TableModel table) => table.Rows.SelectMany(row => row);

    // S2 표 모양 불규칙(병합 셀)·S7 빈 셀 과다 (PRD 6.6.2)
    public static IReadOnlyList<SuspicionSignal> Signals(TableModel table)
    {
        var signals = new List<SuspicionSignal>();
        if (table.HasMergedCells)
            signals.Add(new SuspicionSignal("S2", 0, "병합 셀"));

        var cells = table.Rows.SelectMany(row => row).ToList();
        var empty = cells.Count(cell => cell.Text.Length == 0);
        if (table.IsTable && empty * 10 > cells.Count * 3)
            signals.Add(new SuspicionSignal("S7", 1, $"빈 셀 {empty}/{cells.Count}"));
        return signals;
    }
}
