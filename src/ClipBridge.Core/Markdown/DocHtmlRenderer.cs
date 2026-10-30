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

    // 표·코드 칸 너비 어림 (본문 10pt 기준)
    private const double WideCharPt = 10.5;
    private const double NarrowCharPt = 6.2;
    private const double MonoCharPt = 6.2;
    private const double BoldFactor = 1.08;
    private const double CellPaddingPt = 5.1;
    private const double CodePaddingPt = 6;
    private const double SafetyPt = 6;
    private const double MinColumnWidthPt = 30;
    private const double MinCodeWidthPt = 80;

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

    // 코드 블록은 테두리 있는 한 칸 표 + 고정폭 글꼴. 줄 앞 공백은 &nbsp;로 지킨다.
    // 너비는 가장 긴 줄이 줄바꿈되지 않는 최소 너비 (본문 폭을 넘으면 본문 폭에서 줄바꿈)
    private static void WriteCode(StringBuilder html, CodeBlock code, int depth)
    {
        var lines = code.Lines.ToString().TrimEnd('\n').Split('\n');
        var available = MaxTableWidthPt - depth * IndentPt;
        var needed = lines.Max(line => TextWidthPt(line, monospace: true)) + CodePaddingPt * 2 + SafetyPt;
        var width = Math.Clamp(needed, MinCodeWidthPt, available);
        var nowrap = needed <= available;

        OpenTable(html, width, depth);
        html.Append("<tr>");
        Cell(html, width, "border:1px solid #999;padding:4pt 6pt;background:#f5f5f5", nowrap);
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

    // 열 너비는 그 열의 가장 긴 칸이 줄바꿈되지 않는 최소 너비.
    // 표 전체가 본문 폭 안이면 줄바꿈을 막고, 넘으면 비율대로 줄여 줄바꿈을 허용한다.
    // 너비는 CSS와 HTML 속성으로 함께 준다 (편집기마다 읽는 쪽이 다르다)
    private static void WriteTable(StringBuilder html, MdTable table, int depth)
    {
        var rows = table.Cast<MdTableRow>().ToList();
        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        var widths = new double[columns];
        foreach (var row in rows)
            for (var i = 0; i < row.Count; i++)
            {
                var text = TextWidthPt(CellText((MdTableCell)row[i]), monospace: false) * (row.IsHeader ? BoldFactor : 1);
                widths[i] = Math.Max(widths[i], Math.Max(text + CellPaddingPt * 2 + SafetyPt, MinColumnWidthPt));
            }
        var available = MaxTableWidthPt - depth * IndentPt;
        var nowrap = widths.Sum() <= available;
        var scale = nowrap ? 1 : available / widths.Sum();
        for (var i = 0; i < columns; i++)
            widths[i] *= scale;

        OpenTable(html, widths.Sum(), depth);
        html.Append("<colgroup>");
        foreach (var width in widths)
            html.Append("<col width=\"").Append(Px(width)).Append("\" style=\"width:").Append(Pt(width)).Append("pt\">");
        html.Append("</colgroup>");

        foreach (var row in rows)
        {
            html.Append("<tr>");
            for (var i = 0; i < row.Count; i++)
            {
                var cell = (MdTableCell)row[i];
                Cell(html, widths[i], "border:1px solid #000;padding:1.41pt 5.1pt", nowrap);
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

    private static void OpenTable(StringBuilder html, double widthPt, int depth)
    {
        html.Append("<table border=\"1\" width=\"").Append(Px(widthPt)).Append("\" style=\"border-collapse:collapse;width:").Append(Pt(widthPt)).Append("pt");
        if (depth > 0)
            html.Append(";margin-left:").Append(Pt(depth * IndentPt)).Append("pt");
        html.Append("\">");
    }

    private static void Cell(StringBuilder html, double widthPt, string style, bool nowrap)
    {
        html.Append("<td width=\"").Append(Px(widthPt)).Append("\"");
        if (nowrap)
            html.Append(" nowrap");
        html.Append(" style=\"").Append(style).Append(";width:").Append(Pt(widthPt)).Append("pt");
        if (nowrap)
            html.Append(";white-space:nowrap");
        html.Append("\">");
    }

    // 글자가 없는 문단(빈 대체 글자 이미지 등)은 빈 문단이 되므로 쓰지 않는다 (PRD 6.4)
    private static void Paragraph(StringBuilder html, int depth, bool quote, Action content)
    {
        var start = html.Length;
        html.Append("<p style=\"").Append(Margin(depth, "0 0 4pt"));
        if (quote)
            html.Append(";color:#555");
        html.Append("\">");
        var contentStart = html.Length;
        content();
        if (IsBlank(html.ToString(contentStart, html.Length - contentStart)))
            html.Length = start;
        else
            html.Append("</p>");
    }

    private static bool IsBlank(string content) =>
        content.Replace("&nbsp;", "").Replace("<br>", "").Trim().Length == 0;

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

    // 본문 10pt 기준 글자 폭 어림값. 넉넉하게 잡아 줄바꿈되지 않게 한다
    private static double TextWidthPt(string text, bool monospace) =>
        text.Sum(c => IsWide(c) ? WideCharPt : monospace ? MonoCharPt : NarrowCharPt);

    // 한글·한자·전각 문자
    private static bool IsWide(char c) => c >= 0x1100 && (c <= 0x11FF || c >= 0x2E80);

    // 1pt = 4/3 px. HTML width 속성은 px
    private static string Px(double pt) => Math.Ceiling(pt * 4 / 3).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Pt(double value) => Math.Round(value, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
