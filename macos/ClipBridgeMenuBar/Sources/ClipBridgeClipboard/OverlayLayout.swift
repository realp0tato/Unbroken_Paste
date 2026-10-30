import ClipBridgeContracts
import CoreGraphics
import Foundation

/// Where the cursor badge goes (PRD 4.3). Cocoa screen coordinates: origin bottom-left, y up.
public enum OverlayGeometry {
    public static let diameter: CGFloat = 30
    public static let offset: CGFloat = 16

    /// Badge circle right-below the pointer, flipped to the other side at screen edges and kept on screen.
    public static func badgeFrame(pointer: CGPoint, screen: CGRect, diameter: CGFloat = diameter, offset: CGFloat = offset) -> CGRect {
        var x = pointer.x + offset
        var y = pointer.y - offset - diameter
        if x + diameter > screen.maxX { x = pointer.x - offset - diameter }
        if y < screen.minY { y = pointer.y + offset }
        x = min(max(x, screen.minX), screen.maxX - diameter)
        y = min(max(y, screen.minY), screen.maxY - diameter)
        return CGRect(x: x, y: y, width: diameter, height: diameter)
    }
}

/// What the paste badge shows (PRD 4.3): the target service's icon, never copied text or titles.
public enum OverlayBadge: Equatable {
    /// Icon of the app with this bundle ID; `letter` is the fallback when no icon is found.
    /// `passthrough` marks a plain paste (no conversion), shown with a different border.
    case app(bundleID: String, letter: String, passthrough: Bool)
    /// The target app could not be identified.
    case unknown

    /// Service icons for targets that may sit inside another app (Notion web in a browser).
    static let serviceApps: [TargetApp: (bundleID: String, letter: String)] = [
        .notion: ("notion.id", "N"),
        .word: ("com.microsoft.Word", "W"),
    ]

    public static func forPaste(target: TargetApp, bundleID: String?, appName: String?) -> OverlayBadge {
        if let service = serviceApps[target] {
            return .app(bundleID: service.bundleID, letter: service.letter, passthrough: false)
        }
        guard let bundleID else { return .unknown }
        return .app(bundleID: bundleID, letter: letter(appName: appName, bundleID: bundleID), passthrough: target == .other)
    }

    static func letter(appName: String?, bundleID: String) -> String {
        let name = appName?.trimmingCharacters(in: .whitespaces).nilIfEmpty
            ?? bundleID.split(separator: ".").last.map(String.init)
            ?? bundleID
        return name.first.map { String($0).uppercased() } ?? "?"
    }
}

private extension String {
    var nilIfEmpty: String? { isEmpty ? nil : self }
}
