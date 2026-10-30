import Foundation

enum MarkdownMode: String {
    case stripSymbols = "strip"
    case applyFormatting = "format"
}

/// Option values only (PRD 4.2). Stored in UserDefaults; copied text is never stored.
final class Settings {
    private let defaults = UserDefaults.standard

    var markdownMode: MarkdownMode {
        get { MarkdownMode(rawValue: defaults.string(forKey: "markdownMode") ?? "") ?? .stripSymbols }
        set { defaults.set(newValue.rawValue, forKey: "markdownMode") }
    }

    var aiVerification: Bool {
        get { defaults.object(forKey: "aiVerification") as? Bool ?? true }
        set { defaults.set(newValue, forKey: "aiVerification") }
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
