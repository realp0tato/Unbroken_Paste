using System.Net;
using System.Text;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;

namespace ClipBridge.Core.Markdown;

// Markdown 서식 적용 → Word·한글용 HTML (PRD 6.3.5 대상별 서식 적용 방식, #18)
//
// 웹용 HTML(<h1>·<ul>·<pre>)을 그대로 넘기면 한글 계열은 들여쓰기가 누적돼 글이 페이지 밖으로 밀리고,
// 표 테두리와 코드 블록 상자가 사라진다 (docs/baseline: Claude 답변 → 한컴독스).
// 그래서 모든 블록을 여백·크기를 직접 지정한 문단(<p>)과 테두리를 지정한 표로만 만든다.
internal static class DocHtmlRenderer
{
    // 제목은 제목 스타일 없이 글자 크기·굵기만 (# 20pt, ## 16pt, ### 13pt, 나머지는 본문 크기 굵게)
    private static readonly string?[] HeadingSizes = ["20pt", "16pt", "13pt"];
    private const double IndentPt = 20;
    private const string Monospace = "Consolas, 'D2Coding', monospace";
    // 한글 기본 본문 폭: A4(210mm) - 좌우 여백 30mm×2 = 150mm (ExcelToHangulConverter와 같은 기준)
    private const double MaxTableWidthPt = 150 / 25.4 * 72;
    private static readonly string[] Bullets = ["•", "◦", "▪"];

    public static string Render(MarkdownDocument document)
    {
        var html = new StringBuilder();
        WriteBlocks(html, document, depth: 0, quote: false);
        return html.ToString();
    }

    // depth: 목록·인용 들여쓰기 단계. 단계마다 왼쪽 여백을 직접 준다
    private static void WriteBlocks(StringBuilder html, ContainerBlock container, int depth, bool quote)
    {
        foreach (var block in container)
            WriteBlock(html, block, depth, quote);
    }

    private static void WriteBlock(StringBuilder html, Block block, int depth, bool quote)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var size = heading.Level <= HeadingSizes.Length ? HeadingSizes[heading.Level - 1] : null;
                html.Append("<p style=\"").Append(Margin(depth, "6pt 0 3pt")).Append(";font-weight:bold");
                if (size is not null)
                    html.Append(";font-size:").Append(size);
                html.Append("\">");
                WriteInline(html, heading.Inline);
                html.Append("</p>");
                break;
            case ParagraphBlock paragraph:
                Paragraph(html, depth, quote, () => WriteInline(html, paragraph.Inline));
                break;
            case ListBlock list:
                WriteList(html, list, depth, quote);
                break;
            case CodeBlock code:
                WriteCode(html, code, depth);
                break;
            case MdTable table:
                WriteTable(html, table, depth);
                break;
            case QuoteBlock quoteBlock:
                WriteBlocks(html, quoteBlock, depth + 1, quote: true);
                break;
            case ThematicBreakBlock or LinkReferenceDefinitionGroup:
                break;
            case ContainerBlock other:
                WriteBlocks(html, other, depth, quote);
                break;
        }
    }

    // 글머리·번호를 글자로 직접 넣는다. <ul>·<li>를 쓰지 않아 대상 앱의 목록 들여쓰기가 끼어들지 않는다
    private static void WriteList(StringBuilder html, ListBlock list, int depth, bool quote)
    {
        foreach (var item in list.Cast<ListItemBlock>())
        {
            var marker = list.IsOrdered ? $"{item.Order}{list.OrderedDelimiter}" : Bullets[depth % Bullets.Length];
            var first = true;
            foreach (var child in item)
            {
                if (first && child is ParagraphBlock paragraph)
                    Paragraph(html, depth + 1, quote, () =>
                    {
                        html.Append(WebUtility.HtmlEncode(marker)).Append("&nbsp;");
                        WriteInline(html, paragraph.Inline);
                    });
                else
                    WriteBlock(html, child, depth + 1, quote);
                first = false;
            }
        }
    }

    // 코드 블록은 테두리 있는 한 칸 표 + 고정폭 글꼴. 줄 앞 공백은 &nbsp;로 지킨다
    private static void WriteCode(StringBuilder html, CodeBlock code, int depth)
    {
        var lines = code.Lines.ToString().TrimEnd('\n').Split('\n');
        html.Append("<table border=\"1\" style=\"border-collapse:collapse;width:")
            .Append(Pt(MaxTableWidthPt - depth * IndentPt)).Append("pt");
        if (depth > 0)
            html.Append(";margin-left:").Append(Pt(depth * IndentPt)).Append("pt");
        html.Append("\"><tr><td style=\"border:1px solid #999;padding:4pt 6pt;background:#f5f5f5\">");
        foreach (var line in lines)
        {
            var indent = line.Length - line.TrimStart(' ').Length;
            var rest = WebUtility.HtmlEncode(line.TrimStart(' '));
            html.Append("<p style=\"margin:0;font-family:").Append(Monospace).Append("\">")
                .Append(string.Concat(Enumerable.Repeat("&nbsp;", indent)))
                .Append(rest.Length == 0 && indent == 0 ? "&nbsp;" : rest)
                .Append("</p>");
        }
        html.Append("</td></tr></table>");
    }

    // 비율만 주면 한글이 표를 좁게 잡으므로 열 너비를 pt로 준다. 글자 수에 비례해 나누고 본문 폭을 넘지 않게 줄인다
    private static void WriteTable(StringBuilder html, MdTable table, int depth)
    {
        var rows = table.Cast<MdTableRow>().ToList();
        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        var widths = new double[columns];
        foreach (var row in rows)
            for (var i = 0; i < row.Count; i++)
                widths[i] = Math.Max(widths[i], Math.Clamp(DisplayWidth(CellText((MdTableCell)row[i])) * 6.5 + 14, 40, 220));
        var available = MaxTableWidthPt - depth * IndentPt;
        var scale = widths.Sum() > available ? available / widths.Sum() : 1;

        html.Append("<table border=\"1\" style=\"border-collapse:collapse;width:").Append(Pt(widths.Sum() * scale)).Append("pt");
        if (depth > 0)
            html.Append(";margin-left:").Append(Pt(depth * IndentPt)).Append("pt");
        html.Append("\"><colgroup>");
        foreach (var width in widths)
            html.Append("<col style=\"width:").Append(Pt(width * scale)).Append("pt\">");
        html.Append("</colgroup>");

        foreach (var row in rows)
        {
            html.Append("<tr>");
            for (var i = 0; i < row.Count; i++)
            {
                var cell = (MdTableCell)row[i];
                html.Append("<td style=\"border:1px solid #000;padding:1.41pt 5.1pt;width:").Append(Pt(widths[i] * scale)).Append("pt\">");
                html.Append("<p style=\"margin:0");
                if (row.IsHeader)
                    html.Append(";font-weight:bold");
                if (i < table.ColumnDefinitions.Count && table.ColumnDefinitions[i].Alignment is { } align)
                    html.Append(";text-align:").Append(align switch
                    {
                        TableColumnAlign.Center => "center",
                        TableColumnAlign.Right => "right",
                        _ => "left",
                    });
                html.Append("\">");
                foreach (var paragraph in cell.OfType<ParagraphBlock>())
                    WriteInline(html, paragraph.Inline);
                html.Append("</p></td>");
            }
            html.Append("</tr>");
        }
        html.Append("</table>");
    }

    private static void Paragraph(StringBuilder html, int depth, bool quote, Action content)
    {
        html.Append("<p style=\"").Append(Margin(depth, "0 0 4pt"));
        if (quote)
            html.Append(";color:#555");
        html.Append("\">");
        content();
        html.Append("</p>");
    }

    private static string Margin(int depth, string margin) =>
        depth == 0 ? "margin:" + margin : $"margin:{margin};margin-left:{Pt(depth * IndentPt)}pt";

    private static void WriteInline(StringBuilder html, ContainerInline? container)
    {
        if (container is null)
            return;
        foreach (var inline in container)
            WriteInline(html, inline);
    }

    private static void WriteInline(StringBuilder html, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                html.Append(WebUtility.HtmlEncode(literal.Content.ToString()));
                break;
            case CodeInline code:
                html.Append("<span style=\"font-family:").Append(Monospace).Append("\">")
                    .Append(WebUtility.HtmlEncode(code.Content)).Append("</span>");
                break;
            case LineBreakInline:
                html.Append("<br>");
                break;
            case HtmlEntityInline entity:
                html.Append(WebUtility.HtmlEncode(entity.Transcoded.ToString()));
                break;
            case HtmlInline raw:
                // 원시 HTML은 글자로 (실행 금지)
                html.Append(WebUtility.HtmlEncode(raw.Tag));
                break;
            case AutolinkInline autolink:
                var autoHref = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
                if (autolink.IsEmail || MarkdownEngine.IsSafeLink(autolink.Url))
                    html.Append("<a href=\"").Append(WebUtility.HtmlEncode(autoHref)).Append("\">")
                        .Append(WebUtility.HtmlEncode(autolink.Url)).Append("</a>");
                else
                    html.Append(WebUtility.HtmlEncode(autolink.Url));
                break;
            case LinkInline link:
                // 이미지는 대체 글자만 (외부 이미지 다운로드 금지), 안전하지 않은 링크는 글자만
                if (!link.IsImage && MarkdownEngine.IsSafeLink(link.Url))
                {
                    html.Append("<a href=\"").Append(WebUtility.HtmlEncode(link.Url)).Append("\">");
                    WriteInline(html, link);
                    html.Append("</a>");
                }
                else
                    WriteInline(html, (ContainerInline)link);
                break;
            case EmphasisInline emphasis:
                var tag = emphasis.DelimiterChar == '~' ? "s" : emphasis.DelimiterCount >= 2 ? "b" : "i";
                html.Append('<').Append(tag).Append('>');
                WriteInline(html, (ContainerInline)emphasis);
                html.Append("</").Append(tag).Append('>');
                break;
            case ContainerInline other:
                WriteInline(html, other);
                break;
        }
    }

    private static string CellText(MdTableCell cell) =>
        string.Concat(cell.OfType<ParagraphBlock>().Select(p => MarkdownStripper.InlineText(p.Inline)));

    // 한글·한자 등 넓은 글자는 2칸으로 센다
    private static int DisplayWidth(string text) => text.Sum(c => c >= 0x1100 ? 2 : 1);

    private static string Pt(double value) => Math.Round(value, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
