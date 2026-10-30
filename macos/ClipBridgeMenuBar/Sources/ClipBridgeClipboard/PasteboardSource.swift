import AppKit

/// The pasteboard operations UP needs. Lets the monitor run against a fake in tests.
public protocol PasteboardSource: AnyObject {
    var changeCount: Int { get }
    /// Type names (UTIs) on the pasteboard, in the order the copying app declared them.
    var typeNames: [String] { get }
    func data(forType type: String) -> Data?
    /// Replaces the contents with plain text and returns the new changeCount.
    func writeText(_ text: String) -> Int
}

public final class SystemPasteboard: PasteboardSource {
    private let pasteboard: NSPasteboard

    public init(_ pasteboard: NSPasteboard = .general) {
        self.pasteboard = pasteboard
    }

    public var changeCount: Int { pasteboard.changeCount }

    public var typeNames: [String] { (pasteboard.types ?? []).map(\.rawValue) }

    public func data(forType type: String) -> Data? {
        pasteboard.data(forType: NSPasteboard.PasteboardType(type))
    }

    public func writeText(_ text: String) -> Int {
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        return pasteboard.changeCount
    }
}
