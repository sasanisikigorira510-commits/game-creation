import AppKit
import Foundation

struct Rect {
    var minX: Int
    var minY: Int
    var maxX: Int
    var maxY: Int

    var width: Int { max(0, maxX - minX + 1) }
    var height: Int { max(0, maxY - minY + 1) }
    var area: Int { width * height }
    var centerX: Double { Double(minX + maxX) * 0.5 }
    var centerY: Double { Double(minY + maxY) * 0.5 }
}

let projectRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame"
let sourcePath = "/Users/andou/Desktop/あ/アセット画像/ボタン.png"
let outputRoot = "\(projectRoot)/Assets/Resources/UI/HomeMenu"
let panelPath = "\(outputRoot)/HomeMenuPanel.png"
let backgroundPath = "\(outputRoot)/HomeMenuBackground.png"
// NSBitmapImageRep's y-axis makes the lower visual row appear first here.
let buttonNames = ["EquipmentButton", "FusionButton", "BattleButton", "FormationButton"]

func loadBitmap(_ path: String) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOfFile: path),
          let tiff = image.tiffRepresentation,
          let bitmap = NSBitmapImageRep(data: tiff) else {
        throw NSError(domain: "ImageImport", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not load image: \(path)"])
    }

    guard let converted = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: bitmap.pixelsWide,
        pixelsHigh: bitmap.pixelsHigh,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: bitmap.pixelsWide * 4,
        bitsPerPixel: 32) else {
        throw NSError(domain: "ImageImport", code: 2, userInfo: [NSLocalizedDescriptionKey: "Could not allocate bitmap"])
    }

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: converted)
    image.draw(in: NSRect(x: 0, y: 0, width: bitmap.pixelsWide, height: bitmap.pixelsHigh))
    NSGraphicsContext.restoreGraphicsState()
    return converted
}

func saveBitmap(_ bitmap: NSBitmapImageRep, to path: String) throws {
    guard let data = bitmap.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "ImageImport", code: 3, userInfo: [NSLocalizedDescriptionKey: "Could not encode png"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func pixel(_ bitmap: NSBitmapImageRep, _ x: Int, _ y: Int) -> [Int] {
    var result = [Int](repeating: 0, count: 4)
    bitmap.getPixel(&result, atX: x, y: y)
    return result
}

func setPixel(_ bitmap: NSBitmapImageRep, _ x: Int, _ y: Int, _ rgba: [Int]) {
    var value = rgba
    bitmap.setPixel(&value, atX: x, y: y)
}

func isCheckerBackground(_ rgba: [Int]) -> Bool {
    let r = rgba[0]
    let g = rgba[1]
    let b = rgba[2]
    let maxChannel = max(r, max(g, b))
    let minChannel = min(r, min(g, b))
    return minChannel >= 205 && maxChannel - minChannel <= 12
}

func clearEdgeConnectedCheckerboard(_ bitmap: NSBitmapImageRep) {
    let width = bitmap.pixelsWide
    let height = bitmap.pixelsHigh
    var visited = Array(repeating: false, count: width * height)
    var queue = [(Int, Int)]()

    func enqueue(_ x: Int, _ y: Int) {
        guard x >= 0, x < width, y >= 0, y < height else { return }
        let index = y * width + x
        if visited[index] { return }
        let rgba = pixel(bitmap, x, y)
        if !isCheckerBackground(rgba) { return }
        visited[index] = true
        queue.append((x, y))
    }

    for x in 0..<width {
        enqueue(x, 0)
        enqueue(x, height - 1)
    }

    for y in 0..<height {
        enqueue(0, y)
        enqueue(width - 1, y)
    }

    var head = 0
    while head < queue.count {
        let (x, y) = queue[head]
        head += 1
        setPixel(bitmap, x, y, [0, 0, 0, 0])
        enqueue(x + 1, y)
        enqueue(x - 1, y)
        enqueue(x, y + 1)
        enqueue(x, y - 1)
    }
}

func alphaBounds(_ bitmap: NSBitmapImageRep, alphaThreshold: Int = 8) -> Rect? {
    let width = bitmap.pixelsWide
    let height = bitmap.pixelsHigh
    var rect = Rect(minX: width, minY: height, maxX: -1, maxY: -1)
    for y in 0..<height {
        for x in 0..<width {
            if pixel(bitmap, x, y)[3] > alphaThreshold {
                rect.minX = min(rect.minX, x)
                rect.minY = min(rect.minY, y)
                rect.maxX = max(rect.maxX, x)
                rect.maxY = max(rect.maxY, y)
            }
        }
    }

    return rect.maxX >= rect.minX && rect.maxY >= rect.minY ? rect : nil
}

func alphaBounds(in bitmap: NSBitmapImageRep, searchRect: Rect, alphaThreshold: Int = 8) -> Rect? {
    var rect = Rect(minX: bitmap.pixelsWide, minY: bitmap.pixelsHigh, maxX: -1, maxY: -1)
    for y in max(0, searchRect.minY)...min(bitmap.pixelsHigh - 1, searchRect.maxY) {
        for x in max(0, searchRect.minX)...min(bitmap.pixelsWide - 1, searchRect.maxX) {
            if pixel(bitmap, x, y)[3] > alphaThreshold {
                rect.minX = min(rect.minX, x)
                rect.minY = min(rect.minY, y)
                rect.maxX = max(rect.maxX, x)
                rect.maxY = max(rect.maxY, y)
            }
        }
    }

    return rect.maxX >= rect.minX && rect.maxY >= rect.minY ? rect : nil
}

func connectedComponents(_ bitmap: NSBitmapImageRep, alphaThreshold: Int = 8) -> [Rect] {
    let width = bitmap.pixelsWide
    let height = bitmap.pixelsHigh
    var visited = Array(repeating: false, count: width * height)
    var rects = [Rect]()

    for y in 0..<height {
        for x in 0..<width {
            let index = y * width + x
            if visited[index] || pixel(bitmap, x, y)[3] <= alphaThreshold {
                continue
            }

            visited[index] = true
            var rect = Rect(minX: x, minY: y, maxX: x, maxY: y)
            var queue = [(x, y)]
            var head = 0
            while head < queue.count {
                let (cx, cy) = queue[head]
                head += 1
                rect.minX = min(rect.minX, cx)
                rect.minY = min(rect.minY, cy)
                rect.maxX = max(rect.maxX, cx)
                rect.maxY = max(rect.maxY, cy)

                for (nx, ny) in [(cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)] {
                    guard nx >= 0, nx < width, ny >= 0, ny < height else { continue }
                    let nextIndex = ny * width + nx
                    if visited[nextIndex] || pixel(bitmap, nx, ny)[3] <= alphaThreshold {
                        continue
                    }

                    visited[nextIndex] = true
                    queue.append((nx, ny))
                }
            }

            if rect.area > 1_000 {
                rects.append(rect)
            }
        }
    }

    return rects
}

func crop(_ bitmap: NSBitmapImageRep, rect: Rect, padding: Int) -> NSBitmapImageRep {
    let minX = max(0, rect.minX - padding)
    let minY = max(0, rect.minY - padding)
    let maxX = min(bitmap.pixelsWide - 1, rect.maxX + padding)
    let maxY = min(bitmap.pixelsHigh - 1, rect.maxY + padding)
    let outWidth = maxX - minX + 1
    let outHeight = maxY - minY + 1
    let output = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: outWidth,
        pixelsHigh: outHeight,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: outWidth * 4,
        bitsPerPixel: 32)!

    for y in 0..<outHeight {
        for x in 0..<outWidth {
            setPixel(output, x, y, pixel(bitmap, minX + x, minY + y))
        }
    }

    return output
}

func resized(_ source: NSBitmapImageRep, width: Int, height: Int) -> NSBitmapImageRep {
    let output = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: width * 4,
        bitsPerPixel: 32)!

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: output)
    NSGraphicsContext.current?.imageInterpolation = .high
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: width, height: height).fill()
    let image = NSImage(size: NSSize(width: source.pixelsWide, height: source.pixelsHigh))
    image.addRepresentation(source)
    image.draw(in: NSRect(x: 0, y: 0, width: width, height: height))
    NSGraphicsContext.restoreGraphicsState()
    return output
}

func paste(_ source: NSBitmapImageRep, into destination: NSBitmapImageRep, atX originX: Int, atY originY: Int) {
    for y in 0..<source.pixelsHigh {
        let dy = originY + y
        if dy < 0 || dy >= destination.pixelsHigh { continue }
        for x in 0..<source.pixelsWide {
            let dx = originX + x
            if dx < 0 || dx >= destination.pixelsWide { continue }
            let src = pixel(source, x, y)
            if src[3] == 0 { continue }
            setPixel(destination, dx, dy, src)
        }
    }
}

func blankBitmap(width: Int, height: Int) -> NSBitmapImageRep {
    let bitmap = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: width * 4,
        bitsPerPixel: 32)!

    for y in 0..<height {
        for x in 0..<width {
            setPixel(bitmap, x, y, [0, 0, 0, 0])
        }
    }

    return bitmap
}

func cleanedHomeBackground(_ source: NSBitmapImageRep, cutoffY: Int) -> NSBitmapImageRep {
    let width = source.pixelsWide
    let height = source.pixelsHigh
    let safeCutoff = max(1, min(height - 1, cutoffY))
    let output = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: width * 4,
        bitsPerPixel: 32)!

    for y in 0..<height {
        let sourceY = min(safeCutoff - 1, y)
        for x in 0..<width {
            var rgba = pixel(source, x, sourceY)
            // Background is an opaque illustration; force alpha to avoid importer
            // treating RGB-only source pixels as transparent on re-save.
            rgba[3] = 255
            if y >= safeCutoff {
                let distance = Double(y - safeCutoff) / Double(max(1, height - safeCutoff))
                let fade = 1.0 - min(1.0, distance)
                rgba[0] = Int(2.0 + 5.0 * fade)
                rgba[1] = Int(6.0 + 7.0 * fade)
                rgba[2] = Int(13.0 + 9.0 * fade)
            }
            setPixel(output, x, y, rgba)
        }
    }

    return output
}

let source = try loadBitmap(sourcePath)
clearEdgeConnectedCheckerboard(source)
let halfWidth = source.pixelsWide / 2
let halfHeight = source.pixelsHigh / 2
let quadrantGap = 24
let ordered = [
    Rect(minX: 0, minY: halfHeight + quadrantGap, maxX: halfWidth - quadrantGap, maxY: source.pixelsHigh - 1),
    Rect(minX: halfWidth + quadrantGap, minY: halfHeight + quadrantGap, maxX: source.pixelsWide - 1, maxY: source.pixelsHigh - 1),
    Rect(minX: 0, minY: 0, maxX: halfWidth - quadrantGap, maxY: halfHeight - quadrantGap),
    Rect(minX: halfWidth + quadrantGap, minY: 0, maxX: source.pixelsWide - 1, maxY: halfHeight - quadrantGap)
].enumerated().map { index, searchRect -> Rect in
    guard let bounds = alphaBounds(in: source, searchRect: searchRect) else {
        fatalError("Could not find button content in quadrant \(index)")
    }

    return bounds
}

let extracted = ordered.enumerated().map { index, rect -> NSBitmapImageRep in
    let button = crop(source, rect: rect, padding: 8)
    try? saveBitmap(button, to: "\(outputRoot)/\(buttonNames[index]).png")
    return button
}

let panel = blankBitmap(width: 1024, height: 720)
let placements = [
    (x: 20, y: 360),
    (x: 526, y: 360),
    (x: 20, y: 20),
    (x: 526, y: 20)
]

for index in 0..<extracted.count {
    paste(extracted[index], into: panel, atX: placements[index].x, atY: placements[index].y)
}

try saveBitmap(panel, to: panelPath)

if FileManager.default.fileExists(atPath: backgroundPath) {
    let background = try loadBitmap(backgroundPath)
    let cleaned = cleanedHomeBackground(background, cutoffY: 586)
    try saveBitmap(cleaned, to: backgroundPath)
}

print("Imported home menu buttons into \(panelPath)")
