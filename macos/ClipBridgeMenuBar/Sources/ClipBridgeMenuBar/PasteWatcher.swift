import AppKit
import ApplicationServices
import ClipBridgeClipboard
import ClipBridgeContracts
import os

/// Cmd+V detection and paste target recognition (#34, FR-05, FR-30, PRD 4.5).
///
/// The event tap runs before the target app receives the key, so the paste path (#35) can write
/// the clipboard in time. It always returns the event unchanged: keys are never dropped or resent (NFR-01).
@MainActor
final class PasteWatcher: NSObject {
    struct Paste {
        let target: TargetApp
        let bundleID: String?
        let appName: String?
    }

    static let accessibilitySettingsURL = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
    private static let askedKey = "askedAccessibility"
    private static let log = Logger(subsystem: "app.clipbridge.menubar", category: "paste")

    var onPaste: ((Paste) -> Void)?
    /// Called when the permission state changes (granted later in System Settings, for example).
    var onPermissionChange: (() -> Void)?
    private(set) var isActive = false

    private var tap: CFMachPort?
    private var retryTimer: Timer?

    /// Asks for Accessibility once on first launch; without it pasting works as usual, just without conversion.
    func start() {
        let defaults = UserDefaults.standard
        let prompt = !defaults.bool(forKey: Self.askedKey)
        defaults.set(true, forKey: Self.askedKey)
        let options = ["AXTrustedCheckOptionPrompt": prompt] as CFDictionary  // kAXTrustedCheckOptionPrompt
        if AXIsProcessTrustedWithOptions(options) {
            installTap()
        }
        let timer = Timer(timeInterval: 2, target: self, selector: #selector(checkPermission), userInfo: nil, repeats: true)
        RunLoop.main.add(timer, forMode: .common)
        retryTimer = timer
    }

    @objc private func checkPermission() {
        let trusted = AXIsProcessTrusted()
        if trusted && tap == nil {
            installTap()
        } else if !trusted && tap != nil {
            removeTap()  // permission was revoked while running
        }
    }

    private func installTap() {
        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue)
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: mask,
            callback: pasteTapCallback,
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else { return }
        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        self.tap = tap
        setActive(true)
    }

    private func removeTap() {
        if let tap {
            CGEvent.tapEnable(tap: tap, enable: false)
            CFMachPortInvalidate(tap)
        }
        tap = nil
        setActive(false)
    }

    private func setActive(_ active: Bool) {
        guard active != isActive else { return }
        isActive = active
        onPermissionChange?()
    }

    fileprivate func handle(type: CGEventType, event: CGEvent) {
        switch type {
        case .tapDisabledByTimeout, .tapDisabledByUserInput:
            if let tap {
                CGEvent.tapEnable(tap: tap, enable: true)  // the system turned the tap off; turn it back on
            }
        case .keyDown:
            let flags = event.flags
            guard PasteKey.isPaste(
                keyCode: event.getIntegerValueField(.keyboardEventKeycode),
                command: flags.contains(.maskCommand),
                shift: flags.contains(.maskShift),
                option: flags.contains(.maskAlternate),
                control: flags.contains(.maskControl),
                isRepeat: event.getIntegerValueField(.keyboardEventAutorepeat) != 0
            ) else { return }
            let app = NSWorkspace.shared.frontmostApplication
            let bundleID = app?.bundleIdentifier
            let title = TargetResolver.needsWindowTitle(bundleID: bundleID) ? app.flatMap { focusedWindowTitle(pid: $0.processIdentifier) } : nil
            let target = TargetResolver.resolve(bundleID: bundleID, appName: app?.localizedName, windowTitle: title)
            Self.log.info("paste target=\(target.rawValue, privacy: .public) app=\(bundleID ?? "unknown", privacy: .public)")
            onPaste?(Paste(target: target, bundleID: bundleID, appName: app?.localizedName))
        default:
            break
        }
    }

    /// Focused window title via Accessibility, with a short timeout so a hung app cannot stall the key.
    private func focusedWindowTitle(pid: pid_t) -> String? {
        let app = AXUIElementCreateApplication(pid)
        AXUIElementSetMessagingTimeout(app, 0.05)
        var window: CFTypeRef?
        guard AXUIElementCopyAttributeValue(app, kAXFocusedWindowAttribute as CFString, &window) == .success,
              let window, CFGetTypeID(window) == AXUIElementGetTypeID() else { return nil }
        var title: CFTypeRef?
        guard AXUIElementCopyAttributeValue(window as! AXUIElement, kAXTitleAttribute as CFString, &title) == .success else { return nil }
        return title as? String
    }
}

/// C callback for the event tap. The tap's run loop source is on the main run loop, so this runs on the main thread.
private func pasteTapCallback(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent, userInfo: UnsafeMutableRawPointer?) -> Unmanaged<CGEvent>? {
    if let userInfo {
        let watcher = Unmanaged<PasteWatcher>.fromOpaque(userInfo).takeUnretainedValue()
        MainActor.assumeIsolated {
            watcher.handle(type: type, event: event)
        }
    }
    return Unmanaged.passUnretained(event)
}
