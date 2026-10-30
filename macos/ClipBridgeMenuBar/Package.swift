// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "ClipBridgeMenuBar",
    platforms: [.macOS(.v13)],
    targets: [
        // Swift mirror of src/ClipBridge.Core/Contracts (#29). Foundation only.
        .target(name: "ClipBridgeContracts", path: "Sources/ClipBridgeContracts"),
        // Copy detection, original snapshot and recent copies (#33).
        .target(
            name: "ClipBridgeClipboard",
            dependencies: ["ClipBridgeContracts"],
            path: "Sources/ClipBridgeClipboard"
        ),
        .executableTarget(
            name: "ClipBridgeMenuBar",
            dependencies: ["ClipBridgeContracts", "ClipBridgeClipboard"],
            path: "Sources/ClipBridgeMenuBar"
        ),
        .testTarget(
            name: "ClipBridgeContractsTests",
            dependencies: ["ClipBridgeContracts"],
            path: "Tests/ClipBridgeContractsTests"
        ),
        .testTarget(
            name: "ClipBridgeClipboardTests",
            dependencies: ["ClipBridgeClipboard", "ClipBridgeContracts"],
            path: "Tests/ClipBridgeClipboardTests"
        ),
    ]
)
