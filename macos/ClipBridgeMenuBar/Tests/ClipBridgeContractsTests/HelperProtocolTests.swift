import ClipBridgeContracts
import XCTest

/// Wire format of src/ClipBridge.Helper (README examples).
final class HelperProtocolTests: XCTestCase {
    /// The request as sent, with keys sorted so it can be compared as text.
    private func sorted(_ request: HelperRequest) throws -> String {
        let line = try request.line()
        XCTAssertEqual(line.last, 0x0A, "one message per line")
        let encoder = ContractsJSON.encoder()
        encoder.outputFormatting = [.sortedKeys]
        return String(decoding: try encoder.encode(request), as: UTF8.self)
    }

    func testPrepareRequest() throws {
        let snapshot = ClipboardSnapshot(formats: [ClipFormats.macUtf8Text: Data("a<b".utf8)], sourceApp: nil, sequenceNumber: 1)

        let json = try sorted(.prepare(id: 1, snapshot: snapshot, options: .default))

        // README example: "a<b" as Base64 is "YTxi"
        XCTAssertEqual(json, #"{"id":1,"options":{"aiVerification":true,"markdownMode":"Strip"},"snapshot":{"formats":{"public.utf8-plain-text":"YTxi"},"sequenceNumber":1},"type":"prepare"}"#)
    }

    func testSetOptionsAndShutdownOmitUnusedFields() throws {
        let options = ConversionOptions(markdownMode: .format, aiVerification: false)

        XCTAssertEqual(try sorted(.setOptions(id: 2, options: options)),
                       #"{"id":2,"options":{"aiVerification":false,"markdownMode":"Format"},"type":"setOptions"}"#)
        XCTAssertEqual(try sorted(.shutdown(id: 3)), #"{"id":3,"type":"shutdown"}"#)
    }

    func testSnapshotForCoreKeepsOnlyFormatsCoreReads() {
        let snapshot = ClipboardSnapshot(formats: [
            ClipFormats.macHtml: Data("<p>".utf8),
            ClipFormats.macUtf8Text: Data("p".utf8),
            ClipFormats.macRtf: Data("{\\rtf}".utf8),
            "com.adobe.pdf": Data(count: 1000),
            "public.tiff": Data(count: 1000),
        ], sourceApp: "com.microsoft.Excel", sequenceNumber: 9)

        let trimmed = HelperProtocol.snapshotForCore(snapshot)

        XCTAssertEqual(Set(trimmed.formats.keys), [ClipFormats.macHtml, ClipFormats.macUtf8Text, ClipFormats.macRtf])
        XCTAssertEqual(trimmed.sourceApp, "com.microsoft.Excel")
        XCTAssertEqual(trimmed.sequenceNumber, 9)
    }

    func testDecodesReady() throws {
        XCTAssertEqual(try HelperMessage.decode(line: Data(#"{"type":"ready","protocolVersion":1}"#.utf8)), .ready(protocolVersion: 1))
    }

    func testDecodesResultsWithEscapes() throws {
        let line = #"{"type":"results","id":1,"sequenceNumber":101,"source":"Excel","content":"Table","results":{"Hangul":{"converterId":"C1","isPassthrough":false,"html":"<table></table>","plainText":"표","signals":[]},"Word":{"converterId":"PASS","isPassthrough":true,"html":null,"plainText":null,"signals":[]}}}"#

        guard case .results(let results) = try HelperMessage.decode(line: Data(line.utf8)) else { return XCTFail("expected results") }

        XCTAssertEqual(results.id, 1)
        XCTAssertEqual(results.sequenceNumber, 101)
        XCTAssertEqual(results.source, .excel)
        XCTAssertEqual(results.content, .table)
        XCTAssertEqual(results.result(for: .hangul).html, "<table></table>")
        XCTAssertEqual(results.result(for: .hangul).plainText, "표")
        XCTAssertTrue(results.result(for: .word).isPassthrough)
        XCTAssertEqual(results.result(for: .notion), .passthrough)  // missing target passes through
    }

    func testDecodesUnrequestedUpdate() throws {
        let line = #"{"type":"results","id":null,"sequenceNumber":5,"source":"PlainText","content":"Text","results":{}}"#

        guard case .results(let results) = try HelperMessage.decode(line: Data(line.utf8)) else { return XCTFail("expected results") }

        XCTAssertNil(results.id)
    }

    func testDecodesOkErrorAndUnknown() throws {
        XCTAssertEqual(try HelperMessage.decode(line: Data(#"{"type":"ok","id":3}"#.utf8)), .ok(id: 3))
        XCTAssertEqual(try HelperMessage.decode(line: Data(#"{"type":"error","id":null,"code":"invalid_request","message":"bad"}"#.utf8)),
                       .error(id: nil, code: "invalid_request", message: "bad"))
        XCTAssertEqual(try HelperMessage.decode(line: Data(#"{"type":"progress","id":4}"#.utf8)), .unknown(type: "progress"))
    }

    func testLineBufferJoinsChunksAndSplitsLines() {
        var buffer = LineBuffer()
        let text = "{\"a\":\"한글\"}\n{\"b\":2}\n{\"c\""
        let bytes = Data(text.utf8)
        // Split in the middle of a multi-byte character
        let cut = bytes.firstIndex(of: 0xED)! + 1

        XCTAssertEqual(buffer.append(bytes[..<cut]), [])
        let lines = buffer.append(bytes[cut...])
        XCTAssertEqual(lines.map { String(decoding: $0, as: UTF8.self) }, ["{\"a\":\"한글\"}", "{\"b\":2}"])
        XCTAssertEqual(buffer.append(Data(":3}\n".utf8)).map { String(decoding: $0, as: UTF8.self) }, ["{\"c\":3}"])
    }
}
