import ClipBridgeClipboard
import ClipBridgeContracts
import XCTest

private final class FakePasteboard: PasteboardSource {
    var changeCount = 100
    private var order: [String] = []
    private var items: [String: Data] = [:]

    var typeNames: [String] { order }

    func data(forType type: String) -> Data? { items[type] }

    func writeText(_ text: String) -> Int {
        put([(ClipFormats.macUtf8Text, Data(text.utf8))])
        return changeCount
    }

    /// Simulates another app copying.
    func copy(_ formats: [(String, Data)]) {
        put(formats)
    }

    func copyText(_ text: String, html: String? = nil) {
        var formats = [(ClipFormats.macUtf8Text, Data(text.utf8))]
        if let html { formats.insert((ClipFormats.macHtml, Data(html.utf8)), at: 0) }
        put(formats)
    }

    private func put(_ formats: [(String, Data)]) {
        changeCount += 1
        order = formats.map(\.0)
        items = Dictionary(formats, uniquingKeysWith: { first, _ in first })
    }
}

@MainActor
final class ClipboardMonitorTests: XCTestCase {
    // XCTest makes a fresh instance per test, so each test starts clean.
    private let pasteboard = FakePasteboard()
    // Lazy, so each test calls start() first: the monitor treats what is already on the pasteboard as seen.
    private lazy var monitor = ClipboardMonitor(pasteboard: pasteboard, frontmostApp: { "com.microsoft.Excel" })

    private func start() {
        _ = monitor
    }

    func testNoChangeIsNotACopy() {
        start()
        XCTAssertNil(monitor.poll())
        XCTAssertNil(monitor.snapshot)
    }

    func testCopyKeepsEveryFormatSourceAppAndSequence() {
        start()
        pasteboard.copyText("a\tb", html: "<table></table>")

        guard case .copied(let snapshot) = monitor.poll() else { return XCTFail("expected a copy") }

        XCTAssertEqual(snapshot.formats[ClipFormats.macHtml], Data("<table></table>".utf8))
        XCTAssertEqual(snapshot.formats[ClipFormats.macUtf8Text], Data("a\tb".utf8))
        XCTAssertEqual(snapshot.sourceApp, "com.microsoft.Excel")
        XCTAssertEqual(snapshot.sequenceNumber, Int64(pasteboard.changeCount))
        XCTAssertEqual(monitor.snapshot, snapshot)
        XCTAssertEqual(monitor.history, ["a\tb"])
    }

    func testNonTextCopyIsKeptButNotInHistory() {
        start()
        pasteboard.copy([("public.png", Data([0x89, 0x50]))])

        guard case .copied(let snapshot) = monitor.poll() else { return XCTFail("expected a copy") }

        XCTAssertEqual(snapshot.formats["public.png"], Data([0x89, 0x50]))
        XCTAssertTrue(monitor.history.isEmpty)
    }

    func testOwnWriteIsNotACopyAndKeepsSnapshot() {
        start()
        pasteboard.copyText("original")
        _ = monitor.poll()
        let original = monitor.snapshot

        let ownCount = pasteboard.writeText("converted result")
        monitor.recordOwnWrite(changeCount: ownCount)

        XCTAssertNil(monitor.poll())
        XCTAssertEqual(monitor.snapshot, original)
        XCTAssertEqual(monitor.history, ["original"])
    }

    func testUserCopyAfterOwnWriteIsDetected() {
        start()
        monitor.recordOwnWrite(changeCount: pasteboard.writeText("converted"))
        pasteboard.copyText("user copy")

        guard case .copied(let snapshot) = monitor.poll() else { return XCTFail("expected a copy") }

        XCTAssertEqual(snapshot.formats[ClipFormats.macUtf8Text], Data("user copy".utf8))
    }

    func testConcealedCopyIsIgnoredAndDropsPreviousSnapshot() {
        start()
        pasteboard.copyText("visible")
        _ = monitor.poll()

        pasteboard.copy([("org.nspasteboard.ConcealedType", Data()), (ClipFormats.macUtf8Text, Data("secret".utf8))])

        XCTAssertEqual(monitor.poll(), .ignored)
        XCTAssertNil(monitor.snapshot)
        XCTAssertEqual(monitor.history, ["visible"])
    }

    func testEmptyPasteboardIsIgnored() {
        start()
        pasteboard.copyText("a")
        _ = monitor.poll()

        pasteboard.copy([])

        XCTAssertEqual(monitor.poll(), .ignored)
        XCTAssertNil(monitor.snapshot)
    }

    func testLaterCopyMakesEarlierSnapshotStale() {
        start()
        pasteboard.copyText("A")
        guard case .copied(let a) = monitor.poll() else { return XCTFail("expected A") }
        pasteboard.copyText("B")
        guard case .copied(let b) = monitor.poll() else { return XCTFail("expected B") }

        XCTAssertFalse(monitor.isCurrent(a))
        XCTAssertTrue(monitor.isCurrent(b))
    }

    func testFastCopiesBetweenPollsKeepOnlyTheLatest() {
        start()
        pasteboard.copyText("A")
        pasteboard.copyText("B")

        guard case .copied(let snapshot) = monitor.poll() else { return XCTFail("expected a copy") }

        XCTAssertEqual(snapshot.formats[ClipFormats.macUtf8Text], Data("B".utf8))
        XCTAssertNil(monitor.poll())
    }

    func testHistoryMovesRepeatsToTopAndKeepsTen() {
        start()
        for i in 0..<12 {
            pasteboard.copyText("item \(i)")
            _ = monitor.poll()
        }
        pasteboard.copyText("item 5")
        _ = monitor.poll()

        XCTAssertEqual(monitor.history.count, ClipboardMonitor.maxHistory)
        XCTAssertEqual(monitor.history.first, "item 5")
        XCTAssertEqual(monitor.history.filter { $0 == "item 5" }.count, 1)
        XCTAssertFalse(monitor.history.contains("item 0"))
    }

    func testRecopyBecomesSnapshotWithoutBeingCountedAgain() {
        start()
        pasteboard.copyText("first")
        _ = monitor.poll()
        pasteboard.copyText("second")
        _ = monitor.poll()

        let snapshot = monitor.recopy(at: 1)

        XCTAssertEqual(snapshot?.formats[ClipFormats.macUtf8Text], Data("first".utf8))
        XCTAssertNil(snapshot?.sourceApp)
        XCTAssertEqual(monitor.snapshot, snapshot)
        XCTAssertEqual(monitor.history, ["first", "second"])
        XCTAssertNil(monitor.poll())
    }

    func testPreviewCollapsesWhitespaceAndTruncates() {
        XCTAssertEqual(ClipboardMonitor.preview("a\n\n  b"), "a b")
        XCTAssertEqual(ClipboardMonitor.preview(String(repeating: "가", count: 30)), String(repeating: "가", count: 28) + "…")
    }
}
