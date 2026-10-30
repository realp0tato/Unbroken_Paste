import ClipBridgeContracts
import Foundation

public enum CopyEvent: Equatable {
    /// A new user copy; its snapshot is now the input for every conversion (FR-03).
    case copied(ClipboardSnapshot)
    /// A copy UP must not keep (password manager secret or empty pasteboard). The previous snapshot is dropped.
    case ignored
}

/// Copy detection, the original snapshot and the recent-copy list in one place (#33, PRD 4.2.2, 4.5, 6.5).
/// Everything stays in memory; nothing is written to disk (NFR-07).
@MainActor
public final class ClipboardMonitor {
    public static let maxHistory = 10
    /// Markers that password managers and similar apps set on secrets.
    public static let concealedTypes: Set<String> = [
        "org.nspasteboard.ConcealedType",
        "org.nspasteboard.TransientType",
        "com.agilebits.onepassword",
    ]

    /// The latest user copy, all formats as-is. Kept until the next copy (FR-03, NFR-08).
    public private(set) var snapshot: ClipboardSnapshot?
    /// Recent text copies, newest first (PRD 4.2.2).
    public private(set) var history: [String] = []

    private let pasteboard: PasteboardSource
    private let frontmostApp: @MainActor () -> String?
    private var lastSeen: Int
    private var ownWrites: Set<Int> = []

    /// `frontmostApp` returns the bundle ID of the app in front when the copy is noticed (PRD 4.5).
    public init(pasteboard: PasteboardSource, frontmostApp: @escaping @MainActor () -> String?) {
        self.pasteboard = pasteboard
        self.frontmostApp = frontmostApp
        lastSeen = pasteboard.changeCount
    }

    /// Checks for a change since the last call. Call it periodically on the main thread.
    public func poll() -> CopyEvent? {
        let count = pasteboard.changeCount
        guard count != lastSeen else { return nil }
        lastSeen = count
        let isOwnWrite = ownWrites.contains(count)
        ownWrites = ownWrites.filter { $0 > count }
        if isOwnWrite { return nil }  // FR-02: UP's own write is not a new copy

        let types = pasteboard.typeNames
        guard Self.concealedTypes.isDisjoint(with: types), let captured = capture(types: types, sequence: count, sourceApp: frontmostApp()) else {
            snapshot = nil
            return .ignored
        }
        snapshot = captured
        if let text = Self.text(of: captured) {
            remember(text)
        }
        return .copied(captured)
    }

    /// Call right after UP writes a conversion result, with the changeCount that write produced (FR-02).
    /// The write is then neither a new copy nor a history entry, and the snapshot stays.
    public func recordOwnWrite(changeCount: Int) {
        ownWrites.insert(changeCount)
    }

    /// FR-17: false when a newer copy has replaced the snapshot this work started from.
    public func isCurrent(_ snapshot: ClipboardSnapshot) -> Bool {
        self.snapshot?.sequenceNumber == snapshot.sequenceNumber
    }

    /// The user picked a recent item: it is copied again, moves to the top and becomes the snapshot.
    @discardableResult
    public func recopy(at index: Int) -> ClipboardSnapshot? {
        guard history.indices.contains(index) else { return nil }
        let text = history[index]
        let count = pasteboard.writeText(text)
        lastSeen = count
        snapshot = capture(types: pasteboard.typeNames, sequence: count, sourceApp: nil)
        remember(text)
        return snapshot
    }

    public func clearHistory() {
        history.removeAll()
    }

    public static func preview(_ text: String, limit: Int = 28) -> String {
        let line = text.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        return line.count <= limit ? line : String(line.prefix(limit)) + "…"
    }

    private func capture(types: [String], sequence: Int, sourceApp: String?) -> ClipboardSnapshot? {
        var formats: [String: Data] = [:]
        for type in types where formats[type] == nil {
            if let data = pasteboard.data(forType: type) {
                formats[type] = data
            }
        }
        guard !formats.isEmpty else { return nil }
        return ClipboardSnapshot(formats: formats, sourceApp: sourceApp, sequenceNumber: Int64(sequence))
    }

    private func remember(_ text: String) {
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        history.removeAll { $0 == text }
        history.insert(text, at: 0)
        if history.count > Self.maxHistory {
            history.removeLast(history.count - Self.maxHistory)
        }
    }

    private static func text(of snapshot: ClipboardSnapshot) -> String? {
        snapshot.formats[ClipFormats.macUtf8Text].flatMap { String(data: $0, encoding: .utf8) }
    }
}
