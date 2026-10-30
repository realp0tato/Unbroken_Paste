import AppKit
import ClipBridgeClipboard
import ClipBridgeContracts
import UserNotifications

@MainActor
final class StatusController: NSObject {
    private static let headers: [SlimeMode: String] = [.idle: "대기 중", .busy: "구조 분석 중", .cpu: "CPU 연동 중"]
    private static let copyReaction: TimeInterval = 1.2
    private static let pollInterval: TimeInterval = 0.3

    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let settings: Settings
    private let monitor = ClipboardMonitor(pasteboard: SystemPasteboard()) {
        NSWorkspace.shared.frontmostApplication?.bundleIdentifier
    }
    private let characters: [SlimeCharacter]
    private let animator: SlimeAnimator
    private var frames: [NSImage]
    private var currentCharacter: String
    private var shownFrame = -1
    private var nextPoll: TimeInterval = 0
    private var timer: Timer?

    private let pasteWatcher = PasteWatcher()
    private let overlay = CursorOverlay()

    private let headerItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    private let permissionItem = NSMenuItem(title: "⚠︎ 손쉬운 사용 권한 필요 · 눌러서 설정 열기", action: nil, keyEquivalent: "")
    private let clipboardMenu = NSMenu()
    private var markdownItems: [MarkdownMode: NSMenuItem] = [:]
    private var aiItems: [Bool: NSMenuItem] = [:]
    private var modeItems: [SlimeMode: NSMenuItem] = [:]
    private var characterItems: [String: NSMenuItem] = [:]

    override init() {
        let settings = Settings()
        let characters = SlimeCharacter.loadAll()
        let ready = characters.filter(\.isReady)
        let current = ready.first { $0.id == settings.character } ?? ready.first
        let frames = current?.frames() ?? []
        self.settings = settings
        self.characters = characters
        self.currentCharacter = current?.id ?? ""
        self.frames = frames
        self.animator = SlimeAnimator(frameCount: frames.count)
        super.init()

        if let button = statusItem.button {
            button.toolTip = "UP"
            if let first = frames.first {
                button.image = first
            } else {
                button.title = "UP"  // frames missing: still usable as a text item
            }
        }
        statusItem.menu = buildMenu()
        refreshOptions()
        refreshMode()
        refreshCharacters()
        refreshClipboard()

        let timer = Timer(timeInterval: 1.0 / 60, target: self, selector: #selector(tick), userInfo: nil, repeats: true)
        RunLoop.main.add(timer, forMode: .common)  // keep animating while the menu is open
        self.timer = timer

        overlay.upBadge = current?.badge()
        pasteWatcher.onPermissionChange = { [weak self] in self?.refreshPermission() }
        pasteWatcher.onPaste = { [weak self] paste in
            // Runs inside the event tap: draw the badge after the key is on its way (NFR-11).
            DispatchQueue.main.async {
                self?.overlay.showPaste(target: paste.target, bundleID: paste.bundleID, appName: paste.appName)
            }
        }
        pasteWatcher.start()
        refreshPermission()
    }

    // MARK: - Menu

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(headerItem)
        // Shown only while Accessibility is missing (FR-30): pasting still works, without conversion.
        permissionItem.target = self
        permissionItem.action = #selector(openAccessibilitySettings)
        menu.addItem(permissionItem)
        menu.addItem(.separator())

        // PRD 4.2 menu
        let markdown = addSubmenu("Markdown 처리", to: menu)
        markdownItems[.strip] = addItem("기호 제거 (기본)", to: markdown, action: #selector(pickMarkdown(_:)), object: MarkdownMode.strip.rawValue)
        markdownItems[.format] = addItem("서식 적용", to: markdown, action: #selector(pickMarkdown(_:)), object: MarkdownMode.format.rawValue)

        let ai = addSubmenu("AI 검증 (외부 API 사용)", to: menu)
        aiItems[true] = addItem("켜기 (기본)", to: ai, action: #selector(pickAI(_:)), object: true)
        aiItems[false] = addItem("끄기", to: ai, action: #selector(pickAI(_:)), object: false)
        ai.addItem(.separator())
        addItem("켜져 있으면 복사 내용 일부가 외부 AI API로 전송돼요", to: ai, action: nil)
        menu.addItem(.separator())

        // Slime and clipboard extras
        let state = addSubmenu("상태 변경", to: menu)
        modeItems[.idle] = addItem("대기", to: state, action: #selector(pickMode(_:)), object: SlimeMode.idle.rawValue)
        modeItems[.busy] = addItem("분석 중", to: state, action: #selector(pickMode(_:)), object: SlimeMode.busy.rawValue)
        let cpu = addItem("CPU 연동", to: state, action: #selector(pickMode(_:)), object: SlimeMode.cpu.rawValue)
        cpu.isHidden = true  // kept for later; not shown in the menu for now
        modeItems[.cpu] = cpu
        state.addItem(.separator())
        addItem("맞춤 완료 효과 보기", to: state, action: #selector(celebrate))

        let clipboard = NSMenuItem(title: "클립보드", action: nil, keyEquivalent: "")
        clipboard.submenu = clipboardMenu
        menu.addItem(clipboard)

        let character = addSubmenu("캐릭터", to: menu)
        for entry in characters {
            let item = addItem(entry.name, to: character, action: entry.isReady ? #selector(pickCharacter(_:)) : nil, object: entry.id)
            characterItems[entry.id] = item
        }
        menu.addItem(.separator())

        addItem("UP 종료", to: menu, action: #selector(quit), key: "q")
        return menu
    }

    @discardableResult
    private func addItem(_ title: String, to menu: NSMenu, action: Selector?, object: Any? = nil, key: String = "") -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: key)
        item.target = action == nil ? nil : self
        item.representedObject = object
        menu.addItem(item)
        return item
    }

    private func addSubmenu(_ title: String, to menu: NSMenu) -> NSMenu {
        let submenu = NSMenu()
        let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        item.submenu = submenu
        menu.addItem(item)
        return submenu
    }

    private func refreshOptions() {
        for (mode, item) in markdownItems { item.state = mode == settings.markdownMode ? .on : .off }
        for (value, item) in aiItems { item.state = value == settings.aiVerification ? .on : .off }
    }

    private func refreshMode() {
        headerItem.title = "UP · " + (Self.headers[animator.mode] ?? "")
        for (mode, item) in modeItems { item.state = mode == animator.mode ? .on : .off }
    }

    private func refreshPermission() {
        permissionItem.isHidden = pasteWatcher.isActive
    }

    private func refreshCharacters() {
        for (id, item) in characterItems { item.state = id == currentCharacter ? .on : .off }
    }

    private func refreshClipboard() {
        clipboardMenu.removeAllItems()
        if monitor.history.isEmpty {
            addItem("아직 복사한 내용이 없어요", to: clipboardMenu, action: nil)
        } else {
            addItem("최근 복사 · 누르면 다시 복사", to: clipboardMenu, action: nil)
            for (index, text) in monitor.history.enumerated() {
                let item = addItem("\(index + 1). \(ClipboardMonitor.preview(text))", to: clipboardMenu, action: #selector(recopy(_:)))
                item.tag = index
            }
            clipboardMenu.addItem(.separator())
            addItem("기록 지우기", to: clipboardMenu, action: #selector(clearHistory))
        }
        clipboardMenu.addItem(.separator())
        let react = addItem("복사하면 슬라임 반응", to: clipboardMenu, action: #selector(toggleReact))
        react.state = settings.reactOnCopy ? .on : .off
        addItem("기록은 저장하지 않고 메모리에만 있어요", to: clipboardMenu, action: nil)
    }

    // MARK: - Actions

    @objc private func pickMarkdown(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String, let mode = MarkdownMode(rawValue: raw) else { return }
        settings.markdownMode = mode
        refreshOptions()
    }

    @objc private func pickAI(_ sender: NSMenuItem) {
        guard let on = sender.representedObject as? Bool else { return }
        settings.aiVerification = on
        refreshOptions()
    }

    @objc private func pickMode(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String, let mode = SlimeMode(rawValue: raw) else { return }
        animator.mode = mode
        refreshMode()
    }

    @objc private func celebrate() {
        animator.celebrate(now: ProcessInfo.processInfo.systemUptime)
        notify("변환 결과를 붙였어요 (시뮬레이션)")
    }

    @objc private func pickCharacter(_ sender: NSMenuItem) {
        guard let id = sender.representedObject as? String,
              let entry = characters.first(where: { $0.id == id && $0.isReady }) else { return }
        let newFrames = entry.frames()
        guard !newFrames.isEmpty else { return }
        frames = newFrames
        overlay.upBadge = entry.badge()
        animator.frameCount = newFrames.count
        shownFrame = -1
        currentCharacter = id
        settings.character = id
        refreshCharacters()
    }

    @objc private func recopy(_ sender: NSMenuItem) {
        monitor.recopy(at: sender.tag)
        refreshClipboard()
    }

    @objc private func clearHistory() {
        monitor.clearHistory()
        refreshClipboard()
    }

    @objc private func toggleReact() {
        settings.reactOnCopy.toggle()
        refreshClipboard()
    }

    @objc private func openAccessibilitySettings() {
        NSWorkspace.shared.open(PasteWatcher.accessibilitySettingsURL)
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }

    // MARK: - Animation and clipboard polling

    @objc private func tick() {
        let now = ProcessInfo.processInfo.systemUptime
        if !frames.isEmpty {
            let index = animator.nextFrame(now: now)
            if index != shownFrame {
                statusItem.button?.image = frames[index]
                shownFrame = index
            }
        }
        if now >= nextPoll {
            nextPoll = now + Self.pollInterval
            switch monitor.poll() {
            case .copied?:
                overlay.showCopy()
                if settings.reactOnCopy {
                    animator.react(seconds: Self.copyReaction, now: now)
                }
                refreshClipboard()
            case .ignored?, nil:
                break
            }
        }
    }

    private func notify(_ body: String) {
        // Notifications need an app bundle; `swift run` has none, so skip there.
        guard Bundle.main.bundleIdentifier != nil else { return }
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert]) { granted, _ in
            guard granted else { return }
            let content = UNMutableNotificationContent()
            content.title = "UP"
            content.body = body
            UNUserNotificationCenter.current().add(UNNotificationRequest(identifier: UUID().uuidString, content: content, trigger: nil))
        }
    }
}
