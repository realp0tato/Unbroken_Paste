import AppKit
import ClipBridgeContracts

/// Writes conversion results and restores originals on the general pasteboard (#35, PRD 4.5).
///
/// Data is promised, not copied: the pasteboard gets the type list now and the bytes only when the target
/// app asks. That keeps the write inside the paste-key window (NFR-03) and tells us the moment the target
/// takes the data, which ends the cursor badge (PRD 4.3).
@MainActor
public final class PasteWriter {
    /// Called when a target app asks for promised data.
    public var onDataTaken: (() -> Void)?
    public private(set) var lastWrite: PastePlan.Write?

    private let pasteboard: NSPasteboard
    private var provider: PromisedData?

    public init(pasteboard: NSPasteboard = .general) {
        self.pasteboard = pasteboard
    }

    public var changeCount: Int64 { Int64(pasteboard.changeCount) }

    /// Carries out a plan for the copy `snapshot`. Returns the new changeCount when the clipboard was written.
    @discardableResult
    public func apply(_ plan: PastePlan, snapshot: ClipboardSnapshot?, target: TargetApp) -> Int? {
        switch plan {
        case .leave:
            return nil
        case let .writeResult(payload):
            guard let snapshot else { return nil }
            return promise(payload, sequence: snapshot.sequenceNumber, target: target)
        case .restoreOriginal:
            guard let snapshot else { return nil }
            return promise(snapshot.formats, sequence: snapshot.sequenceNumber, target: nil)
        }
    }

    /// On quit promised data would vanish with the app, so put the original back with real bytes.
    /// Does nothing unless the clipboard still holds UP's own write.
    public func settleBeforeQuit(snapshot: ClipboardSnapshot?) {
        guard let lastWrite, lastWrite.changeCount == changeCount, let snapshot, snapshot.sequenceNumber == lastWrite.sequence else { return }
        let item = NSPasteboardItem()
        for (type, data) in snapshot.formats {
            item.setData(data, forType: NSPasteboard.PasteboardType(type))
        }
        pasteboard.clearContents()
        pasteboard.writeObjects([item])
        provider = nil
        self.lastWrite = nil
    }

    private func promise(_ payload: [String: Data], sequence: Int64, target: TargetApp?) -> Int {
        let provider = PromisedData(payload: payload) { [weak self] in self?.onDataTaken?() }
        let item = NSPasteboardItem()
        item.setDataProvider(provider, forTypes: payload.keys.map { NSPasteboard.PasteboardType($0) })
        pasteboard.clearContents()
        pasteboard.writeObjects([item])
        self.provider = provider  // keep it alive while the promise is on the clipboard
        let count = pasteboard.changeCount
        lastWrite = PastePlan.Write(changeCount: Int64(count), sequence: sequence, target: target)
        return count
    }
}

/// Hands over the bytes when an app asks for a promised type.
private final class PromisedData: NSObject, NSPasteboardItemDataProvider {
    private let payload: [String: Data]
    private let taken: () -> Void

    init(payload: [String: Data], taken: @escaping () -> Void) {
        self.payload = payload
        self.taken = taken
    }

    func pasteboard(_ pasteboard: NSPasteboard?, item: NSPasteboardItem, provideDataForType type: NSPasteboard.PasteboardType) {
        if let data = payload[type.rawValue] {
            item.setData(data, forType: type)
        }
        taken()
    }

    func pasteboardFinishedWithDataProvider(_ pasteboard: NSPasteboard) {}
}
