import AppKit
import Foundation

struct Pixel {
    var r: UInt8
    var g: UInt8
    var b: UInt8
    var a: UInt8
}

func loadImage(_ path: String) throws -> NSImage {
    guard let image = NSImage(contentsOfFile: path) else {
        throw NSError(domain: "rebuild_attack_sheet", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to load \(path)"])
    }
    return image
}

func crop(_ src: NSImage, x: Int, width: Int, height: Int) -> NSBitmapImageRep {
    let crop = NSImage(size: NSSize(width: width, height: height))
    crop.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: width, height: height).fill()
    src.draw(
        in: NSRect(x: 0, y: 0, width: width, height: height),
        from: NSRect(x: x, y: 0, width: width, height: height),
        operation: .copy,
        fraction: 1.0)
    crop.unlockFocus()
    return NSBitmapImageRep(data: crop.tiffRepresentation!)!
}

func isBackgroundCandidate(_ pixel: Pixel) -> Bool {
    if pixel.a == 0 {
        return false
    }

    let minRGB = min(pixel.r, min(pixel.g, pixel.b))
    let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
    return minRGB >= 220 && (maxRGB - minRGB) <= 20
}

func removeCheckerboard(_ rep: NSBitmapImageRep) {
    guard let data = rep.bitmapData else {
        return
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    let bytesPerRow = rep.bytesPerRow

    func readPixel(_ x: Int, _ y: Int) -> Pixel {
        let offset = y * bytesPerRow + x * bytesPerPixel
        return Pixel(
            r: data[offset],
            g: data[offset + 1],
            b: data[offset + 2],
            a: data[offset + 3])
    }

    func clearAlpha(_ x: Int, _ y: Int) {
        let offset = y * bytesPerRow + x * bytesPerPixel
        data[offset + 3] = 0
    }

    var visited = Array(repeating: false, count: width * height)
    func markVisited(_ x: Int, _ y: Int) { visited[y * width + x] = true }
    func wasVisited(_ x: Int, _ y: Int) -> Bool { visited[y * width + x] }

    var queue: [(Int, Int)] = []
    queue.reserveCapacity((width * 2) + (height * 2))

    func tryEnqueue(_ x: Int, _ y: Int) {
        if x < 0 || y < 0 || x >= width || y >= height || wasVisited(x, y) {
            return
        }

        if isBackgroundCandidate(readPixel(x, y)) {
            markVisited(x, y)
            queue.append((x, y))
        }
    }

    for x in 0..<width {
        tryEnqueue(x, 0)
        tryEnqueue(x, height - 1)
    }

    for y in 0..<height {
        tryEnqueue(0, y)
        tryEnqueue(width - 1, y)
    }

    var index = 0
    while index < queue.count {
        let (x, y) = queue[index]
        index += 1
        clearAlpha(x, y)
        tryEnqueue(x + 1, y)
        tryEnqueue(x - 1, y)
        tryEnqueue(x, y + 1)
        tryEnqueue(x, y - 1)
    }
}

func padToCanvas(_ rep: NSBitmapImageRep, targetWidth: Int, targetHeight: Int) -> NSBitmapImageRep {
    let dest = NSImage(size: NSSize(width: targetWidth, height: targetHeight))
    let drawWidth = rep.pixelsWide
    let drawHeight = rep.pixelsHigh
    let offsetX = max(0, (targetWidth - drawWidth) / 2)
    let offsetY = max(0, (targetHeight - drawHeight) / 2)

    dest.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: targetWidth, height: targetHeight).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    let nsImage = NSImage(size: NSSize(width: drawWidth, height: drawHeight))
    nsImage.addRepresentation(rep)
    nsImage.draw(
        in: NSRect(x: offsetX, y: offsetY, width: drawWidth, height: drawHeight),
        from: NSRect(x: 0, y: 0, width: drawWidth, height: drawHeight),
        operation: .copy,
        fraction: 1.0)
    dest.unlockFocus()

    return NSBitmapImageRep(data: dest.tiffRepresentation!)!
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "rebuild_attack_sheet", code: 2, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

let args = CommandLine.arguments
if args.count != 4 {
    fputs("usage: rebuild_attack_sheet.swift <sheet-path> <output-dir> <output-prefix>\n", stderr)
    exit(1)
}

let sheetPath = args[1]
let outputDir = args[2]
let outputPrefix = args[3]

let sheetImage = try loadImage(sheetPath)
try FileManager.default.createDirectory(atPath: outputDir, withIntermediateDirectories: true)

let frameWidth = 627
let frameHeight = 627
let outputWidth = 760
let outputHeight = 627

for index in 0..<4 {
    let frame = crop(sheetImage, x: index * frameWidth, width: frameWidth, height: frameHeight)
    removeCheckerboard(frame)
    let padded = padToCanvas(frame, targetWidth: outputWidth, targetHeight: outputHeight)
    try save(padded, to: "\(outputDir)/\(outputPrefix)_\(index).png")
}
