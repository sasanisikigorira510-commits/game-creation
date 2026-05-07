import AppKit
import Foundation

struct Pixel {
    let r: UInt8
    let g: UInt8
    let b: UInt8
    let a: UInt8
}

struct IntRect {
    let x: Int
    let y: Int
    let width: Int
    let height: Int
    var maxX: Int { x + width }
    var maxY: Int { y + height }
}

func bitmap(from image: NSImage, rect: NSRect) -> NSBitmapImageRep {
    let target = NSImage(size: rect.size)
    target.lockFocus()
    NSColor.clear.set()
    NSRect(origin: .zero, size: rect.size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    image.draw(in: NSRect(origin: .zero, size: rect.size), from: rect, operation: .copy, fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func image(from rep: NSBitmapImageRep) -> NSImage {
    let source = NSImage(size: NSSize(width: rep.pixelsWide, height: rep.pixelsHigh))
    source.addRepresentation(rep)
    return source
}

func readPixel(in rep: NSBitmapImageRep, data: UnsafeMutablePointer<UInt8>, x: Int, y: Int) -> Pixel {
    let bytesPerPixel = rep.bitsPerPixel / 8
    let offset = y * rep.bytesPerRow + x * bytesPerPixel
    return Pixel(r: data[offset], g: data[offset + 1], b: data[offset + 2], a: data[offset + 3])
}

func isBackgroundCandidate(_ pixel: Pixel) -> Bool {
    if pixel.a == 0 {
        return false
    }

    let minRGB = min(pixel.r, min(pixel.g, pixel.b))
    let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
    return minRGB >= 180 && (maxRGB - minRGB) <= 72
}

func clearPixel(in rep: NSBitmapImageRep, data: UnsafeMutablePointer<UInt8>, x: Int, y: Int) {
    let bytesPerPixel = rep.bitsPerPixel / 8
    let offset = y * rep.bytesPerRow + x * bytesPerPixel
    data[offset] = 0
    data[offset + 1] = 0
    data[offset + 2] = 0
    data[offset + 3] = 0
}

func clearEdgeBackground(_ rep: NSBitmapImageRep) {
    guard let data = rep.bitmapData else {
        return
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    var visited = Array(repeating: false, count: width * height)
    var queue: [(Int, Int)] = []

    func enqueue(_ x: Int, _ y: Int) {
        if x < 0 || y < 0 || x >= width || y >= height {
            return
        }

        let index = y * width + x
        if visited[index] {
            return
        }

        if isBackgroundCandidate(readPixel(in: rep, data: data, x: x, y: y)) {
            visited[index] = true
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
        clearPixel(in: rep, data: data, x: x, y: y)
        for neighborY in max(0, y - 1)...min(height - 1, y + 1) {
            for neighborX in max(0, x - 1)...min(width - 1, x + 1) {
                enqueue(neighborX, neighborY)
            }
        }
    }

    func touchesTransparent(_ x: Int, _ y: Int, radius: Int) -> Bool {
        for neighborY in max(0, y - radius)...min(height - 1, y + radius) {
            for neighborX in max(0, x - radius)...min(width - 1, x + radius) {
                if neighborX == x && neighborY == y {
                    continue
                }

                if readPixel(in: rep, data: data, x: neighborX, y: neighborY).a <= 8 {
                    return true
                }
            }
        }

        return false
    }

    func isDetachedBackgroundCandidate(_ pixel: Pixel) -> Bool {
        if pixel.a <= 8 {
            return false
        }

        let minRGB = min(pixel.r, min(pixel.g, pixel.b))
        let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
        let saturation = maxRGB - minRGB
        return (minRGB >= 220 && saturation <= 52) || (minRGB >= 205 && saturation <= 22)
    }

    func collectDetachedComponent(startX: Int, startY: Int, visited componentVisited: inout [Bool]) -> (pixels: [(Int, Int)], touchesTransparent: Bool, averageMinRGB: Int, averageSaturation: Int) {
        var component: [(Int, Int)] = []
        var queue: [(Int, Int)] = [(startX, startY)]
        componentVisited[startY * width + startX] = true
        var index = 0
        var sumMinRGB = 0
        var sumSaturation = 0
        var componentTouchesTransparent = false

        while index < queue.count {
            let (x, y) = queue[index]
            index += 1
            component.append((x, y))

            let pixel = readPixel(in: rep, data: data, x: x, y: y)
            let minRGB = Int(min(pixel.r, min(pixel.g, pixel.b)))
            let maxRGB = Int(max(pixel.r, max(pixel.g, pixel.b)))
            sumMinRGB += minRGB
            sumSaturation += maxRGB - minRGB
            componentTouchesTransparent = componentTouchesTransparent || touchesTransparent(x, y, radius: 2)

            for neighborY in max(0, y - 1)...min(height - 1, y + 1) {
                for neighborX in max(0, x - 1)...min(width - 1, x + 1) {
                    if neighborX == x && neighborY == y {
                        continue
                    }

                    let neighborIndex = neighborY * width + neighborX
                    if componentVisited[neighborIndex] || !isDetachedBackgroundCandidate(readPixel(in: rep, data: data, x: neighborX, y: neighborY)) {
                        continue
                    }

                    componentVisited[neighborIndex] = true
                    queue.append((neighborX, neighborY))
                }
            }
        }

        let count = max(1, component.count)
        return (
            pixels: component,
            touchesTransparent: componentTouchesTransparent,
            averageMinRGB: sumMinRGB / count,
            averageSaturation: sumSaturation / count)
    }

    var componentVisited = visited
    let minimumDetachedArea = max(12, (width * height) / 20_000)
    for y in 0..<height {
        for x in 0..<width {
            let componentIndex = y * width + x
            if componentVisited[componentIndex] || !isDetachedBackgroundCandidate(readPixel(in: rep, data: data, x: x, y: y)) {
                continue
            }

            let component = collectDetachedComponent(startX: x, startY: y, visited: &componentVisited)
            let clearlyWhiteIsland = component.pixels.count >= minimumDetachedArea &&
                component.averageMinRGB >= 238 &&
                component.averageSaturation <= 32
            let checkerboardIsland = component.pixels.count >= minimumDetachedArea &&
                component.averageMinRGB >= 214 &&
                component.averageSaturation <= 14

            if component.touchesTransparent || clearlyWhiteIsland || checkerboardIsland {
                for (componentX, componentY) in component.pixels {
                    clearPixel(in: rep, data: data, x: componentX, y: componentY)
                }
            }
        }
    }
}

func removeSmallDetachedComponents(_ rep: NSBitmapImageRep, alphaThreshold: UInt8 = 12, minimumPixelsToKeep: Int = 420) {
    guard let data = rep.bitmapData else {
        return
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    var visited = Array(repeating: false, count: width * height)

    func isVisible(_ x: Int, _ y: Int) -> Bool {
        let offset = y * rep.bytesPerRow + x * bytesPerPixel
        return data[offset + 3] > alphaThreshold
    }

    for y in 0..<height {
        for x in 0..<width {
            let startIndex = y * width + x
            if visited[startIndex] || !isVisible(x, y) {
                continue
            }

            var component: [(Int, Int)] = []
            var queue: [(Int, Int)] = [(x, y)]
            visited[startIndex] = true
            var queueIndex = 0

            while queueIndex < queue.count {
                let (currentX, currentY) = queue[queueIndex]
                queueIndex += 1
                component.append((currentX, currentY))

                for neighborY in max(0, currentY - 1)...min(height - 1, currentY + 1) {
                    for neighborX in max(0, currentX - 1)...min(width - 1, currentX + 1) {
                        if neighborX == currentX && neighborY == currentY {
                            continue
                        }

                        let neighborIndex = neighborY * width + neighborX
                        if visited[neighborIndex] || !isVisible(neighborX, neighborY) {
                            continue
                        }

                        visited[neighborIndex] = true
                        queue.append((neighborX, neighborY))
                    }
                }
            }

            if component.count < minimumPixelsToKeep {
                for (componentX, componentY) in component {
                    clearPixel(in: rep, data: data, x: componentX, y: componentY)
                }
            }
        }
    }
}

func columnHasContent(in rep: NSBitmapImageRep, minimumPixels: Int) -> [Bool] {
    guard let data = rep.bitmapData else {
        return []
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    var result = Array(repeating: false, count: width)

    for x in 0..<width {
        var count = 0
        for y in 0..<height {
            let offset = y * rep.bytesPerRow + x * bytesPerPixel
            if data[offset + 3] > 12 {
                count += 1
                if count >= minimumPixels {
                    result[x] = true
                    break
                }
            }
        }
    }

    return result
}

func bridgedColumns(_ columns: [Bool], maximumGap: Int) -> [Bool] {
    var bridged = columns
    var x = 0
    while x < bridged.count {
        if bridged[x] {
            x += 1
            continue
        }

        let start = x
        while x < bridged.count && !bridged[x] {
            x += 1
        }

        if start > 0 && x < bridged.count && x - start <= maximumGap {
            for index in start..<x {
                bridged[index] = true
            }
        }
    }

    return bridged
}

func spans(from columns: [Bool], minimumWidth: Int) -> [Range<Int>] {
    var result: [Range<Int>] = []
    var x = 0
    while x < columns.count {
        while x < columns.count && !columns[x] {
            x += 1
        }

        let start = x
        while x < columns.count && columns[x] {
            x += 1
        }

        if x > start && x - start >= minimumWidth {
            result.append(start..<x)
        }
    }

    return result
}

func contentBounds(in rep: NSBitmapImageRep, xRange: Range<Int>) -> IntRect? {
    guard let data = rep.bitmapData else {
        return nil
    }

    let bytesPerPixel = rep.bitsPerPixel / 8
    var minX = rep.pixelsWide
    var minY = rep.pixelsHigh
    var maxX = -1
    var maxY = -1

    for y in 0..<rep.pixelsHigh {
        for x in xRange {
            let offset = y * rep.bytesPerRow + x * bytesPerPixel
            if data[offset + 3] <= 12 {
                continue
            }

            minX = min(minX, x)
            minY = min(minY, y)
            maxX = max(maxX, x)
            maxY = max(maxY, y)
        }
    }

    guard maxX >= minX && maxY >= minY else {
        return nil
    }

    return IntRect(x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1)
}

func crop(_ rep: NSBitmapImageRep, rect: IntRect) -> NSBitmapImageRep {
    let source = image(from: rep)
    let target = NSImage(size: NSSize(width: rect.width, height: rect.height))
    target.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: rect.width, height: rect.height).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    source.draw(
        in: NSRect(x: 0, y: 0, width: rect.width, height: rect.height),
        from: NSRect(x: rect.x, y: rect.y, width: rect.width, height: rect.height),
        operation: .copy,
        fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func pad(_ rep: NSBitmapImageRep, size: Int) -> NSBitmapImageRep {
    let source = image(from: rep)
    let target = NSImage(size: NSSize(width: size, height: size))
    target.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: size, height: size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    let drawX = (size - rep.pixelsWide) / 2
    let drawY = (size - rep.pixelsHigh) / 2
    source.draw(
        in: NSRect(x: drawX, y: drawY, width: rep.pixelsWide, height: rep.pixelsHigh),
        from: NSRect(x: 0, y: 0, width: rep.pixelsWide, height: rep.pixelsHigh),
        operation: .copy,
        fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "fix_apprentice_swordsman_idle", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

func ensureMeta(for path: String, templatePath: String) throws {
    let metaPath = "\(path).meta"
    if FileManager.default.fileExists(atPath: metaPath) {
        return
    }

    var text = try String(contentsOfFile: templatePath, encoding: .utf8)
    let guid = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
    text = text.replacingOccurrences(of: #"guid: [0-9a-f]+"#, with: "guid: \(guid)", options: .regularExpression)
    try text.write(toFile: metaPath, atomically: true, encoding: .utf8)
}

let sourcePath = "/Users/andou/Desktop/あ/モンスター一覧/剣士/剣士１/待機.png"
let outputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/MonsterBattle"
let outputPrefix = "\(outputRoot)/mon_apprentice_swordsman_idle"
let metaTemplate = "\(outputRoot)/mon_apprentice_swordsman_move_0.png.meta"

guard let sourceImage = NSImage(contentsOfFile: sourcePath) else {
    throw NSError(domain: "fix_apprentice_swordsman_idle", code: 2, userInfo: [NSLocalizedDescriptionKey: "failed to load \(sourcePath)"])
}

let sourceWidth = Int(sourceImage.size.width.rounded())
let sourceHeight = Int(sourceImage.size.height.rounded())
let sheet = bitmap(from: sourceImage, rect: NSRect(x: 0, y: 0, width: sourceWidth, height: sourceHeight))
clearEdgeBackground(sheet)

let columns = bridgedColumns(columnHasContent(in: sheet, minimumPixels: max(3, sourceHeight / 256)), maximumGap: 64)
let detectedSpans = spans(from: columns, minimumWidth: max(36, sourceWidth / 48))
let frameSpans: [Range<Int>]
if detectedSpans.count == 4 {
    frameSpans = detectedSpans
} else if sourceWidth == 1774 && sourceHeight == 887 {
    frameSpans = [
        0..<455,
        455..<887,
        887..<1260,
        1260..<1774
    ]
} else {
    // The source sheet is visually laid out as four equal slots even when adjacent
    // poses are close enough that column detection merges them into two groups.
    frameSpans = (0..<4).map { index in
        let start = Int((Double(sourceWidth) * Double(index) / 4.0).rounded())
        let end = Int((Double(sourceWidth) * Double(index + 1) / 4.0).rounded())
        return start..<end
    }
}

for index in 0..<16 {
    let path = "\(outputPrefix)_\(index).png"
    if FileManager.default.fileExists(atPath: path) {
        try FileManager.default.removeItem(atPath: path)
    }
}

for (index, span) in frameSpans.enumerated() {
    guard let bounds = contentBounds(in: sheet, xRange: span) else {
        continue
    }

    let padding = 36
    let x = max(0, bounds.x - padding)
    let y = max(0, bounds.y - padding)
    let maxX = min(sourceWidth, bounds.maxX + padding)
    let maxY = min(sourceHeight, bounds.maxY + padding)
    let cropRect = IntRect(x: x, y: y, width: maxX - x, height: maxY - y)
    let cropped = crop(sheet, rect: cropRect)
    clearEdgeBackground(cropped)
    removeSmallDetachedComponents(cropped)
    let frameSize = max(627, max(cropped.pixelsWide, cropped.pixelsHigh))
    let framed = pad(cropped, size: frameSize)
    clearEdgeBackground(framed)
    removeSmallDetachedComponents(framed)
    let outputPath = "\(outputPrefix)_\(index).png"
    try save(framed, to: outputPath)
    try ensureMeta(for: outputPath, templatePath: metaTemplate)
}

print("Fixed apprentice swordsman idle frames.")
