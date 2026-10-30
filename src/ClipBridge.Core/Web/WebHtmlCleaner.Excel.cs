using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ClipBridge.Core.Web;

// Excel 대상(C9)용 정리. Excel은 HTML 표를 스스로 잘 받으므로 Markdown으로 다시 쓰지 않고,
// ToMarkdown과 같은 기준으로 찌꺼기만 고친 원본 문서를 돌려준다. 웹뿐 아니라 Word·한글에서 복사한 HTML에도 쓴다.
// Word는 표 테두리 등을 <style>에 두므로 head·style은 그대로 둔다
public static partial class WebHtmlCleaner
{
    public const string ExcelBlankPlaceholder = "빈칸";

    // 고친 것이 없으면 null (Excel 기본 붙여넣기와 같으므로 통과)
    public static string? CleanForExcel(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var body = (IElement?)document.Body ?? document.DocumentElement;
        var changed = false;

        void Remove(IEnumerable<IElement> elements)
        {
            foreach (var element in elements.ToList())
            {
                element.Remove();
                changed = true;
            }
        }

        Remove(body.QuerySelectorAll("script, noscript, template, [hidden], [aria-hidden='true'], sup.reference, .mw-editsection"));
        Remove(body.QuerySelectorAll("[style]").Where(IsHiddenByStyle));
        Remove(body.QuerySelectorAll("sup").Where(IsFootnote));
        // 표 안에서 칸 내용이 "빈칸"뿐인 칸 (위키백과 "빈칸" 틀, Word·한글 표에 적힌 "빈칸"): 빈 칸으로
        foreach (var cell in body.QuerySelectorAll("td, th").Where(IsBlankPlaceholder))
        {
            cell.TextContent = "";
            changed = true;
        }
        if (!changed)
            return null;

        // macOS public.html은 헤더가 없으므로 문자 인코딩을 밝혀 둔다
        if (document.Head is { } head && head.QuerySelector("meta[charset], meta[http-equiv]") is null)
        {
            var meta = document.CreateElement("meta");
            meta.SetAttribute("charset", "utf-8");
            head.Prepend(meta);
        }
        return document.DocumentElement.OuterHtml;
    }

    private static bool IsBlankPlaceholder(IElement cell) =>
        cell.TextContent.Length > 0 && (cell.ClassList.Contains("table-na") || cell.TextContent.Trim() == ExcelBlankPlaceholder);
}
