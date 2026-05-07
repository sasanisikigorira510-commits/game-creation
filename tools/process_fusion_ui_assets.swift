import AppKit
import Foundation

struct FusionAsset {
    let sourceName: String
    let outputName: String
    let needsTransparency: Bool
}

let downloads = "/Users/andou/Downloads"
let sourceRoot = "/Users/andou/Desktop/あ/アセット画像/配合関係画像/ChatGPT_2026-05-03"
let originalsRoot = sourceRoot + "/originals"
let unityRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/FusionPage"

let assets: [FusionAsset] = [
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (1).png", outputName: "FusionBackground.png", needsTransparency: false),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (2).png", outputName: "FusionMainFrame.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (3).png", outputName: "FusionParentSlot.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (4).png", outputName: "FusionResultSlot.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (5).png", outputName: "FusionRosterFrame.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (6).png", outputName: "FusionRitualFrame.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (7).png", outputName: "FusionConfirmButton.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_37_59 (8).png", outputName: "FusionSmallButton.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_38_00 (9).png", outputName: "FusionMagicCircle.png", needsTransparency: true),
    FusionAsset(sourceName: "ChatGPT Image 2026年5月3日 21_38_00 (10).png", outputName: "FusionSuccessEffect.png", needsTransparency: true)
]

func createDirectory(_ path: String) throws {
    try FileManager.default.createDirectory(atPath: path, withIntermediateDirectories: true)
}

func loadImage(_ path: String) -> NSBitmapImageRep? {
    guard let image = NSImage(contentsOfFile: path) else {
        return nil
    }

    var rect = NSRect(origin: .zero, size: image.size)
    guard let cgImage = image.cgImage(forProposedRect: &rect, context: nil, hints: nil) else {
        return nil
    }

    return NSBitmapImageRep(cgImage: cgImage)
}

func savePNG(_ bitmap: NSBitmapImageRep, to path: String) throws {
    guard let data = bitmap.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "FusionAssetProcessor", code: 1, userInfo: [NSLocalizedDescriptionKey: "PNG encode failed: \(path)"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func isGeneratedBackgroundPixel(red: Int, green: Int, blue: Int) -> Bool {
    let maxValue = max(red, max(green, blue))
    let minValue = min(red, min(green, blue))
    let average = (red + green + blue) / 3

    // ChatGPT sometimes bakes checkerboards or pale preview backgrounds into images
    // that were requested as transparent. Strip only low-saturation light pixels so
    // metal edges, gems, and colored glows remain intact.
    return maxValue - minValue <= 22 && average >= 205
}

func transparentized(_ source: NSBitmapImageRep) -> NSBitmapImageRep? {
    let width = source.pixelsWide
    let height = source.pixelsHigh
    guard let output = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: width * 4,
        bitsPerPixel: 32
    ) else {
        return nil
    }

    for y in 0..<height {
        for x in 0..<width {
            guard let color = source.colorAt(x: x, y: y)?.usingColorSpace(.deviceRGB) else {
                continue
            }

            let red = Int((color.redComponent * 255.0).rounded())
            let green = Int((color.greenComponent * 255.0).rounded())
            let blue = Int((color.blueComponent * 255.0).rounded())
            let shouldClear = isGeneratedBackgroundPixel(red: red, green: green, blue: blue)
            let alpha: CGFloat = shouldClear ? 0.0 : 1.0
            output.setColor(NSColor(deviceRed: color.redComponent, green: color.greenComponent, blue: color.blueComponent, alpha: alpha), atX: x, y: y)
        }
    }

    return output
}

try createDirectory(sourceRoot)
try createDirectory(originalsRoot)
try createDirectory(unityRoot)

for asset in assets {
    let sourcePath = downloads + "/" + asset.sourceName
    let originalPath = originalsRoot + "/" + asset.outputName
    let sourceCopyPath = sourceRoot + "/" + asset.outputName
    let unityPath = unityRoot + "/" + asset.outputName

    guard FileManager.default.fileExists(atPath: sourcePath) else {
        throw NSError(domain: "FusionAssetProcessor", code: 2, userInfo: [NSLocalizedDescriptionKey: "Missing source: \(sourcePath)"])
    }

    if FileManager.default.fileExists(atPath: originalPath) {
        try FileManager.default.removeItem(atPath: originalPath)
    }
    try FileManager.default.copyItem(atPath: sourcePath, toPath: originalPath)

    guard let bitmap = loadImage(sourcePath) else {
        throw NSError(domain: "FusionAssetProcessor", code: 3, userInfo: [NSLocalizedDescriptionKey: "Could not read image: \(sourcePath)"])
    }

    let processed = asset.needsTransparency ? (transparentized(bitmap) ?? bitmap) : bitmap
    try savePNG(processed, to: sourceCopyPath)
    try savePNG(processed, to: unityPath)

    print("Saved \(asset.outputName)")
}
