import Darwin
import Foundation

enum SlimeMode: String {
    case idle, busy, cpu
}

/// Time-based playback: the frame follows elapsed time, so speed changes never stutter.
final class SlimeAnimator {
    static let idleCycle = 1.5
    static let busyCycle = 0.6

    var frameCount: Int
    var mode: SlimeMode = .idle
    private var phase = 0.0
    private var last: TimeInterval?
    private var fastUntil = 0.0
    private let cpuMeter = CPUMeter()
    private var cpuLoad = 0.0
    private var cpuSampledAt = -Double.infinity

    init(frameCount: Int) {
        self.frameCount = frameCount
    }

    func react(seconds: TimeInterval, now: TimeInterval) {
        fastUntil = max(fastUntil, now + seconds)
    }

    func nextFrame(now: TimeInterval) -> Int {
        guard frameCount > 0 else { return 0 }
        let elapsed = last.map { now - $0 } ?? 0
        last = now
        phase = (phase + elapsed / cycle(now: now)).truncatingRemainder(dividingBy: 1)
        return min(Int(phase * Double(frameCount)), frameCount - 1)
    }

    private func cycle(now: TimeInterval) -> TimeInterval {
        if now < fastUntil { return Self.busyCycle }
        switch mode {
        case .idle: return Self.idleCycle
        case .busy: return Self.busyCycle
        case .cpu:
            if now - cpuSampledAt >= 1 {
                cpuLoad = cpuMeter.sample() ?? cpuLoad
                cpuSampledAt = now
            }
            return 1.8 - 1.3 * cpuLoad
        }
    }
}

/// Whole-machine CPU usage from host tick counters (0...1).
final class CPUMeter {
    private let host = mach_host_self()
    private var previous: (busy: UInt64, total: UInt64)?

    func sample() -> Double? {
        var info = host_cpu_load_info()
        var count = mach_msg_type_number_t(MemoryLayout<host_cpu_load_info_data_t>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &info) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics(host, HOST_CPU_LOAD_INFO, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return nil }
        let ticks = info.cpu_ticks  // user, system, idle, nice
        let busy = UInt64(ticks.0) + UInt64(ticks.1) + UInt64(ticks.3)
        let total = busy + UInt64(ticks.2)
        defer { previous = (busy, total) }
        guard let p = previous, total > p.total, busy >= p.busy else { return nil }
        return min(1, Double(busy - p.busy) / Double(total - p.total))
    }
}
