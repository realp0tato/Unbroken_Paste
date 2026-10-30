import ClipBridgeContracts
import Foundation

/// Option values only (PRD 4.2). Stored in UserDefaults; copied text is never stored.
final class Settings {
    private let defaults = UserDefaults.standard

    var markdownMode: MarkdownMode {
        get {
            // Case-insensitive so values saved before the Core naming ("strip"/"format") still load.
            let saved = defaults.string(forKey: "markdownMode")?.lowercased()
            return MarkdownMode.allCases.first { $0.rawValue.lowercased() == saved } ?? ConversionOptions.default.markdownMode
        }
        set { defaults.set(newValue.rawValue, forKey: "markdownMode") }
    }

    var aiVerification: Bool {
        get { defaults.object(forKey: "aiVerification") as? Bool ?? ConversionOptions.default.aiVerification }
        set { defaults.set(newValue, forKey: "aiVerification") }
    }

    /// Current options in the shape Core expects on every Get (PRD 4.2: applies without copying again).
    var conversionOptions: ConversionOptions {
        ConversionOptions(markdownMode: markdownMode, aiVerification: aiVerification)
    }

    var reactOnCopy: Bool {
        get { defaults.object(forKey: "reactOnCopy") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "reactOnCopy") }
    }

    var character: String? {
        get { defaults.string(forKey: "character") }
        set { defaults.set(newValue, forKey: "character") }
    }
}
