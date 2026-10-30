import AppKit
import Darwin

@main
enum ClipBridgeMenuBarApp {
    @MainActor
    static func main() {
        guard SingleInstance.acquire() else {
            FileHandle.standardError.write(Data("ClipBridge is already running in the menu bar.\n".utf8))
            return
        }
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)  // menu bar only, no Dock icon
        let controller = StatusController()
        withExtendedLifetime(controller) {
            app.run()
        }
    }
}

/// One ClipBridge per user: an exclusive lock on a file in the user's temp directory.
enum SingleInstance {
    static func acquire() -> Bool {
        let path = FileManager.default.temporaryDirectory.appendingPathComponent("app.clipbridge.menubar.lock").path
        let fd = open(path, O_CREAT | O_RDWR, 0o600)
        guard fd >= 0 else { return true }  // cannot check; do not block startup
        if flock(fd, LOCK_EX | LOCK_NB) == 0 {
            return true  // fd stays open on purpose: the OS releases the lock when the app exits
        }
        close(fd)
        return false
    }
}
