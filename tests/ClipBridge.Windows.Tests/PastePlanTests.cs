using System.Text;
using ClipBridge.Core;

namespace ClipBridge.Windows.Tests;

// macOS PastePlanTests와 같은 경우를 Windows 규칙(소유 창·시퀀스 번호)으로 확인한다
public class PastePlanTests
{
    private const long Copy = 10;

    private static readonly ConversionResult Converted = new("C1", false, "<table></table>", "표", []);
    private static readonly ConversionResult TextOnly = new("TX", false, null, "줄", []);

    private static PastePlan Plan(
        ConversionResult result, long? copy = Copy, long clipboard = Copy, bool owns = false, LastWrite? last = null,
        TargetApp target = TargetApp.Hangul) =>
        PastePlan.Decide(copy, clipboard, owns, last, result, target);

    [Fact]
    public void WritesResultOverTheOriginal()
    {
        var plan = Plan(Converted);

        Assert.Equal(PasteAction.WriteResult, plan.Action);
        Assert.Equal([ClipFormats.WinHtml, ClipFormats.WinUnicodeText], plan.Payload.Keys);
        Assert.Equal(Encoding.Unicode.GetBytes("표\0"), plan.Payload[ClipFormats.WinUnicodeText]);
        var cfHtml = Encoding.UTF8.GetString(plan.Payload[ClipFormats.WinHtml]);
        Assert.StartsWith("Version:0.9", cfHtml);
        Assert.Contains("<!--StartFragment--><table></table><!--EndFragment-->", cfHtml);

        Assert.Equal([ClipFormats.WinUnicodeText], Plan(TextOnly).Payload.Keys);
    }

    [Fact]
    public void NoCopyOrNoResult_LeavesTheClipboard()
    {
        Assert.Equal(PastePlan.Leave, Plan(Converted, copy: null));
        Assert.Equal(PastePlan.Leave, Plan(ConversionResult.Passthrough));
        Assert.Equal(PastePlan.Leave, Plan(new ConversionResult("C1", false, null, null, [])));
    }

    // 스냅샷 뒤에 다른 앱이 클립보드를 바꿨으면(아직 감지 못한 새 복사) 절대 덮어쓰지 않는다
    [Fact]
    public void NewerCopyNotYetSeen_IsNeverOverwritten()
    {
        Assert.Equal(PastePlan.Leave, Plan(Converted, clipboard: Copy + 1));
        Assert.Equal(PastePlan.Leave, Plan(Converted, clipboard: Copy + 2, owns: false, last: new(Copy, TargetApp.Hangul)));
    }

    [Fact]
    public void SameTargetTwice_DoesNotRewrite() =>
        Assert.Equal(PastePlan.Leave, Plan(Converted, clipboard: Copy + 1, owns: true, last: new(Copy, TargetApp.Hangul)));

    // 9.1: 한 번 복사 → 한글·Notion 순서로 붙이면 대상마다 그 대상의 결과
    [Fact]
    public void OneCopyToSeveralTargets_WritesEachTargetsResult()
    {
        var plan = Plan(TextOnly, clipboard: Copy + 1, owns: true, last: new(Copy, TargetApp.Hangul), target: TargetApp.Notion);

        Assert.Equal(PasteAction.WriteResult, plan.Action);
    }

    // PRD 6.5: 다른 대상용 결과를 쓴 뒤 통과 대상(메모장 등)에 붙이면 원본을 되돌린다
    [Fact]
    public void PassthroughAfterOurWrite_RestoresTheOriginal()
    {
        Assert.Equal(PastePlan.RestoreOriginal,
            Plan(ConversionResult.Passthrough, clipboard: Copy + 1, owns: true, last: new(Copy, TargetApp.Hangul), target: TargetApp.Other));

        // 이미 원본을 되돌려 놓았으면 그대로 둔다
        var afterRestore = new LastWrite(Copy, null);
        Assert.Equal(PastePlan.Leave,
            Plan(ConversionResult.Passthrough, clipboard: Copy + 2, owns: true, last: afterRestore, target: TargetApp.Other));
        Assert.Equal(PasteAction.WriteResult,
            Plan(Converted, clipboard: Copy + 2, owns: true, last: afterRestore, target: TargetApp.Word).Action);
    }

    // 원본이 그대로면 통과 대상에서는 클립보드를 건드리지 않는다 (Excel → Word가 Excel 고유 포맷을 그대로 받음)
    [Fact]
    public void PassthroughOnTheOriginal_LeavesIt() =>
        Assert.Equal(PastePlan.Leave, Plan(ConversionResult.Passthrough, target: TargetApp.Word));

    // 이전 복사 때 쓴 기록은 지금 복사의 것으로 치지 않는다
    [Fact]
    public void WriteFromAnEarlierCopy_DoesNotCount()
    {
        var old = new LastWrite(Copy - 5, TargetApp.Hangul);

        Assert.Equal(PastePlan.Leave, Plan(ConversionResult.Passthrough, owns: false, last: old, target: TargetApp.Other));
        Assert.Equal(PastePlan.Leave, Plan(ConversionResult.Passthrough, clipboard: Copy + 1, owns: true, last: old, target: TargetApp.Other));
    }
}
