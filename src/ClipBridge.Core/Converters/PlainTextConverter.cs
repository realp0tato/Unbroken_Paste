using System.Net;
using System.Text;

namespace ClipBridge.Core;

// TX 일반 텍스트 줄간격 정리 (PRD 6.3.9, #6). 글자는 바꾸지 않고 6.4절 줄간격 보정만 한다.
// 직사각형 TSV는 대상에 따라 C1(한글·Word)/C2(Notion)의 표 규칙으로 표를 만든다
public static class PlainTextConverter
{
    public const string Id = "TX";

    // 빈 줄이 있던 자리의 문단 아래 여백. 빈 문단 대신 여백으로 간격을 남긴다 (값은 9.1 검증에서 확정)
    private const string GapAfter = "10pt";

    public static ConversionResult Convert(ClipboardSnapshot snapshot, TargetApp target) =>
        ClipText.GetPlainText(snapshot) is { } text && text.Length <= ExcelClipboard.MaxTextLength
            ? Convert(text, target)
            : ConversionResult.Passthrough;

    public static ConversionResult Convert(string text, TargetApp target)
    {
        if (ReadTable(text) is { } table)
        {
            var converted = target == TargetApp.Notion ? ExcelToNotionConverter.Convert(table) : ExcelToHangulConverter.Convert(table);
            return converted with { ConverterId = Id };
        }

        var plain = LineSpacing.NormalizeText(text);
        return plain.Length == 0 ? ConversionResult.Passthrough : new ConversionResult(Id, false, TextHtml(plain), plain, []);
    }

    // 직사각형 TSV: 2행 2열 이상이고 모든 행의 칸 수가 같음. 끝의 빈 줄은 행으로 세지 않는다.
    // 한 줄짜리 탭 글은 표로 보지 않는다 (SourceDetector도 이 기준으로 표·글을 나눈다)
    internal static TableModel? ReadTable(string text)
    {
        var trimmed = LineSpacing.BlankToEmpty(text).TrimEnd('\n');
        return trimmed.Contains('\t') && TsvParser.Parse(trimmed) is { IsTable: true, RowCount: >= 2 } table ? table : null;
    }

    // 줄마다 한 문단. 빈 줄은 문단으로 만들지 않고 바로 위 문단의 아래 여백으로 바꾼다 (PRD 6.4).
    // 줄 앞·연속 공백은 HTML에서 하나로 줄어들므로 &nbsp;로 지킨다. C5 기호 제거 모드도 쓴다
    internal static string TextHtml(string plain)
    {
        var lines = plain.Split('\n');
        var html = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
                continue;
            var gap = i + 1 < lines.Length && lines[i + 1].Length == 0;
            html.Append("<p style=\"margin:0 0 ").Append(gap ? GapAfter : "0").Append("\">")
                .Append(KeepSpaces(WebUtility.HtmlEncode(lines[i])))
                .Append("</p>");
        }
        return html.ToString();
    }

    private static string KeepSpaces(string line)
    {
        var builder = new StringBuilder(line.Length);
        for (var i = 0; i < line.Length; i++)
            builder.Append(line[i] == ' ' && (i == 0 || line[i - 1] == ' ') ? "&nbsp;" : line[i]);
        return builder.ToString();
    }
}
