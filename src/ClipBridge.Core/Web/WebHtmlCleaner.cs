using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ClipBridge.Core.Web;

// 정리된 Markdown과, 처리하지 못해 버린 요소에 대한 의심 신호(S3)
public sealed record WebCleanResult(string Markdown, IReadOnlyList<SuspicionSignal> Signals);

// 웹 HTML 정리 공통 모듈 (PRD 6.3.3–6.3.4, NFR-12, #9).
// 사이트 찌꺼기(글꼴·색·스크립트·각주·배지·목차·복사 버튼 등)를 걷어내고, 남은 구조를 Markdown으로 바꾼다.
// 이후는 기존 렌더러를 쓴다: 한글(C3)은 DocHtmlRenderer, Notion(C4)은 Markdown 엔진.
// 스타일 속성은 아예 옮기지 않으므로 사이트 글꼴·색·배경은 저절로 빠진다.
public static partial class WebHtmlCleaner
{
    // 내용째 지운다. 글자로 남기면 찌꺼기가 되는 것들
    private const string RemovedSelector =
        "script, style, noscript, template, head, meta, link, svg, img, picture, video, audio, canvas, " +
        "button, input, select, textarea, " +
        "[hidden], [aria-hidden='true'], " +
        // 위키백과: 각주 표시, 편집 링크
        "sup.reference, .mw-editsection, " +
        // MDN: 목차
        ".reference-toc";

    // 사이드바·목차. 브라우저가 본문까지 이 안에 넣어 보내기도 하므로(Safari) 본문이 없을 때만 지운다
    private const string SidebarSelector = "nav, aside";
    // 접기 상자(MDN 배지 등)는 제목·코드·표가 없으면 지우고, 있으면 머리 줄(summary)만 지운다
    private const string MainContentSelector = "h1, h2, h3, h4, h5, h6, pre, table";
    private const string ContentSelector = "p, h1, h2, h3, h4, h5, h6, pre, table";
    private const string BlockSelector = "p, div, section, article, h1, h2, h3, h4, h5, h6, ul, ol, li, pre, blockquote, table";

    // 처리할 수 없어 버리는 요소 → 의심 신호 S3 (PRD 6.6.2)
    private const string UnsupportedSelector = "iframe, object, embed, form, math";

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "section", "article", "main", "header", "footer", "figure", "figcaption",
        "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "pre", "blockquote", "table", "dl", "dt", "dd", "hr",
    };

    public static WebCleanResult ToMarkdown(string html, bool keepLinks)
    {
        var document = new HtmlParser().ParseDocument(html);
        var root = (IElement?)document.Body ?? document.DocumentElement;

        var signals = new List<SuspicionSignal>();
        var unsupported = root.QuerySelectorAll(UnsupportedSelector).ToList();
        if (unsupported.Count > 0)
            signals.Add(new SuspicionSignal("S3", 2, "처리하지 못한 요소: " + string.Join(", ", unsupported.Select(e => e.LocalName).Distinct())));

        foreach (var element in root.QuerySelectorAll(RemovedSelector + ", " + UnsupportedSelector).ToList())
            element.Remove();
        foreach (var sidebar in root.QuerySelectorAll(SidebarSelector).Where(e => e.QuerySelector(ContentSelector) is null).ToList())
            sidebar.Remove();
        foreach (var details in root.QuerySelectorAll("details").Reverse().ToList())
        {
            if (details.QuerySelector(MainContentSelector) is null)
                details.Remove();
            else
                foreach (var summary in details.Children.Where(c => c.LocalName == "summary").ToList())
                    summary.Remove();
        }
        foreach (var element in root.QuerySelectorAll("[style]").Where(IsHiddenByStyle).ToList())
            element.Remove();
        // 위키백과 "빈칸" 틀: 빈 칸 표시 글자를 지운다
        foreach (var cell in root.QuerySelectorAll("td.table-na, th.table-na"))
            cell.TextContent = "";
        // 각주로 가는 링크만 있는 위첨자 (사이트마다 클래스가 달라도 #cite 링크면 각주)
        foreach (var sup in root.QuerySelectorAll("sup").Where(IsFootnote).ToList())
            sup.Remove();
        RemoveCodeHeaders(root);

        var markdown = new StringBuilder();
        WriteBlocks(markdown, root, keepLinks, listDepth: 0);
        return new WebCleanResult(BlankLines().Replace(markdown.ToString(), "\n\n").Trim('\n'), signals);
    }

    // ---- 블록 ----

    private static void WriteBlocks(StringBuilder md, INode parent, bool keepLinks, int listDepth)
    {
        var inline = new StringBuilder();
        void FlushInline()
        {
            var text = inline.ToString().Trim();
            if (text.Length > 0)
                Block(md, EscapeLineStart(text));
            inline.Clear();
        }

        foreach (var node in parent.ChildNodes)
        {
            // 처음 보는 태그(사이트 전용 태그 등)도 안에 블록이 있으면 블록으로 다룬다
            if (node is IElement element && (BlockTags.Contains(element.LocalName) || element.QuerySelector(BlockSelector) is not null))
            {
                FlushInline();
                WriteBlock(md, element, keepLinks, listDepth);
            }
            else
                WriteInline(inline, node, keepLinks);
        }
        FlushInline();
    }

    private static void WriteBlock(StringBuilder md, IElement element, bool keepLinks, int listDepth)
    {
        switch (element.LocalName)
        {
            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                // 제목을 감싼 링크는 사이트의 고정 링크(#앵커)라 글자만 남긴다
                var text = InlineText(element, keepLinks: false);
                if (text.Length > 0)
                    Block(md, new string('#', element.LocalName[1] - '0') + " " + text);
                break;
            case "p" or "dt" or "dd" or "figcaption":
                var paragraph = InlineText(element, keepLinks);
                if (paragraph.Length > 0)
                    Block(md, EscapeLineStart(paragraph));
                break;
            case "ul" or "ol":
                WriteList(md, element, keepLinks, listDepth);
                break;
            case "pre":
                WriteCode(md, element);
                break;
            case "blockquote":
                var quote = new StringBuilder();
                WriteBlocks(quote, element, keepLinks, listDepth);
                var lines = quote.ToString().Trim('\n').Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line);
                Block(md, string.Join('\n', lines));
                break;
            case "table":
                WriteTable(md, element, keepLinks);
                break;
            case "hr":
                break;
            default:
                WriteBlocks(md, element, keepLinks, listDepth);
                break;
        }
    }

    private static void WriteList(StringBuilder md, IElement list, bool keepLinks, int depth)
    {
        var ordered = list.LocalName == "ol";
        var number = int.TryParse(list.GetAttribute("start"), out var start) ? start : 1;
        var indent = new string(' ', depth * 3);
        var lines = new StringBuilder();
        foreach (var item in list.Children.Where(child => child.LocalName == "li"))
        {
            var marker = ordered ? $"{number++}. " : "- ";
            // 항목 첫 줄은 글자, 안쪽 목록은 한 단계 더 들여쓴다
            var text = new StringBuilder();
            var nested = new StringBuilder();
            foreach (var node in item.ChildNodes)
            {
                if (node is IElement child && child.LocalName is "ul" or "ol")
                    WriteList(nested, child, keepLinks, depth + 1);
                else if (node is IElement block && BlockTags.Contains(block.LocalName))
                    text.Append(' ').Append(InlineText(block, keepLinks));
                else
                    WriteInline(text, node, keepLinks);
            }
            var line = Whitespace().Replace(text.ToString(), " ").Trim();
            if (line.Length == 0 && nested.Length == 0)
                continue;
            lines.Append(indent).Append(marker).Append(line).Append('\n');
            if (nested.Length > 0)
                lines.Append(nested.ToString().Trim('\n')).Append('\n');
        }
        if (depth == 0)
            Block(md, lines.ToString().TrimEnd('\n'));
        else
            md.Append(lines);
    }

    // 코드는 글자 그대로. 안에 ```가 있으면 더 긴 울타리를 쓴다
    private static void WriteCode(StringBuilder md, IElement pre)
    {
        var code = pre.TextContent.Replace("\r\n", "\n").TrimEnd('\n');
        if (code.Trim().Length == 0)
            return;
        var fence = "```";
        while (code.Contains(fence, StringComparison.Ordinal))
            fence += "`";
        Block(md, fence + "\n" + code + "\n" + fence);
    }

    // 파이프 표. 병합 칸은 풀어서 첫 칸에 값, 나머지는 빈 칸 (PRD 6.3.2·6.3.4)
    private static void WriteTable(StringBuilder md, IElement table, bool keepLinks)
    {
        if (table.QuerySelector("table") is not null)
        {
            // 중첩 표는 표로 만들지 않고 글자로 (PRD 6.3.10)
            WriteBlocks(md, table, keepLinks, 0);
            return;
        }

        var grid = new List<List<string>>();
        var pending = new Dictionary<(int Row, int Col), string>();
        var rows = table.QuerySelectorAll("tr").ToList();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = new List<string>();
            var col = 0;
            foreach (var cell in rows[r].Children.Where(c => c.LocalName is "td" or "th"))
            {
                while (pending.Remove((r, col)))
                    row.Add("");
                row.Add(EscapeCell(InlineText(cell, keepLinks)));
                var colSpan = Math.Max(1, int.TryParse(cell.GetAttribute("colspan"), out var cs) ? cs : 1);
                var rowSpan = Math.Max(1, int.TryParse(cell.GetAttribute("rowspan"), out var rs) ? rs : 1);
                for (var i = 1; i < colSpan; i++)
                    row.Add("");
                for (var down = 1; down < rowSpan; down++)
                    for (var i = 0; i < colSpan; i++)
                        pending[(r + down, col + i)] = "";
                col += colSpan;
            }
            while (pending.Remove((r, col++)))
                row.Add("");
            if (row.Count > 0)
                grid.Add(row);
        }
        if (grid.Count == 0)
            return;

        var columns = grid.Max(row => row.Count);
        foreach (var row in grid)
            while (row.Count < columns)
                row.Add("");

        var text = new StringBuilder();
        text.Append("| ").Append(string.Join(" | ", grid[0])).Append(" |\n");
        text.Append('|').Append(string.Concat(Enumerable.Repeat("---|", columns))).Append('\n');
        foreach (var row in grid.Skip(1))
            text.Append("| ").Append(string.Join(" | ", row)).Append(" |\n");
        Block(md, text.ToString().TrimEnd('\n'));
    }

    // ---- 인라인 ----

    private static string InlineText(INode node, bool keepLinks)
    {
        var text = new StringBuilder();
        foreach (var child in node.ChildNodes)
            WriteInline(text, child, keepLinks);
        return Whitespace().Replace(text.ToString(), " ").Trim();
    }

    private static void WriteInline(StringBuilder md, INode node, bool keepLinks)
    {
        if (node is IText text)
        {
            md.Append(Escape(text.Data));
            return;
        }
        if (node is not IElement element)
            return;

        switch (element.LocalName)
        {
            case "br":
                md.Append(' ');
                break;
            case "strong" or "b":
                Wrap(md, "**", InlineText(element, keepLinks));
                break;
            case "em" or "i":
                Wrap(md, "*", InlineText(element, keepLinks));
                break;
            case "del" or "s" or "strike":
                Wrap(md, "~~", InlineText(element, keepLinks));
                break;
            case "code" or "kbd" or "samp":
                var code = Whitespace().Replace(element.TextContent, " ").Trim();
                if (code.Length > 0)
                {
                    var tick = code.Contains('`') ? "``" : "`";
                    md.Append(tick).Append(code.Contains('`') ? " " + code + " " : code).Append(tick);
                }
                break;
            case "a":
                var label = InlineText(element, keepLinks);
                var href = element.GetAttribute("href");
                if (keepLinks && label.Length > 0 && href is not null && IsWebLink(href))
                    md.Append('[').Append(label).Append("](").Append(href.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29")).Append(')');
                else
                    md.Append(label);
                break;
            default:
                foreach (var child in element.ChildNodes)
                    WriteInline(md, child, keepLinks);
                break;
        }
    }

    // 안쪽 글자는 InlineText가 앞뒤 공백을 잘라 둔다 (공백이 붙으면 Markdown이 강조로 읽지 않는다)
    private static void Wrap(StringBuilder md, string mark, string text)
    {
        if (text.Length == 0)
            return;
        md.Append(mark).Append(text).Append(mark);
    }

    private static void Block(StringBuilder md, string text) => md.Append(text).Append("\n\n");

    // ---- 정리 규칙 ----

    private static bool IsHiddenByStyle(IElement element) =>
        HiddenStyle().IsMatch(element.GetAttribute("style") ?? "");

    private static bool IsFootnote(IElement sup) =>
        sup.QuerySelectorAll("a").Any(a => (a.GetAttribute("href") ?? "").Contains("#cite", StringComparison.OrdinalIgnoreCase))
        && sup.TextContent.Trim().StartsWith('[');

    // <pre> 바로 앞의 짧은 머리말(언어 이름 + 복사 버튼 자리)은 지운다 (MDN 등). 버튼은 이미 지워져 있다
    private static void RemoveCodeHeaders(IElement root)
    {
        foreach (var pre in root.QuerySelectorAll("pre").ToList())
        {
            var header = pre.PreviousElementSibling;
            if (header is null || header.LocalName is "p" or "pre" or "table" or "ul" or "ol" or "blockquote" || header.LocalName.StartsWith('h'))
                continue;
            if (header.TextContent.Trim().Length <= 12 && header.QuerySelector("p, pre, table, ul, ol") is null)
                header.Remove();
        }
    }

    private static bool IsWebLink(string href) =>
        href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    // Markdown 기호로 읽힐 글자는 이스케이프한다 (원래 글자가 그대로 보이게)
    private static string Escape(string text) => MarkdownChars().Replace(text, @"\$0");

    // 줄 맨 앞의 #·-·+·"숫자." 는 제목·목록으로 읽히므로 이스케이프한다 ("1." → "1\.")
    private static string EscapeLineStart(string text) =>
        LineStartSyntax().Replace(text, m => m.Groups[3].Success
            ? m.Groups[1].Value + m.Groups[3].Value + "\\" + m.Groups[4].Value
            : m.Groups[1].Value + "\\" + m.Groups[2].Value);

    private static string EscapeCell(string text) => text.Replace("\n", " ");

    [GeneratedRegex(@"[\\`*_\[\]|<>~]")]
    private static partial Regex MarkdownChars();
    [GeneratedRegex(@"^(\s*)(#|[-+](?=\s)|(\d+)([.)])(?=\s))")]
    private static partial Regex LineStartSyntax();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
    [GeneratedRegex(@"display\s*:\s*none|visibility\s*:\s*hidden", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenStyle();
}
