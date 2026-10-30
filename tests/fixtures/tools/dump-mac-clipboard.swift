// macOS 클립보드의 모든 타입을 tests/fixtures 규칙대로 저장한다 (#32)
//
// 사용법: 원본 앱에서 복사한 뒤 바로 실행
//   swift tests/fixtures/tools/dump-mac-clipboard.swift <저장 폴더> <원본 앱 번들 ID> "<복사한 내용 설명>"
// 예)
//   swift tests/fixtures/tools/dump-mac-clipboard.swift tests/fixtures/excel/mac-basic-table com.microsoft.Excel "굵은 머리글 2열 3행 표"

import AppKit

let args = CommandLine.arguments
guard args.count == 4 else {
    FileHandle.standardError.write("사용법: swift dump-mac-clipboard.swift <저장 폴더> <원본 앱 번들 ID> \"<설명>\"\n".data(using: .utf8)!)
    exit(1)
}
let outDir = URL(fileURLWithPath: args[1])
let sourceBundleId = args[2]
let description = args[3]

let pasteboard = NSPasteboard.general
guard let item = pasteboard.pasteboardItems?.first, !item.types.isEmpty else {
    FileHandle.standardError.write("클립보드가 비어 있습니다\n".data(using: .utf8)!)
    exit(1)
}
if (pasteboard.pasteboardItems?.count ?? 0) > 1 {
    FileHandle.standardError.write("경고: 항목이 여러 개입니다. 첫 항목만 저장합니다\n".data(using: .utf8)!)
}

try FileManager.default.createDirectory(at: outDir, withIntermediateDirectories: true)

// 텍스트 타입은 .txt, 나머지는 .bin (tests/fixtures/README.md)
let textTypes: Set<String> = ["public.utf8-plain-text", "public.utf16-plain-text", "public.utf16-external-plain-text"]
var formats: [String] = []
for type in item.types {
    guard let data = item.data(forType: type) else { continue }
    let name = type.rawValue
    let ext = textTypes.contains(name) ? "txt" : "bin"
    // 파일 이름에 쓸 수 없는 '/'만 바꾼다
    let fileName = name.replacingOccurrences(of: "/", with: "_") + "." + ext
    try data.write(to: outDir.appendingPathComponent(fileName))
    formats.append(name)
    print("\(fileName)\t\(data.count) bytes")
}

var appVersion: String? = nil
if let appURL = NSWorkspace.shared.urlForApplication(withBundleIdentifier: sourceBundleId) {
    appVersion = Bundle(url: appURL)?.infoDictionary?["CFBundleShortVersionString"] as? String
}
let os = ProcessInfo.processInfo.operatingSystemVersion
let meta: [String: Any] = [
    "os": "mac",
    "osVersion": "\(os.majorVersion).\(os.minorVersion).\(os.patchVersion)",
    "sourceApp": sourceBundleId,
    "sourceAppVersion": appVersion ?? NSNull(),
    "description": description,
    "formats": formats,
]
let json = try JSONSerialization.data(withJSONObject: meta, options: [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes])
try json.write(to: outDir.appendingPathComponent("meta.json"))
print("meta.json 저장 → \(outDir.path)")
