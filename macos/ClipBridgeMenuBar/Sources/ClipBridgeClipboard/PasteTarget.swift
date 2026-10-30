import ClipBridgeContracts
import Foundation

/// Paste target from the frontmost app (PRD 4.5, #34). Anything unrecognised is `.other`, which passes through.
public enum TargetResolver {
    /// Exact bundle IDs. Hancom HWP is matched by `isHancomHwp` because its ID differs by version.
    public static let bundleTargets: [String: TargetApp] = [
        "com.microsoft.Word": .word,
        "notion.id": .notion,
        "net.golbin.hop": .hangul,  // HOP, open-source HWP editor for macOS
    ]

    /// Browsers where web targets (Notion, Hancom Docs web HWP) are recognised from the focused window (PRD 6.2).
    public static let browsers: Set<String> = [
        "com.apple.Safari",
        "com.google.Chrome",
        "com.microsoft.edgemac",
        "org.mozilla.firefox",
        "com.naver.Whale",
        "company.thebrowser.Browser",  // Arc
    ]

    /// Only browsers need the window title and URL, which cost an Accessibility call.
    public static func needsWindowTitle(bundleID: String?) -> Bool {
        guard let bundleID else { return false }
        return browsers.contains(bundleID)
    }

    /// `windowURL` is the page address when the browser exposes it (Safari does), checked before the title.
    public static func resolve(bundleID: String?, appName: String?, windowTitle: String?, windowURL: String? = nil) -> TargetApp {
        guard let bundleID else { return .other }
        if let target = bundleTargets[bundleID] { return target }
        if isHancomHwp(bundleID: bundleID, appName: appName) { return .hangul }
        if browsers.contains(bundleID) {
            return webTarget(url: windowURL, title: windowTitle)
        }
        return .other
    }

    /// Notion web and Hancom Docs' web HWP editor (webhwp.hancomdocs.com) inside a browser.
    static func webTarget(url: String?, title: String?) -> TargetApp {
        if let host = url.flatMap({ URL(string: $0)?.host?.lowercased() }) {
            if host == "notion.so" || host.hasSuffix(".notion.so") || host.hasSuffix(".notion.site") { return .notion }
            if host.hasPrefix("webhwp.") && host.hasSuffix("hancomdocs.com") { return .hangul }
        }
        guard let title else { return .other }
        if title.range(of: "Notion", options: .caseInsensitive) != nil { return .notion }
        if title.contains("한컴독스") || title.range(of: "Hancom Docs", options: .caseInsensitive) != nil { return .hangul }
        return .other
    }

    /// Hancom's word processor (not its spreadsheet or slides apps).
    static func isHancomHwp(bundleID: String, appName: String?) -> Bool {
        let id = bundleID.lowercased()
        guard id.hasPrefix("com.hancom.") else { return false }
        if id.contains("hwp") { return true }
        guard let appName else { return false }
        return appName.localizedCaseInsensitiveContains("HWP") || appName.contains("한글")
    }
}

/// Which key presses count as a paste on macOS (FR-05).
public enum PasteKey {
    /// kVK_ANSI_V. Also the V key on the Korean 2-set layout, where Cmd+V still pastes.
    public static let vKeyCode: Int64 = 9

    /// Cmd+V only: no Shift (often "paste and match style"), Option or Control, and no key repeat.
    public static func isPaste(keyCode: Int64, command: Bool, shift: Bool, option: Bool, control: Bool, isRepeat: Bool) -> Bool {
        keyCode == vKeyCode && command && !shift && !option && !control && !isRepeat
    }
}
