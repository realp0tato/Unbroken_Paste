import AppKit

@main
enum ClipBridgeMenuBarApp {
    @MainActor
    static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)  // menu bar only, no Dock icon
        let controller = StatusController()
        withExtendedLifetime(controller) {
            app.run()
        }
    }
}
