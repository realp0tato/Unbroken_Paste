using System.Text;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
// Core의 표 모델(TableCell 등)과 이름이 겹쳐 Markdig 표 타입은 별칭으로 쓴다
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;

namespace ClipBridge.Core.Markdown;

// 기호 제거 규칙 (PRD 6.3.5 기호 제거 규칙 표)
internal static class MarkdownStripper
{
    public static string Strip(MarkdownDocument document, string source)
    {
        var builder = new StringBuilder();
        WriteBlocks(builder, document, source, indent: "", columnOffset: 0);
        return builder.ToString().TrimEnd('\n');
    }

    // 블록 사이 빈 줄은 원문에 있던 곳에만 1줄 둔다 (연속 빈 줄은 1줄로). 표 앞뒤는 항상 1줄 (PRD 6.4)
    // indent: 이어지는 줄 앞에 붙일 공백. columnOffset: 목록 들여쓰기를 계산할 때 뺄 원문 열(인용 기호 > 만큼)
    private static void WriteBlocks(StringBuilder builder, ContainerBlock container, string source, string indent, int columnOffset)
    {
        Block? previous = null;
        foreach (var block in container)
        {
            if (block is ThematicBreakBlock or LinkReferenceDefinitionGroup)
                continue;
            if (previous is not null)
                builder.Append(previous is MdTable || block is MdTable || HasBlankLineBetween(previous, block, source) ? "\n\n" : "\n");
            WriteBlock(builder, block, source, indent, columnOffset);
            previous = block;
        }
    }

    private static void WriteBlock(StringBuilder builder, Block block, string source, string indent, int columnOffset)
    {
        switch (block)
        {
            case HeadingBlock heading:
                builder.Append(indent).Append(InlineText(heading.Inline));
                break;
            case ParagraphBlock paragraph:
                AppendIndented(builder, InlineText(paragraph.Inline), indent);
                break;
            case CodeBlock code:
                // 울타리 줄(```)은 빠지고 안의 코드는 그대로 남는다
                AppendIndented(builder, code.Lines.ToString(), indent);
                break;
            case ListBlock list:
                WriteList(builder, list, source, columnOffset);
                break;
            case MdTable table:
                WriteTable(builder, table, indent);
                break;
            case QuoteBlock quote:
                // > 기호를 빼면 안의 내용이 그만큼 왼쪽으로 온다
                WriteBlocks(builder, quote, source, indent, quote.Count > 0 ? quote[0].Column : columnOffset);
                break;
            case ContainerBlock other:
                WriteBlocks(builder, other, source, indent, columnOffset);
                break;
        }
    }

    private static void WriteList(StringBuilder builder, ListBlock list, string source, int columnOffset)
    {
        var first = true;
        foreach (var item in list.Cast<ListItemBlock>())
        {
            if (!first)
                builder.Append('\n');
            first = false;

            // 글머리 기호는 빼고 번호는 남긴다. 들여쓰기는 유지
            var marker = list.IsOrdered ? $"{item.Order}{list.OrderedDelimiter} " : "";
            var itemIndent = new string(' ', Math.Max(0, item.Column - columnOffset));
            builder.Append(itemIndent).Append(marker);

            var childIndent = itemIndent + new string(' ', marker.Length);
            var firstChild = true;
            foreach (var child in item)
            {
                if (!firstChild)
                    builder.Append('\n');
                if (firstChild && child is ParagraphBlock paragraph)
                    AppendIndented(builder, InlineText(paragraph.Inline), childIndent, indentFirstLine: false);
                else
                    WriteBlock(builder, child, source, childIndent, columnOffset);
                firstChild = false;
            }
        }
    }

    private static void WriteTable(StringBuilder builder, MdTable table, string indent)
    {
        var first = true;
        foreach (var row in table.Cast<MdTableRow>())
        {
            if (!first)
                builder.Append('\n');
            first = false;
            var cells = row.Cast<MdTableCell>().Select(cell =>
                string.Join(" ", cell.OfType<ParagraphBlock>().Select(p => InlineText(p.Inline))));
            builder.Append(indent).Append(string.Join('\t', cells));
        }
    }

    public static string InlineText(Inline? inline)
    {
        var builder = new StringBuilder();
        AppendInline(builder, inline);
        return builder.ToString();
    }

    private static void AppendInline(StringBuilder builder, Inline? inline)
    {
        switch (inline)
        {
            case null:
                return;
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case AutolinkInline autolink:
                builder.Append(autolink.Url);
                break;
            case LineBreakInline:
                builder.Append('\n');
                break;
            case HtmlEntityInline entity:
                builder.Append(entity.Transcoded.ToString());
                break;
            case HtmlInline html:
                builder.Append(html.Tag);
                break;
            case ContainerInline container:
                // 굵게·기울임·취소선·링크·이미지: 감싼 기호와 주소는 버리고 안의 글자만 남긴다
                foreach (var child in container)
                    AppendInline(builder, child);
                break;
        }
    }

    private static void AppendIndented(StringBuilder builder, string text, string indent, bool indentFirstLine = true)
    {
        var lines = text.TrimEnd('\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                builder.Append('\n');
            if (i > 0 || indentFirstLine)
                builder.Append(indent);
            builder.Append(lines[i]);
        }
    }

    private static bool HasBlankLineBetween(Block previous, Block next, string source)
    {
        var start = Math.Min(previous.Span.End + 1, source.Length);
        var end = Math.Min(next.Span.Start, source.Length);
        if (end <= start)
            return false;
        return source.AsSpan(start, end - start).Count('\n') >= 2;
    }
}
