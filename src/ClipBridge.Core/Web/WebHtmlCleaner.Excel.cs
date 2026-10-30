using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ClipBridge.Core.Web;

// Excel(C9)용 정리. Excel은 웹 HTML 표를 스스로 잘 받으므로 Markdown으로 다시 쓰지 않고,
// ToMarkdown과 같은 기준으로 찌꺼기만 지운 HTML을 돌려준다
public static partial class WebHtmlCleaner
{
    // 지운 것이 없으면 null (Excel 기본 붙여넣기와 같으므로 통과)
    public static string? CleanForExcel(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var root = (IElement?)document.Body ?? document.DocumentElement;
        var changed = false;

        void Remove(IEnumerable<IElement> elements)
        {
            foreach (var element in elements.ToList())
            {
                element.Remove();
                changed = true;
            }
        }

        Remove(root.QuerySelectorAll("script, style, noscript, template, [hidden], [aria-hidden='true'], sup.reference, .mw-editsection"));
        Remove(root.QuerySelectorAll("[style]").Where(IsHiddenByStyle));
        Remove(root.QuerySelectorAll("sup").Where(IsFootnote));
        // 위키백과 "빈칸" 틀: 빈 칸 표시 글자를 지운다
        foreach (var cell in root.QuerySelectorAll("td.table-na, th.table-na").Where(cell => cell.TextContent.Length > 0))
        {
            cell.TextContent = "";
            changed = true;
        }

        // 원본 HTML의 charset 선언은 head에 있어 빠지므로 다시 붙인다 (macOS public.html은 헤더가 없다)
        return changed ? "<meta charset=\"utf-8\">" + root.InnerHtml.Trim() : null;
    }
}
