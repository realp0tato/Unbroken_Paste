import AppKit

/// Recent text copies, kept in memory only and never written to disk.
@MainActor
final class ClipboardHistory {
    static let maxItems = 10
    /// Markers that password managers and similar apps set on secrets.
    private static let skipped: Set<String> = [
        "org.nspasteboard.ConcealedType",
        "org.nspasteboard.TransientType",
        "com.agilebits.onepassword",
    ]

    private(set) var items: [String] = []
    private let pasteboard = NSPasteboard.general
    private var lastSeen: Int

    init() {
        lastSeen = pasteboard.changeCount
    }

    /// Returns true when a new text copy was recorded.
    func poll() -> Bool {
        let count = pasteboard.changeCount
        guard count != lastSeen else { return false }
        lastSeen = count
        let types = Set((pasteboard.types ?? []).map(\.rawValue))
        guard types.isDisjoint(with: Self.skipped),
              let text = pasteboard.string(forType: .string),
              !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return false }
        items.removeAll { $0 == text }
        items.insert(text, at: 0)
        if items.count > Self.maxItems {
            items.removeLast(items.count - Self.maxItems)
        }
        return true
    }

    func recopy(at index: Int) {
        guard items.indices.contains(index) else { return }
        let text = items.remove(at: index)
        items.insert(text, at: 0)
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        lastSeen = pasteboard.changeCount
    }

    func clear() {
        items.removeAll()
    }

    static func preview(_ text: String, limit: Int = 28) -> String {
        let line = text.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        return line.count <= limit ? line : String(line.prefix(limit)) + "…"
    }
}
