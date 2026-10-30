import ClipBridgeContracts
import Foundation
import os

/// Runs ClipBridge.Helper for the whole session and keeps the latest results (#36, FR-29, PRD 4.5).
///
/// Copy → `prepare` → results for every target arrive in the background. Paste reads `result(for:sequence:)`
/// without calling the Helper. Anything missing, stale or failed is a passthrough, so the app keeps
/// pasting normally even when the Helper is not there (PRD 10).
@MainActor
final class HelperClient: NSObject {
    private static let log = Logger(subsystem: "app.clipbridge.menubar", category: "helper")
    private static let responseTimeout: TimeInterval = 10
    private static let maxRestartDelay: TimeInterval = 30

    private(set) var isReady = false
    /// Results for the current copy only (FR-17).
    private(set) var latest: HelperResults?

    private var process: Process?
    private var input: FileHandle?
    private var buffer = LineBuffer()
    private var nextID: Int64 = 1
    private var options = ConversionOptions.default
    /// The copy the app currently cares about; sent again after a restart.
    private var current: ClipboardSnapshot?
    private var waitingSince: TimeInterval?
    private var restartDelay: TimeInterval = 1
    private var restartAt: TimeInterval?
    private var stopping = false
    private var watchdog: Timer?

    func start(options: ConversionOptions) {
        self.options = options
        signal(SIGPIPE, SIG_IGN)  // a dead Helper must not kill the app on write
        launch()
        let timer = Timer(timeInterval: 1, target: self, selector: #selector(checkHealth), userInfo: nil, repeats: true)
        RunLoop.main.add(timer, forMode: .common)
        watchdog = timer
    }

    /// New copy: ask for results for every target. Earlier results stop being used right away.
    func prepare(_ snapshot: ClipboardSnapshot) {
        current = snapshot
        latest = nil
        sendPrepare()
    }

    /// The copy was dropped (concealed or empty clipboard): paste passes through.
    func clear() {
        current = nil
        latest = nil
    }

    func setOptions(_ options: ConversionOptions) {
        self.options = options
        send(.setOptions(id: takeID(), options: options))
    }

    /// Result for a paste. Passthrough unless results for exactly this copy are here.
    func result(for target: TargetApp, sequence: Int64) -> ConversionResult {
        guard let latest, latest.sequenceNumber == sequence else { return .passthrough }
        return latest.result(for: target)
    }

    func shutdown() {
        stopping = true
        watchdog?.invalidate()
        guard let process, process.isRunning else { return }
        send(.shutdown(id: takeID()))
        try? input?.close()  // EOF also ends the Helper's read loop
        let deadline = Date().addingTimeInterval(0.5)
        while process.isRunning && Date() < deadline {
            usleep(10_000)
        }
        if process.isRunning { process.terminate() }
    }

    // MARK: - Process

    private func launch() {
        guard let url = Self.executableURL() else {
            Self.log.error("helper executable not found; pasting passes through")
            return
        }
        let process = Process()
        process.executableURL = url
        let stdin = Pipe(), stdout = Pipe()
        process.standardInput = stdin
        process.standardOutput = stdout
        process.standardError = FileHandle.nullDevice
        stdout.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            if data.isEmpty { handle.readabilityHandler = nil }
            DispatchQueue.main.async { self?.receive(data) }
        }
        process.terminationHandler = { [weak self] ended in
            let status = ended.terminationStatus
            DispatchQueue.main.async { self?.exited(process: ended, status: status) }
        }
        do {
            try process.run()
        } catch {
            Self.log.error("helper launch failed: \(error.localizedDescription, privacy: .public)")
            scheduleRestart()
            return
        }
        self.process = process
        input = stdin.fileHandleForWriting
        buffer = LineBuffer()
        isReady = false
        waitingSince = ProcessInfo.processInfo.systemUptime  // waiting for "ready"
        Self.log.info("helper started: \(url.lastPathComponent, privacy: .public)")
    }

    private func exited(process ended: Process, status: Int32) {
        guard ended === process else { return }
        process = nil
        input = nil
        isReady = false
        waitingSince = nil
        latest = nil
        guard !stopping else { return }
        Self.log.error("helper exited (status \(status, privacy: .public)); restarting")
        scheduleRestart()
    }

    private func scheduleRestart() {
        restartAt = ProcessInfo.processInfo.systemUptime + restartDelay
        restartDelay = min(restartDelay * 2, Self.maxRestartDelay)
    }

    /// Restarts a dead Helper and kills one that stopped answering.
    @objc private func checkHealth() {
        let now = ProcessInfo.processInfo.systemUptime
        if let restartAt, now >= restartAt, process == nil, !stopping {
            self.restartAt = nil
            launch()
        } else if let waitingSince, now - waitingSince > Self.responseTimeout, let process {
            Self.log.error("helper not responding; restarting")
            self.waitingSince = nil
            process.terminate()  // terminationHandler schedules the restart
        }
    }

    // MARK: - Messages

    private func receive(_ data: Data) {
        for line in buffer.append(data) {
            do {
                handle(try HelperMessage.decode(line: line))
            } catch {
                Self.log.error("unreadable helper message: \(error.localizedDescription, privacy: .public)")
            }
        }
    }

    private func handle(_ message: HelperMessage) {
        switch message {
        case let .ready(version):
            if version != HelperProtocol.version {
                Self.log.error("helper protocol \(version, privacy: .public), app expects \(HelperProtocol.version, privacy: .public)")
            }
            isReady = true
            waitingSince = nil
            restartDelay = 1
            send(.setOptions(id: takeID(), options: options))
            if current != nil { sendPrepare() }  // resend the current copy after a restart
        case let .results(results):
            waitingSince = nil
            guard results.sequenceNumber == current?.sequenceNumber else { return }  // FR-17: an older copy
            latest = results
            let summary = TargetApp.allCases.map { "\($0.rawValue)=\(results.result(for: $0).converterId)" }.joined(separator: " ")
            Self.log.info("results seq=\(results.sequenceNumber, privacy: .public) source=\(results.source.rawValue, privacy: .public) \(summary, privacy: .public)")
        case .ok:
            waitingSince = nil
        case let .error(_, code, message):
            waitingSince = nil
            Self.log.error("helper error \(code, privacy: .public): \(message, privacy: .public)")
        case let .unknown(type):
            Self.log.info("ignored helper message type \(type, privacy: .public)")
        }
    }

    private func sendPrepare() {
        guard let current else { return }
        send(.prepare(id: takeID(), snapshot: HelperProtocol.snapshotForCore(current), options: options))
    }

    private func send(_ request: HelperRequest) {
        guard isReady || request.type == "shutdown", let input else { return }  // before "ready", ready() resends
        do {
            try input.write(contentsOf: try request.line())
            if waitingSince == nil { waitingSince = ProcessInfo.processInfo.systemUptime }
        } catch {
            Self.log.error("helper write failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    private func takeID() -> Int64 {
        defer { nextID += 1 }
        return nextID
    }

    // MARK: - Locating the Helper

    #if arch(arm64)
    private static let helperName = "ClipBridge.Helper-arm64"
    private static let devFolder = "helper-arm64"
    #else
    private static let helperName = "ClipBridge.Helper-x64"
    private static let devFolder = "helper-x64"
    #endif

    /// `UP_HELPER` env var, then the app bundle (Contents/MacOS), then the build folder used by `swift run`.
    static func executableURL() -> URL? {
        let fileManager = FileManager.default
        if let path = ProcessInfo.processInfo.environment["UP_HELPER"], fileManager.isExecutableFile(atPath: path) {
            return URL(fileURLWithPath: path)
        }
        if let bundled = Bundle.main.url(forAuxiliaryExecutable: helperName) {
            return bundled
        }
        let dev = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // ClipBridgeMenuBar
            .deletingLastPathComponent()  // Sources
            .deletingLastPathComponent()  // package root
            .appendingPathComponent("build/\(devFolder)/ClipBridge.Helper")
        return fileManager.isExecutableFile(atPath: dev.path) ? dev : nil
    }
}
