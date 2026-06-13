import AppKit
import Foundation

let projectRoot = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let inputURL = projectRoot.appendingPathComponent("WitchTowerGame/tmp/imagegen/home_quest_button/QuestButtonRound_source_image2.png")
let outputURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeMenu/QuestButtonRound.png")

func loadBitmap(_ url: URL) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOf: url) else {
        throw NSError(domain: "HomeQuestButton", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not load \(url.path)"])
    }

    guard let source = image.representations.first else {
        throw NSError(domain: "HomeQuestButton", code: 2, userInfo: [NSLocalizedDescriptionKey: "No representation for \(url.path)"])
    }

    let width = source.pixelsWide
    let height = source.pixelsHigh
    guard let bitmap = NSBitmapImageRep(
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
        throw NSError(domain: "HomeQuestButton", code: 3, userInfo: [NSLocalizedDescriptionKey: "Could not allocate bitmap"])
    }

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
    NSColor.clear.setFill()
    NSRect(x: 0, y: 0, width: width, height: height).fill()
    image.draw(in: NSRect(x: 0, y: 0, width: width, height: height))
    NSGraphicsContext.restoreGraphicsState()
    return bitmap
}

func pixel(_ bitmap: NSBitmapImageRep, x: Int, y: Int) -> [Int] {
    var rgba = [Int](repeating: 0, count: 4)
    bitmap.getPixel(&rgba, atX: x, y: y)
    return rgba
}

func setPixel(_ bitmap: NSBitmapImageRep, x: Int, y: Int, _ rgba: [Int]) {
    var value = rgba
    bitmap.setPixel(&value, atX: x, y: y)
}

func averageBorderKey(_ bitmap: NSBitmapImageRep) -> [Double] {
    let width = bitmap.pixelsWide
    let height = bitmap.pixelsHigh
    let sample = max(6, min(width, height) / 28)
    var totals = [Double](repeating: 0, count: 3)
    var count = 0.0

    for y in 0..<height {
        for x in 0..<width where x < sample || x >= width - sample || y < sample || y >= height - sample {
            let rgba = pixel(bitmap, x: x, y: y)
            totals[0] += Double(rgba[0])
            totals[1] += Double(rgba[1])
            totals[2] += Double(rgba[2])
            count += 1.0
        }
    }

    return totals.map { $0 / max(1.0, count) }
}

func chromaDistance(_ rgba: [Int], _ key: [Double]) -> Double {
    let dr = Double(rgba[0]) - key[0]
    let dg = Double(rgba[1]) - key[1]
    let db = Double(rgba[2]) - key[2]
    return sqrt(dr * dr + dg * dg + db * db)
}

func removeChroma(_ bitmap: NSBitmapImageRep) {
    let width = bitmap.pixelsWide
    let height = bitmap.pixelsHigh
    let key = averageBorderKey(bitmap)
    let transparentDistance = 96.0
    let opaqueDistance = 236.0
    let keyDominantChannel = key.enumerated().max(by: { $0.element < $1.element })?.offset ?? 1

    for y in 0..<height {
        for x in 0..<width {
            var rgba = pixel(bitmap, x: x, y: y)
            let distance = chromaDistance(rgba, key)
            let strongestNonGreen = max(rgba[0], rgba[2])
            let isStrongGreen = rgba[1] > 80 &&
                rgba[1] - strongestNonGreen > 24 &&
                Double(rgba[1]) > Double(rgba[0]) * 1.25 &&
                Double(rgba[1]) > Double(rgba[2]) * 1.25
            if distance <= transparentDistance || isStrongGreen {
                rgba[3] = 0
            } else if distance < opaqueDistance {
                let t = (distance - transparentDistance) / (opaqueDistance - transparentDistance)
                rgba[3] = Int(Double(rgba[3]) * max(0.0, min(1.0, t)))
                let spill = Int((1.0 - t) * 120.0)
                rgba[keyDominantChannel] = max(0, rgba[keyDominantChannel] - spill)
            }

            setPixel(bitmap, x: x, y: y, rgba)
        }
    }
}

func saveBitmap(_ bitmap: NSBitmapImageRep, to url: URL) throws {
    guard let pngData = bitmap.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "HomeQuestButton", code: 5, userInfo: [NSLocalizedDescriptionKey: "Could not encode \(url.path)"])
    }

    try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
    try pngData.write(to: url, options: .atomic)
}

let source = try loadBitmap(inputURL)
removeChroma(source)
let sourceCornerAlpha = [
    pixel(source, x: 0, y: 0)[3],
    pixel(source, x: source.pixelsWide - 1, y: 0)[3],
    pixel(source, x: 0, y: source.pixelsHigh - 1)[3],
    pixel(source, x: source.pixelsWide - 1, y: source.pixelsHigh - 1)[3]
]
try saveBitmap(source, to: outputURL)

print("Wrote \(outputURL.path)")
print("Corner alpha \(sourceCornerAlpha)")
