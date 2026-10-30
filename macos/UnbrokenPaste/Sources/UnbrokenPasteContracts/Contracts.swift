import Foundation

// Swift mirror of src/ClipBridge.Core/Contracts (#29).
// JSON matches ContractsJsonContext: camelCase keys, enums as strings, bytes as Base64.
// Changing these types needs the same approval as the C# contracts (Contracts/README.md).

/// Format names used in `ClipboardSnapshot.formats`. macOS passes UTI names as-is (ClipFormats.cs).
public enum ClipFormats {
    public static let macHtml = "public.html"            // UTF-8, no header
    public static let macUtf8Text = "public.utf8-plain-text"
    public static let macRtf = "public.rtf"
}

/// Source detected by Core (PRD 6.1).
public enum SourceKind: String, Codable, Sendable {
    case excel = "Excel", web = "Web", markdown = "Markdown", plainText = "PlainText", other = "Other"
}

/// Content kind detected by Core (PRD 6.1).
public enum ContentKind: String, Codable, Sendable {
    case table = "Table", text = "Text"
}

/// Paste target. The app shell detects it and passes only this value to Core (PRD 4.5).
public enum TargetApp: String, Codable, Sendable, CaseIterable {
    case hangul = "Hangul", notion = "Notion", word = "Word", excel = "Excel", other = "Other"
}

/// Markdown handling option (PRD 4.2, 6.3.5).
public enum MarkdownMode: String, Codable, Sendable, CaseIterable {
    case strip = "Strip", format = "Format"
}

/// Original clipboard snapshot the app shell builds right after a copy (PRD 6.5).
public struct ClipboardSnapshot: Codable, Equatable, Sendable {
    /// Format name → original bytes. Byte-to-string decoding is Core's job.
    public var formats: [String: Data]
    /// Copying app. macOS passes the bundle ID ("com.microsoft.Excel").
    public var sourceApp: String?
    /// NSPasteboard.changeCount (FR-17).
    public var sequenceNumber: Int64

    public init(formats: [String: Data], sourceApp: String?, sequenceNumber: Int64) {
        self.formats = formats
        self.sourceApp = sourceApp
        self.sequenceNumber = sequenceNumber
    }
}

/// Menu options (PRD 4.2). Sent with every Get so option changes apply without copying again.
public struct ConversionOptions: Codable, Equatable, Sendable {
    public var markdownMode: MarkdownMode
    public var aiVerification: Bool

    public init(markdownMode: MarkdownMode, aiVerification: Bool) {
        self.markdownMode = markdownMode
        self.aiVerification = aiVerification
    }

    public static let `default` = ConversionOptions(markdownMode: .format, aiVerification: true)
}

/// Suspicion signal S1–S9 (PRD 6.6.2).
public struct SuspicionSignal: Codable, Equatable, Sendable {
    public var id: String
    public var score: Int
    public var detail: String

    public init(id: String, score: Int, detail: String) {
        self.id = id
        self.score = score
        self.detail = detail
    }
}

public struct ConversionResult: Codable, Equatable, Sendable {
    /// "C1"…"C8", "TX", "PASS"
    public var converterId: String
    /// When true the app shell writes the original snapshot back unchanged (PRD 6.5).
    public var isPassthrough: Bool
    /// Plain HTML fragment. macOS writes it to public.html as-is (no CF_HTML header).
    public var html: String?
    public var plainText: String?
    public var signals: [SuspicionSignal]

    public init(converterId: String, isPassthrough: Bool, html: String?, plainText: String?, signals: [SuspicionSignal]) {
        self.converterId = converterId
        self.isPassthrough = isPassthrough
        self.html = html
        self.plainText = plainText
        self.signals = signals
    }

    public static let passthrough = ConversionResult(converterId: "PASS", isPassthrough: true, html: nil, plainText: nil, signals: [])
}

/// JSON coders matching the C# ContractsJsonContext (Data encodes as Base64 by default).
public enum ContractsJSON {
    public static func encoder() -> JSONEncoder {
        JSONEncoder()
    }

    public static func decoder() -> JSONDecoder {
        JSONDecoder()
    }
}
