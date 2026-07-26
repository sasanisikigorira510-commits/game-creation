import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

struct SpiritSheetJob {
    let source: String
    let output: String
}

let frameCount = 4
let outputFrameWidth = 128
let outputFrameHeight = 128
let outputWidth = outputFrameWidth * frameCount
let outputHeight = outputFrameHeight
let outputPadding = 6

let jobs = [
    SpiritSheetJob(
        source: "/Users/andou/.codex/generated_images/019f41c8-af2f-7bc1-ad74-25efd79234c3/ig_0e8be8085107196c016a4f257e2148819187cc7ab18b0f9199.png",
        output: "Assets/Resources/UI/BattleSpirit/Animation/SpiritGenbuSummon128SheetImage2.png"
    ),
    SpiritSheetJob(
        source: "/Users/andou/.codex/generated_images/019f41c8-af2f-7bc1-ad74-25efd79234c3/ig_0e8be8085107196c016a4f25c5ee1c819188fad87e37b5157c.png",
        output: "Assets/Resources/UI/BattleSpirit/Animation/SpiritGenbuIdle128SheetImage2.png"
    ),
    SpiritSheetJob(
        source: "/Users/andou/.codex/generated_images/019f41c8-af2f-7bc1-ad74-25efd79234c3/ig_0e8be8085107196c016a4f26099be88191b10f8517abcb4abd.png",
        output: "Assets/Resources/UI/BattleSpirit/Animation/SpiritSeiryuSummon128SheetImage2.png"
    ),
    SpiritSheetJob(
        source: "/Users/andou/.codex/generated_images/019f41c8-af2f-7bc1-ad74-25efd79234c3/ig_0e8be8085107196c016a4f265300d88191aed3ea21413a1f36.png",
        output: "Assets/Resources/UI/BattleSpirit/Animation/SpiritSeiryuIdle128SheetImage2.png"
    )
]

func clamp(_ value: Int, _ minimum: Int = 0, _ maximum: Int = 255) -> UInt8 {
    UInt8(Swift.max(minimum, Swift.min(maximum, value)))
}

func quantize(_ value: UInt8, step: Int) -> UInt8 {
    let rounded = Int((Double(value) / Double(step)).rounded()) * step
    return clamp(rounded)
}

func loadRGBA(path: String) throws -> (pixels: [UInt8], width: Int, height: Int) {
    let url = URL(fileURLWithPath: path)
    guard
        let source = CGImageSourceCreateWithURL(url as CFURL, nil),
        let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
    else {
        throw NSError(domain: "SpiritSheet", code: 1, userInfo: [NSLocalizedDescriptionKey: "Failed to load image: \(path)"])
    }

    let width = image.width
    let height = image.height
    var pixels = Array(repeating: UInt8(0), count: width * height * 4)
    let colorSpace = CGColorSpaceCreateDeviceRGB()
    let bitmapInfo = CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue)

    guard let context = CGContext(
        data: &pixels,
        width: width,
        height: height,
        bitsPerComponent: 8,
        bytesPerRow: width * 4,
        space: colorSpace,
        bitmapInfo: bitmapInfo.rawValue
    ) else {
        throw NSError(domain: "SpiritSheet", code: 2, userInfo: [NSLocalizedDescriptionKey: "Failed to create bitmap context"])
    }

    context.interpolationQuality = .none
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
    return (pixels, width, height)
}

func sourcePixel(_ source: [UInt8], width: Int, height: Int, x: Int, y: Int) -> (r: UInt8, g: UInt8, b: UInt8, a: UInt8) {
    let safeX = Swift.max(0, Swift.min(width - 1, x))
    let safeY = Swift.max(0, Swift.min(height - 1, y))
    let index = (safeY * width + safeX) * 4
    return (source[index], source[index + 1], source[index + 2], source[index + 3])
}

func alphaForChromaKey(r: UInt8, g: UInt8, b: UInt8) -> UInt8 {
    let red = Int(r)
    let green = Int(g)
    let blue = Int(b)
    let directDistance = abs(red - 255) + abs(green - 0) + abs(blue - 255)
    let veryMagenta = red > 205 && blue > 190 && green < 95
    let brightMagenta = red > 170 && blue > 165 && green < 70 && directDistance < 190

    if veryMagenta || brightMagenta {
        return 0
    }

    if red > 150 && blue > 150 && green < 120 {
        let distance = Double(directDistance)
        let alpha = Int(((distance - 145.0) / 135.0) * 255.0)
        return clamp(alpha, 0, 255)
    }

    return 220
}

struct CropRect {
    var minX: Int
    var minY: Int
    var maxX: Int
    var maxY: Int

    var width: Int {
        maxX - minX + 1
    }

    var height: Int {
        maxY - minY + 1
    }
}

func detectContentCrop(source: [UInt8], sourceWidth: Int, sourceHeight: Int, sourceFrameWidth: Int) -> CropRect {
    var crop = CropRect(minX: sourceFrameWidth, minY: sourceHeight, maxX: 0, maxY: 0)

    for frame in 0..<frameCount {
        let frameOffsetX = frame * sourceFrameWidth
        for y in 0..<sourceHeight {
            for localX in 0..<sourceFrameWidth {
                let sample = sourcePixel(source, width: sourceWidth, height: sourceHeight, x: frameOffsetX + localX, y: y)
                if alphaForChromaKey(r: sample.r, g: sample.g, b: sample.b) == 0 {
                    continue
                }

                crop.minX = Swift.min(crop.minX, localX)
                crop.minY = Swift.min(crop.minY, y)
                crop.maxX = Swift.max(crop.maxX, localX)
                crop.maxY = Swift.max(crop.maxY, y)
            }
        }
    }

    if crop.minX > crop.maxX || crop.minY > crop.maxY {
        return CropRect(minX: 0, minY: 0, maxX: sourceFrameWidth - 1, maxY: sourceHeight - 1)
    }

    let marginX = Swift.max(6, crop.width / 16)
    let marginY = Swift.max(6, crop.height / 16)
    crop.minX = Swift.max(0, crop.minX - marginX)
    crop.minY = Swift.max(0, crop.minY - marginY)
    crop.maxX = Swift.min(sourceFrameWidth - 1, crop.maxX + marginX)
    crop.maxY = Swift.min(sourceHeight - 1, crop.maxY + marginY)
    return crop
}

func process(job: SpiritSheetJob) throws {
    let loaded = try loadRGBA(path: job.source)
    let source = loaded.pixels
    let sourceWidth = loaded.width
    let sourceHeight = loaded.height
    let sourceFrameWidth = sourceWidth / frameCount
    let crop = detectContentCrop(
        source: source,
        sourceWidth: sourceWidth,
        sourceHeight: sourceHeight,
        sourceFrameWidth: sourceFrameWidth)
    let maxDrawWidth = outputFrameWidth - outputPadding * 2
    let maxDrawHeight = outputFrameHeight - outputPadding * 2
    let scale = Swift.min(Double(maxDrawWidth) / Double(crop.width), Double(maxDrawHeight) / Double(crop.height))
    let drawWidth = Swift.max(1, Int((Double(crop.width) * scale).rounded()))
    let drawHeight = Swift.max(1, Int((Double(crop.height) * scale).rounded()))
    let drawOffsetX = (outputFrameWidth - drawWidth) / 2
    let drawOffsetY = (outputFrameHeight - drawHeight) / 2
    var output = Array(repeating: UInt8(0), count: outputWidth * outputHeight * 4)

    for frame in 0..<frameCount {
        for y in drawOffsetY..<(drawOffsetY + drawHeight) {
            for x in drawOffsetX..<(drawOffsetX + drawWidth) {
                let sourceX = frame * sourceFrameWidth + crop.minX + Int((Double(x - drawOffsetX) + 0.5) / scale)
                let sourceY = crop.minY + Int((Double(y - drawOffsetY) + 0.5) / scale)
                let sample = sourcePixel(source, width: sourceWidth, height: sourceHeight, x: sourceX, y: sourceY)
                var alpha = alphaForChromaKey(r: sample.r, g: sample.g, b: sample.b)

                if alpha > 0 && alpha < 56 {
                    alpha = 0
                }

                let outX = frame * outputFrameWidth + x
                let outIndex = (y * outputWidth + outX) * 4
                if alpha == 0 {
                    output[outIndex] = 0
                    output[outIndex + 1] = 0
                    output[outIndex + 2] = 0
                    output[outIndex + 3] = 0
                    continue
                }

                output[outIndex] = quantize(sample.r, step: 36)
                output[outIndex + 1] = quantize(sample.g, step: 36)
                output[outIndex + 2] = quantize(sample.b, step: 36)
                output[outIndex + 3] = alpha
            }
        }
    }

    let outputURL = URL(fileURLWithPath: job.output)
    try FileManager.default.createDirectory(at: outputURL.deletingLastPathComponent(), withIntermediateDirectories: true)

    let data = Data(output)
    guard let provider = CGDataProvider(data: data as CFData) else {
        throw NSError(domain: "SpiritSheet", code: 3, userInfo: [NSLocalizedDescriptionKey: "Failed to create data provider"])
    }

    let colorSpace = CGColorSpaceCreateDeviceRGB()
    let bitmapInfo = CGBitmapInfo(rawValue: CGImageAlphaInfo.last.rawValue)
    guard let image = CGImage(
        width: outputWidth,
        height: outputHeight,
        bitsPerComponent: 8,
        bitsPerPixel: 32,
        bytesPerRow: outputWidth * 4,
        space: colorSpace,
        bitmapInfo: bitmapInfo,
        provider: provider,
        decode: nil,
        shouldInterpolate: false,
        intent: .defaultIntent
    ) else {
        throw NSError(domain: "SpiritSheet", code: 4, userInfo: [NSLocalizedDescriptionKey: "Failed to create output image"])
    }

    guard
        let destination = CGImageDestinationCreateWithURL(outputURL as CFURL, UTType.png.identifier as CFString, 1, nil)
    else {
        throw NSError(domain: "SpiritSheet", code: 5, userInfo: [NSLocalizedDescriptionKey: "Failed to create output destination"])
    }

    CGImageDestinationAddImage(destination, image, nil)
    if !CGImageDestinationFinalize(destination) {
        throw NSError(domain: "SpiritSheet", code: 6, userInfo: [NSLocalizedDescriptionKey: "Failed to write output image"])
    }

    print("Wrote \(job.output)")
}

do {
    for job in jobs {
        try process(job: job)
    }
} catch {
    fputs("Error: \(error.localizedDescription)\n", stderr)
    exit(1)
}
