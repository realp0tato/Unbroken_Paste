using System.Text;
using ClipBridge.Core.Tests.Fixtures;
using ClipBridge.Core.Web;

namespace ClipBridge.Core.Tests.Web;

// 웹 HTML 정리 (#9) — 기준선(docs/baseline)에서 찾은 찌꺼기를 규칙마다 확인한다
public class WebHtmlCleanerTests
{
    private static string Md(string html, bool keepLinks = false) => WebHtmlCleaner.ToMarkdown(html, keepLinks).Markdown;

    // ---- 지우는 것 ----

    [Theory]
    [InlineData("<p>본문<sup class=\"reference\"><a href=\"#cite_note-16\">[16]</a></sup></p>", "본문")]
    [InlineData("<p>서구<sup><a href=\"#cite_note-a\">[a]</a></sup>, 무안군</p>", "서구, 무안군")]
    [InlineData("<p>글<script>alert(1)</script><style>p{}</style></p>", "글")]
    [InlineData("<p>보임</p><p hidden>숨김</p><p aria-hidden=\"true\">숨김</p><p style=\"display: none\">숨김</p>", "보임")]
    [InlineData("<p>글</p><button>Copy</button><img src=\"a.png\" alt=\"그림\"><svg><text>x</text></svg>", "글")]
    [InlineData("<h3>제목<span class=\"mw-editsection\">[편집]</span></h3>", "### 제목")]
    public void RemovesSiteJunk(string html, string expected)
    {
        Assert.Equal(expected, Md(html));
    }

    [Fact]
    public void WikipediaEmptyCellMarker_BecomesEmpty()
    {
        var md = Md("<table><tr><th>시</th><th>구</th></tr><tr><td class=\"table-na\">빈칸</td><td>25</td></tr></table>");

        Assert.Equal("| 시 | 구 |\n|---|---|\n|  | 25 |", md);
    }

    [Fact]
    public void SidebarWithoutContent_IsRemoved_SidebarWithContent_IsKept()
    {
        Assert.Equal("본문", Md("<nav><ul><li><a href=\"#a\">목차</a></li></ul></nav><p>본문</p>"));
        Assert.Equal("본문", Md("<aside><section><p>본문</p></section></aside>"));
    }

    [Fact]
    public void Details_WithoutHeading_IsRemoved_WithHeading_DropsSummaryOnly()
    {
        Assert.Equal("본문", Md("<details><summary>Baseline</summary><p>설명</p></details><p>본문</p>"));
        Assert.Equal("# 제목\n\n본문", Md("<details><summary>요약</summary><h1>제목</h1><p>본문</p></details>"));
    }

    [Fact]
    public void CodeHeaderBeforePre_IsRemoved()
    {
        var md = Md("<div><span>JS</span><button>Copy</button></div><pre><code>let a = 1;</code></pre>");

        Assert.Equal("```\nlet a = 1;\n```", md);
    }

    [Fact]
    public void UnsupportedElements_RaiseS3()
    {
        var result = WebHtmlCleaner.ToMarkdown("<p>글</p><iframe src=\"x\"></iframe><form><input></form>", keepLinks: false);

        Assert.Equal("글", result.Markdown);
        Assert.Equal("S3", Assert.Single(result.Signals).Id);
    }

    // ---- 남기는 구조 ----

    [Fact]
    public void Structure_HeadingsFormattingListsCode()
    {
        var html = "<h2>소제목</h2><p><strong>굵게</strong>와 <em>기울임</em>, <code>코드</code>, <del>취소</del></p>"
                 + "<ul><li>하나<ul><li>안쪽</li></ul></li><li>둘</li></ul><ol start=\"3\"><li>셋</li></ol>"
                 + "<pre><code>  들여쓰기\n둘째 줄</code></pre>";

        Assert.Equal(
            "## 소제목\n\n**굵게**와 *기울임*, `코드`, ~~취소~~\n\n- 하나\n   - 안쪽\n- 둘\n\n3. 셋\n\n```\n  들여쓰기\n둘째 줄\n```",
            Md(html));
    }

    [Fact]
    public void CustomTagWrappingPre_IsTreatedAsBlock()
    {
        Assert.Equal("```\na\nb\n```", Md("<mdn-code-example><pre><code>a\nb</code></pre></mdn-code-example>"));
    }

    [Theory]
    [InlineData(false, "<p><a href=\"https://example.com\">링크</a></p>", "링크")]
    [InlineData(true, "<p><a href=\"https://example.com\">링크</a></p>", "[링크](https://example.com)")]
    [InlineData(true, "<p><a href=\"javascript:x\">위험</a></p>", "위험")]
    [InlineData(true, "<p><a href=\"#section\">앵커</a></p>", "앵커")]
    [InlineData(true, "<h2><a href=\"https://example.com/#a\">제목</a></h2>", "## 제목")]
    public void Links(bool keepLinks, string html, string expected)
    {
        Assert.Equal(expected, Md(html, keepLinks));
    }

    // 위키백과 주소의 괄호(시_(행정_구역))가 짝이 안 맞으면 Markdown이 링크로 읽지 못해 [글](주소)가 글자로 붙는다
    [Fact]
    public void LinkWithParentheses_StaysLinkInNotionHtml()
    {
        var html = "<p><a href=\"https://ko.wikipedia.org/wiki/Si_(unit)\">시</a></p>";

        var result = WebToNotionConverter.Convert(html);

        Assert.Contains("<a href=\"https://ko.wikipedia.org/wiki/Si_%28unit%29\">시</a>", result.Html);
        Assert.DoesNotContain("](", result.Html);
    }

    [Fact]
    public void MergedCells_AreUnmergedWithValueInFirstCell()
    {
        var md = Md("<table><tr><th colspan=\"2\">상반기</th><th rowspan=\"2\">비고</th></tr><tr><td>1분기</td><td>2분기</td></tr></table>");

        Assert.Equal("| 상반기 |  | 비고 |\n|---|---|---|\n| 1분기 | 2분기 |  |", md);
    }

    [Theory]
    [InlineData("<p>2*3 과 a_b, [x]</p>", @"2\*3 과 a\_b, \[x\]")]
    [InlineData("<p># 해시로 시작</p>", @"\# 해시로 시작")]
    [InlineData("<p>1. 번호처럼 보이는 문장</p>", @"1\. 번호처럼 보이는 문장")]
    [InlineData("<p>- 줄표로 시작</p>", @"\- 줄표로 시작")]
    public void PlainCharacters_AreEscapedSoTheyStayText(string html, string expected)
    {
        Assert.Equal(expected, Md(html));
    }

    [Fact]
    public void EscapedCharacters_ComeBackAsTextAfterRendering()
    {
        var html = "<p>2*3, a_b, # 해시, 1. 번호</p>";

        var plain = MarkdownToHangulConverter.Convert(Md(html), MarkdownMode.Strip).PlainText;

        Assert.Equal("2*3, a_b, # 해시, 1. 번호", plain);
    }

    // ---- 실제 샘플 (#32) ----

    private static string Fixture(string name) =>
        Md(Encoding.UTF8.GetString(Fixtures.Fixture.Load("web/mac-" + name).Formats[ClipFormats.MacHtml]));

    [Theory]
    [InlineData("article")]
    [InlineData("blog")]
    [InlineData("table")]
    [InlineData("mixed")]
    public void Fixtures_ChromeAndSafari_GiveSameResult(string kind)
    {
        // Chrome 블로그 샘플은 복사 범위가 한 글자 늦게 시작해 제목 첫 글자가 빠져 있다
        Assert.Equal(Fixture("safari-" + kind), Fixture("chrome-" + kind).Replace("# lipboard", "# Clipboard"));
    }

    [Theory]
    [InlineData("chrome-article")]
    [InlineData("safari-blog")]
    [InlineData("chrome-table")]
    [InlineData("safari-mixed")]
    public void Fixtures_HaveNoSiteJunk(string name)
    {
        var md = Fixture(name);

        foreach (var junk in new[] { "빈칸", "[16]", "\\[16\\]", "[a]", "jsCopy", "Copy", "In this article", "Baseline", "Widely available" })
            Assert.DoesNotContain(junk, md);
        Assert.DoesNotContain("style=", md);
    }

    [Fact]
    public void Fixture_Blog_KeepsHeadingsAndCodeBlock()
    {
        var md = Fixture("safari-blog");

        Assert.StartsWith("# Clipboard API", md);
        Assert.Contains("## 클립보드 접근", md);
        Assert.Contains("```\nnavigator.clipboard\n  .readText()", md);
    }

    [Fact]
    public void Fixture_Table_Is8ColumnsBy18Rows()
    {
        var lines = Fixture("chrome-table").Split('\n');

        Assert.Equal(19, lines.Length);   // 머리글 + 구분선 + 17행
        Assert.All(lines, line => Assert.Equal(9, line.Count(c => c == '|')));
        Assert.StartsWith("| 행정 구역 | 넓이 (km2) |", lines[0]);
    }

    [Fact]
    public void Fixture_Article_SubheadingsAreHeadings()
    {
        var md = Fixture("chrome-article");

        Assert.Contains("### 아미가OS", md);
        Assert.Contains("### 안드로이드", md);
    }
}
