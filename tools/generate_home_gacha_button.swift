import AppKit
import CoreGraphics
import Foundation
import ImageIO

let projectRoot = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let sourceURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeMenu/FusionButton.png")
let outputURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeMenu/GachaButton.png")

func rgbToHsv(_ r: Double, _ g: Double, _ b: Double) -> (h: Double, s: Double, v: Double) {
    let maxValue = max(r, max(g, b))
    let minValue = min(r, min(g, b))
    let delta = maxValue - minValue
    var hue = 0.0

    if delta > 0.0001 {
        if maxValue == r {
            hue = ((g - b) / delta).truncatingRemainder(dividingBy: 6.0)
        } else if maxValue == g {
            hue = ((b - r) / delta) + 2.0
        } else {
            hue = ((r - g) / delta) + 4.0
        }

        hue /= 6.0
        if hue < 0.0 {
            hue += 1.0
        }
    }

    let saturation = maxValue <= 0.0001 ? 0.0 : delta / maxValue
    return (hue, saturation, maxValue)
}

func hsvToRgb(_ h: Double, _ s: Double, _ v: Double) -> (r: Double, g: Double, b: Double) {
    if s <= 0.0001 {
        return (v, v, v)
    }

    let scaledHue = h * 6.0
    let sector = floor(scaledHue)
    let fraction = scaledHue - sector
    let p = v * (1.0 - s)
    let q = v * (1.0 - s * fraction)
    let t = v * (1.0 - s * (1.0 - fraction))

    switch Int(sector) % 6 {
    case 0:
        return (v, t, p)
    case 1:
        return (q, v, p)
    case 2:
        return (p, v, t)
    case 3:
        return (p, q, v)
    case 4:
        return (t, p, v)
    default:
        return (v, p, q)
    }
}

func clamp(_ value: Double) -> Double {
    min(1.0, max(0.0, value))
}

guard
    let imageSource = CGImageSourceCreateWithURL(sourceURL as CFURL, nil),
    let sourceImage = CGImageSourceCreateImageAtIndex(imageSource, 0, nil)
else {
    fatalError("Failed to load source image at \(sourceURL.path)")
}

let width = sourceImage.width
let height = sourceImage.height
let colorSpace = CGColorSpaceCreateDeviceRGB()
let bytesPerRow = width * 4
var pixels = [UInt8](repeating: 0, count: bytesPerRow * height)

guard let bitmapContext = CGContext(
    data: &pixels,
    width: width,
    height: height,
    bitsPerComponent: 8,
    bytesPerRow: bytesPerRow,
    space: colorSpace,
    bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
) else {
    fatalError("Failed to create bitmap context")
}

bitmapContext.clear(CGRect(x: 0, y: 0, width: width, height: height))
bitmapContext.draw(sourceImage, in: CGRect(x: 0, y: 0, width: width, height: height))

for y in 0..<height {
    for x in 0..<width {
        let offset = (y * width + x) * 4
        let alpha = Double(pixels[offset + 3]) / 255.0
        if alpha <= 0.01 {
            continue
        }

        var red = clamp(Double(pixels[offset]) / 255.0 / alpha)
        var green = clamp(Double(pixels[offset + 1]) / 255.0 / alpha)
        var blue = clamp(Double(pixels[offset + 2]) / 255.0 / alpha)
        let hsv = rgbToHsv(red, green, blue)
        let isGreenMagic = hsv.s > 0.16 && green > red * 0.9 && green > blue * 0.9

        if isGreenMagic {
            let xNorm = Double(x) / Double(width)
            let yNorm = Double(y) / Double(height)
            let centerWeight = 1.0 - min(1.0, abs(xNorm - 0.5) * 2.1)
            let edgeGemWeight = max(0.0, 1.0 - min(1.0, abs(yNorm - 0.5) * 3.0))
            let goldMix = max(0.0, hsv.v - 0.72) * 1.7 + centerWeight * 0.12 + edgeGemWeight * 0.08
            let hue = goldMix > 0.55 ? 0.12 : 0.765
            let saturation = clamp(hsv.s * 1.12 + 0.06)
            let value = clamp(hsv.v * 1.06 + 0.03)
            let converted = hsvToRgb(hue, saturation, value)
            red = converted.r
            green = converted.g
            blue = converted.b
        }

        pixels[offset] = UInt8(clamp(red * alpha) * 255.0)
        pixels[offset + 1] = UInt8(clamp(green * alpha) * 255.0)
        pixels[offset + 2] = UInt8(clamp(blue * alpha) * 255.0)
    }
}

guard
    let provider = CGDataProvider(data: Data(pixels) as CFData),
    let processedImage = CGImage(
        width: width,
        height: height,
        bitsPerComponent: 8,
        bitsPerPixel: 32,
        bytesPerRow: bytesPerRow,
        space: colorSpace,
        bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue),
        provider: provider,
        decode: nil,
        shouldInterpolate: true,
        intent: .defaultIntent
    ),
    let outputRep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    )
else {
    fatalError("Failed to create output image")
}

let canvasSize = NSSize(width: width, height: height)
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: outputRep)
NSColor.clear.setFill()
NSRect(origin: .zero, size: canvasSize).fill()

NSImage(cgImage: processedImage, size: canvasSize).draw(in: NSRect(origin: .zero, size: canvasSize))

let coverRect = NSRect(
    x: CGFloat(width) * 0.145,
    y: CGFloat(height) * 0.19,
    width: CGFloat(width) * 0.71,
    height: CGFloat(height) * 0.60
)
let coverPath = NSBezierPath(roundedRect: coverRect, xRadius: 46, yRadius: 46)
NSGradient(colors: [
    NSColor(calibratedRed: 0.055, green: 0.020, blue: 0.115, alpha: 1.0),
    NSColor(calibratedRed: 0.190, green: 0.050, blue: 0.305, alpha: 1.0),
    NSColor(calibratedRed: 0.065, green: 0.020, blue: 0.130, alpha: 1.0)
])?.draw(in: coverPath, angle: 0)

NSColor(calibratedRed: 0.97, green: 0.68, blue: 0.20, alpha: 0.26).setStroke()
coverPath.lineWidth = 2.0
coverPath.stroke()

let center = NSPoint(x: CGFloat(width) * 0.5, y: CGFloat(height) * 0.50)
for index in 0..<3 {
    let inset = CGFloat(index) * 17
    let alpha = CGFloat(0.36 - Double(index) * 0.08)
    let ringRect = NSRect(
        x: center.x - 142 + inset,
        y: center.y - 92 + inset * 0.55,
        width: 284 - inset * 2,
        height: 184 - inset * 1.1
    )
    let ring = NSBezierPath(ovalIn: ringRect)
    NSColor(calibratedRed: 0.98, green: 0.72, blue: 0.2, alpha: alpha).setStroke()
    ring.lineWidth = CGFloat(4 - index)
    ring.stroke()
}

for index in 0..<18 {
    let angle = Double(index) / 18.0 * Double.pi * 2.0
    let radiusX = CGFloat(165 + (index % 3) * 8)
    let radiusY = CGFloat(106 + (index % 4) * 5)
    let point = NSPoint(
        x: center.x + CGFloat(cos(angle)) * radiusX,
        y: center.y + CGFloat(sin(angle)) * radiusY
    )
    let starSize = CGFloat(index % 3 == 0 ? 5 : 3)
    NSColor(calibratedRed: 1.0, green: 0.78, blue: 0.28, alpha: 0.54).setFill()
    NSBezierPath(ovalIn: NSRect(x: point.x - starSize * 0.5, y: point.y - starSize * 0.5, width: starSize, height: starSize)).fill()
}

let paragraph = NSMutableParagraphStyle()
paragraph.alignment = .center
let font = NSFont(name: "ToppanBunkyuMidashiMinchoStdN-ExtraBold", size: 108)
    ?? NSFont(name: "YuMin-Extrabold", size: 108)
    ?? NSFont.boldSystemFont(ofSize: 108)
let textRect = NSRect(
    x: CGFloat(width) * 0.12,
    y: CGFloat(height) * 0.34,
    width: CGFloat(width) * 0.76,
    height: CGFloat(height) * 0.28
)
let label = "ガチャ"
let glowAttributes: [NSAttributedString.Key: Any] = [
    .font: font,
    .foregroundColor: NSColor(calibratedRed: 1.0, green: 0.72, blue: 0.20, alpha: 0.45),
    .paragraphStyle: paragraph
]
let shadowAttributes: [NSAttributedString.Key: Any] = [
    .font: font,
    .foregroundColor: NSColor(calibratedRed: 0.02, green: 0.01, blue: 0.02, alpha: 0.82),
    .paragraphStyle: paragraph
]
let mainAttributes: [NSAttributedString.Key: Any] = [
    .font: font,
    .foregroundColor: NSColor(calibratedRed: 0.98, green: 0.96, blue: 0.88, alpha: 1.0),
    .paragraphStyle: paragraph
]

NSAttributedString(string: label, attributes: glowAttributes).draw(in: textRect.offsetBy(dx: 0, dy: -2))
NSAttributedString(string: label, attributes: shadowAttributes).draw(in: textRect.offsetBy(dx: 5, dy: -7))
NSAttributedString(string: label, attributes: mainAttributes).draw(in: textRect)

NSGraphicsContext.restoreGraphicsState()

guard let pngData = outputRep.representation(using: .png, properties: [:]) else {
    fatalError("Failed to encode PNG")
}

try pngData.write(to: outputURL, options: .atomic)
print("Wrote \(outputURL.path) (\(width)x\(height))")
