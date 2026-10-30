import UnbrokenPasteClipboard
import UnbrokenPasteContracts
import XCTest

final class PastePlanTests: XCTestCase {
    private let converted = ConversionResult(converterId: "C1", isPassthrough: false, html: "<table></table>", plainText: "표", signals: [])
    private let textOnly = ConversionResult(converterId: "TX", isPassthrough: false, html: nil, plainText: "줄", signals: [])

    private func plan(seq: Int64? = 10, count: Int64 = 10, last: PastePlan.Write? = nil,
                      result: ConversionResult, target: TargetApp = .hangul) -> PastePlan {
        PastePlan.decide(snapshotSequence: seq, changeCount: count, lastWrite: last, result: result, target: target)
    }

    func testWritesResultOverTheOriginal() {
        XCTAssertEqual(plan(result: converted), .writeResult([
            ClipFormats.macHtml: Data("<table></table>".utf8),  // no CF_HTML header on macOS
            ClipFormats.macUtf8Text: Data("표".utf8),
        ]))
        XCTAssertEqual(plan(result: textOnly), .writeResult([ClipFormats.macUtf8Text: Data("줄".utf8)]))
    }

    func testNoCopyOrNoResultLeavesTheClipboard() {
        XCTAssertEqual(plan(seq: nil, result: converted), .leave)
        XCTAssertEqual(plan(result: .passthrough), .leave)
        // A non-passthrough result with nothing to write behaves like a passthrough
        XCTAssertEqual(plan(result: ConversionResult(converterId: "C1", isPassthrough: false, html: nil, plainText: nil, signals: [])), .leave)
    }

    func testNewerCopyNotYetSeenIsNeverOverwritten() {
        // Snapshot is copy 10, but the clipboard is already at 11 and UP did not write it
        XCTAssertEqual(plan(count: 11, result: converted), .leave)
        XCTAssertEqual(plan(count: 12, last: .init(changeCount: 11, sequence: 10, target: .hangul), result: converted), .leave)
    }

    func testSameTargetTwiceDoesNotRewrite() {
        let last = PastePlan.Write(changeCount: 11, sequence: 10, target: .hangul)
        XCTAssertEqual(plan(count: 11, last: last, result: converted, target: .hangul), .leave)
    }

    func testOneCopyToSeveralTargetsWritesEachTargetsResult() {
        let afterHangul = PastePlan.Write(changeCount: 11, sequence: 10, target: .hangul)
        XCTAssertEqual(plan(count: 11, last: afterHangul, result: textOnly, target: .notion),
                       .writeResult([ClipFormats.macUtf8Text: Data("줄".utf8)]))
    }

    func testPassthroughAfterOurWriteRestoresTheOriginal() {
        let afterHangul = PastePlan.Write(changeCount: 11, sequence: 10, target: .hangul)
        XCTAssertEqual(plan(count: 11, last: afterHangul, result: .passthrough, target: .other), .restoreOriginal)

        let afterRestore = PastePlan.Write(changeCount: 12, sequence: 10, target: nil)
        XCTAssertEqual(plan(count: 12, last: afterRestore, result: .passthrough, target: .other), .leave)
        XCTAssertEqual(plan(count: 12, last: afterRestore, result: converted, target: .word), .writeResult([
            ClipFormats.macHtml: Data("<table></table>".utf8),
            ClipFormats.macUtf8Text: Data("표".utf8),
        ]))
    }

    func testWriteFromAnEarlierCopyDoesNotCount() {
        // UP wrote for copy 8; the user then copied 10 (clipboard = 10): the clipboard holds the original
        let old = PastePlan.Write(changeCount: 9, sequence: 8, target: .hangul)
        XCTAssertEqual(plan(count: 10, last: old, result: .passthrough, target: .other), .leave)
    }
}
