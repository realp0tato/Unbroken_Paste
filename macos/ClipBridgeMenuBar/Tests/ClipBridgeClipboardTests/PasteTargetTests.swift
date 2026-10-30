import ClipBridgeClipboard
import ClipBridgeContracts
import XCTest

final class PasteTargetTests: XCTestCase {
    private func resolve(_ id: String?, name: String? = nil, title: String? = nil) -> TargetApp {
        TargetResolver.resolve(bundleID: id, appName: name, windowTitle: title)
    }

    func testDesktopApps() {
        // Bundle IDs confirmed on a Mac (#34)
        XCTAssertEqual(resolve("com.microsoft.Word"), .word)
        XCTAssertEqual(resolve("notion.id"), .notion)
        XCTAssertEqual(resolve("net.golbin.hop"), .hangul)
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

    func testWebTargetsFromBrowserAddress() {
        func web(_ url: String?, title: String? = "제목 없음") -> TargetApp {
            TargetResolver.resolve(bundleID: "com.apple.Safari", appName: "Safari", windowTitle: title, windowURL: url)
        }
        // Address checked first, so pages whose title lacks the service name still count
        XCTAssertEqual(web("https://webhwp.hancomdocs.com/webhwp/?mode=HWP_EDITOR&docId=abc&lang=ko_KR"), .hangul)
        XCTAssertEqual(web("https://www.notion.so/Meeting-notes-123"), .notion)
        XCTAssertEqual(web("https://team.notion.site/page"), .notion)
        // Other Hancom Docs pages (file list, other editors) are not the HWP editor
        XCTAssertEqual(web("https://www.hancomdocs.com/ko/home"), .other)
        XCTAssertEqual(web("https://example.com/notion.so"), .other)
    }

    func testHancomDocsFromBrowserTitle() {
        XCTAssertEqual(resolve("com.google.Chrome", title: "보고서.hwp - 한컴독스"), .hangul)
        XCTAssertEqual(resolve("com.google.Chrome", title: "Report - Hancom Docs"), .hangul)
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
