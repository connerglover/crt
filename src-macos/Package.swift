// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "CRT",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "CRT", targets: ["CRT"]),
        .library(name: "CRTCore", targets: ["CRTCore"]),
    ],
    targets: [
        .target(
            name: "CRTCore"
        ),
        .executableTarget(
            name: "CRT",
            dependencies: ["CRTCore"]
        ),
        .testTarget(
            name: "CRTCoreTests",
            dependencies: ["CRTCore"]
        ),
    ]
)
