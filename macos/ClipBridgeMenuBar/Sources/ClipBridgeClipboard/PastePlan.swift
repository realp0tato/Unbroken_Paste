import ClipBridgeContracts
import Foundation

/// What UP should do to the clipboard at the moment of a paste (#35, PRD 4.4, 6.5, FR-07, FR-16).
public enum PastePlan: Equatable {
    /// Touch nothing; the key goes on and the app pastes whatever is there.
    case leave
    /// Put this conversion result on the clipboard (format name → bytes).
    case writeResult([String: Data])
    /// Put the original snapshot back (a passthrough target after UP wrote a conversion earlier).
    case restoreOriginal

    /// The last clipboard write UP made.
    public struct Write: Equatable {
        public var changeCount: Int64
        public var sequence: Int64
        /// The target the result was written for; nil when the original was restored.
        public var target: TargetApp?

        public init(changeCount: Int64, sequence: Int64, target: TargetApp?) {
            self.changeCount = changeCount
            self.sequence = sequence
            self.target = target
        }
    }

    /// - Parameters:
    ///   - snapshotSequence: the current copy's changeCount, nil when there is no usable copy.
    ///   - changeCount: the clipboard's changeCount right now.
    public static func decide(snapshotSequence: Int64?, changeCount: Int64, lastWrite: Write?, result: ConversionResult, target: TargetApp) -> PastePlan {
        guard let sequence = snapshotSequence else { return .leave }
        let holdsOurWrite = lastWrite.map { $0.changeCount == changeCount && $0.sequence == sequence } ?? false
        let holdsOriginal = changeCount == sequence
        // Someone else changed the clipboard after the snapshot (a copy not noticed yet): never overwrite it.
        guard holdsOurWrite || holdsOriginal else { return .leave }

        let payload = payload(of: result)
        if !payload.isEmpty {
            if holdsOurWrite, lastWrite?.target == target { return .leave }  // already this target's result
            return .writeResult(payload)
        }
        if holdsOurWrite, lastWrite?.target != nil { return .restoreOriginal }  // PRD 6.5: give back the original
        return .leave
    }

    /// macOS writes HTML as public.html without a CF_HTML header (Core contract), text as UTF-8.
    static func payload(of result: ConversionResult) -> [String: Data] {
        guard !result.isPassthrough else { return [:] }
        var payload: [String: Data] = [:]
        if let html = result.html { payload[ClipFormats.macHtml] = Data(html.utf8) }
        if let text = result.plainText { payload[ClipFormats.macUtf8Text] = Data(text.utf8) }
        return payload
    }
}
