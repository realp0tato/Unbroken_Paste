// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "UnbrokenPaste",
    platforms: [.macOS(.v13)],
    targets: [
        // Swift mirror of src/ClipBridge.Core/Contracts (#29). Foundation only.
        .target(name: "UnbrokenPasteContracts", path: "Sources/UnbrokenPasteContracts"),
        // Copy detection, original snapshot and recent copies (#33).
        .target(
            name: "UnbrokenPasteClipboard",
            dependencies: ["UnbrokenPasteContracts"],
            path: "Sources/UnbrokenPasteClipboard"
        ),
        .executableTarget(
            name: "UnbrokenPaste",
            dependencies: ["UnbrokenPasteContracts", "UnbrokenPasteClipboard"],
            path: "Sources/UnbrokenPaste"
        ),
        .testTarget(
            name: "UnbrokenPasteContractsTests",
            dependencies: ["UnbrokenPasteContracts"],
            path: "Tests/UnbrokenPasteContractsTests"
        ),
        .testTarget(
            name: "UnbrokenPasteClipboardTests",
            dependencies: ["UnbrokenPasteClipboard", "UnbrokenPasteContracts"],
            path: "Tests/UnbrokenPasteClipboardTests"
        ),
    ]
)
