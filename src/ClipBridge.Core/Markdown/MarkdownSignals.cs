using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ClipBridge.Core.Markdown;

// 의심 신호 S4: Markdown 문법이 애매함 (PRD 6.6.2)
internal static class MarkdownSignals
{
    private static readonly string[] UnclosedMarks = ["**", "__", "~~", "`"];

    public static IReadOnlyList<SuspicionSignal> Find(MarkdownDocument document, string source)
    {
        var details = new List<string>();

        // 짝이 맞지 않은 기호는 해석되지 않고 글자로 남는다
        var literals = document.Descendants<LiteralInline>()
            .Where(literal => !literal.IsFirstCharacterEscaped)
            .ToList();
        var unclosed = UnclosedMarks
            .Where(mark => literals.Any(literal => literal.Content.ToString().Contains(mark)))
            .ToList();
        if (unclosed.Count > 0)
            details.Add("닫히지 않은 기호: " + string.Join(" ", unclosed));
        if (literals.Any(literal => HasLoneAsterisk(literal, source)))
            details.Add("단독 *");

        // 구분선(|---|)이 없어 표로 해석되지 않은 파이프 줄
        // 파싱이 끝나면 블록의 줄 정보가 비워지므로 원문에서 문단 위치를 잘라 본다
        var pipeLines = document.Descendants<ParagraphBlock>()
            .SelectMany(paragraph => source.Substring(paragraph.Span.Start, paragraph.Span.Length).Split('\n'))
            .Select(line => line.Trim())
            .Any(line => line.Length > 1 && line.StartsWith('|') && line.IndexOf('|', 1) > 0);
        if (pipeLines)
            details.Add("구분선 없는 파이프 줄");

        return details.Count == 0 ? [] : [new SuspicionSignal("S4", 1, string.Join(", ", details))];
    }

    // "*강조"처럼 한쪽만 붙은 *는 강조 기호를 의도했을 가능성이 높다. "2*3"처럼 양쪽이 글자면 일반 기호다.
    // 짝 없는 *는 따로 떨어진 글자 조각이 되므로 앞뒤 글자는 원문에서 본다
    private static bool HasLoneAsterisk(LiteralInline literal, string source)
    {
        var end = Math.Min(literal.Span.End, source.Length - 1);
        for (var i = literal.Span.Start; i <= end; i++)
        {
            if (source[i] != '*')
                continue;
            var before = i > 0 && !char.IsWhiteSpace(source[i - 1]);
            var after = i < source.Length - 1 && !char.IsWhiteSpace(source[i + 1]);
            if (before != after)
                return true;
        }
        return false;
    }
}
