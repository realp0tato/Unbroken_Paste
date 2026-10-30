// tests/fixtures 샘플을 저장할 때와 같은 형식 그대로 macOS 클립보드에 되돌려 넣는다 (#55)
// dump-mac-clipboard.swift의 반대 동작이다. 같은 입력으로 UP 없이/UP 사용 붙여넣기를 비교할 때 쓴다
//
// 사용법: 실행한 뒤 대상 앱에서 바로 붙여넣기
//   swift tests/fixtures/tools/restore-mac-clipboard.swift <샘플 폴더>
// 예)
//   swift tests/fixtures/tools/restore-mac-clipboard.swift tests/fixtures/web/mac-safari-table

import AppKit

let args = CommandLine.arguments
guard args.count == 2 else {
    FileHandle.standardError.write("사용법: swift restore-mac-clipboard.swift <샘플 폴더>\n".data(using: .utf8)!)
    exit(1)
}
let sampleDir = URL(fileURLWithPath: args[1])

func fail(_ message: String) -> Never {
    FileHandle.standardError.write((message + "\n").data(using: .utf8)!)
    exit(1)
}

// 형식 목록과 순서는 meta.json을 따른다. 앱은 앞쪽 형식을 먼저 고르므로 순서도 원본과 같아야 한다
guard let metaData = try? Data(contentsOf: sampleDir.appendingPathComponent("meta.json")),
      let meta = try? JSONSerialization.jsonObject(with: metaData) as? [String: Any],
      let formats = meta["formats"] as? [String], !formats.isEmpty else {
    fail("meta.json이 없거나 formats 목록이 비어 있습니다: \(sampleDir.path)")
}

// 파일 이름 규칙은 dump-mac-clipboard.swift와 같다: '/'만 '_'로 바꾸고 텍스트는 .txt, 나머지는 .bin
func fileURL(for type: String) -> URL? {
    let base = type.replacingOccurrences(of: "/", with: "_")
    return ["txt", "bin"]
        .map { sampleDir.appendingPathComponent(base + "." + $0) }
        .first { FileManager.default.fileExists(atPath: $0.path) }
}

let item = NSPasteboardItem()
var restored: [(String, Int)] = []
for type in formats {
    guard let url = fileURL(for: type) else { fail("형식 파일이 없습니다: \(type)") }
    let data = try Data(contentsOf: url)
    item.setData(data, forType: NSPasteboard.PasteboardType(type))
    restored.append((type, data.count))
}

let pasteboard = NSPasteboard.general
pasteboard.clearContents()
guard pasteboard.writeObjects([item]) else { fail("클립보드에 쓰지 못했습니다") }

// 실제로 들어간 형식을 다시 읽어 바이트 수까지 확인한다
for (type, size) in restored {
    let actual = pasteboard.data(forType: NSPasteboard.PasteboardType(type))?.count
    let mark = actual == size ? "OK" : "불일치(\(actual.map(String.init) ?? "없음"))"
    print("\(type)\t\(size) bytes\t\(mark)")
}
let description = meta["description"] as? String ?? ""
print("클립보드에 넣었습니다 → \(description)")
