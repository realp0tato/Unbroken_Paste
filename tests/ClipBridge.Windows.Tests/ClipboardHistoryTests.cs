using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

// #44: macOS 클립보드 기록과 같은 규칙 (PRD 4.2.2)
public class ClipboardHistoryTests
{
    [Fact]
    public void Remember_NewestFirstMovesDuplicatesAndKeepsTen()
    {
        var history = new ClipboardHistory();
        for (var i = 1; i <= 12; i++)
            history.Remember($"글 {i}");
        history.Remember("글 5");

        Assert.Equal(ClipboardHistory.MaxItems, history.Items.Count);
        Assert.Equal(["글 5", "글 12", "글 11", "글 10", "글 9", "글 8", "글 7", "글 6", "글 4", "글 3"], history.Items);
    }

    [Fact]
    public void Remember_IgnoresBlankText()
    {
        var history = new ClipboardHistory();
        history.Remember(" \r\n\t");

        Assert.Empty(history.Items);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var history = new ClipboardHistory();
        history.Remember("가");
        history.Clear();

        Assert.Empty(history.Items);
    }

    [Fact]
    public void TextOf_ReadsUnicodeTextWithoutTerminator()
    {
        var snapshot = new ClipboardSnapshot(
            new Dictionary<string, byte[]> { [ClipFormats.WinUnicodeText] = Encoding.Unicode.GetBytes("표\t값\r\n\0") }, "EXCEL.EXE", 1);

        Assert.Equal("표\t값\r\n", ClipboardHistory.TextOf(snapshot));
        Assert.Null(ClipboardHistory.TextOf(new ClipboardSnapshot(new Dictionary<string, byte[]>(), null, 2)));
    }

    [Theory]
    [InlineData("짧은 글", "짧은 글")]
    [InlineData("첫 줄\r\n\r\n  둘째\t줄", "첫 줄 둘째 줄")]
    [InlineData("가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다", "가나다라마바사아자차카타파하가나다라마바사아자차카타파하…")]
    public void Preview_CollapsesWhitespaceAndCutsAt28(string text, string expected) =>
        Assert.Equal(expected, ClipboardHistory.Preview(text));
}
