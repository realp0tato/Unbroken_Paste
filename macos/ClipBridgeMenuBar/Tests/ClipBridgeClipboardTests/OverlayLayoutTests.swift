import ClipBridgeClipboard
import ClipBridgeContracts
import CoreGraphics
import XCTest

final class OverlayLayoutTests: XCTestCase {
    private let screen = CGRect(x: 0, y: 0, width: 1440, height: 900)

    func testBadgeSitsRightBelowPointer() {
        let frame = OverlayGeometry.badgeFrame(pointer: CGPoint(x: 500, y: 500), screen: screen)

        XCTAssertEqual(frame, CGRect(x: 516, y: 454, width: 30, height: 30))
    }

    func testFlipsLeftAtRightEdge() {
        let frame = OverlayGeometry.badgeFrame(pointer: CGPoint(x: 1430, y: 500), screen: screen)

        XCTAssertEqual(frame.minX, 1430 - 16 - 30)
        XCTAssertLessThanOrEqual(frame.maxX, screen.maxX)
    }

    func testFlipsUpAtBottomEdge() {
        let frame = OverlayGeometry.badgeFrame(pointer: CGPoint(x: 500, y: 10), screen: screen)

        XCTAssertEqual(frame.minY, 10 + 16)
    }

    func testStaysOnSecondScreen() {
        let right = CGRect(x: 1440, y: 0, width: 1920, height: 1080)
        let frame = OverlayGeometry.badgeFrame(pointer: CGPoint(x: 3350, y: 5), screen: right)

        XCTAssertTrue(right.contains(frame), "\(frame)")
    }

    func testConvertedTargetsUseTheirServiceIcon() {
        XCTAssertEqual(OverlayBadge.forPaste(target: .word, bundleID: "com.microsoft.Word", appName: "Microsoft Word"),
                       .app(bundleID: "com.microsoft.Word", letter: "W", passthrough: false))
        // Notion web inside Safari still shows Notion, not Safari
        XCTAssertEqual(OverlayBadge.forPaste(target: .notion, bundleID: "com.apple.Safari", appName: "Safari"),
                       .app(bundleID: "notion.id", letter: "N", passthrough: false))
        XCTAssertEqual(OverlayBadge.forPaste(target: .hangul, bundleID: "net.golbin.hop", appName: "HOP"),
                       .app(bundleID: "net.golbin.hop", letter: "H", passthrough: false))
    }

    func testHancomDocsInBrowserShowsHangulLetterNotBrowserIcon() {
        XCTAssertEqual(OverlayBadge.forPaste(target: .hangul, bundleID: "com.apple.Safari", appName: "Safari"),
                       .letter("한", passthrough: false))
    }

    func testBorderFollowsWhetherAResultWasWritten() {
        // Word target, but Core passed through (e.g. Excel → Word): quiet border
        XCTAssertEqual(OverlayBadge.forPaste(target: .word, bundleID: "com.microsoft.Word", appName: "Microsoft Word", converted: false),
                       .app(bundleID: "com.microsoft.Word", letter: "W", passthrough: true))
        XCTAssertEqual(OverlayBadge.forPaste(target: .hangul, bundleID: "net.golbin.hop", appName: "HOP", converted: true),
                       .app(bundleID: "net.golbin.hop", letter: "H", passthrough: false))
    }

    func testOtherAppsAreMarkedPassthrough() {
        XCTAssertEqual(OverlayBadge.forPaste(target: .other, bundleID: "com.apple.TextEdit", appName: "텍스트 편집기"),
                       .app(bundleID: "com.apple.TextEdit", letter: "텍", passthrough: true))
    }

    func testUnknownAppGetsUnknownBadge() {
        XCTAssertEqual(OverlayBadge.forPaste(target: .other, bundleID: nil, appName: nil), .unknown)
    }

    func testLetterFallsBackToBundleID() {
        XCTAssertEqual(OverlayBadge.forPaste(target: .other, bundleID: "com.example.notes", appName: nil),
                       .app(bundleID: "com.example.notes", letter: "N", passthrough: true))
    }
}
