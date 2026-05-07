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
        throw NSError(domain: "import_custom_rarity_frames", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to load \(path)"])
    }

    return image
}

func bitmap(from image: NSImage) -> NSBitmapImageRep {
    let rect = NSRect(x: 0, y: 0, width: image.size.width, height: image.size.height)
    let target = NSImage(size: rect.size)
    target.lockFocus()
    NSColor.clear.set()
    rect.fill()
    NSGraphicsContext.current?.imageInterpolation = .high
    image.draw(in: rect, from: rect, operation: .copy, fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func image(from rep: NSBitmapImageRep) -> NSImage {
    let result = NSImage(size: NSSize(width: rep.pixelsWide, height: rep.pixelsHigh))
    result.addRepresentation(rep)
    return result
}

func isCheckerboardCandidate(_ pixel: Pixel) -> Bool {
    if pixel.a <= 8 {
        return false
    }

    let minRGB = min(pixel.r, min(pixel.g, pixel.b))
    let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
    return minRGB >= 205 && (maxRGB - minRGB) <= 26
}

func clearCheckerboardBackground(_ rep: NSBitmapImageRep) {
    guard let data = rep.bitmapData else {
        return
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    let bytesPerRow = rep.bytesPerRow

    if bytesPerPixel < 4 {
        return
    }

    func readPixel(_ x: Int, _ y: Int) -> Pixel {
        let offset = y * bytesPerRow + x * bytesPerPixel
        return Pixel(r: data[offset], g: data[offset + 1], b: data[offset + 2], a: data[offset + 3])
    }

    func clear(_ x: Int, _ y: Int) {
        let offset = y * bytesPerRow + x * bytesPerPixel
        data[offset] = 0
        data[offset + 1] = 0
        data[offset + 2] = 0
        data[offset + 3] = 0
    }

    var visited = Array(repeating: false, count: width * height)
    func hasVisited(_ x: Int, _ y: Int) -> Bool { visited[y * width + x] }
    func mark(_ x: Int, _ y: Int) { visited[y * width + x] = true }

    var queue: [(Int, Int)] = []
    queue.reserveCapacity((width + height) * 2)

    func enqueue(_ x: Int, _ y: Int) {
        if x < 0 || y < 0 || x >= width || y >= height || hasVisited(x, y) {
            return
        }

        if isCheckerboardCandidate(readPixel(x, y)) {
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

        for neighborY in max(0, y - 1)...min(height - 1, y + 1) {
            for neighborX in max(0, x - 1)...min(width - 1, x + 1) {
                if neighborX == x && neighborY == y {
                    continue
                }

                enqueue(neighborX, neighborY)
            }
        }
    }

    func collectComponent(startX: Int, startY: Int) -> [(Int, Int)] {
        var component: [(Int, Int)] = []
        var componentQueue: [(Int, Int)] = [(startX, startY)]
        mark(startX, startY)

        var componentIndex = 0
        while componentIndex < componentQueue.count {
            let (x, y) = componentQueue[componentIndex]
            componentIndex += 1
            component.append((x, y))

            for neighborY in max(0, y - 1)...min(height - 1, y + 1) {
                for neighborX in max(0, x - 1)...min(width - 1, x + 1) {
                    if neighborX == x && neighborY == y {
                        continue
                    }

                    if neighborX < 0 || neighborY < 0 || neighborX >= width || neighborY >= height || hasVisited(neighborX, neighborY) {
                        continue
                    }

                    if isCheckerboardCandidate(readPixel(neighborX, neighborY)) {
                        mark(neighborX, neighborY)
                        componentQueue.append((neighborX, neighborY))
                    }
                }
            }
        }

        return component
    }

    let minimumBackgroundComponentArea = max(400, (width * height) / 1500)
    for y in 0..<height {
        for x in 0..<width {
            if hasVisited(x, y) || !isCheckerboardCandidate(readPixel(x, y)) {
                continue
            }

            let component = collectComponent(startX: x, startY: y)
            if component.count >= minimumBackgroundComponentArea {
                for (componentX, componentY) in component {
                    clear(componentX, componentY)
                }
            }
        }
    }
}

func resize(_ source: NSBitmapImageRep, width: Int, height: Int, fitPreservingAspect: Bool) -> NSBitmapImageRep {
    let sourceImage = image(from: source)
    let target = NSImage(size: NSSize(width: width, height: height))
    target.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: width, height: height).fill()
    NSGraphicsContext.current?.imageInterpolation = .high

    let drawRect: NSRect
    if fitPreservingAspect {
        let scale = min(CGFloat(width) / CGFloat(source.pixelsWide), CGFloat(height) / CGFloat(source.pixelsHigh))
        let drawWidth = CGFloat(source.pixelsWide) * scale
        let drawHeight = CGFloat(source.pixelsHigh) * scale
        drawRect = NSRect(
            x: (CGFloat(width) - drawWidth) * 0.5,
            y: (CGFloat(height) - drawHeight) * 0.5,
            width: drawWidth,
            height: drawHeight)
    } else {
        drawRect = NSRect(x: 0, y: 0, width: width, height: height)
    }

    sourceImage.draw(
        in: drawRect,
        from: NSRect(x: 0, y: 0, width: source.pixelsWide, height: source.pixelsHigh),
        operation: .copy,
        fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "import_custom_rarity_frames", code: 2, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

let args = CommandLine.arguments
guard args.count == 3 else {
    fputs("usage: import_custom_rarity_frames.swift <source-folder> <unity-project-root>\n", stderr)
    exit(1)
}

let sourceRoot = args[1]
let outputRoot = "\(args[2])/Assets/Resources/MonsterCardFrames"
try FileManager.default.createDirectory(atPath: outputRoot, withIntermediateDirectories: true)

for rank in 1...6 {
    let sourcePath = "\(sourceRoot)/クラス\(rank).png"
    let fullWidthSourcePath = "\(sourceRoot)/クラス\(String(rank).applyingTransform(.fullwidthToHalfwidth, reverse: true) ?? String(rank)).png"
    let path = FileManager.default.fileExists(atPath: sourcePath) ? sourcePath : fullWidthSourcePath
    let rep = bitmap(from: try loadImage(path))
    clearCheckerboardBackground(rep)

    try save(resize(rep, width: 768, height: 1024, fitPreservingAspect: false), to: "\(outputRoot)/monster_class_\(rank)_card_frame.png")
    try save(resize(rep, width: 768, height: 768, fitPreservingAspect: true), to: "\(outputRoot)/monster_class_\(rank)_slot_frame.png")
}

print("Imported custom rarity frames from \(sourceRoot).")
