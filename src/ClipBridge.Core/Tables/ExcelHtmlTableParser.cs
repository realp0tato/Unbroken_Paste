using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace ClipBridge.Core;

// Excel이 복사하는 HTML 표를 TableModel로 읽는다.
// Excel은 서식을 <style>의 .xlNN 클래스로 주므로 문서 전체(CF_HTML이면 StartHTML부터)를 넘겨야 한다
public static partial class ExcelHtmlTableParser
{
    private static readonly HtmlParser Parser = new();

    // 표가 정확히 하나가 아니거나, 중첩 표·불규칙 표·크기 초과면 null(통과)
    public static TableModel? Parse(string html)
    {
        using var document = Parser.ParseDocument(html);

        var tables = document.QuerySelectorAll("table");
        if (tables.Length != 1)
            return null;

        var classStyles = ReadClassStyles(document);
        var rows = new List<IReadOnlyList<TableCell>>();
        foreach (var tr in tables[0].QuerySelectorAll("tr"))
        {
            var row = new List<TableCell>();
            foreach (var td in tr.Children.Where(e => e is IHtmlTableCellElement))
                row.Add(ReadCell((IHtmlTableCellElement)td, classStyles));
            rows.Add(row);
            if (rows.Count > TableModel.MaxRows)
                return null;
        }

        return TableModel.Create(rows, ReadColumnWidths(tables[0]));
    }

    private static TableCell ReadCell(IHtmlTableCellElement td, IReadOnlyDictionary<string, string> classStyles)
    {
        var style = (td.ClassName is { } cls && classStyles.TryGetValue(cls, out var classStyle) ? classStyle : "")
            + ";" + (td.GetAttribute("style") ?? "");

        var bold = CssValue(style, "font-weight") is "700" or "bold" or "bolder"
            || WrapsWholeCell(td, "b", "strong");
        var italic = CssValue(style, "font-style") is "italic" or "oblique"
            || WrapsWholeCell(td, "i", "em");
        var align = (CssValue(style, "text-align") ?? td.GetAttribute("align")?.ToLowerInvariant()) switch
        {
            "left" => CellAlign.Left,
            "center" or "centre" => CellAlign.Center,
            "right" => CellAlign.Right,
            _ => CellAlign.None,
        };

        return new TableCell(ReadText(td), Math.Max(td.ColumnSpan, 1), Math.Max(td.RowSpan, 1), bold, italic, align);
    }

    // 셀 표시 값. HTML 공백은 하나로 줄이고 <br>은 셀 안 줄바꿈(\n)으로 바꾼다
    private static string ReadText(INode node)
    {
        var text = new StringBuilder();
        AppendText(node, text);
        return string.Join('\n', text.ToString().Split('\n').Select(line => line.Trim()));
    }

    private static void AppendText(INode node, StringBuilder text)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText t)
                text.Append(Whitespace().Replace(t.Data, " ").Replace(' ', ' '));
            else if (child is IElement { LocalName: "br" })
                text.Append('\n');
            else if (child is IElement)
                AppendText(child, text);
        }
    }

    // 셀의 글자 전체가 해당 태그 안에 있는지 (부분 서식은 MVP에서 무시)
    private static bool WrapsWholeCell(IElement td, params string[] tags)
    {
        var all = td.TextContent.Trim();
        return all.Length > 0 && td.QuerySelectorAll(string.Join(',', tags)).Any(e => e.TextContent.Trim() == all);
    }

    // <col width=72 span=2> → 열마다 너비
    private static IReadOnlyList<int>? ReadColumnWidths(IElement table)
    {
        var widths = new List<int>();
        foreach (var col in table.QuerySelectorAll("col"))
        {
            if (!int.TryParse(col.GetAttribute("width"), out var width))
                return null;
            var span = int.TryParse(col.GetAttribute("span"), out var s) && s > 0 ? s : 1;
            widths.AddRange(Enumerable.Repeat(width, span));
        }
        return widths.Count > 0 ? widths : null;
    }

    // <style> 안의 ".xl65 { ... }" 규칙 → 클래스 이름별 선언
    private static Dictionary<string, string> ReadClassStyles(IDocument document)
    {
        var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in document.QuerySelectorAll("style"))
        foreach (Match match in ClassRule().Matches(style.TextContent))
            styles[match.Groups[1].Value] = match.Groups[2].Value;
        return styles;
    }

    // 뒤에 나온 선언이 이긴다
    private static string? CssValue(string style, string property)
    {
        string? value = null;
        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon > 0 && declaration[..colon].Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
                value = declaration[(colon + 1)..].Trim().ToLowerInvariant();
        }
        return value;
    }

    [GeneratedRegex(@"\.([\w-]+)\s*\{([^}]*)\}")]
    private static partial Regex ClassRule();

    [GeneratedRegex(@"[ \t\r\n\f]+")]
    private static partial Regex Whitespace();
}
