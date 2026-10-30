namespace ClipBridge.Core;

public enum CellAlign { None, Left, Center, Right }

// 셀 하나. Text는 화면 표시 값이고 셀 안 줄바꿈은 \n
public sealed record TableCell(
    string Text,
    int ColSpan = 1,
    int RowSpan = 1,
    bool Bold = false,
    bool Italic = false,
    CellAlign Align = CellAlign.None)
{
    public static TableCell Empty { get; } = new("");
}

// Excel HTML·TSV·웹 표가 공통으로 쓰는 표 모델 (PRD 6.3.10).
// Rows는 HTML 표처럼 실제 셀만 담고, 병합으로 가려진 칸은 빠진다
public sealed class TableModel
{
    // NFR-05: 하나라도 넘으면 통과
    public const int MaxColumns = 32;
    public const int MaxRows = 500;
    public const int MaxCells = 5000;

    internal TableModel(IReadOnlyList<IReadOnlyList<TableCell>> rows, int columnCount, IReadOnlyList<int>? columnWidths)
    {
        Rows = rows;
        ColumnCount = columnCount;
        ColumnWidths = columnWidths;
        HasMergedCells = rows.Any(row => row.Any(cell => cell.ColSpan > 1 || cell.RowSpan > 1));
    }

    public IReadOnlyList<IReadOnlyList<TableCell>> Rows { get; }
    public int RowCount => Rows.Count;
    public int ColumnCount { get; }

    // 열 너비(px). Excel HTML의 <col width>에서 읽고, 없거나 열 수와 맞지 않으면 null
    public IReadOnlyList<int>? ColumnWidths { get; }

    // 의심 신호 S2에 쓴다 (PRD 6.6.2)
    public bool HasMergedCells { get; }

    // 열 2개 이상 → 표, 1열(단일 셀 포함) → 글 (PRD 6.1)
    public bool IsTable => ColumnCount >= 2;

    // 병합을 풀어 모든 칸이 1×1인 표로 만든다. 값은 첫 칸에 두고 나머지는 빈칸 (C2·C4)
    public TableModel Unmerge()
    {
        if (!HasMergedCells)
            return this;

        var grid = new TableCell[RowCount, ColumnCount];
        foreach (var (rowIndex, colIndex, cell) in Positions())
        {
            for (var r = 0; r < cell.RowSpan; r++)
            for (var c = 0; c < cell.ColSpan; c++)
                grid[rowIndex + r, colIndex + c] = r == 0 && c == 0 ? cell with { ColSpan = 1, RowSpan = 1 } : TableCell.Empty;
        }

        var rows = new List<IReadOnlyList<TableCell>>(RowCount);
        for (var r = 0; r < RowCount; r++)
        {
            var row = new TableCell[ColumnCount];
            for (var c = 0; c < ColumnCount; c++)
                row[c] = grid[r, c];
            rows.Add(row);
        }
        return new TableModel(rows, ColumnCount, ColumnWidths);
    }

    // 셀마다 표 위의 시작 위치(행, 열)
    public IEnumerable<(int Row, int Column, TableCell Cell)> Positions()
    {
        var occupied = new bool[RowCount, ColumnCount];
        for (var r = 0; r < RowCount; r++)
        {
            var c = 0;
            foreach (var cell in Rows[r])
            {
                while (occupied[r, c])
                    c++;
                for (var dr = 0; dr < cell.RowSpan; dr++)
                for (var dc = 0; dc < cell.ColSpan; dc++)
                    occupied[r + dr, c + dc] = true;
                yield return (r, c, cell);
                c += cell.ColSpan;
            }
        }
    }

    // 직사각형이고 크기 제한 안이면 표를 만들고, 아니면 null(통과)
    internal static TableModel? Create(IReadOnlyList<IReadOnlyList<TableCell>> rows, IReadOnlyList<int>? columnWidths = null)
    {
        if (rows.Count is 0 or > MaxRows)
            return null;

        var columnCount = MeasureColumns(rows);
        if (columnCount is null or > MaxColumns || rows.Count * columnCount > MaxCells)
            return null;

        if (columnWidths is not null && columnWidths.Count != columnCount)
            columnWidths = null;
        return new TableModel(rows, columnCount.Value, columnWidths);
    }

    // 병합을 반영해 행마다 차지하는 열 수를 세고, 모두 같을 때만 그 값을 돌려준다
    private static int? MeasureColumns(IReadOnlyList<IReadOnlyList<TableCell>> rows)
    {
        // 아래 행으로 이어지는 rowspan이 차지한 칸: 열 번호 → 남은 행 수
        var carried = new Dictionary<int, int>();
        int? width = null;

        for (var r = 0; r < rows.Count; r++)
        {
            var occupied = carried.Keys.ToHashSet();
            var next = new Dictionary<int, int>();
            foreach (var (col, left) in carried)
                if (left > 1)
                    next[col] = left - 1;

            var c = 0;
            foreach (var cell in rows[r])
            {
                if (cell.ColSpan < 1 || cell.RowSpan < 1 || r + cell.RowSpan > rows.Count)
                    return null;
                while (occupied.Contains(c))
                    c++;
                for (var dc = 0; dc < cell.ColSpan; dc++)
                {
                    if (!occupied.Add(c + dc))
                        return null;
                    if (cell.RowSpan > 1)
                        next[c + dc] = cell.RowSpan - 1;
                }
                c += cell.ColSpan;
            }

            var rowWidth = occupied.Count == 0 ? 0 : occupied.Max() + 1;
            if (rowWidth != occupied.Count || (width is not null && rowWidth != width))
                return null;
            width = rowWidth;
            carried = next;
        }
        return width is > 0 ? width : null;
    }
}
