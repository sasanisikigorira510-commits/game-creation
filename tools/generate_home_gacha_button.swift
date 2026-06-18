import AppKit
import CoreGraphics
import Foundation
import ImageIO

let projectRoot = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let sourceURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeMenu/FusionButton.png")
let outputURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeMenu/GachaButton.png")
let bottomNavOutputURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeRedesign/HomeBottomNavGacha.png")
let bottomNavBarURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeRedesign/HomeBottomNavBar.png")
let generatedIconURL = projectRoot.appendingPathComponent("tools/assets/GachaButtonIcon_image2_alpha.png")
let fallbackIconURL = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/GachaPage/GachaCapsule.png")

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

func aspectFitRect(for imageSize: NSSize, in targetRect: NSRect) -> NSRect {
    let imageWidth = max(1.0, imageSize.width)
    let imageHeight = max(1.0, imageSize.height)
    let scale = min(targetRect.width / imageWidth, targetRect.height / imageHeight)
    let fittedSize = NSSize(width: imageWidth * scale, height: imageHeight * scale)
    return NSRect(
        x: targetRect.midX - fittedSize.width * 0.5,
        y: targetRect.midY - fittedSize.height * 0.5,
        width: fittedSize.width,
        height: fittedSize.height
    )
}

func writePNG(_ bitmap: NSBitmapImageRep, to url: URL) throws {
    guard let pngData = bitmap.representation(using: .png, properties: [:]) else {
        fatalError("Failed to encode PNG")
    }

    try pngData.write(to: url, options: .atomic)
}

func drawSummonIcon(_ icon: NSImage, in targetRect: NSRect, imageInterpolation: NSImageInterpolation = .none) {
    let iconRect = aspectFitRect(for: icon.size, in: targetRect)
    NSGraphicsContext.current?.imageInterpolation = imageInterpolation
    icon.draw(in: iconRect, from: .zero, operation: .sourceOver, fraction: 1.0)
    NSGraphicsContext.current?.imageInterpolation = .high
}

guard
    let imageSource = CGImageSourceCreateWithURL(sourceURL as CFURL, nil),
    let sourceImage = CGImageSourceCreateImageAtIndex(imageSource, 0, nil)
else {
    fatalError("Failed to load source image at \(sourceURL.path)")
}

let iconURL = FileManager.default.fileExists(atPath: generatedIconURL.path) ? generatedIconURL : fallbackIconURL
guard let gachaIcon = NSImage(contentsOf: iconURL) else {
    fatalError("Failed to load gacha icon at \(iconURL.path)")
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
    y: CGFloat(height) * 0.16,
    width: CGFloat(width) * 0.71,
    height: CGFloat(height) * 0.66
)
let coverPath = NSBezierPath(roundedRect: coverRect, xRadius: 42, yRadius: 42)
NSGradient(colors: [
    NSColor(calibratedRed: 0.025, green: 0.024, blue: 0.050, alpha: 1.0),
    NSColor(calibratedRed: 0.055, green: 0.038, blue: 0.105, alpha: 1.0),
    NSColor(calibratedRed: 0.018, green: 0.018, blue: 0.038, alpha: 1.0)
])?.draw(in: coverPath, angle: 0)

NSColor(calibratedRed: 0.78, green: 0.60, blue: 0.34, alpha: 0.58).setStroke()
coverPath.lineWidth = 3.0
coverPath.stroke()

let center = NSPoint(x: coverRect.midX, y: coverRect.midY + CGFloat(height) * 0.08)
for index in 0..<16 {
    let angle = Double(index) / 16.0 * Double.pi * 2.0
    let end = NSPoint(
        x: center.x + CGFloat(cos(angle)) * CGFloat(width) * 0.22,
        y: center.y + CGFloat(sin(angle)) * CGFloat(height) * 0.17
    )
    let ray = NSBezierPath()
    ray.move(to: center)
    ray.line(to: end)
    NSColor(calibratedRed: 0.42, green: 0.62, blue: 1.0, alpha: 0.13).setStroke()
    ray.lineWidth = index % 4 == 0 ? 4.0 : 2.0
    ray.stroke()
}

let iconTargetRect = NSRect(
    x: CGFloat(width) * 0.150,
    y: CGFloat(height) * 0.300,
    width: CGFloat(width) * 0.70,
    height: CGFloat(height) * 0.58
)
drawSummonIcon(gachaIcon, in: iconTargetRect)

let paragraph = NSMutableParagraphStyle()
paragraph.alignment = .center
let font = NSFont(name: "ToppanBunkyuMidashiMinchoStdN-ExtraBold", size: 92)
    ?? NSFont(name: "YuMin-Extrabold", size: 92)
    ?? NSFont.boldSystemFont(ofSize: 92)
let textRect = NSRect(
    x: CGFloat(width) * 0.12,
    y: CGFloat(height) * 0.185,
    width: CGFloat(width) * 0.76,
    height: CGFloat(height) * 0.25
)
let label = "召喚"
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

let bottomNavWidth = 216
let bottomNavHeight = 190
guard
    let bottomNavRep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: bottomNavWidth,
        pixelsHigh: bottomNavHeight,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    )
else {
    fatalError("Failed to create bottom nav output image")
}

NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bottomNavRep)
NSColor.clear.setFill()
NSRect(x: 0, y: 0, width: bottomNavWidth, height: bottomNavHeight).fill()

if
    let bottomNavBarSource = CGImageSourceCreateWithURL(bottomNavBarURL as CFURL, nil),
    let bottomNavBarImage = CGImageSourceCreateImageAtIndex(bottomNavBarSource, 0, nil),
    let croppedGachaSegment = bottomNavBarImage.cropping(to: CGRect(x: 216, y: 0, width: bottomNavWidth, height: bottomNavHeight))
{
    NSImage(cgImage: croppedGachaSegment, size: NSSize(width: bottomNavWidth, height: bottomNavHeight))
        .draw(in: NSRect(x: 0, y: 0, width: bottomNavWidth, height: bottomNavHeight))
} else if let bottomNavSource = NSImage(contentsOf: bottomNavOutputURL) {
    bottomNavSource.draw(in: NSRect(x: 0, y: 0, width: bottomNavWidth, height: bottomNavHeight))
}

let navIconSize = CGFloat(138)
let navIconCenterX = CGFloat(bottomNavWidth) * 0.5
drawSummonIcon(
    gachaIcon,
    in: NSRect(
        x: navIconCenterX - navIconSize * 0.5 + 10,
        y: 51,
        width: navIconSize,
        height: navIconSize
    ))
NSGraphicsContext.restoreGraphicsState()

try writePNG(bottomNavRep, to: bottomNavOutputURL)
print("Wrote \(bottomNavOutputURL.path) (\(bottomNavWidth)x\(bottomNavHeight))")
