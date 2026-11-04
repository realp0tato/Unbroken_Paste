import AppKit
import UnbrokenPasteClipboard
import UnbrokenPasteContracts
import os

@MainActor
final class StatusController: NSObject {
    private static let copyReaction: TimeInterval = 1.2
    private static let pollInterval: TimeInterval = 0.3
    private static let log = Logger(subsystem: "app.unbrokenpaste.menubar", category: "paste")

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
    private let helper = HelperClient()
    private let writer = PasteWriter()

    private let permissionItem = NSMenuItem(title: "⚠︎ 손쉬운 사용 권한 필요 · 눌러서 설정 열기", action: nil, keyEquivalent: "")
    private let helperItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    private let warningSeparator = NSMenuItem.separator()
    private let clipboardMenu = NSMenu()
    private var markdownItems: [MarkdownMode: NSMenuItem] = [:]
    private var aiItems: [Bool: NSMenuItem] = [:]
    private var characterItems: [String: NSMenuItem] = [:]
    private var characterFrames: [String: [NSImage]] = [:]
    private var characterMenuOpen = false
    private var shownCharacterFrame = -1

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
        refreshCharacters()
        refreshClipboard()

        let timer = Timer(timeInterval: 1.0 / 60, target: self, selector: #selector(tick), userInfo: nil, repeats: true)
        RunLoop.main.add(timer, forMode: .common)  // keep animating while the menu is open
        self.timer = timer

        overlay.upBadge = current?.badge()
        pasteWatcher.onPermissionChange = { [weak self] in self?.refreshPermission() }
        writer.onDataTaken = { [weak self] in self?.overlay.dataTaken() }
        pasteWatcher.onPaste = { [weak self] paste in
            // Runs inside the event tap, before the target app gets the key (#35).
            let converted = self?.writeForPaste(paste) ?? false
            // Draw the badge after the key is on its way (NFR-11).
            DispatchQueue.main.async {
                self?.overlay.showPaste(target: paste.target, bundleID: paste.bundleID, appName: paste.appName, converted: converted)
            }
        }
        pasteWatcher.start()
        refreshPermission()
        helper.onStatusChange = { [weak self] in self?.refreshHelper() }
        helper.start(options: settings.conversionOptions)
        refreshHelper()
    }

    // MARK: - Menu

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        // Shown only while Accessibility is missing (FR-30): pasting still works, without conversion.
        permissionItem.target = self
        permissionItem.action = #selector(openAccessibilitySettings)
        menu.addItem(permissionItem)
        // Shown only while the converter (Helper) is not connected; pasting then passes through (PRD 10).
        menu.addItem(helperItem)
        menu.addItem(warningSeparator)  // hidden with the two warnings so the menu never starts with a line

        // PRD 4.2 menu
        let markdown = addSubmenu("Markdown 처리", to: menu)
        markdownItems[.format] = addItem("서식 적용 (기본)", to: markdown, action: #selector(pickMarkdown(_:)), object: MarkdownMode.format.rawValue)
        markdownItems[.strip] = addItem("기호 제거", to: markdown, action: #selector(pickMarkdown(_:)), object: MarkdownMode.strip.rawValue)

        let ai = addSubmenu("AI 검증", to: menu)
        aiItems[true] = addItem("켜기 (기본)", to: ai, action: #selector(pickAI(_:)), object: true)
        aiItems[false] = addItem("끄기", to: ai, action: #selector(pickAI(_:)), object: false)
        ai.addItem(.separator())
        addItem("켜져 있으면 복사 내용 일부가 외부 AI API로 전송돼요", to: ai, action: nil)
        menu.addItem(.separator())

        // Clipboard and character extras (PRD 4.2.2)
        let clipboard = NSMenuItem(title: "클립보드", action: nil, keyEquivalent: "")
        clipboard.submenu = clipboardMenu
        menu.addItem(clipboard)

        let character = addSubmenu("캐릭터", to: menu)
        character.delegate = self
        for entry in characters {
            // 이름 대신 캐릭터 모양 자체로 알아보게: 메뉴 막대와 같은 템플릿 프레임(투명 배경)을 아이콘으로 쓴다.
            // 메뉴가 열려 있는 동안은 통통 뛰는 모습까지 미리 보여준다 (tick() 참고)
            let previewFrames = entry.frames()
            let item = addItem(previewFrames.isEmpty ? entry.name : "", to: character, action: entry.isReady ? #selector(pickCharacter(_:)) : nil, object: entry.id)
            characterFrames[entry.id] = previewFrames
            if let first = previewFrames.first {
                item.image = Self.squareIcon(first, name: entry.name)
            }
            characterItems[entry.id] = item
        }
        menu.addItem(.separator())

        addItem("Unbroken Paste 정보", to: menu, action: #selector(showAbout))
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

    /// 캐릭터 프레임(세로로 긴 템플릿 이미지)을 비율 그대로 정사각형 칸 가운데에 맞춰, 메뉴 아이콘 칸이 찌그러뜨리지 않게 한다
    private static func squareIcon(_ source: NSImage, side: CGFloat = 20, name: String) -> NSImage {
        let canvas = NSImage(size: NSSize(width: side, height: side))
        canvas.isTemplate = true
        canvas.accessibilityDescription = name
        let aspect = source.size.width / max(source.size.height, 0.01)
        let fit = aspect >= 1 ? NSSize(width: side, height: side / aspect) : NSSize(width: side * aspect, height: side)
        let origin = NSPoint(x: (side - fit.width) / 2, y: (side - fit.height) / 2)
        canvas.lockFocus()
        source.draw(in: NSRect(origin: origin, size: fit), from: .zero, operation: .sourceOver, fraction: 1)
        canvas.unlockFocus()
        return canvas
    }

    private func refreshOptions() {
        for (mode, item) in markdownItems { item.state = mode == settings.markdownMode ? .on : .off }
        for (value, item) in aiItems { item.state = value == settings.aiVerification ? .on : .off }
    }

    private func refreshHelper() {
        switch helper.status {
        case .ready:
            helperItem.isHidden = true
        case .starting:
            helperItem.title = "변환 엔진 연결 중 · 붙여넣기는 평소대로"
            helperItem.isHidden = false
        case .missing:
            helperItem.title = "변환 엔진 없음 · .NET 설치 후 다시 빌드"
            helperItem.isHidden = false
        }
        refreshWarningSeparator()
    }

    private func refreshPermission() {
        permissionItem.isHidden = pasteWatcher.isActive
        refreshWarningSeparator()
    }

    private func refreshWarningSeparator() {
        warningSeparator.isHidden = permissionItem.isHidden && helperItem.isHidden
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
        helper.setOptions(settings.conversionOptions)
    }

    @objc private func pickAI(_ sender: NSMenuItem) {
        guard let on = sender.representedObject as? Bool else { return }
        settings.aiVerification = on
        refreshOptions()
        helper.setOptions(settings.conversionOptions)
    }

    @objc private func showAbout() {
        About.show()
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
        if let snapshot = monitor.recopy(at: sender.tag) {
            helper.prepare(snapshot)
        }
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
        writer.settleBeforeQuit(snapshot: monitor.snapshot)
        helper.shutdown()
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
        if characterMenuOpen, let count = characterFrames.values.first?.count, count > 0 {
            let phase = (now / SlimeAnimator.idleCycle).truncatingRemainder(dividingBy: 1)
            let index = min(Int(phase * Double(count)), count - 1)
            if index != shownCharacterFrame {
                shownCharacterFrame = index
                for entry in characters {
                    guard let item = characterItems[entry.id], let frame = characterFrames[entry.id]?[index] else { continue }
                    item.image = Self.squareIcon(frame, name: entry.name)
                }
            }
        }
        if now >= nextPoll {
            nextPoll = now + Self.pollInterval
            switch monitor.poll() {
            case let .copied(snapshot)?:
                helper.prepare(snapshot)
                overlay.showCopy()
                if settings.reactOnCopy {
                    animator.react(seconds: Self.copyReaction, now: now)
                }
                refreshClipboard()
            case .ignored?:
                helper.clear()
            case nil:
                break
            }
        }
    }

    /// Puts the target's conversion result (or the original back) on the clipboard before the paste lands (#35).
    /// Returns whether the clipboard now holds a conversion for this target. Never blocks or drops the key.
    private func writeForPaste(_ paste: PasteWatcher.Paste) -> Bool {
        let started = ProcessInfo.processInfo.systemUptime
        let snapshot = monitor.snapshot
        let result = snapshot.map { helper.result(for: paste.target, sequence: $0.sequenceNumber) } ?? .passthrough
        let plan = PastePlan.decide(snapshotSequence: snapshot?.sequenceNumber, changeCount: writer.changeCount,
                                    lastWrite: writer.lastWrite, result: result, target: paste.target)
        if let count = writer.apply(plan, snapshot: snapshot, target: paste.target) {
            monitor.recordOwnWrite(changeCount: count)  // FR-02: not a new copy, not a history entry
        }
        let converted = writer.lastWrite.map { $0.target == paste.target && $0.changeCount == writer.changeCount } ?? false
        let action: String
        switch plan {
        case .leave: action = "leave"
        case .writeResult: action = "write"
        case .restoreOriginal: action = "restore"
        }
        let ms = Int((ProcessInfo.processInfo.systemUptime - started) * 1000)
        Self.log.info("paste \(action, privacy: .public) converter=\(result.converterId, privacy: .public) target=\(paste.target.rawValue, privacy: .public) \(ms, privacy: .public)ms")
        return converted
    }
}

// MARK: - Character menu animation

extension StatusController: NSMenuDelegate {
    func menuWillOpen(_ menu: NSMenu) {
        characterMenuOpen = true
        shownCharacterFrame = -1  // 다음 tick()에서 곧바로 한 프레임을 그리게
    }

    func menuDidClose(_ menu: NSMenu) {
        characterMenuOpen = false
    }
}
