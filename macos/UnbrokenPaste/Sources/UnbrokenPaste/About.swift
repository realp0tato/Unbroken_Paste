import AppKit

/// "Unbroken Paste 정보" window: the standard macOS About panel with the team credits.
enum About {
    static let team: [(name: String, role: String)] = [
        ("장세찬", "Excel 변환기 · macOS 보조 프로그램"),
        ("박재현", "인터넷·Markdown 변환기 · 출처 판별 · AI 검증"),
        ("이창민", "Windows 앱"),
        ("윤승희", "macOS 앱"),
    ]
    static let copyright = "© 2026 UP 팀 (장세찬 · 박재현 · 이창민 · 윤승희). All rights reserved."
    static let repository = URL(string: "https://github.com/realp0tato/donga_hackathon")!

    @MainActor
    static func show() {
        let info = Bundle.main.infoDictionary
        var options: [NSApplication.AboutPanelOptionKey: Any] = [
            .applicationName: "Unbroken Paste",
            .applicationVersion: info?["CFBundleShortVersionString"] as? String ?? "개발 빌드",
            .version: info?["CFBundleVersion"] as? String ?? "",
            .credits: credits(),
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): copyright,
        ]
        // `swift run` has no bundle icon; fall back to the copy badge so the panel still shows UP.
        if Bundle.main.bundleIdentifier == nil, let badge = SlimeCharacter.loadAll().first?.badge() {
            options[.applicationIcon] = badge
        }
        // A menu bar app is never frontmost on its own; bring the panel forward.
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: options)
    }

    private static func credits() -> NSAttributedString {
        let center = NSMutableParagraphStyle()
        center.alignment = .center
        center.paragraphSpacing = 2
        let body: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 11),
            .foregroundColor: NSColor.labelColor,
            .paragraphStyle: center,
        ]
        let muted = body.merging([.foregroundColor: NSColor.secondaryLabelColor]) { $1 }
        let bold = body.merging([.font: NSFont.boldSystemFont(ofSize: 11)]) { $1 }

        let text = NSMutableAttributedString()
        text.append(NSAttributedString(string: "복사한 내용을 붙여넣는 앱에 맞게\n정리해서 붙여 주는 앱\n\n", attributes: body))
        text.append(NSAttributedString(string: "만든 사람 · UP 팀\n", attributes: bold))
        for member in team {
            text.append(NSAttributedString(string: member.name, attributes: bold))
            text.append(NSAttributedString(string: "  \(member.role)\n", attributes: muted))
        }
        text.append(NSAttributedString(string: "\n대학 AX 해커톤 2026\n", attributes: muted))
        text.append(NSAttributedString(string: "github.com/realp0tato/donga_hackathon", attributes: body.merging([.link: repository]) { $1 }))
        return text
    }
}
