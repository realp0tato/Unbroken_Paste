import Foundation

// Swift side of the ClipBridge.Helper stdio protocol (src/ClipBridge.Helper/README.md, #31, #36).
// One JSON message per line, UTF-8, "\n" line endings.

public enum HelperProtocol {
    public static let version = 1

    /// Formats worth sending to the Helper. Core reads only these; Excel adds PDF, images and OLE data
    /// that would make each message hundreds of KB (Helper README). The full snapshot stays in the app.
    public static let formatsForCore: Set<String> = [ClipFormats.macHtml, ClipFormats.macUtf8Text, ClipFormats.macRtf]

    public static func snapshotForCore(_ snapshot: ClipboardSnapshot) -> ClipboardSnapshot {
        var trimmed = snapshot
        trimmed.formats = snapshot.formats.filter { formatsForCore.contains($0.key) }
        return trimmed
    }
}

/// App → Helper.
public struct HelperRequest: Encodable, Equatable {
    public var type: String
    public var id: Int64
    public var snapshot: ClipboardSnapshot?
    public var options: ConversionOptions?

    public static func prepare(id: Int64, snapshot: ClipboardSnapshot, options: ConversionOptions) -> HelperRequest {
        HelperRequest(type: "prepare", id: id, snapshot: snapshot, options: options)
    }

    public static func setOptions(id: Int64, options: ConversionOptions) -> HelperRequest {
        HelperRequest(type: "setOptions", id: id, snapshot: nil, options: options)
    }

    public static func shutdown(id: Int64) -> HelperRequest {
        HelperRequest(type: "shutdown", id: id, snapshot: nil, options: nil)
    }

    /// One protocol line including the trailing "\n".
    public func line() throws -> Data {
        var data = try ContractsJSON.encoder().encode(self)
        data.append(0x0A)
        return data
    }
}

/// Results for one copy, keyed by target. `id` is nil for an unrequested update (AI verification result B).
public struct HelperResults: Decodable, Equatable {
    public var id: Int64?
    public var sequenceNumber: Int64
    public var source: SourceKind
    public var content: ContentKind
    public var results: [String: ConversionResult]

    /// A missing target means pass through (Core contract: no result = passthrough).
    public func result(for target: TargetApp) -> ConversionResult {
        results[target.rawValue] ?? .passthrough
    }
}

/// Helper → app.
public enum HelperMessage: Equatable {
    case ready(protocolVersion: Int)
    case results(HelperResults)
    case ok(id: Int64?)
    case error(id: Int64?, code: String, message: String)
    /// A type this app does not know; ignored so a newer Helper does not break the app.
    case unknown(type: String)

    public static func decode(line: Data) throws -> HelperMessage {
        let decoder = ContractsJSON.decoder()
        let head = try decoder.decode(Head.self, from: line)
        switch head.type {
        case "ready":
            return .ready(protocolVersion: try decoder.decode(Ready.self, from: line).protocolVersion)
        case "results":
            return .results(try decoder.decode(HelperResults.self, from: line))
        case "ok":
            return .ok(id: head.id)
        case "error":
            let error = try decoder.decode(ErrorBody.self, from: line)
            return .error(id: head.id, code: error.code, message: error.message)
        default:
            return .unknown(type: head.type)
        }
    }

    private struct Head: Decodable {
        var type: String
        var id: Int64?
    }

    private struct Ready: Decodable {
        var protocolVersion: Int
    }

    private struct ErrorBody: Decodable {
        var code: String
        var message: String
    }
}

/// Splits a byte stream into "\n"-terminated lines. Splitting on 0x0A is safe for UTF-8.
public struct LineBuffer {
    private var pending = Data()

    public init() {}

    public mutating func append(_ data: Data) -> [Data] {
        pending.append(data)
        var lines: [Data] = []
        while let newline = pending.firstIndex(of: 0x0A) {
            let line = pending[pending.startIndex..<newline]
            if !line.isEmpty { lines.append(Data(line)) }
            pending = Data(pending[pending.index(after: newline)...])
        }
        return lines
    }
}
