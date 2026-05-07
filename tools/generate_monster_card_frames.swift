import AppKit
import Foundation

struct FramePalette {
    let dark: NSColor
    let mid: NSColor
    let light: NSColor
    let glow: NSColor
}

let palettes: [FramePalette] = [
    FramePalette(dark: NSColor(calibratedRed: 0.36, green: 0.28, blue: 0.18, alpha: 1), mid: NSColor(calibratedRed: 0.70, green: 0.54, blue: 0.32, alpha: 1), light: NSColor(calibratedRed: 1.00, green: 0.82, blue: 0.50, alpha: 1), glow: NSColor(calibratedRed: 1.00, green: 0.62, blue: 0.26, alpha: 0.78)),
    FramePalette(dark: NSColor(calibratedRed: 0.28, green: 0.32, blue: 0.42, alpha: 1), mid: NSColor(calibratedRed: 0.58, green: 0.68, blue: 0.86, alpha: 1), light: NSColor(calibratedRed: 0.90, green: 0.96, blue: 1.00, alpha: 1), glow: NSColor(calibratedRed: 0.48, green: 0.78, blue: 1.00, alpha: 0.76)),
    FramePalette(dark: NSColor(calibratedRed: 0.40, green: 0.24, blue: 0.06, alpha: 1), mid: NSColor(calibratedRed: 0.84, green: 0.56, blue: 0.12, alpha: 1), light: NSColor(calibratedRed: 1.00, green: 0.92, blue: 0.48, alpha: 1), glow: NSColor(calibratedRed: 1.00, green: 0.78, blue: 0.20, alpha: 0.82)),
    FramePalette(dark: NSColor(calibratedRed: 0.24, green: 0.12, blue: 0.46, alpha: 1), mid: NSColor(calibratedRed: 0.58, green: 0.32, blue: 0.92, alpha: 1), light: NSColor(calibratedRed: 0.92, green: 0.70, blue: 1.00, alpha: 1), glow: NSColor(calibratedRed: 0.74, green: 0.36, blue: 1.00, alpha: 0.82)),
    FramePalette(dark: NSColor(calibratedRed: 0.44, green: 0.10, blue: 0.08, alpha: 1), mid: NSColor(calibratedRed: 0.92, green: 0.26, blue: 0.20, alpha: 1), light: NSColor(calibratedRed: 1.00, green: 0.78, blue: 0.45, alpha: 1), glow: NSColor(calibratedRed: 1.00, green: 0.28, blue: 0.18, alpha: 0.82)),
    FramePalette(dark: NSColor(calibratedRed: 0.12, green: 0.12, blue: 0.24, alpha: 1), mid: NSColor(calibratedRed: 0.44, green: 0.74, blue: 0.98, alpha: 1), light: NSColor(calibratedRed: 1.00, green: 0.88, blue: 0.58, alpha: 1), glow: NSColor(calibratedRed: 0.92, green: 0.58, blue: 1.00, alpha: 0.88))
]

func pathForRoundedRect(_ rect: NSRect, radius: CGFloat) -> NSBezierPath {
    NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
}

func stroke(_ path: NSBezierPath, color: NSColor, width: CGFloat) {
    color.setStroke()
    path.lineWidth = width
    path.stroke()
}

func fill(_ path: NSBezierPath, color: NSColor) {
    color.setFill()
    path.fill()
}

func drawCorner(at origin: CGPoint, flipX: Bool, flipY: Bool, size: CGFloat, palette: FramePalette) {
    let sx: CGFloat = flipX ? -1 : 1
    let sy: CGFloat = flipY ? -1 : 1

    func p(_ x: CGFloat, _ y: CGFloat) -> CGPoint {
        CGPoint(x: origin.x + (x * sx), y: origin.y + (y * sy))
    }

    let wing = NSBezierPath()
    wing.move(to: p(0, 0))
    wing.line(to: p(size * 0.50, size * 0.12))
    wing.line(to: p(size * 0.88, size * 0.36))
    wing.line(to: p(size * 0.36, size * 0.46))
    wing.line(to: p(size * 0.15, size * 0.88))
    wing.line(to: p(0, 0))
    fill(wing, color: palette.dark.withAlphaComponent(0.94))
    stroke(wing, color: palette.light.withAlphaComponent(0.96), width: size * 0.045)
    stroke(wing, color: palette.mid.withAlphaComponent(0.92), width: size * 0.022)

    let gem = NSBezierPath()
    gem.move(to: p(size * 0.18, size * 0.18))
    gem.line(to: p(size * 0.30, size * 0.08))
    gem.line(to: p(size * 0.42, size * 0.18))
    gem.line(to: p(size * 0.30, size * 0.32))
    gem.close()
    fill(gem, color: palette.glow)
    stroke(gem, color: palette.light, width: size * 0.018)
}

func drawFrame(width: Int, height: Int, palette: FramePalette) -> NSBitmapImageRep {
    let image = NSImage(size: NSSize(width: width, height: height))
    image.lockFocus()
    NSColor.clear.setFill()
    NSRect(x: 0, y: 0, width: width, height: height).fill()
    NSGraphicsContext.current?.imageInterpolation = .high

    let canvas = NSRect(x: 0, y: 0, width: width, height: height)
    let minSide = CGFloat(min(width, height))
    let inset = minSide * 0.055
    let outer = canvas.insetBy(dx: inset, dy: inset)
    let middle = outer.insetBy(dx: minSide * 0.030, dy: minSide * 0.030)
    let inner = outer.insetBy(dx: minSide * 0.068, dy: minSide * 0.068)
    let corner = minSide * 0.17

    let shadow = pathForRoundedRect(outer.offsetBy(dx: 0, dy: -minSide * 0.012), radius: minSide * 0.060)
    stroke(shadow, color: NSColor.black.withAlphaComponent(0.34), width: minSide * 0.070)

    stroke(pathForRoundedRect(outer, radius: minSide * 0.060), color: palette.dark, width: minSide * 0.078)
    stroke(pathForRoundedRect(outer, radius: minSide * 0.060), color: palette.mid, width: minSide * 0.048)
    stroke(pathForRoundedRect(middle, radius: minSide * 0.045), color: palette.light, width: minSide * 0.018)
    stroke(pathForRoundedRect(inner, radius: minSide * 0.032), color: palette.dark.withAlphaComponent(0.86), width: minSide * 0.020)
    stroke(pathForRoundedRect(inner.insetBy(dx: minSide * 0.015, dy: minSide * 0.015), radius: minSide * 0.024), color: palette.glow.withAlphaComponent(0.42), width: minSide * 0.012)

    let topGem = NSBezierPath(ovalIn: NSRect(x: outer.midX - minSide * 0.030, y: outer.maxY - minSide * 0.052, width: minSide * 0.060, height: minSide * 0.060))
    fill(topGem, color: palette.glow)
    stroke(topGem, color: palette.light, width: minSide * 0.010)

    for i in 0..<4 {
        let t = CGFloat(i) / 3.0
        let x = outer.minX + outer.width * (0.23 + t * 0.54)
        let bolt = NSBezierPath(ovalIn: NSRect(x: x - minSide * 0.014, y: outer.maxY - minSide * 0.041, width: minSide * 0.028, height: minSide * 0.028))
        fill(bolt, color: palette.dark)
        stroke(bolt, color: palette.light.withAlphaComponent(0.72), width: minSide * 0.006)
    }

    drawCorner(at: CGPoint(x: outer.minX, y: outer.maxY), flipX: false, flipY: true, size: corner, palette: palette)
    drawCorner(at: CGPoint(x: outer.maxX, y: outer.maxY), flipX: true, flipY: true, size: corner, palette: palette)
    drawCorner(at: CGPoint(x: outer.minX, y: outer.minY), flipX: false, flipY: false, size: corner, palette: palette)
    drawCorner(at: CGPoint(x: outer.maxX, y: outer.minY), flipX: true, flipY: false, size: corner, palette: palette)

    image.unlockFocus()
    return NSBitmapImageRep(data: image.tiffRepresentation!)!
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "generate_monster_card_frames", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

let args = CommandLine.arguments
guard args.count == 2 else {
    fputs("usage: generate_monster_card_frames.swift <unity-project-root>\n", stderr)
    exit(1)
}

let outputRoot = "\(args[1])/Assets/Resources/MonsterCardFrames"
try FileManager.default.createDirectory(atPath: outputRoot, withIntermediateDirectories: true)

for (index, palette) in palettes.enumerated() {
    let rank = index + 1
    try save(drawFrame(width: 768, height: 1024, palette: palette), to: "\(outputRoot)/monster_class_\(rank)_card_frame.png")
    try save(drawFrame(width: 768, height: 768, palette: palette), to: "\(outputRoot)/monster_class_\(rank)_slot_frame.png")
}

print("Generated \(palettes.count * 2) monster card frames.")
