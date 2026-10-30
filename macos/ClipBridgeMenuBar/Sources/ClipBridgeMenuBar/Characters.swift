import AppKit

enum AppResources {
    /// Contents/Resources inside ClipBridge.app, or the package's Resources folder for `swift run`.
    static let root: URL = {
        if let bundled = Bundle.main.resourceURL,
           FileManager.default.fileExists(atPath: bundled.appendingPathComponent("characters.json").path) {
            return bundled
        }
        return URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // ClipBridgeMenuBar
            .deletingLastPathComponent()  // Sources
            .deletingLastPathComponent()  // package root
            .appendingPathComponent("Resources")
    }()
}

struct SlimeCharacter: Decodable {
    let id: String
    let name: String
    let comingSoon: Bool?

    enum CodingKeys: String, CodingKey {
        case id, name
        case comingSoon = "coming_soon"
    }

    private var folder: URL { AppResources.root.appendingPathComponent("Characters").appendingPathComponent(id) }

    var isReady: Bool {
        comingSoon != true && FileManager.default.fileExists(atPath: folder.path)
    }

    /// 36x22pt template frames: the system tints them for light and dark menu bars.
    func frames() -> [NSImage] {
        let files = (try? FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil)) ?? []
        return files
            .filter { $0.lastPathComponent.hasPrefix("frame_") && $0.pathExtension == "png" }
            .sorted { $0.lastPathComponent < $1.lastPathComponent }
            .compactMap { url in
                guard let image = NSImage(contentsOf: url) else { return nil }
                image.size = NSSize(width: 36, height: 22)
                image.isTemplate = true
                return image
            }
    }

    static func loadAll() -> [SlimeCharacter] {
        let url = AppResources.root.appendingPathComponent("characters.json")
        guard let data = try? Data(contentsOf: url),
              let list = try? JSONDecoder().decode([SlimeCharacter].self, from: data) else { return [] }
        return list
    }
}
