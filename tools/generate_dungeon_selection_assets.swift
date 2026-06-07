import AppKit
import Foundation

let outputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/DungeonSelect"
let battleOutputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/BattleBackgrounds"
let generatedBackgroundRoot = "/Users/andou/Desktop/あ/game-creation/tools/generated_dungeon_backgrounds"

func color(_ hex: UInt32, alpha: CGFloat = 1.0) -> NSColor {
    let r = CGFloat((hex >> 16) & 0xff) / 255.0
    let g = CGFloat((hex >> 8) & 0xff) / 255.0
    let b = CGFloat(hex & 0xff) / 255.0
    return NSColor(deviceRed: r, green: g, blue: b, alpha: alpha)
}

func render(size: CGSize, draw: () -> Void) throws -> NSBitmapImageRep {
    guard let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: Int(size.width),
        pixelsHigh: Int(size.height),
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    ) else {
        throw NSError(domain: "DungeonSelectionAssets", code: 1, userInfo: [NSLocalizedDescriptionKey: "bitmap allocation failed"])
    }

    guard let context = NSGraphicsContext(bitmapImageRep: rep) else {
        throw NSError(domain: "DungeonSelectionAssets", code: 2, userInfo: [NSLocalizedDescriptionKey: "graphics context failed"])
    }

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = context
    NSColor.clear.setFill()
    NSRect(origin: .zero, size: size).fill()
    context.imageInterpolation = .high
    draw()
    NSGraphicsContext.restoreGraphicsState()

    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "DungeonSelectionAssets", code: 2, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func guid() -> String {
    UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
}

func writeFolderMeta(_ folderPath: String) throws {
    let metaPath = "\(folderPath).meta"
    if FileManager.default.fileExists(atPath: metaPath) {
        return
    }

    let text = """
fileFormatVersion: 2
guid: \(guid())
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 

"""

    try text.write(toFile: metaPath, atomically: true, encoding: .utf8)
}

func writeSpriteMeta(for path: String) throws {
    let metaPath = "\(path).meta"
    if FileManager.default.fileExists(atPath: metaPath) {
        return
    }

    let text = """
fileFormatVersion: 2
guid: \(guid())
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 

"""

    try text.write(toFile: metaPath, atomically: true, encoding: .utf8)
}

func fillGradient(_ rect: NSRect, top: NSColor, bottom: NSColor) {
    let gradient = NSGradient(starting: top, ending: bottom)
    gradient?.draw(in: rect, angle: 90)
}

func drawRoundedRect(_ rect: NSRect, radius: CGFloat, fill: NSColor, stroke: NSColor, strokeWidth: CGFloat = 3) {
    let path = NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
    fill.setFill()
    path.fill()
    path.lineWidth = strokeWidth
    stroke.setStroke()
    path.stroke()
}

func drawGlowOval(center: CGPoint, radiusX: CGFloat, radiusY: CGFloat, color: NSColor, layers: Int) {
    for layer in stride(from: layers, through: 1, by: -1) {
        let p = CGFloat(layer) / CGFloat(layers)
        color.withAlphaComponent(color.alphaComponent * (0.03 + (1 - p) * 0.07)).setFill()
        NSBezierPath(ovalIn: NSRect(x: center.x - radiusX * p, y: center.y - radiusY * p, width: radiusX * p * 2, height: radiusY * p * 2)).fill()
    }
}

func strokeLine(_ from: CGPoint, _ to: CGPoint, color: NSColor, width: CGFloat) {
    let path = NSBezierPath()
    path.move(to: from)
    path.line(to: to)
    path.lineWidth = width
    path.lineCapStyle = .round
    color.setStroke()
    path.stroke()
}

func drawCrystal(_ center: CGPoint, size: CGFloat, fill: NSColor, stroke: NSColor) {
    let path = NSBezierPath()
    path.move(to: CGPoint(x: center.x, y: center.y + size))
    path.line(to: CGPoint(x: center.x + size * 0.55, y: center.y))
    path.line(to: CGPoint(x: center.x, y: center.y - size))
    path.line(to: CGPoint(x: center.x - size * 0.55, y: center.y))
    path.close()
    fill.setFill()
    path.fill()
    path.lineWidth = max(1, size * 0.08)
    stroke.setStroke()
    path.stroke()
}

func drawRuneCircle(center: CGPoint, radius: CGFloat, accent: NSColor, alpha: CGFloat) {
    let ring = NSBezierPath(ovalIn: NSRect(x: center.x - radius, y: center.y - radius, width: radius * 2, height: radius * 2))
    ring.lineWidth = max(2, radius * 0.035)
    accent.withAlphaComponent(alpha).setStroke()
    ring.stroke()

    for i in 0..<8 {
        let angle = CGFloat(i) * (.pi / 4)
        let inner = CGPoint(x: center.x + cos(angle) * radius * 0.48, y: center.y + sin(angle) * radius * 0.48)
        let outer = CGPoint(x: center.x + cos(angle) * radius * 0.86, y: center.y + sin(angle) * radius * 0.86)
        strokeLine(inner, outer, color: accent.withAlphaComponent(alpha * 0.76), width: max(1.5, radius * 0.018))
    }
}

func loadSourceImage(_ path: String) throws -> NSImage {
    guard let image = NSImage(contentsOfFile: path) else {
        throw NSError(domain: "DungeonSelectionAssets", code: 3, userInfo: [NSLocalizedDescriptionKey: "source image missing: \(path)"])
    }

    if let bitmap = image.representations.compactMap({ $0 as? NSBitmapImageRep }).first {
        image.size = NSSize(width: bitmap.pixelsWide, height: bitmap.pixelsHigh)
    }

    return image
}

func clamp(_ value: CGFloat, _ minimum: CGFloat, _ maximum: CGFloat) -> CGFloat {
    min(max(value, minimum), maximum)
}

func drawImageCover(_ image: NSImage, in rect: NSRect, focusY: CGFloat, zoom: CGFloat = 1.0, fraction: CGFloat = 1.0) {
    let sourceSize = image.size
    let safeZoom = max(1.0, zoom)
    let destAspect = rect.width / rect.height
    let sourceAspect = sourceSize.width / sourceSize.height
    var cropWidth: CGFloat
    var cropHeight: CGFloat

    if sourceAspect > destAspect {
        cropHeight = sourceSize.height / safeZoom
        cropWidth = cropHeight * destAspect
    } else {
        cropWidth = sourceSize.width / safeZoom
        cropHeight = cropWidth / destAspect
    }

    cropWidth = min(cropWidth, sourceSize.width)
    cropHeight = min(cropHeight, sourceSize.height)
    let cropX = (sourceSize.width - cropWidth) * 0.5
    let cropY = clamp(sourceSize.height * focusY - cropHeight * 0.5, 0, sourceSize.height - cropHeight)
    image.draw(in: rect, from: NSRect(x: cropX, y: cropY, width: cropWidth, height: cropHeight), operation: .sourceOver, fraction: fraction)
}

func drawCardVignette(_ rect: NSRect, accent: NSColor) {
    fillGradient(NSRect(x: rect.minX, y: rect.minY, width: rect.width, height: rect.height * 0.38), top: color(0x000000, alpha: 0.54), bottom: color(0x000000, alpha: 0.04))
    fillGradient(NSRect(x: rect.minX, y: rect.maxY - rect.height * 0.36, width: rect.width, height: rect.height * 0.36), top: color(0x000000, alpha: 0.08), bottom: color(0x000000, alpha: 0.62))
    fillGradient(NSRect(x: rect.minX, y: rect.minY, width: rect.width * 0.24, height: rect.height), top: color(0x000000, alpha: 0.40), bottom: color(0x000000, alpha: 0.22))
    fillGradient(NSRect(x: rect.maxX - rect.width * 0.24, y: rect.minY, width: rect.width * 0.24, height: rect.height), top: color(0x000000, alpha: 0.22), bottom: color(0x000000, alpha: 0.40))
    drawGlowOval(center: CGPoint(x: rect.midX, y: rect.midY), radiusX: rect.width * 0.38, radiusY: rect.height * 0.28, color: accent.withAlphaComponent(0.18), layers: 10)
}

func drawPipe(_ from: CGPoint, _ to: CGPoint, color pipeColor: NSColor, rim: NSColor) {
    strokeLine(from, to, color: color(0x02070a, alpha: 0.62), width: 17)
    strokeLine(from, to, color: pipeColor, width: 11)
    strokeLine(from, to, color: rim, width: 2)
}

func drawBookcaseSilhouette(_ rect: NSRect, accent: NSColor, flip: Bool) {
    drawRoundedRect(rect, radius: 6, fill: color(0x09050a, alpha: 0.58), stroke: color(0xffd78a, alpha: 0.16), strokeWidth: 2)
    for row in 0..<3 {
        let shelfY = rect.minY + CGFloat(row + 1) * rect.height * 0.24
        strokeLine(
            CGPoint(x: rect.minX + 10, y: shelfY),
            CGPoint(x: rect.maxX - 10, y: shelfY + CGFloat(flip ? -4 : 4)),
            color: color(0xf2b66a, alpha: 0.18),
            width: 3
        )

        for col in 0..<8 {
            let width = CGFloat(8 + (col + row) % 3 * 4)
            let height = CGFloat(26 + (col * 5 + row * 7) % 24)
            let x = rect.minX + 16 + CGFloat(col) * (rect.width - 32) / 8
            let y = shelfY - height + 2
            drawRoundedRect(
                NSRect(x: x, y: y, width: width, height: height),
                radius: 2,
                fill: [color(0x592a48), color(0x1b4d67), color(0x70502c), accent][(col + row) % 4].withAlphaComponent(0.46),
                stroke: color(0xffffff, alpha: 0.05),
                strokeWidth: 1
            )
        }
    }
}

func drawCardForeground(_ rect: NSRect, accent: NSColor, mood: Int) {
    switch mood {
    case 0:
        let gateColors = [color(0xff8148), color(0x6acfff), color(0xe0c16a), color(0xf0f5ff), color(0xa679ff)]
        for i in 0..<5 {
            let x = rect.minX + rect.width * (0.22 + CGFloat(i) * 0.14)
            drawGlowOval(center: CGPoint(x: x, y: rect.minY + rect.height * 0.52), radiusX: 24, radiusY: 38, color: gateColors[i].withAlphaComponent(0.24), layers: 7)
        }
    case 1:
        let pipeColor = color(0x253242, alpha: 0.78)
        drawPipe(CGPoint(x: rect.minX + 24, y: rect.maxY - 38), CGPoint(x: rect.maxX - 78, y: rect.minY + 42), color: pipeColor, rim: accent.withAlphaComponent(0.20))
        drawPipe(CGPoint(x: rect.minX + 118, y: rect.maxY - 22), CGPoint(x: rect.maxX - 20, y: rect.maxY - 118), color: color(0x151d27, alpha: 0.72), rim: accent.withAlphaComponent(0.18))
        drawGear(center: CGPoint(x: rect.minX + 102, y: rect.minY + 92), radius: 38, accent: accent.withAlphaComponent(0.34))
        drawGear(center: CGPoint(x: rect.maxX - 82, y: rect.maxY - 78), radius: 34, accent: color(0xff8a52, alpha: 0.26))
        for i in 0..<5 {
            let x = rect.minX + 245 + CGFloat(i) * 46
            strokeLine(CGPoint(x: x, y: rect.minY + 48), CGPoint(x: x + 18, y: rect.minY + 130), color: color(0xa8c1d8, alpha: 0.16), width: 4)
        }
    case 2:
        drawBookcaseSilhouette(NSRect(x: rect.minX + 24, y: rect.minY + 54, width: 158, height: 210), accent: accent, flip: false)
        drawBookcaseSilhouette(NSRect(x: rect.maxX - 184, y: rect.minY + 48, width: 158, height: 218), accent: accent, flip: true)
        drawRuneCircle(center: CGPoint(x: rect.midX, y: rect.midY + 4), radius: 86, accent: accent, alpha: 0.20)
        drawGlowOval(center: CGPoint(x: rect.midX, y: rect.midY - 2), radiusX: 126, radiusY: 48, color: accent.withAlphaComponent(0.16), layers: 9)
    case 3:
        drawGlowOval(center: CGPoint(x: rect.midX, y: rect.minY + 44), radiusX: rect.width * 0.46, radiusY: 42, color: color(0xff5e24, alpha: 0.30), layers: 10)
        for i in 0..<7 {
            let x = rect.minX + 68 + CGFloat(i) * 92
            strokeLine(CGPoint(x: x, y: rect.minY + CGFloat(34 + i % 2 * 18)), CGPoint(x: x + CGFloat(42 - i % 3 * 14), y: rect.minY + CGFloat(86 + i % 3 * 16)), color: color(0xffbf6e, alpha: 0.21), width: 4)
        }
    case 4:
        for i in 0..<10 {
            let x = rect.minX + CGFloat(42 + i * 72)
            let y = rect.minY + CGFloat(42 + (i * 41) % 212)
            drawCrystal(CGPoint(x: x, y: y), size: CGFloat(13 + i % 3 * 5), fill: accent.withAlphaComponent(0.38), stroke: color(0xffffff, alpha: 0.14))
        }
        for i in 0..<24 {
            let x = rect.minX + CGFloat((i * 67) % Int(rect.width - 58) + 30)
            let y = rect.minY + CGFloat((i * 43) % Int(rect.height - 62) + 34)
            NSBezierPath(ovalIn: NSRect(x: x, y: y, width: 2.2, height: 2.2)).fill()
        }
    default:
        drawRuneCircle(center: CGPoint(x: rect.midX, y: rect.midY), radius: 114, accent: accent, alpha: 0.24)
        drawRuneCircle(center: CGPoint(x: rect.midX, y: rect.midY), radius: 68, accent: color(0x8fffd9), alpha: 0.14)
        drawGlowOval(center: CGPoint(x: rect.midX, y: rect.midY), radiusX: 148, radiusY: 74, color: accent.withAlphaComponent(0.18), layers: 10)
        for i in 0..<6 {
            let y = rect.minY + 86 + CGFloat(i) * 28
            strokeLine(CGPoint(x: rect.midX - 38, y: y), CGPoint(x: rect.midX + 38, y: y + CGFloat(i % 2 == 0 ? 6 : -6)), color: color(0xf4d6ff, alpha: 0.10), width: 3)
        }
    }
}

func drawIllustratedDungeonCard(fileName: String, sourcePath: String, accent: NSColor, mood: Int, focusY: CGFloat, zoom: CGFloat = 1.0) throws {
    let image = try loadSourceImage(sourcePath)
    let rep = try render(size: CGSize(width: 760, height: 360)) {
        let outer = NSRect(x: 8, y: 8, width: 744, height: 344)
        let content = outer.insetBy(dx: 3, dy: 3)
        drawRoundedRect(outer, radius: 40, fill: color(0x020407, alpha: 0.96), stroke: color(0x000000, alpha: 0.70), strokeWidth: 5)

        NSGraphicsContext.saveGraphicsState()
        NSBezierPath(roundedRect: content, xRadius: 34, yRadius: 34).addClip()
        drawImageCover(image, in: content, focusY: focusY, zoom: zoom)
        accent.withAlphaComponent(0.10).setFill()
        content.fill()
        drawCardVignette(content, accent: accent)
        NSGraphicsContext.restoreGraphicsState()

        let border = NSBezierPath(roundedRect: content, xRadius: 34, yRadius: 34)
        border.lineWidth = 4
        accent.withAlphaComponent(0.72).setStroke()
        border.stroke()
        let inner = NSBezierPath(roundedRect: content.insetBy(dx: 16, dy: 16), xRadius: 24, yRadius: 24)
        inner.lineWidth = 1.5
        color(0xffffff, alpha: 0.16).setStroke()
        inner.stroke()
    }

    let path = "\(outputRoot)/\(fileName).png"
    try savePNG(rep, to: path)
    try writeSpriteMeta(for: path)
}

func drawIllustratedBattleBackdrop(fileName: String, sourcePath: String, accent: NSColor, focusY: CGFloat = 0.5) throws {
    let image = try loadSourceImage(sourcePath)
    let rep = try render(size: CGSize(width: 1170, height: 2532)) {
        let rect = NSRect(x: 0, y: 0, width: 1170, height: 2532)
        drawImageCover(image, in: rect, focusY: focusY)
        accent.withAlphaComponent(0.07).setFill()
        rect.fill()
        drawGlowOval(center: CGPoint(x: 585, y: 1320), radiusX: 470, radiusY: 620, color: accent.withAlphaComponent(0.16), layers: 18)
        fillGradient(NSRect(x: 0, y: 0, width: 1170, height: 560), top: color(0x000000, alpha: 0.50), bottom: color(0x000000, alpha: 0.02))
        fillGradient(NSRect(x: 0, y: 1980, width: 1170, height: 552), top: color(0x000000, alpha: 0.04), bottom: color(0x000000, alpha: 0.58))
    }

    let path = "\(battleOutputRoot)/\(fileName).png"
    try savePNG(rep, to: path)
    try writeSpriteMeta(for: path)
}

func drawPanelFrame(fileName: String) throws {
    let rep = try render(size: CGSize(width: 760, height: 360)) {
        let outer = NSRect(x: 8, y: 8, width: 744, height: 344)
        let inner = NSRect(x: 28, y: 28, width: 704, height: 304)
        drawRoundedRect(outer, radius: 44, fill: color(0x000000, alpha: 0.06), stroke: color(0xffd36a, alpha: 0.92), strokeWidth: 8)
        drawRoundedRect(inner, radius: 34, fill: color(0x000000, alpha: 0.00), stroke: color(0xffffff, alpha: 0.16), strokeWidth: 2)
        for corner in [
            CGPoint(x: 58, y: 58),
            CGPoint(x: 702, y: 58),
            CGPoint(x: 58, y: 302),
            CGPoint(x: 702, y: 302)
        ] {
            drawCrystal(corner, size: 18, fill: color(0xffe38b, alpha: 0.95), stroke: color(0xffffff, alpha: 0.42))
            drawGlowOval(center: corner, radiusX: 42, radiusY: 42, color: color(0xffb94d, alpha: 0.45), layers: 8)
        }
    }

    let path = "\(outputRoot)/\(fileName).png"
    try savePNG(rep, to: path)
    try writeSpriteMeta(for: path)
}

func drawPolygon(_ points: [CGPoint], fill: NSColor, stroke: NSColor? = nil, lineWidth: CGFloat = 2) {
    guard let first = points.first else {
        return
    }

    let path = NSBezierPath()
    path.move(to: first)
    for point in points.dropFirst() {
        path.line(to: point)
    }

    path.close()
    fill.setFill()
    path.fill()
    if let stroke {
        path.lineWidth = lineWidth
        stroke.setStroke()
        path.stroke()
    }
}

func drawCaveDoor(_ rect: NSRect, accent: NSColor) {
    let path = NSBezierPath()
    path.move(to: CGPoint(x: rect.minX, y: rect.minY))
    path.line(to: CGPoint(x: rect.minX, y: rect.midY))
    path.curve(
        to: CGPoint(x: rect.maxX, y: rect.midY),
        controlPoint1: CGPoint(x: rect.minX + rect.width * 0.08, y: rect.maxY),
        controlPoint2: CGPoint(x: rect.maxX - rect.width * 0.08, y: rect.maxY)
    )
    path.line(to: CGPoint(x: rect.maxX, y: rect.minY))
    path.close()
    color(0x02070b, alpha: 0.88).setFill()
    path.fill()
    path.lineWidth = 3
    accent.withAlphaComponent(0.74).setStroke()
    path.stroke()
    drawGlowOval(center: CGPoint(x: rect.midX, y: rect.minY + rect.height * 0.28), radiusX: rect.width * 0.34, radiusY: rect.height * 0.12, color: accent.withAlphaComponent(0.52), layers: 7)
}

func drawGear(center: CGPoint, radius: CGFloat, accent: NSColor) {
    let teeth = 12
    let path = NSBezierPath()
    for i in 0..<(teeth * 2) {
        let angle = CGFloat(i) * .pi / CGFloat(teeth)
        let r = i % 2 == 0 ? radius : radius * 0.78
        let point = CGPoint(x: center.x + cos(angle) * r, y: center.y + sin(angle) * r)
        if i == 0 {
            path.move(to: point)
        } else {
            path.line(to: point)
        }
    }

    path.close()
    color(0x172130, alpha: 0.92).setFill()
    path.fill()
    path.lineWidth = 4
    accent.withAlphaComponent(0.70).setStroke()
    path.stroke()
    let hub = NSBezierPath(ovalIn: NSRect(x: center.x - radius * 0.26, y: center.y - radius * 0.26, width: radius * 0.52, height: radius * 0.52))
    color(0x071019, alpha: 0.92).setFill()
    hub.fill()
    hub.lineWidth = 3
    color(0xffffff, alpha: 0.18).setStroke()
    hub.stroke()
}

func drawBook(_ rect: NSRect, cover: NSColor, page: NSColor) {
    drawRoundedRect(rect, radius: 7, fill: cover.withAlphaComponent(0.92), stroke: color(0xffffff, alpha: 0.16), strokeWidth: 2)
    let pageRect = rect.insetBy(dx: rect.width * 0.16, dy: rect.height * 0.16)
    drawRoundedRect(pageRect, radius: 4, fill: page.withAlphaComponent(0.82), stroke: color(0x2f2540, alpha: 0.45), strokeWidth: 1)
    strokeLine(CGPoint(x: rect.midX, y: pageRect.minY + 4), CGPoint(x: rect.midX, y: pageRect.maxY - 4), color: color(0x2d2340, alpha: 0.50), width: 1.5)
}

func drawShelf(_ rect: NSRect, accent: NSColor) {
    drawRoundedRect(rect, radius: 8, fill: color(0x170e19, alpha: 0.84), stroke: accent.withAlphaComponent(0.28), strokeWidth: 2)
    for row in 0..<3 {
        let y = rect.minY + CGFloat(row + 1) * rect.height / 4
        strokeLine(CGPoint(x: rect.minX + 12, y: y), CGPoint(x: rect.maxX - 12, y: y), color: color(0xe8c07a, alpha: 0.20), width: 3)
        for col in 0..<10 {
            let w = CGFloat(8 + (col + row) % 3 * 4)
            let h = CGFloat(22 + (col * 7 + row * 3) % 18)
            let x = rect.minX + 18 + CGFloat(col) * 24
            let bookRect = NSRect(x: x, y: y - h + 2, width: w, height: h)
            drawRoundedRect(bookRect, radius: 2, fill: [color(0x6c294f), color(0x1f5c77), color(0x805b2d), accent][(col + row) % 4].withAlphaComponent(0.82), stroke: color(0xffffff, alpha: 0.08), strokeWidth: 1)
        }
    }
}

func drawSword(center: CGPoint, length: CGFloat, accent: NSColor) {
    strokeLine(CGPoint(x: center.x - length * 0.34, y: center.y - length * 0.34), CGPoint(x: center.x + length * 0.34, y: center.y + length * 0.34), color: color(0xeaf4ff, alpha: 0.72), width: 7)
    strokeLine(CGPoint(x: center.x - length * 0.13, y: center.y - length * 0.23), CGPoint(x: center.x - length * 0.24, y: center.y - length * 0.12), color: accent.withAlphaComponent(0.86), width: 9)
    drawCrystal(CGPoint(x: center.x + length * 0.39, y: center.y + length * 0.39), size: length * 0.08, fill: color(0xffffff, alpha: 0.78), stroke: accent.withAlphaComponent(0.58))
}

func drawDungeonCard(fileName: String, accent: NSColor, mood: Int) throws {
    let rep = try render(size: CGSize(width: 760, height: 360)) {
        let rect = NSRect(x: 0, y: 0, width: 760, height: 360)
        drawRoundedRect(rect.insetBy(dx: 12, dy: 12), radius: 42, fill: color(0x071019, alpha: 0.96), stroke: accent.withAlphaComponent(0.72), strokeWidth: 5)
        drawGlowOval(center: CGPoint(x: 380, y: 178), radiusX: 350, radiusY: 140, color: accent.withAlphaComponent(0.70), layers: 16)
        drawRoundedRect(NSRect(x: 38, y: 42, width: 684, height: 276), radius: 30, fill: color(0x0a1018, alpha: 0.62), stroke: color(0xffffff, alpha: 0.10), strokeWidth: 2)

        switch mood {
        case 0:
            fillGradient(NSRect(x: 44, y: 48, width: 672, height: 264), top: color(0x0b1722), bottom: color(0x140b0a))
            for i in 0..<11 {
                let x = CGFloat(54 + i * 66)
                drawPolygon([
                    CGPoint(x: x, y: 306),
                    CGPoint(x: x + CGFloat(16 + i % 3 * 7), y: 306),
                    CGPoint(x: x + CGFloat(8 + i % 4 * 3), y: CGFloat(226 - i % 3 * 20))
                ], fill: color(0x1c1514, alpha: 0.96), stroke: color(0xffffff, alpha: 0.05), lineWidth: 1)
            }
            drawPolygon([
                CGPoint(x: 46, y: 56), CGPoint(x: 126, y: 242), CGPoint(x: 194, y: 306), CGPoint(x: 242, y: 56)
            ], fill: color(0x08090d, alpha: 0.72))
            drawPolygon([
                CGPoint(x: 714, y: 56), CGPoint(x: 646, y: 236), CGPoint(x: 560, y: 306), CGPoint(x: 510, y: 56)
            ], fill: color(0x08090d, alpha: 0.72))
            let doorColors = [color(0xff7640), color(0x60caff), color(0xd4c28a), color(0xffdf78), color(0x9d76ff)]
            for i in 0..<5 {
                drawCaveDoor(NSRect(x: CGFloat(126 + i * 106), y: 70, width: 62, height: 106), accent: doorColors[i])
            }
            drawSword(center: CGPoint(x: 522, y: 198), length: 100, accent: color(0xffdf78))
            drawGear(center: CGPoint(x: 234, y: 204), radius: 34, accent: color(0x60caff))
        case 1:
            fillGradient(NSRect(x: 44, y: 48, width: 672, height: 264), top: color(0x0b1f2a), bottom: color(0x070910))
            for i in 0..<6 {
                let y = CGFloat(78 + i * 38)
                strokeLine(CGPoint(x: 64, y: y), CGPoint(x: 700, y: y + CGFloat(i % 2 == 0 ? 14 : -10)), color: color(0x465360, alpha: 0.42), width: CGFloat(9 + i % 2 * 4))
            }
            drawGear(center: CGPoint(x: 168, y: 206), radius: 58, accent: accent)
            drawGear(center: CGPoint(x: 584, y: 166), radius: 48, accent: color(0xff7b4f))
            drawRoundedRect(NSRect(x: 292, y: 74, width: 176, height: 164), radius: 18, fill: color(0x101722, alpha: 0.92), stroke: accent.withAlphaComponent(0.52), strokeWidth: 4)
            drawGlowOval(center: CGPoint(x: 380, y: 104), radiusX: 120, radiusY: 38, color: color(0xff8d44, alpha: 0.50), layers: 9)
            for i in 0..<4 {
                strokeLine(CGPoint(x: CGFloat(316 + i * 44), y: 228), CGPoint(x: CGFloat(302 + i * 52), y: 300), color: accent.withAlphaComponent(0.40), width: 6)
            }
        case 2:
            fillGradient(NSRect(x: 44, y: 48, width: 672, height: 264), top: color(0x160d20), bottom: color(0x07070d))
            drawShelf(NSRect(x: 66, y: 74, width: 220, height: 210), accent: accent)
            drawShelf(NSRect(x: 474, y: 74, width: 220, height: 210), accent: accent)
            drawGlowOval(center: CGPoint(x: 380, y: 168), radiusX: 168, radiusY: 92, color: accent.withAlphaComponent(0.34), layers: 11)
            drawBook(NSRect(x: 320, y: 112, width: 120, height: 92), cover: color(0x57318c), page: color(0xf4d98c))
            drawRuneCircle(center: CGPoint(x: 380, y: 178), radius: 88, accent: accent, alpha: 0.34)
            strokeLine(CGPoint(x: 380, y: 258), CGPoint(x: 380, y: 296), color: color(0xffd985, alpha: 0.50), width: 4)
            drawGlowOval(center: CGPoint(x: 380, y: 248), radiusX: 56, radiusY: 20, color: color(0xffd985, alpha: 0.38), layers: 8)
        case 3:
            fillGradient(NSRect(x: 44, y: 48, width: 672, height: 264), top: color(0x35120b), bottom: color(0x080409))
            drawPolygon([
                CGPoint(x: 42, y: 52), CGPoint(x: 180, y: 288), CGPoint(x: 310, y: 52)
            ], fill: color(0x110708, alpha: 0.90))
            drawPolygon([
                CGPoint(x: 718, y: 52), CGPoint(x: 584, y: 286), CGPoint(x: 456, y: 52)
            ], fill: color(0x110708, alpha: 0.90))
            drawPolygon([
                CGPoint(x: 260, y: 58), CGPoint(x: 344, y: 250), CGPoint(x: 418, y: 250), CGPoint(x: 506, y: 58)
            ], fill: color(0xff4a21, alpha: 0.60), stroke: color(0xffd070, alpha: 0.38), lineWidth: 4)
            drawGlowOval(center: CGPoint(x: 384, y: 102), radiusX: 156, radiusY: 50, color: color(0xff6e36, alpha: 0.62), layers: 10)
            strokeLine(CGPoint(x: 74, y: 76), CGPoint(x: 682, y: 96), color: color(0xffbd69, alpha: 0.36), width: 12)
            drawSword(center: CGPoint(x: 560, y: 212), length: 116, accent: color(0xffa45c))
            drawCrystal(CGPoint(x: 214, y: 150), size: 48, fill: color(0x2a1715, alpha: 0.96), stroke: color(0xff8c4a, alpha: 0.62))
        case 4:
            fillGradient(NSRect(x: 44, y: 48, width: 672, height: 264), top: color(0x0f2335), bottom: color(0x050810))
            for i in 0..<5 {
                let x = CGFloat(106 + i * 132)
                drawRoundedRect(NSRect(x: x, y: 74, width: 42, height: 190), radius: 10, fill: color(0x111a27, alpha: 0.92), stroke: accent.withAlphaComponent(0.34), strokeWidth: 3)
                drawCrystal(CGPoint(x: x + 21, y: 278), size: 22, fill: accent.withAlphaComponent(0.82), stroke: color(0xffffff, alpha: 0.28))
            }
            drawPolygon([
                CGPoint(x: 224, y: 70), CGPoint(x: 314, y: 214), CGPoint(x: 446, y: 214), CGPoint(x: 536, y: 70)
            ], fill: color(0x172338, alpha: 0.82), stroke: color(0xdff7ff, alpha: 0.20), lineWidth: 3)
            drawGlowOval(center: CGPoint(x: 382, y: 154), radiusX: 180, radiusY: 80, color: accent.withAlphaComponent(0.32), layers: 10)
            for i in 0..<9 {
                drawCrystal(CGPoint(x: CGFloat(84 + i * 76), y: CGFloat(88 + (i * 43) % 150)), size: CGFloat(14 + i % 3 * 6), fill: accent.withAlphaComponent(0.78), stroke: color(0xffffff, alpha: 0.26))
            }
        default:
            fillGradient(NSRect(x: 44, y: 48, width: 672, height: 264), top: color(0x180c31), bottom: color(0x030309))
            drawPolygon([
                CGPoint(x: 318, y: 64), CGPoint(x: 340, y: 242), CGPoint(x: 380, y: 306), CGPoint(x: 424, y: 242), CGPoint(x: 446, y: 64)
            ], fill: color(0x080712, alpha: 0.92), stroke: accent.withAlphaComponent(0.42), lineWidth: 4)
            for i in 0..<5 {
                let y = CGFloat(98 + i * 34)
                strokeLine(CGPoint(x: 350, y: y), CGPoint(x: 414, y: y), color: color(0xf0d9ff, alpha: 0.20), width: 3)
            }
            drawRuneCircle(center: CGPoint(x: 384, y: 172), radius: 118, accent: accent, alpha: 0.38)
            drawBook(NSRect(x: 156, y: 158, width: 76, height: 58), cover: color(0x56377b), page: color(0xdec4ff))
            drawBook(NSRect(x: 526, y: 132, width: 78, height: 60), cover: color(0x263c73), page: color(0xf5e3a6))
            drawGlowOval(center: CGPoint(x: 210, y: 228), radiusX: 60, radiusY: 24, color: accent.withAlphaComponent(0.34), layers: 7)
            drawGlowOval(center: CGPoint(x: 566, y: 210), radiusX: 68, radiusY: 28, color: color(0x8fffd9, alpha: 0.26), layers: 7)
        }
    }

    let path = "\(outputRoot)/\(fileName).png"
    try savePNG(rep, to: path)
    try writeSpriteMeta(for: path)
}

func drawBattleBackdrop(fileName: String, top: NSColor, bottom: NSColor, accent: NSColor, mood: Int) throws {
    let rep = try render(size: CGSize(width: 1170, height: 2532)) {
        let rect = NSRect(x: 0, y: 0, width: 1170, height: 2532)
        fillGradient(rect, top: top, bottom: bottom)
        drawGlowOval(center: CGPoint(x: 585, y: 1450), radiusX: 520, radiusY: 720, color: accent.withAlphaComponent(0.34), layers: 26)
        drawGlowOval(center: CGPoint(x: 585, y: 520), radiusX: 620, radiusY: 260, color: accent.withAlphaComponent(0.22), layers: 18)

        switch mood {
        case 3:
            for i in 0..<11 {
                let x = CGFloat(60 + i * 112)
                strokeLine(CGPoint(x: x, y: 0), CGPoint(x: x + CGFloat(i % 2 == 0 ? 120 : -90), y: 2040), color: color(0x45100b, alpha: 0.56), width: CGFloat(28 + i % 3 * 9))
            }
            for i in 0..<16 {
                drawGlowOval(center: CGPoint(x: CGFloat((i * 173) % 1080 + 45), y: CGFloat(280 + (i * 149) % 1680)), radiusX: 72, radiusY: 34, color: accent.withAlphaComponent(0.36), layers: 8)
            }
        case 4:
            for i in 0..<14 {
                let x = CGFloat(50 + i * 82)
                strokeLine(CGPoint(x: x, y: 120), CGPoint(x: x + CGFloat(i % 2 == 0 ? 34 : -44), y: 2180), color: color(0x1b2434, alpha: 0.72), width: CGFloat(18 + i % 3 * 5))
            }
            for i in 0..<18 {
                drawCrystal(CGPoint(x: CGFloat((i * 127) % 1050 + 60), y: CGFloat(360 + (i * 211) % 1740)), size: CGFloat(22 + i % 5 * 8), fill: accent.withAlphaComponent(0.70), stroke: color(0xffffff, alpha: 0.22))
            }
        default:
            drawRuneCircle(center: CGPoint(x: 585, y: 1370), radius: 330, accent: accent, alpha: 0.22)
            drawRuneCircle(center: CGPoint(x: 585, y: 1370), radius: 220, accent: accent, alpha: 0.28)
            for i in 0..<18 {
                let x = CGFloat((i * 149) % 1080 + 45)
                strokeLine(CGPoint(x: x, y: CGFloat(240 + (i * 91) % 360)), CGPoint(x: x + CGFloat(i % 2 == 0 ? 120 : -120), y: CGFloat(1620 + (i * 101) % 620)), color: accent.withAlphaComponent(0.24), width: CGFloat(5 + i % 4))
            }
        }
    }

    let path = "\(battleOutputRoot)/\(fileName).png"
    try savePNG(rep, to: path)
    try writeSpriteMeta(for: path)
}

func drawFloorNode(fileName: String, fill: NSColor, stroke: NSColor, core: NSColor) throws {
    let rep = try render(size: CGSize(width: 180, height: 180)) {
        let center = CGPoint(x: 90, y: 90)
        drawGlowOval(center: center, radiusX: 80, radiusY: 80, color: core.withAlphaComponent(0.60), layers: 12)
        let ring = NSBezierPath(ovalIn: NSRect(x: 20, y: 20, width: 140, height: 140))
        fill.setFill()
        ring.fill()
        ring.lineWidth = 7
        stroke.setStroke()
        ring.stroke()
        drawCrystal(center, size: 46, fill: core.withAlphaComponent(0.90), stroke: color(0xffffff, alpha: 0.45))
    }

    let path = "\(outputRoot)/\(fileName).png"
    try savePNG(rep, to: path)
    try writeSpriteMeta(for: path)
}

try FileManager.default.createDirectory(atPath: outputRoot, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: battleOutputRoot, withIntermediateDirectories: true)
try writeFolderMeta(outputRoot)
try writeFolderMeta(battleOutputRoot)

let backgroundSource = try loadSourceImage("\(generatedBackgroundRoot)/AbyssalGrimoireSpire_Generated.png")
let background = try render(size: CGSize(width: 1080, height: 1920)) {
    let rect = NSRect(x: 0, y: 0, width: 1080, height: 1920)
    drawImageCover(backgroundSource, in: rect, focusY: 0.52, zoom: 1.0)
    color(0x020307, alpha: 0.54).setFill()
    rect.fill()
    fillGradient(NSRect(x: 0, y: 0, width: 1080, height: 620), top: color(0x000000, alpha: 0.76), bottom: color(0x000000, alpha: 0.08))
    fillGradient(NSRect(x: 0, y: 1300, width: 1080, height: 620), top: color(0x000000, alpha: 0.06), bottom: color(0x000000, alpha: 0.70))
    drawGlowOval(center: CGPoint(x: 540, y: 1120), radiusX: 380, radiusY: 520, color: color(0x9b6bff, alpha: 0.20), layers: 18)
}
let backgroundPath = "\(outputRoot)/DungeonSelectBackground.png"
try savePNG(background, to: backgroundPath)
try writeSpriteMeta(for: backgroundPath)

try drawPanelFrame(fileName: "DungeonCardFrame_Elite")
try drawIllustratedDungeonCard(fileName: "DungeonCard_BlightCavern", sourcePath: "\(generatedBackgroundRoot)/BlightCavern_Generated.png", accent: color(0xff6e36), mood: 0, focusY: 0.52, zoom: 1.0)
try drawIllustratedDungeonCard(fileName: "DungeonCard_GearCrypt", sourcePath: "\(generatedBackgroundRoot)/GearCrypt_Generated.png", accent: color(0x39c8ff), mood: 1, focusY: 0.54, zoom: 1.0)
try drawIllustratedDungeonCard(fileName: "DungeonCard_CurseLibrary", sourcePath: "\(generatedBackgroundRoot)/CurseLibrary_Generated.png", accent: color(0xae5cff), mood: 2, focusY: 0.52, zoom: 1.0)
try drawIllustratedDungeonCard(fileName: "DungeonCard_EmberDrakePass", sourcePath: "\(generatedBackgroundRoot)/EmberDrakePass_Generated.png", accent: color(0xff6e36), mood: 3, focusY: 0.52, zoom: 1.0)
try drawIllustratedDungeonCard(fileName: "DungeonCard_StarOreCitadel", sourcePath: "\(generatedBackgroundRoot)/StarOreCitadel_Generated.png", accent: color(0x79d7ff), mood: 4, focusY: 0.52, zoom: 1.0)
try drawIllustratedDungeonCard(fileName: "DungeonCard_AbyssalGrimoireSpire", sourcePath: "\(generatedBackgroundRoot)/AbyssalGrimoireSpire_Generated.png", accent: color(0xca78ff), mood: 5, focusY: 0.52, zoom: 1.0)
try drawFloorNode(fileName: "FloorNodeUnlocked", fill: color(0x101821, alpha: 0.96), stroke: color(0xd0a44d, alpha: 0.95), core: color(0xffd15c))
try drawFloorNode(fileName: "FloorNodeSelected", fill: color(0x10251e, alpha: 0.96), stroke: color(0x7cffc8, alpha: 0.95), core: color(0x4dffb0))
try drawFloorNode(fileName: "FloorNodeLocked", fill: color(0x101218, alpha: 0.76), stroke: color(0x55606c, alpha: 0.86), core: color(0x8d98a6, alpha: 0.60))
try drawIllustratedBattleBackdrop(fileName: "dungeon4_1170x2532", sourcePath: "\(generatedBackgroundRoot)/EmberDrakePass_Generated.png", accent: color(0xff6032), focusY: 0.52)
try drawIllustratedBattleBackdrop(fileName: "dungeon5_1170x2532", sourcePath: "\(generatedBackgroundRoot)/StarOreCitadel_Generated.png", accent: color(0x7bdfff), focusY: 0.52)
try drawIllustratedBattleBackdrop(fileName: "dungeon6_1170x2532", sourcePath: "\(generatedBackgroundRoot)/AbyssalGrimoireSpire_Generated.png", accent: color(0xc56bff), focusY: 0.52)

print("Generated dungeon selection assets in \(outputRoot)")
