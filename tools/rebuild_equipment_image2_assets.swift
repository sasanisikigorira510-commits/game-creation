import AppKit
import CoreGraphics
import Foundation

let projectRoot = "/Users/andou/Desktop/あ/game-creation"
let resourcesRoot = "\(projectRoot)/WitchTowerGame/Assets/Resources"
let sourceRoot = "\(projectRoot)/tools/generated_equipment_assets"

let backgroundSourcePath = "\(sourceRoot)/image2_sources/equipment_arcane_background_source.png"
let legacySourceRoot = "\(sourceRoot)/legacy_icon_sources"

func color(_ hex: UInt32, alpha: CGFloat = 1.0) -> NSColor {
    NSColor(
        deviceRed: CGFloat((hex >> 16) & 0xff) / 255.0,
        green: CGFloat((hex >> 8) & 0xff) / 255.0,
        blue: CGFloat(hex & 0xff) / 255.0,
        alpha: alpha
    )
}

func render(size: CGSize, opaque: Bool = false, draw: () -> Void) throws -> NSBitmapImageRep {
    guard let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: Int(size.width),
        pixelsHigh: Int(size.height),
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bitmapFormat: [],
        bytesPerRow: 0,
        bitsPerPixel: 0
    ) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 1, userInfo: [NSLocalizedDescriptionKey: "bitmap allocation failed"])
    }
    rep.size = size

    NSGraphicsContext.saveGraphicsState()
    guard let context = NSGraphicsContext(bitmapImageRep: rep) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 1, userInfo: [NSLocalizedDescriptionKey: "graphics context failed"])
    }
    NSGraphicsContext.current = context
    (opaque ? color(0x070b12) : NSColor.clear).setFill()
    NSRect(origin: .zero, size: size).fill()
    NSGraphicsContext.current?.imageInterpolation = .high
    draw()
    NSGraphicsContext.restoreGraphicsState()
    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    try FileManager.default.createDirectory(atPath: URL(fileURLWithPath: path).deletingLastPathComponent().path, withIntermediateDirectories: true)
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 2, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
    }
    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func loadImage(_ path: String) throws -> NSImage {
    guard let image = NSImage(contentsOfFile: path) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 3, userInfo: [NSLocalizedDescriptionKey: "image not found: \(path)"])
    }
    return image
}

func drawAspectFill(_ image: NSImage, in rect: NSRect) {
    let sourceSize = image.size
    let scale = max(rect.width / sourceSize.width, rect.height / sourceSize.height)
    let drawSize = NSSize(width: sourceSize.width * scale, height: sourceSize.height * scale)
    let drawRect = NSRect(
        x: rect.midX - drawSize.width / 2,
        y: rect.midY - drawSize.height / 2,
        width: drawSize.width,
        height: drawSize.height
    )
    image.draw(in: drawRect, from: .zero, operation: .sourceOver, fraction: 1.0)
}

func rounded(_ rect: NSRect, radius: CGFloat, fill: NSColor, stroke: NSColor? = nil, lineWidth: CGFloat = 1) {
    let path = NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
    fill.setFill()
    path.fill()
    if let stroke {
        path.lineWidth = lineWidth
        stroke.setStroke()
        path.stroke()
    }
}

func strokeRounded(_ rect: NSRect, radius: CGFloat, color: NSColor, width: CGFloat) {
    let path = NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
    path.lineWidth = width
    color.setStroke()
    path.stroke()
}

func line(_ points: [CGPoint], color: NSColor, width: CGFloat) {
    guard let first = points.first else { return }
    let path = NSBezierPath()
    path.move(to: first)
    for point in points.dropFirst() {
        path.line(to: point)
    }
    path.lineCapStyle = .round
    path.lineJoinStyle = .round
    path.lineWidth = width
    color.setStroke()
    path.stroke()
}

func glow(center: CGPoint, radiusX: CGFloat, radiusY: CGFloat, color: NSColor, layers: Int) {
    guard layers > 0 else { return }
    for layer in stride(from: layers, through: 1, by: -1) {
        let p = CGFloat(layer) / CGFloat(layers)
        let alpha = color.alphaComponent * (0.018 + (1 - p) * 0.045)
        let rect = NSRect(
            x: center.x - radiusX * p,
            y: center.y - radiusY * p,
            width: radiusX * p * 2,
            height: radiusY * p * 2
        )
        color.withAlphaComponent(alpha).setFill()
        NSBezierPath(ovalIn: rect).fill()
    }
}

struct Bitmap {
    var data: [UInt8]
    let width: Int
    let height: Int
}

struct Box {
    var minX: Int
    var minY: Int
    var maxX: Int
    var maxY: Int
    var width: Int { maxX - minX + 1 }
    var height: Int { maxY - minY + 1 }
    var area: Int { width * height }
}

func bitmap(from image: NSImage) throws -> Bitmap {
    guard let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 4, userInfo: [NSLocalizedDescriptionKey: "cgImage failed"])
    }

    let width = cgImage.width
    let height = cgImage.height
    var data = [UInt8](repeating: 0, count: width * height * 4)
    let colorSpace = CGColorSpaceCreateDeviceRGB()
    data.withUnsafeMutableBytes { bytes in
        guard let context = CGContext(
            data: bytes.baseAddress,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return }
        context.interpolationQuality = .high
        context.clear(CGRect(x: 0, y: 0, width: width, height: height))
        context.draw(cgImage, in: CGRect(x: 0, y: 0, width: width, height: height))
    }

    return Bitmap(data: data, width: width, height: height)
}

func image(from bitmap: Bitmap) throws -> NSImage {
    let colorSpace = CGColorSpaceCreateDeviceRGB()
    let data = Data(bitmap.data) as CFData
    guard let provider = CGDataProvider(data: data),
          let cgImage = CGImage(
            width: bitmap.width,
            height: bitmap.height,
            bitsPerComponent: 8,
            bitsPerPixel: 32,
            bytesPerRow: bitmap.width * 4,
            space: colorSpace,
            bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue),
            provider: provider,
            decode: nil,
            shouldInterpolate: true,
            intent: .defaultIntent
          ) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 5, userInfo: [NSLocalizedDescriptionKey: "bitmap image failed"])
    }

    return NSImage(cgImage: cgImage, size: NSSize(width: bitmap.width, height: bitmap.height))
}

func alphaBox(_ bitmap: Bitmap, threshold: UInt8 = 18) -> Box? {
    var minX = bitmap.width
    var minY = bitmap.height
    var maxX = -1
    var maxY = -1

    for y in 0..<bitmap.height {
        for x in 0..<bitmap.width {
            let index = (y * bitmap.width + x) * 4 + 3
            if bitmap.data[index] > threshold {
                minX = min(minX, x)
                minY = min(minY, y)
                maxX = max(maxX, x)
                maxY = max(maxY, y)
            }
        }
    }

    return maxX >= minX && maxY >= minY ? Box(minX: minX, minY: minY, maxX: maxX, maxY: maxY) : nil
}

func saveBackground() throws {
    let source = try loadImage(backgroundSourcePath)
    let rep = try render(size: CGSize(width: 1024, height: 1536), opaque: true) {
        drawAspectFill(source, in: NSRect(x: 0, y: 0, width: 1024, height: 1536))
        color(0x020610, alpha: 0.22).setFill()
        NSRect(x: 0, y: 0, width: 1024, height: 1536).fill()

        let topGradient = NSGradient(colors: [
            color(0x020610, alpha: 0.95),
            color(0x020610, alpha: 0.12)
        ])
        topGradient?.draw(in: NSRect(x: 0, y: 1110, width: 1024, height: 426), angle: 270)

        let bottomGradient = NSGradient(colors: [
            color(0x020610, alpha: 0.92),
            color(0x020610, alpha: 0.08)
        ])
        bottomGradient?.draw(in: NSRect(x: 0, y: 0, width: 1024, height: 520), angle: 90)

        glow(center: CGPoint(x: 512, y: 670), radiusX: 390, radiusY: 300, color: color(0x4ceeff, alpha: 0.48), layers: 14)
        glow(center: CGPoint(x: 512, y: 715), radiusX: 300, radiusY: 220, color: color(0x9b5cff, alpha: 0.42), layers: 12)
    }
    try savePNG(rep, to: "\(resourcesRoot)/EquipmentBackgrounds/equipment_scene_background.png")
}

func tintedImage(_ image: NSImage, tint: NSColor, alpha: CGFloat) throws -> NSImage {
    let rep = try render(size: image.size) {
        image.draw(in: NSRect(origin: .zero, size: image.size), from: .zero, operation: .sourceOver, fraction: 1)
        NSGraphicsContext.current?.compositingOperation = .sourceAtop
        tint.withAlphaComponent(alpha).setFill()
        NSRect(origin: .zero, size: image.size).fill()
        NSGraphicsContext.current?.compositingOperation = .sourceOver
    }
    guard let data = rep.representation(using: .png, properties: [:]), let result = NSImage(data: data) else {
        throw NSError(domain: "EquipmentImage2Assets", code: 7, userInfo: [NSLocalizedDescriptionKey: "tint failed"])
    }
    return result
}

func drawEnhancedSource(_ image: NSImage, accent: NSColor, in rect: NSRect) throws {
    let black = try tintedImage(image, tint: color(0x02050a), alpha: 0.95)
    let glowImage = try tintedImage(image, tint: accent, alpha: 0.62)
    black.draw(in: rect.insetBy(dx: -3, dy: -3).offsetBy(dx: 2, dy: -3), from: .zero, operation: .sourceOver, fraction: 0.62)
    glowImage.draw(in: rect.insetBy(dx: -5, dy: -5), from: .zero, operation: .sourceOver, fraction: 0.30)
    image.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1)
}

func makeEnhancedIcon(sourceName: String, outputSubfolder: String, accent: NSColor, goldAmount: CGFloat = 0.18) throws {
    let image = try loadImage("\(legacySourceRoot)/\(sourceName)")
    let rep = try render(size: CGSize(width: 128, height: 128)) {
        try? drawEnhancedSource(image, accent: accent, in: NSRect(x: 8, y: 8, width: 112, height: 112))
    }
    try savePNG(rep, to: "\(resourcesRoot)/\(outputSubfolder)/\(sourceName)")
}

func makeLockIcon(name: String, locked: Bool) throws {
    let accent = locked ? color(0xffc85a) : color(0x35eaff)
    let bodyFill = locked ? color(0x342316, alpha: 0.96) : color(0x102832, alpha: 0.96)
    let rep = try render(size: CGSize(width: 128, height: 128)) {
        let shackle = NSBezierPath()
        shackle.lineWidth = 9
        shackle.lineCapStyle = .round
        shackle.move(to: CGPoint(x: locked ? 40 : 82, y: 72))
        shackle.curve(to: CGPoint(x: locked ? 88 : 40, y: 72),
                      controlPoint1: CGPoint(x: locked ? 40 : 94, y: 108),
                      controlPoint2: CGPoint(x: locked ? 88 : 28, y: 108))
        color(0x071018, alpha: 0.95).setStroke()
        shackle.stroke()
        shackle.lineWidth = 5
        color(0xa9bccb, alpha: 0.92).setStroke()
        shackle.stroke()

        rounded(NSRect(x: 27, y: 26, width: 74, height: 54), radius: 9, fill: bodyFill, stroke: accent.withAlphaComponent(0.9), lineWidth: 4)
        rounded(NSRect(x: 34, y: 51, width: 60, height: 19), radius: 5, fill: color(0xffffff, alpha: 0.08))
        color(0x02060b, alpha: 0.95).setFill()
        NSBezierPath(ovalIn: NSRect(x: 57, y: 44, width: 14, height: 14)).fill()
        rounded(NSRect(x: 61, y: 34, width: 6, height: 17), radius: 3, fill: color(0x02060b, alpha: 0.95))
        line([CGPoint(x: 35, y: 28), CGPoint(x: 96, y: 77)], color: color(0xffffff, alpha: 0.14), width: 2)
    }
    try savePNG(rep, to: "\(resourcesRoot)/EquipmentUi/\(name)")
}

func saveIcons() throws {
    let icons: [(String, NSColor, CGFloat)] = [
        ("eq_bronze_blade_icon.png", color(0xffb45c), 0.20),
        ("eq_iron_blade_icon.png", color(0x72d7ff), 0.16),
        ("eq_gold_blade_icon.png", color(0xffdc72), 0.28),
        ("eq_cloth_armor_icon.png", color(0x84b7ff), 0.14),
        ("eq_leather_armor_icon.png", color(0xb88955), 0.18),
        ("eq_plate_armor_icon.png", color(0x9feaff), 0.16),
        ("eq_green_ring_icon.png", color(0x67f4ad), 0.14),
        ("eq_red_ring_icon.png", color(0xff7272), 0.20),
        ("eq_violet_pendant_icon.png", color(0xc487ff), 0.18)
    ]
    for (name, accent, goldAmount) in icons {
        try makeEnhancedIcon(sourceName: name, outputSubfolder: "EquipmentIcons", accent: accent, goldAmount: goldAmount)
    }

    let relics: [(String, NSColor, CGFloat)] = [
        ("relic_safe_ember_icon.png", color(0x65f2c2), 0.16),
        ("relic_risky_ember_icon.png", color(0xff8c42), 0.28),
        ("relic_volatile_ember_icon.png", color(0xbf73ff), 0.18)
    ]
    for (name, accent, goldAmount) in relics {
        try makeEnhancedIcon(sourceName: name, outputSubfolder: "EquipmentRelics", accent: accent, goldAmount: goldAmount)
    }

    try makeLockIcon(name: "ui_lock_locked_icon.png", locked: true)
    try makeLockIcon(name: "ui_lock_unlocked_icon.png", locked: false)
}

try saveBackground()
try saveIcons()
print("Rebuilt equipment Image2-aligned assets.")
