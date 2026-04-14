import AppKit
import Foundation
import ImageIO
import UniformTypeIdentifiers

struct SpriteJob {
    let inputPath: String
    let outputPath: String
}

struct RGBA {
    var r: UInt8
    var g: UInt8
    var b: UInt8
    var a: UInt8
}

@inline(__always)
func colorDistanceSquared(_ lhs: RGBA, _ rhs: RGBA) -> Int {
    let dr = Int(lhs.r) - Int(rhs.r)
    let dg = Int(lhs.g) - Int(rhs.g)
    let db = Int(lhs.b) - Int(rhs.b)
    return dr * dr + dg * dg + db * db
}

func loadPixels(from path: String) throws -> (pixels: [RGBA], width: Int, height: Int) {
    let url = URL(fileURLWithPath: path)
    guard
        let source = CGImageSourceCreateWithURL(url as CFURL, nil),
        let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
    else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 1, userInfo: [NSLocalizedDescriptionKey: "Failed to load image at \(path)"])
    }

    let width = image.width
    let height = image.height
    let bytesPerPixel = 4
    let bytesPerRow = width * bytesPerPixel
    var raw = [UInt8](repeating: 0, count: width * height * bytesPerPixel)

    guard let context = CGContext(
        data: &raw,
        width: width,
        height: height,
        bitsPerComponent: 8,
        bytesPerRow: bytesPerRow,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
    ) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 2, userInfo: [NSLocalizedDescriptionKey: "Failed to create bitmap context for \(path)"])
    }

    context.interpolationQuality = .none
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))

    var pixels = [RGBA]()
    pixels.reserveCapacity(width * height)
    for index in stride(from: 0, to: raw.count, by: 4) {
        pixels.append(RGBA(r: raw[index], g: raw[index + 1], b: raw[index + 2], a: raw[index + 3]))
    }

    return (pixels, width, height)
}

func averageEdgeColor(pixels: [RGBA], width: Int, height: Int) -> RGBA {
    var totalR = 0
    var totalG = 0
    var totalB = 0
    var count = 0

    func accumulate(_ x: Int, _ y: Int) {
        let pixel = pixels[(y * width) + x]
        totalR += Int(pixel.r)
        totalG += Int(pixel.g)
        totalB += Int(pixel.b)
        count += 1
    }

    for x in 0..<width {
        accumulate(x, 0)
        accumulate(x, height - 1)
    }

    if height > 2 {
        for y in 1..<(height - 1) {
            accumulate(0, y)
            accumulate(width - 1, y)
        }
    }

    return RGBA(
        r: UInt8(totalR / max(count, 1)),
        g: UInt8(totalG / max(count, 1)),
        b: UInt8(totalB / max(count, 1)),
        a: 255
    )
}

func knockOutBackground(pixels: inout [RGBA], width: Int, height: Int) {
    let background = averageEdgeColor(pixels: pixels, width: width, height: height)
    let threshold = 55 * 55
    var queue = [(Int, Int)]()
    var head = 0
    var visited = [Bool](repeating: false, count: width * height)

    func enqueue(_ x: Int, _ y: Int) {
        let index = (y * width) + x
        if visited[index] {
            return
        }

        let pixel = pixels[index]
        if pixel.a < 8 || colorDistanceSquared(pixel, background) <= threshold {
            visited[index] = true
            queue.append((x, y))
        }
    }

    for x in 0..<width {
        enqueue(x, 0)
        enqueue(x, height - 1)
    }

    if height > 2 {
        for y in 1..<(height - 1) {
            enqueue(0, y)
            enqueue(width - 1, y)
        }
    }

    let directions = [(1, 0), (-1, 0), (0, 1), (0, -1)]

    while head < queue.count {
        let (x, y) = queue[head]
        head += 1
        let index = (y * width) + x
        pixels[index].a = 0

        for (dx, dy) in directions {
            let nx = x + dx
            let ny = y + dy
            guard nx >= 0, ny >= 0, nx < width, ny < height else {
                continue
            }

            let neighborIndex = (ny * width) + nx
            if visited[neighborIndex] {
                continue
            }

            let neighbor = pixels[neighborIndex]
            if neighbor.a < 8 || colorDistanceSquared(neighbor, background) <= threshold {
                visited[neighborIndex] = true
                queue.append((nx, ny))
            }
        }
    }
}

func cropBounds(for pixels: [RGBA], width: Int, height: Int) -> CGRect? {
    var minX = width
    var maxX = -1
    var minY = height
    var maxY = -1

    for y in 0..<height {
        for x in 0..<width {
            if pixels[(y * width) + x].a > 8 {
                minX = min(minX, x)
                maxX = max(maxX, x)
                minY = min(minY, y)
                maxY = max(maxY, y)
            }
        }
    }

    guard maxX >= minX, maxY >= minY else {
        return nil
    }

    return CGRect(x: minX, y: minY, width: (maxX - minX) + 1, height: (maxY - minY) + 1)
}

func makeCGImage(from pixels: [RGBA], width: Int, height: Int) throws -> CGImage {
    let bytesPerPixel = 4
    let bytesPerRow = width * bytesPerPixel
    let data = Data(bytes: pixels, count: pixels.count * MemoryLayout<RGBA>.size)
    guard let provider = CGDataProvider(data: data as CFData) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 3, userInfo: [NSLocalizedDescriptionKey: "Failed to create data provider"])
    }

    guard let image = CGImage(
        width: width,
        height: height,
        bitsPerComponent: 8,
        bitsPerPixel: 32,
        bytesPerRow: bytesPerRow,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue),
        provider: provider,
        decode: nil,
        shouldInterpolate: false,
        intent: .defaultIntent
    ) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 4, userInfo: [NSLocalizedDescriptionKey: "Failed to create CGImage"])
    }

    return image
}

func savePreparedSprite(inputPath: String, outputPath: String) throws {
    var (pixels, width, height) = try loadPixels(from: inputPath)
    knockOutBackground(pixels: &pixels, width: width, height: height)

    guard let bounds = cropBounds(for: pixels, width: width, height: height) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 5, userInfo: [NSLocalizedDescriptionKey: "Image became empty after background knockout: \(inputPath)"])
    }

    let source = try makeCGImage(from: pixels, width: width, height: height)
    guard let cropped = source.cropping(to: bounds) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 6, userInfo: [NSLocalizedDescriptionKey: "Failed to crop image: \(inputPath)"])
    }

    let canvasSize = 512
    let targetInset = 384
    let scale = min(Double(targetInset) / Double(cropped.width), Double(targetInset) / Double(cropped.height))
    let scaledWidth = max(Int(round(Double(cropped.width) * scale)), 1)
    let scaledHeight = max(Int(round(Double(cropped.height) * scale)), 1)
    let drawX = (canvasSize - scaledWidth) / 2
    let drawY = 32

    guard let context = CGContext(
        data: nil,
        width: canvasSize,
        height: canvasSize,
        bitsPerComponent: 8,
        bytesPerRow: canvasSize * 4,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
    ) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 7, userInfo: [NSLocalizedDescriptionKey: "Failed to create output context"])
    }

    context.clear(CGRect(x: 0, y: 0, width: canvasSize, height: canvasSize))
    context.interpolationQuality = .none
    context.draw(cropped, in: CGRect(x: drawX, y: drawY, width: scaledWidth, height: scaledHeight))

    guard let outputImage = context.makeImage() else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 8, userInfo: [NSLocalizedDescriptionKey: "Failed to render output image"])
    }

    let outputURL = URL(fileURLWithPath: outputPath)
    try FileManager.default.createDirectory(at: outputURL.deletingLastPathComponent(), withIntermediateDirectories: true)

    guard let destination = CGImageDestinationCreateWithURL(outputURL as CFURL, UTType.png.identifier as CFString, 1, nil) else {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 9, userInfo: [NSLocalizedDescriptionKey: "Failed to create image destination"])
    }

    CGImageDestinationAddImage(destination, outputImage, nil)
    if !CGImageDestinationFinalize(destination) {
        throw NSError(domain: "PrepareFamilyBattleSprites", code: 10, userInfo: [NSLocalizedDescriptionKey: "Failed to save image to \(outputPath)"])
    }
}

let arguments = CommandLine.arguments.dropFirst()
guard arguments.count.isMultiple(of: 2), !arguments.isEmpty else {
    fputs("Usage: prepare_family_battle_sprites.swift <input> <output> [<input> <output> ...]\n", stderr)
    exit(1)
}

for pairStart in stride(from: 0, to: arguments.count, by: 2) {
    let input = arguments[arguments.index(arguments.startIndex, offsetBy: pairStart)]
    let output = arguments[arguments.index(arguments.startIndex, offsetBy: pairStart + 1)]
    try savePreparedSprite(inputPath: String(input), outputPath: String(output))
    print("Wrote \(output)")
}
