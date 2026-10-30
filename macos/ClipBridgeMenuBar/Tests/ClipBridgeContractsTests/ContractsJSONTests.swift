import ClipBridgeContracts
import XCTest

/// Mirrors tests/ClipBridge.Core.Tests/Contracts/ContractsJsonTests.cs so both sides agree on the wire format.
final class ContractsJSONTests: XCTestCase {
    private func encode<T: Encodable>(_ value: T) throws -> String {
        let encoder = ContractsJSON.encoder()
        encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
        return String(decoding: try encoder.encode(value), as: UTF8.self)
    }

    private func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        try ContractsJSON.decoder().decode(type, from: Data(json.utf8))
    }

    func testSnapshotRoundTripsWithBase64Bytes() throws {
        let snapshot = ClipboardSnapshot(
            formats: [ClipFormats.macHtml: Data([0x3C, 0x70, 0x3E])],
            sourceApp: "com.microsoft.Excel",
            sequenceNumber: 42
        )

        let json = try encode(snapshot)

        XCTAssertTrue(json.contains("\"public.html\":\"PHA+\""), json)
        XCTAssertTrue(json.contains("\"sourceApp\":\"com.microsoft.Excel\""), json)
        XCTAssertEqual(try decode(ClipboardSnapshot.self, json), snapshot)
    }

    func testSnapshotFromCSharpJSON() throws {
        let json = #"{"formats":{"public.html":"PHA+"},"sourceApp":"com.microsoft.Excel","sequenceNumber":42}"#

        let snapshot = try decode(ClipboardSnapshot.self, json)

        XCTAssertEqual(snapshot.formats[ClipFormats.macHtml], Data([0x3C, 0x70, 0x3E]))
        XCTAssertEqual(snapshot.sequenceNumber, 42)
    }

    func testSnapshotWithoutSourceAppDecodes() throws {
        let snapshot = try decode(ClipboardSnapshot.self, #"{"formats":{},"sourceApp":null,"sequenceNumber":7}"#)

        XCTAssertNil(snapshot.sourceApp)
    }

    func testOptionsUseStringEnums() throws {
        let options = ConversionOptions(markdownMode: .format, aiVerification: false)

        // C#: {"markdownMode":"Format","aiVerification":false}; compared with sorted keys
        XCTAssertEqual(try encode(options), #"{"aiVerification":false,"markdownMode":"Format"}"#)
        XCTAssertEqual(try decode(ConversionOptions.self, #"{"markdownMode":"Format","aiVerification":false}"#), options)
    }

    func testDefaultOptionsMatchCore() {
        // C#: ConversionOptions.Default = new(MarkdownMode.Format, AiVerification: true) (PRD v1.11, #56)
        XCTAssertEqual(ConversionOptions.default, ConversionOptions(markdownMode: .format, aiVerification: true))
    }

    func testResultFromCSharpJSONWithEscapes() throws {
        // System.Text.Json escapes non-ASCII and '<' by default; this is what the Helper will send.
        let json = #"{"converterId":"C1","isPassthrough":false,"html":"<table></table>","plainText":"x","signals":[{"id":"S2","score":0,"detail":"병합 셀"}]}"#

        let result = try decode(ConversionResult.self, json)

        XCTAssertEqual(result.html, "<table></table>")
        XCTAssertEqual(result.signals, [SuspicionSignal(id: "S2", score: 0, detail: "병합 셀")])
    }

    func testResultRoundTrips() throws {
        let result = ConversionResult(converterId: "C1", isPassthrough: false, html: "<table></table>", plainText: "x",
                                      signals: [SuspicionSignal(id: "S2", score: 0, detail: "병합 셀")])

        XCTAssertEqual(try decode(ConversionResult.self, try encode(result)), result)
    }

    func testPassthroughMatchesCore() throws {
        // C#: ConversionResult.Passthrough = new("PASS", true, null, null, [])
        let fromCSharp = try decode(ConversionResult.self,
                                    #"{"converterId":"PASS","isPassthrough":true,"html":null,"plainText":null,"signals":[]}"#)

        XCTAssertEqual(fromCSharp, .passthrough)
    }

    func testEnumNamesMatchCore() {
        XCTAssertEqual(TargetApp.allCases.map(\.rawValue), ["Hangul", "Notion", "Word", "Other"])
        XCTAssertEqual(MarkdownMode.allCases.map(\.rawValue), ["Strip", "Format"])
        XCTAssertEqual(SourceKind.plainText.rawValue, "PlainText")
        XCTAssertEqual(ContentKind.table.rawValue, "Table")
    }
}
