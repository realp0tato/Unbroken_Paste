using System.Text;
using System.Text.RegularExpressions;
using ClipBridge.Core;

namespace ClipBridge.Core.Tests.Contracts;

public class CfHtmlTests
{
    private static int Offset(string cfHtml, string name) =>
        int.Parse(Regex.Match(cfHtml, $@"{name}:(\d+)").Groups[1].Value);

    [Theory]
    [InlineData("<p>hello</p>", null)]
    [InlineData("<p>한글 표 ✓</p>", "https://example.com/")]
    public void Wrap_OffsetsPointToFragmentInUtf8Bytes(string fragment, string? sourceUrl)
    {
        var bytes = CfHtml.Wrap(fragment, sourceUrl);
        var text = Encoding.UTF8.GetString(bytes);

        var start = Offset(text, "StartFragment");
        var end = Offset(text, "EndFragment");
        Assert.Equal(fragment, Encoding.UTF8.GetString(bytes, start, end - start));

        var startHtml = Offset(text, "StartHTML");
        Assert.StartsWith("<html>", Encoding.UTF8.GetString(bytes, startHtml, bytes.Length - startHtml));
        Assert.Equal(bytes.Length, Offset(text, "EndHTML"));

        if (sourceUrl is not null)
            Assert.Contains($"SourceURL:{sourceUrl}\r\n", text);
    }
}
