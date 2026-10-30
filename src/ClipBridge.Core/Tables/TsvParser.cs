using System.Text;

namespace ClipBridge.Core;

// Excel이 복사하는 TSV: 셀은 탭, 행은 줄바꿈으로 구분한다.
// 셀 안에 줄바꿈·탭·따옴표가 있으면 따옴표로 감싸고, 안의 따옴표는 ""로 쓴다
public static class TsvParser
{
    // 직사각형이 아니거나 크기 제한을 넘으면 null(통과)
    public static TableModel? Parse(string text)
    {
        var rows = new List<IReadOnlyList<TableCell>>();
        var row = new List<TableCell>();
        var cell = new StringBuilder();
        var i = 0;

        while (i < text.Length)
        {
            if (cell.Length == 0 && text[i] == '"' && TryReadQuoted(text, ref i, cell))
                continue;

            var ch = text[i];
            if (ch == '\t')
            {
                row.Add(new TableCell(cell.ToString()));
                cell.Clear();
                i++;
            }
            else if (ch is '\r' or '\n')
            {
                row.Add(new TableCell(cell.ToString()));
                cell.Clear();
                rows.Add(row);
                row = [];
                i += ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                if (rows.Count > TableModel.MaxRows)
                    return null;
            }
            else
            {
                cell.Append(ch);
                i++;
            }
        }

        // 마지막 줄바꿈 뒤에는 행이 없다
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(new TableCell(cell.ToString()));
            rows.Add(row);
        }

        return TableModel.Create(rows);
    }

    // 따옴표로 감싼 셀을 읽는다. 닫는 따옴표 바로 뒤가 탭·줄바꿈·끝이 아니면
    // 따옴표로 시작하는 일반 셀로 보고 false를 돌려준다
    private static bool TryReadQuoted(string text, ref int index, StringBuilder cell)
    {
        var value = new StringBuilder();
        var i = index + 1;
        while (i < text.Length)
        {
            if (text[i] != '"')
            {
                value.Append(text[i++]);
                continue;
            }
            if (i + 1 < text.Length && text[i + 1] == '"')
            {
                value.Append('"');
                i += 2;
                continue;
            }

            var end = i + 1;
            if (end < text.Length && text[end] is not ('\t' or '\r' or '\n'))
                return false;

            // 셀 안 줄바꿈은 \n으로 통일
            cell.Append(value.ToString().Replace("\r\n", "\n").Replace('\r', '\n'));
            index = end;
            return true;
        }
        return false;
    }
}
