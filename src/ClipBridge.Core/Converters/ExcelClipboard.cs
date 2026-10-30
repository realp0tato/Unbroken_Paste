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
    public static TableModel? ReadTable(ClipboardSnapshot snapshot)
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
