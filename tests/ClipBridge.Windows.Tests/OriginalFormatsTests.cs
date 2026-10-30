using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

// #80: 원본을 되돌릴 때 Excel의 OLE·링크·이미지 포맷까지 원래 순서대로 남는다
public class OriginalFormatsTests
{
    private static byte[] Bytes(int length, byte value = 1) => Enumerable.Repeat(value, length).ToArray();

    [Fact]
    public void Merge_KeepsClipboardOrderAndUsesSnapshotDataForConvertedFormats()
    {
        var snapshot = new Dictionary<string, byte[]>
        {
            [ClipFormats.WinHtml] = Bytes(3, 7),
            [ClipFormats.WinUnicodeText] = Bytes(2, 8),
        };
        // 실제 Excel 복사 순서. 스냅샷 포맷은 두 번째 읽기에서 "읽지 않음"(null)이다
        (string, byte[]?)[] order =
        [
            ("Biff12", Bytes(4)), (ClipFormats.WinHtml, null), (ClipFormats.WinUnicodeText, null),
            ("Embed Source", Bytes(5)), ("Link Source", Bytes(6)),
        ];

        var merged = OriginalFormats.Merge(order, snapshot, out var skipped);

        Assert.Equal(["Biff12", ClipFormats.WinHtml, ClipFormats.WinUnicodeText, "Embed Source", "Link Source"], merged.Keys);
        Assert.Equal(Bytes(3, 7), merged[ClipFormats.WinHtml]);
        Assert.Empty(skipped);
    }

    [Fact]
    public void Merge_SkipsOleInternalUnreadAndOversizedFormats()
    {
        var snapshot = new Dictionary<string, byte[]> { [ClipFormats.WinUnicodeText] = Bytes(2) };
        (string, byte[]?)[] order =
        [
            ("DataObject", Bytes(4)), ("Ole Private Data", Bytes(4)), ("CF_ENHMETAFILE", null),
            ("PNG", new byte[OriginalFormats.MaxBytesPerFormat + 1]), (ClipFormats.WinUnicodeText, null), ("Csv", Bytes(3)),
        ];

        var merged = OriginalFormats.Merge(order, snapshot, out var skipped);

        Assert.Equal([ClipFormats.WinUnicodeText, "Csv"], merged.Keys);
        Assert.Equal(["DataObject", "Ole Private Data", "CF_ENHMETAFILE", "PNG"], skipped);
    }

    [Fact]
    public void Merge_StopsAddingWhenTotalLimitIsReached()
    {
        var big = (int)(OriginalFormats.MaxBytesPerFormat - 1);
        (string, byte[]?)[] order = [("A", new byte[big]), ("B", new byte[big]), ("C", new byte[big]), ("D", new byte[big]), ("E", new byte[big])];

        var merged = OriginalFormats.Merge(order, new Dictionary<string, byte[]>(), out var skipped);

        Assert.True(merged.Values.Sum(data => (long)data.Length) <= OriginalFormats.MaxTotalBytes);
        Assert.Contains("A", merged.Keys);
        Assert.Equal(["E"], skipped);
    }

    // 두 번째로 읽는 사이 스냅샷 포맷이 목록에서 빠져도 변환 입력은 잃지 않는다
    [Fact]
    public void Merge_KeepsSnapshotFormatsMissingFromOrder()
    {
        var snapshot = new Dictionary<string, byte[]> { [ClipFormats.WinHtml] = Bytes(3) };

        var merged = OriginalFormats.Merge([("Csv", Bytes(1))], snapshot, out _);

        Assert.Equal(["Csv", ClipFormats.WinHtml], merged.Keys);
    }

    [Fact]
    public void ShouldRead_SkipsSnapshotAndOleInternalFormats()
    {
        var snapshot = new HashSet<string> { ClipFormats.WinHtml };

        Assert.False(OriginalFormats.ShouldRead(ClipFormats.WinHtml, snapshot));
        Assert.False(OriginalFormats.ShouldRead("Ole Private Data", snapshot));
        Assert.True(OriginalFormats.ShouldRead("Embed Source", snapshot));
    }
}
