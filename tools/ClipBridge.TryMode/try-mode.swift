// 개발용 UP 시험 모드 (#55): 켜 두면 복사할 때마다 변환 결과를 클립보드에 넣어 둔다
// 앱 껍데기(#35)와 변환기 선택(#14)이 완성되기 전까지, 변환 결과를 눈으로 비교하는 용도다
//
// 사용법 (저장소 루트에서)
//   swift tools/ClipBridge.TryMode/try-mode.swift --target Hangul --mode Format
//   → 복사 → 대상 앱에서 Cmd+V. 끄려면 Ctrl+C
//
// 붙여넣는 순간 앱을 알아내지 않으므로 --target으로 미리 정한다 (Hangul·Notion·Word)

import AppKit

// 터미널이 아닌 곳으로 출력을 보내도 한 줄씩 바로 나오게 한다
setvbuf(stdout, nil, _IOLBF, 0)

let args = CommandLine.arguments
func option(_ name: String) -> String? {
    guard let index = args.firstIndex(of: name), index + 1 < args.count else { return nil }
    return args[index + 1]
}
guard let target = option("--target"), let mode = option("--mode") else {
    FileHandle.standardError.write("사용법: swift try-mode.swift --target Hangul|Notion|Word --mode Format|Strip\n".data(using: .utf8)!)
    exit(1)
}

func run(_ launchPath: String, _ arguments: [String]) -> (status: Int32, output: String) {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: launchPath)
    process.arguments = arguments
    let pipe = Pipe()
    process.standardOutput = pipe
    process.standardError = pipe
    try? process.run()
    let data = pipe.fileHandleForReading.readDataToEndOfFile()
    process.waitUntilExit()
    return (process.terminationStatus, String(data: data, encoding: .utf8) ?? "")
}

// 변환 부분(C#)은 한 번만 빌드하고, 복사할 때마다 빌드된 dll을 바로 실행한다
let repo = FileManager.default.currentDirectoryPath
let project = repo + "/tools/ClipBridge.TryMode/ClipBridge.TryMode.csproj"
let dll = repo + "/tools/ClipBridge.TryMode/bin/Release/net10.0/ClipBridge.TryMode.dll"
let dotnet = ["/usr/local/share/dotnet/dotnet", "/opt/homebrew/bin/dotnet", "/usr/local/bin/dotnet"]
    .first { FileManager.default.isExecutableFile(atPath: $0) } ?? "/usr/local/share/dotnet/dotnet"
print("변환기 빌드 중…")
let build = run(dotnet, ["build", project, "-c", "Release", "-v", "q", "-nologo"])
guard build.status == 0 else {
    print(build.output)
    exit(1)
}

let work = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("up-try-mode")
let pasteboard = NSPasteboard.general
var lastSeen = pasteboard.changeCount
print("UP 시험 모드 켜짐 — 대상: \(target), Markdown: \(mode). 복사하면 변환합니다. 끄기: Ctrl+C")

func convertCurrentClipboard() {
    guard let item = pasteboard.pasteboardItems?.first else { return }
    // 복사한 직후 앞에 있는 앱을 복사한 앱으로 본다 (PRD 4.5)
    let sourceApp = NSWorkspace.shared.frontmostApplication?.bundleIdentifier ?? ""

    let input = work.appendingPathComponent("input")
    let output = work.appendingPathComponent("output")
    try? FileManager.default.removeItem(at: work)
    try? FileManager.default.createDirectory(at: input, withIntermediateDirectories: true)
    let textTypes: Set<String> = ["public.utf8-plain-text", "public.utf16-plain-text", "public.utf16-external-plain-text"]
    for type in item.types {
        guard let data = item.data(forType: type) else { continue }
        let name = type.rawValue.replacingOccurrences(of: "/", with: "_") + (textTypes.contains(type.rawValue) ? ".txt" : ".bin")
        try? data.write(to: input.appendingPathComponent(name))
    }

    let result = run(dotnet, [dll, input.path, output.path, "--target", target, "--mode", mode, "--source-app", sourceApp])
    let line = result.output.trimmingCharacters(in: .whitespacesAndNewlines)
    let time = DateFormatter.localizedString(from: Date(), dateStyle: .none, timeStyle: .medium)
    guard result.status == 0 else {
        print("[\(time)] \(line)")
        return
    }

    // 변환 결과(HTML·일반 텍스트)만 클립보드에 넣는다. 원본은 시험 모드를 끄고 다시 복사하면 된다
    let newItem = NSPasteboardItem()
    for (type, ext) in [("public.html", "bin"), ("public.utf8-plain-text", "txt")] {
        if let data = try? Data(contentsOf: output.appendingPathComponent("\(type).\(ext)")) {
            newItem.setData(data, forType: NSPasteboard.PasteboardType(type))
        }
    }
    pasteboard.clearContents()
    pasteboard.writeObjects([newItem])
    // 자기가 쓴 변경은 새 복사로 보지 않는다 (FR-02)
    lastSeen = pasteboard.changeCount
    print("[\(time)] \(line) → 클립보드에 넣음")
}

let timer = Timer(timeInterval: 0.3, repeats: true) { _ in
    let count = pasteboard.changeCount
    guard count != lastSeen else { return }
    lastSeen = count
    convertCurrentClipboard()
}
RunLoop.main.add(timer, forMode: .common)
RunLoop.main.run()
