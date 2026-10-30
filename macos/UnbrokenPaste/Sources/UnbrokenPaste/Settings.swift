import UnbrokenPasteContracts
import Foundation

/// Option values only (PRD 4.2). Stored in UserDefaults; copied text is never stored.
final class Settings {
    /// Bundle ID before the rename to Unbroken Paste (#47).
    static let legacyDomain = "app.clipbridge.menubar"
    private static let migratedKey = "migratedLegacyDefaults"
    private static let migratedKeys = ["markdownMode", "aiVerification", "reactOnCopy", "character"]

    private let defaults = UserDefaults.standard

    init() {
        Self.migrateLegacyDefaults(into: defaults)
    }

    /// Copies menu options saved under the old bundle ID once, so the rename does not reset them.
    /// "askedAccessibility" is left behind on purpose: the renamed app needs its own permission prompt.
    static func migrateLegacyDefaults(into defaults: UserDefaults, legacy: UserDefaults? = UserDefaults(suiteName: legacyDomain)) {
        guard !defaults.bool(forKey: migratedKey) else { return }
        defaults.set(true, forKey: migratedKey)
        guard let legacy else { return }
        for key in migratedKeys where defaults.object(forKey: key) == nil {
            if let value = legacy.object(forKey: key) {
                defaults.set(value, forKey: key)
            }
        }
    }

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
