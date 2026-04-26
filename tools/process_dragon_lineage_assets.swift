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
        throw NSError(domain: "process_dragon_lineage_assets", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to load \(path)"])
    }

    return image
}

func bitmap(from image: NSImage, rect: NSRect) -> NSBitmapImageRep {
    let target = NSImage(size: rect.size)
    target.lockFocus()
    NSColor.clear.set()
    NSRect(origin: .zero, size: rect.size).fill()
    image.draw(
        in: NSRect(origin: .zero, size: rect.size),
        from: rect,
        operation: .copy,
        fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func isBackgroundCandidate(_ pixel: Pixel) -> Bool {
    if pixel.a == 0 {
        return false
    }

    let minRGB = min(pixel.r, min(pixel.g, pixel.b))
    let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
    return minRGB >= 218 && (maxRGB - minRGB) <= 28
}

func clearEdgeBackground(_ rep: NSBitmapImageRep) {
    guard let data = rep.bitmapData else {
        return
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    let bytesPerRow = rep.bytesPerRow

    func readPixel(_ x: Int, _ y: Int) -> Pixel {
        let offset = y * bytesPerRow + x * bytesPerPixel
        return Pixel(r: data[offset], g: data[offset + 1], b: data[offset + 2], a: data[offset + 3])
    }

    func clear(_ x: Int, _ y: Int) {
        let offset = y * bytesPerRow + x * bytesPerPixel
        data[offset + 3] = 0
    }

    var visited = Array(repeating: false, count: width * height)
    func mark(_ x: Int, _ y: Int) { visited[y * width + x] = true }
    func hasVisited(_ x: Int, _ y: Int) -> Bool { visited[y * width + x] }

    var queue: [(Int, Int)] = []
    queue.reserveCapacity((width * 2) + (height * 2))

    func enqueue(_ x: Int, _ y: Int) {
        if x < 0 || y < 0 || x >= width || y >= height || hasVisited(x, y) {
            return
        }

        if isBackgroundCandidate(readPixel(x, y)) {
            mark(x, y)
            queue.append((x, y))
        }
    }

    for x in 0..<width {
        enqueue(x, 0)
        enqueue(x, height - 1)
    }

    for y in 0..<height {
        enqueue(0, y)
        enqueue(width - 1, y)
    }

    var index = 0
    while index < queue.count {
        let (x, y) = queue[index]
        index += 1
        clear(x, y)
        enqueue(x + 1, y)
        enqueue(x - 1, y)
        enqueue(x, y + 1)
        enqueue(x, y - 1)
    }
}

func pad(_ rep: NSBitmapImageRep, width targetWidth: Int, height targetHeight: Int) -> NSBitmapImageRep {
    let target = NSImage(size: NSSize(width: targetWidth, height: targetHeight))
    let drawWidth = min(rep.pixelsWide, targetWidth)
    let drawHeight = min(rep.pixelsHigh, targetHeight)
    let offsetX = max(0, (targetWidth - drawWidth) / 2)
    let offsetY = max(0, (targetHeight - drawHeight) / 2)

    let frame = NSImage(size: NSSize(width: rep.pixelsWide, height: rep.pixelsHigh))
    frame.addRepresentation(rep)

    target.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: targetWidth, height: targetHeight).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    frame.draw(
        in: NSRect(x: offsetX, y: offsetY, width: drawWidth, height: drawHeight),
        from: NSRect(x: 0, y: 0, width: drawWidth, height: drawHeight),
        operation: .copy,
        fraction: 1.0)
    target.unlockFocus()

    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "process_dragon_lineage_assets", code: 2, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

func processPortrait(source: String, destination: String) throws {
    let image = try loadImage(source)
    let rep = bitmap(from: image, rect: NSRect(x: 0, y: 0, width: image.size.width, height: image.size.height))
    clearEdgeBackground(rep)
    try save(rep, to: destination)
}

func processSheet(source: String, outputDirectory: String, prefix: String, minimumOutputWidth: Int = 760) throws {
    let image = try loadImage(source)
    let frameCount = 4
    let frameWidth = Int(image.size.width) / frameCount
    let frameHeight = Int(image.size.height)
    let outputWidth = max(minimumOutputWidth, frameWidth)
    let outputHeight = frameHeight

    try FileManager.default.createDirectory(atPath: outputDirectory, withIntermediateDirectories: true)

    for index in 0..<frameCount {
        let frame = bitmap(
            from: image,
            rect: NSRect(x: index * frameWidth, y: 0, width: frameWidth, height: frameHeight))
        clearEdgeBackground(frame)
        let padded = pad(frame, width: outputWidth, height: outputHeight)
        try save(padded, to: "\(outputDirectory)/\(prefix)_\(index).png")
    }
}

let args = CommandLine.arguments
if args.count != 6 && args.count != 7 {
    fputs("usage: process_dragon_lineage_assets.swift <source-root> <portrait-out-dir> <battle-out-dir> <source-folder> <output-key> [effect-out-dir]\n", stderr)
    exit(1)
}

let sourceRoot = args[1]
let portraitOutputDir = args[2]
let battleOutputDir = args[3]
let sourceFolder = args[4]
let outputKey = args[5]
let effectOutputDir = args.count == 7 ? args[6] : nil

let sourceDir = "\(sourceRoot)/\(sourceFolder)"
try FileManager.default.createDirectory(atPath: portraitOutputDir, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: battleOutputDir, withIntermediateDirectories: true)

try processPortrait(
    source: "\(sourceDir)/姿絵.png",
    destination: "\(portraitOutputDir)/\(outputKey).png")
try processSheet(
    source: "\(sourceDir)/待機.png",
    outputDirectory: battleOutputDir,
    prefix: "mon_\(outputKey)_idle")
try processSheet(
    source: "\(sourceDir)/移動.png",
    outputDirectory: battleOutputDir,
    prefix: "mon_\(outputKey)_move")
try processSheet(
    source: "\(sourceDir)/攻撃.png",
    outputDirectory: battleOutputDir,
    prefix: "mon_\(outputKey)_attack")

if let effectOutputDir {
    let effectSource = "\(sourceDir)/エフェクト.png"
    if FileManager.default.fileExists(atPath: effectSource) {
        try processSheet(
            source: effectSource,
            outputDirectory: effectOutputDir,
            prefix: "fx_\(outputKey)_attack",
            minimumOutputWidth: 760)
    }
}
