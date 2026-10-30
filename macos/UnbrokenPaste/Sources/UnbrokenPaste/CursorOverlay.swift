import AppKit
import UnbrokenPasteClipboard
import UnbrokenPasteContracts

/// Round badge next to the mouse pointer (#38, FR-18, PRD 4.3).
/// A non-activating, click-through panel: it never takes focus, keyboard input or IME composition.
@MainActor
final class CursorOverlay: NSObject {
    private static let copyDuration: TimeInterval = 0.8
    private static let pasteTimeout: TimeInterval = 1.0
    private static let fadeDuration: TimeInterval = 0.15
    private static let shadowMargin: CGFloat = 6

    /// Round character image shown on copy, filling the whole badge (the character's `badge.png`).
    var upBadge: NSImage?

    private let panel: OverlayPanel
    private let badgeView = BadgeView()
    private var iconCache: [String: NSImage?] = [:]  // bundle ID → icon (nil = none found), NFR-11
    private var timer: Timer?
    private var hideAt: TimeInterval = 0

    override init() {
        let side = OverlayGeometry.diameter + 2 * Self.shadowMargin
        panel = OverlayPanel(contentRect: NSRect(x: 0, y: 0, width: side, height: side), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: true)
        super.init()
        panel.isFloatingPanel = true
        panel.level = .popUpMenu
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.ignoresMouseEvents = true  // click-through
        panel.hidesOnDeactivate = false
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false  // the badge draws its own shadow
        panel.isReleasedWhenClosed = false
        badgeView.margin = Self.shadowMargin
        panel.contentView = badgeView
    }

    func showCopy() {
        badgeView.content = upBadge.map { .fill($0) } ?? .letter("UP")
        badgeView.passthrough = false
        show(for: Self.copyDuration)
    }

    func showPaste(target: TargetApp, bundleID: String?, appName: String?, converted: Bool) {
        switch OverlayBadge.forPaste(target: target, bundleID: bundleID, appName: appName, converted: converted) {
        case let .app(id, letter, passthrough):
            badgeView.content = icon(for: id).map { .image($0) } ?? .letter(letter)
            badgeView.passthrough = passthrough
        case let .letter(letter, passthrough):
            badgeView.content = .letter(letter)
            badgeView.passthrough = passthrough
        case .unknown:
            badgeView.content = .letter("?")
            badgeView.passthrough = true
        }
        show(for: Self.pasteTimeout)
    }

    /// The target app took the clipboard data (deferred provider, #35): fade out now instead of waiting 1 second.
    func dataTaken() {
        hideAt = min(hideAt, ProcessInfo.processInfo.systemUptime)
    }

    private func show(for duration: TimeInterval) {
        hideAt = ProcessInfo.processInfo.systemUptime + duration
        badgeView.needsDisplay = true
        panel.alphaValue = 1
        follow()
        panel.orderFrontRegardless()
        if timer == nil {
            let timer = Timer(timeInterval: 1.0 / 60, target: self, selector: #selector(tick), userInfo: nil, repeats: true)
            RunLoop.main.add(timer, forMode: .common)
            self.timer = timer
        }
    }

    /// Follows the pointer and fades out over 150ms once the display time is up.
    @objc private func tick() {
        let now = ProcessInfo.processInfo.systemUptime
        if now >= hideAt + Self.fadeDuration {
            panel.orderOut(nil)
            timer?.invalidate()
            timer = nil
            return
        }
        panel.alphaValue = now <= hideAt ? 1 : 1 - (now - hideAt) / Self.fadeDuration
        follow()
    }

    private func follow() {
        let pointer = NSEvent.mouseLocation
        guard let screen = NSScreen.screens.first(where: { NSMouseInRect(pointer, $0.frame, false) }) ?? NSScreen.main else { return }
        let badge = OverlayGeometry.badgeFrame(pointer: pointer, screen: screen.frame)
        panel.setFrame(badge.insetBy(dx: -Self.shadowMargin, dy: -Self.shadowMargin), display: false)
    }

    private func icon(for bundleID: String) -> NSImage? {
        if let cached = iconCache[bundleID] { return cached }
        let icon = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleID).map { NSWorkspace.shared.icon(forFile: $0.path) }
        iconCache[bundleID] = icon
        return icon
    }
}

private final class OverlayPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

private final class BadgeView: NSView {
    enum Content {
        /// Round artwork that fills the whole badge (the UP character on copy).
        case fill(NSImage)
        /// App icon inside the badge.
        case image(NSImage)
        case letter(String)
    }

    var content: Content = .letter("UP")
    var passthrough = false
    var margin: CGFloat = 6

    override func draw(_ dirtyRect: NSRect) {
        let circle = bounds.insetBy(dx: margin, dy: margin)
        let path = NSBezierPath(ovalIn: circle)

        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow()
        shadow.shadowBlurRadius = 4
        shadow.shadowOffset = NSSize(width: 0, height: -1)
        shadow.shadowColor = NSColor.black.withAlphaComponent(0.3)
        shadow.set()
        NSColor.windowBackgroundColor.setFill()
        path.fill()
        NSGraphicsContext.restoreGraphicsState()

        switch content {
        case let .fill(image):
            NSGraphicsContext.saveGraphicsState()
            path.addClip()
            image.draw(in: circle)
            NSGraphicsContext.restoreGraphicsState()
        case let .image(image):
            image.draw(in: fit(image.size, in: circle.insetBy(dx: 5, dy: 5)))
        case let .letter(text):
            let attributes: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: text.count > 1 ? 10 : 13, weight: .semibold),
                .foregroundColor: NSColor.labelColor,
            ]
            let size = text.size(withAttributes: attributes)
            text.draw(at: NSPoint(x: circle.midX - size.width / 2, y: circle.midY - size.height / 2), withAttributes: attributes)
        }

        // Plain paste (no conversion) gets a quiet border; converted targets get the accent colour (PRD 4.3).
        (passthrough ? NSColor.separatorColor : NSColor.controlAccentColor).setStroke()
        path.lineWidth = passthrough ? 1 : 1.5
        path.stroke()
    }

    private func fit(_ size: NSSize, in box: NSRect) -> NSRect {
        guard size.width > 0, size.height > 0 else { return box }
        let scale = min(box.width / size.width, box.height / size.height)
        let w = size.width * scale, h = size.height * scale
        return NSRect(x: box.midX - w / 2, y: box.midY - h / 2, width: w, height: h)
    }
}
