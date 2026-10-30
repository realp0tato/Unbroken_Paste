import ClipBridgeClipboard
import ClipBridgeContracts
import XCTest

final class PasteTargetTests: XCTestCase {
    private func resolve(_ id: String?, name: String? = nil, title: String? = nil) -> TargetApp {
        TargetResolver.resolve(bundleID: id, appName: name, windowTitle: title)
    }

    func testDesktopApps() {
        XCTAssertEqual(resolve("com.microsoft.Word"), .word)
        XCTAssertEqual(resolve("notion.id"), .notion)
    }

    func testHancomHwpVariants() {
        XCTAssertEqual(resolve("com.hancom.office.hwp12.mac.general"), .hangul)
        XCTAssertEqual(resolve("com.hancom.office.hwp.mac"), .hangul)
        XCTAssertEqual(resolve("com.hancom.office.hancomoffice", name: "Hancom Office HWP"), .hangul)
        XCTAssertEqual(resolve("com.hancom.office.hancomoffice", name: "한컴오피스 한글"), .hangul)
    }

    func testOtherHancomAppsPassThrough() {
        XCTAssertEqual(resolve("com.hancom.office.hcell.mac", name: "Hancom Office HCell"), .other)
    }

    func testNotionWebOnlyFromBrowserTitle() {
        XCTAssertEqual(resolve("com.google.Chrome", title: "회의록 – Notion"), .notion)
        XCTAssertEqual(resolve("com.apple.Safari", title: "notion"), .notion)
        XCTAssertEqual(resolve("com.google.Chrome", title: "Google 문서"), .other)
        XCTAssertEqual(resolve("com.google.Chrome", title: nil), .other)
        XCTAssertEqual(resolve("com.apple.TextEdit", title: "Notion 메모"), .other)
    }

    func testUnknownOrMissingAppPassesThrough() {
        XCTAssertEqual(resolve("com.microsoft.Excel"), .other)
        XCTAssertEqual(resolve(nil), .other)
    }

    func testWindowTitleOnlyForBrowsers() {
        XCTAssertTrue(TargetResolver.needsWindowTitle(bundleID: "com.apple.Safari"))
        XCTAssertFalse(TargetResolver.needsWindowTitle(bundleID: "com.microsoft.Word"))
        XCTAssertFalse(TargetResolver.needsWindowTitle(bundleID: nil))
    }

    func testOnlyPlainCmdVIsAPaste() {
        func paste(_ code: Int64 = PasteKey.vKeyCode, cmd: Bool = true, shift: Bool = false, opt: Bool = false, ctrl: Bool = false, rep: Bool = false) -> Bool {
            PasteKey.isPaste(keyCode: code, command: cmd, shift: shift, option: opt, control: ctrl, isRepeat: rep)
        }
        XCTAssertTrue(paste())
        XCTAssertFalse(paste(cmd: false))
        XCTAssertFalse(paste(shift: true))
        XCTAssertFalse(paste(opt: true))
        XCTAssertFalse(paste(ctrl: true))
        XCTAssertFalse(paste(rep: true))
        XCTAssertFalse(paste(8))  // C
    }
}
