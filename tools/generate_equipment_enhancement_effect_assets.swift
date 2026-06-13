import AppKit
import Foundation

let outputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/EquipmentEnhance"
let image2SourceRoot = "/Users/andou/Desktop/あ/game-creation/tools/generated_equipment_enhancement_image2_sources"
let successImage2SheetPath = "\(image2SourceRoot)/enhance_success_image2_sheet.png"
let failImage2SheetPath = "\(image2SourceRoot)/enhance_fail_image2_sheet.png"
let destroyImage2SheetPath = "\(image2SourceRoot)/rejected_destroy_image2_sheet_has_monster.png"

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
        bitsPerPixel: 0) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 1, userInfo: [NSLocalizedDescriptionKey: "render allocation failed"])
    }

    rep.size = size
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    NSColor.clear.setFill()
    NSRect(origin: .zero, size: size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    draw()
    NSGraphicsContext.restoreGraphicsState()

    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 2, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func loadBitmap(_ path: String) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOfFile: path),
          let data = try? Data(contentsOf: URL(fileURLWithPath: path)),
          let sourceRep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 3, userInfo: [NSLocalizedDescriptionKey: "image2 sheet load failed: \(path)"])
    }

    let size = CGSize(width: sourceRep.pixelsWide, height: sourceRep.pixelsHigh)
    guard let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: sourceRep.pixelsWide,
        pixelsHigh: sourceRep.pixelsHigh,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 4, userInfo: [NSLocalizedDescriptionKey: "image2 sheet normalize failed: \(path)"])
    }

    rep.size = size
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    NSColor.black.setFill()
    NSRect(origin: .zero, size: size).fill()
    image.draw(
        in: NSRect(origin: .zero, size: size),
        from: NSRect(origin: .zero, size: image.size),
        operation: .copy,
        fraction: 1)
    NSGraphicsContext.restoreGraphicsState()
    return rep
}

func transparentImage2Frame(from sheet: NSBitmapImageRep, frameIndex: Int, alphaBoost: CGFloat = 1.18) throws -> NSBitmapImageRep {
    let columns = 4
    let rows = 2
    let outputSize = 512
    let cellWidth = sheet.pixelsWide / columns
    let cellHeight = sheet.pixelsHigh / rows
    let column = frameIndex % columns
    let rowFromTop = frameIndex / columns
    let scale = min(CGFloat(outputSize) / CGFloat(cellWidth), CGFloat(outputSize) / CGFloat(cellHeight))
    let drawWidth = CGFloat(cellWidth) * scale
    let drawHeight = CGFloat(cellHeight) * scale
    let offsetX = (CGFloat(outputSize) - drawWidth) * 0.5
    let offsetY = (CGFloat(outputSize) - drawHeight) * 0.5

    guard let out = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: outputSize,
        pixelsHigh: outputSize,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 4, userInfo: [NSLocalizedDescriptionKey: "frame bitmap allocation failed"])
    }

    for y in 0..<outputSize {
        for x in 0..<outputSize {
            let localX = (CGFloat(x) - offsetX) / scale
            let localY = (CGFloat(y) - offsetY) / scale
            guard localX >= 0, localX < CGFloat(cellWidth), localY >= 0, localY < CGFloat(cellHeight) else {
                out.setColor(NSColor.clear, atX: x, y: y)
                continue
            }

            let sourceX = min(sheet.pixelsWide - 1, max(0, column * cellWidth + Int(localX)))
            let sourceY = min(sheet.pixelsHigh - 1, max(0, rowFromTop * cellHeight + (cellHeight - 1 - Int(localY))))
            guard let sourceColor = sheet.colorAt(x: sourceX, y: sourceY) else {
                out.setColor(NSColor.clear, atX: x, y: y)
                continue
            }

            var r: CGFloat = 0
            var g: CGFloat = 0
            var b: CGFloat = 0
            var a: CGFloat = 0
            sourceColor.getRed(&r, green: &g, blue: &b, alpha: &a)
            let maxChannel = max(r, max(g, b))
            if maxChannel < 0.018 || a <= 0.001 {
                out.setColor(NSColor.clear, atX: x, y: y)
                continue
            }

            let alpha = min(1, pow(maxChannel, 0.72) * alphaBoost) * a
            let divisor = max(alpha, 0.001)
            out.setColor(NSColor(
                deviceRed: min(1, r / divisor),
                green: min(1, g / divisor),
                blue: min(1, b / divisor),
                alpha: alpha),
                atX: x,
                y: y)
        }
    }

    return out
}

func guid() -> String {
    UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
}

func ensureSpriteMeta(for path: String) throws {
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
    filterMode: 0
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

func drawGlowOval(center: CGPoint, radiusX: CGFloat, radiusY: CGFloat, color: NSColor, layers: Int) {
    guard layers > 0 else { return }
    for layer in stride(from: layers, through: 1, by: -1) {
        let p = CGFloat(layer) / CGFloat(layers)
        color.withAlphaComponent(color.alphaComponent * (0.035 + (1 - p) * 0.09)).setFill()
        NSBezierPath(ovalIn: NSRect(
            x: center.x - radiusX * p,
            y: center.y - radiusY * p,
            width: radiusX * p * 2,
            height: radiusY * p * 2)).fill()
    }
}

func strokePath(points: [CGPoint], color: NSColor, width: CGFloat) {
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

func drawRing(center: CGPoint, radius: CGFloat, color: NSColor, width: CGFloat, dash: [CGFloat]? = nil) {
    let path = NSBezierPath(ovalIn: NSRect(x: center.x - radius, y: center.y - radius, width: radius * 2, height: radius * 2))
    path.lineWidth = width
    if let dash {
        path.setLineDash(dash, count: dash.count, phase: radius * 0.2)
    }
    color.setStroke()
    path.stroke()
}

func drawSpark(center: CGPoint, color: NSColor, size: CGFloat, width: CGFloat = 2) {
    strokePath(points: [CGPoint(x: center.x - size, y: center.y), CGPoint(x: center.x + size, y: center.y)], color: color, width: width)
    strokePath(points: [CGPoint(x: center.x, y: center.y - size), CGPoint(x: center.x, y: center.y + size)], color: color, width: width)
}

func drawShard(center: CGPoint, angle: CGFloat, distance: CGFloat, size: CGFloat, color: NSColor) {
    let x = center.x + cos(angle) * distance
    let y = center.y + sin(angle) * distance
    let tip = CGPoint(x: x + cos(angle) * size * 1.5, y: y + sin(angle) * size * 1.5)
    let left = CGPoint(x: x + cos(angle + 2.35) * size, y: y + sin(angle + 2.35) * size)
    let right = CGPoint(x: x + cos(angle - 2.35) * size, y: y + sin(angle - 2.35) * size)
    let path = NSBezierPath()
    path.move(to: tip)
    path.line(to: left)
    path.line(to: right)
    path.close()
    color.setFill()
    path.fill()
}

try FileManager.default.createDirectory(atPath: outputRoot, withIntermediateDirectories: true)

let cyan = color(0x33eaff)
let blue = color(0x1764ff)
let gold = color(0xffd86b)
let whiteGold = color(0xfff6cf)
let red = color(0xff3d2e)
let violet = color(0xa95cff)
let smoke = color(0x77808f)
let black = color(0x08070b)

let successImage2Sheet = try loadBitmap(successImage2SheetPath)
let failImage2Sheet = try loadBitmap(failImage2SheetPath)
let destroyImage2Sheet = try loadBitmap(destroyImage2SheetPath)
let destroyImage2FrameMap = [2, 2, 2, 5, 5, 6, 6, 7]

let runeCircle = try transparentImage2Frame(from: successImage2Sheet, frameIndex: 7, alphaBoost: 1.08)
try savePNG(runeCircle, to: "\(outputRoot)/EnhanceRuneCircle.png")
try ensureSpriteMeta(for: "\(outputRoot)/EnhanceRuneCircle.png")

for frame in 0..<8 {
    let t = CGFloat(frame) / 7
    let success = try transparentImage2Frame(from: successImage2Sheet, frameIndex: frame, alphaBoost: 1.22)
    try savePNG(success, to: "\(outputRoot)/EnhanceSuccess_\(frame).png")
    try ensureSpriteMeta(for: "\(outputRoot)/EnhanceSuccess_\(frame).png")

    let fail = try transparentImage2Frame(from: failImage2Sheet, frameIndex: frame, alphaBoost: 1.34)
    try savePNG(fail, to: "\(outputRoot)/EnhanceFail_\(frame).png")
    try ensureSpriteMeta(for: "\(outputRoot)/EnhanceFail_\(frame).png")

    let destroy: NSBitmapImageRep
    if frame >= 1 {
        destroy = try transparentImage2Frame(from: destroyImage2Sheet, frameIndex: destroyImage2FrameMap[frame], alphaBoost: 1.30)
    } else {
        destroy = try render(size: CGSize(width: 512, height: 512)) {
        let c = CGPoint(x: 256, y: 256)
        let pulse = sin(t * .pi)
        let flash = max(0, 1 - abs(t - 0.30) * 3.5)
        let fade = max(0.12, 1 - t * 0.62)
        drawGlowOval(center: c, radiusX: 74 + 226 * t, radiusY: 74 + 226 * t, color: red.withAlphaComponent(0.72 * fade), layers: 18)
        drawGlowOval(center: c, radiusX: 42 + 118 * pulse, radiusY: 42 + 118 * pulse, color: whiteGold.withAlphaComponent(0.34 * flash), layers: 8)
        drawGlowOval(center: c, radiusX: 130 + 90 * t, radiusY: 96 + 84 * t, color: black.withAlphaComponent(0.58 * t), layers: 10)
        drawRing(center: c, radius: 46 + 184 * t, color: red.withAlphaComponent(0.92 * fade), width: 18 - 10 * t)
        drawRing(center: c, radius: 78 + 120 * t, color: gold.withAlphaComponent(0.46 * pulse), width: 6, dash: [14, 8])
        for i in 0..<42 {
            let a = CGFloat(i) * .pi * 2 / 42 + CGFloat((i * 11) % 17) * 0.035
            let d = 28 + 258 * t + CGFloat((i * 23) % 37)
            let shardColor = i % 4 == 0
                ? whiteGold.withAlphaComponent(0.82 * fade)
                : (i % 3 == 0 ? smoke.withAlphaComponent(0.62 * fade) : red.withAlphaComponent(0.92 * fade))
            drawShard(center: c, angle: a, distance: d, size: 6 + CGFloat(i % 5) * 3 + t * 8, color: shardColor)
        }
        for i in 0..<34 {
            let a = CGFloat(i) * .pi * 2 / 34 + t * .pi * 0.5
            let d = 34 + 218 * t + CGFloat((i * 13) % 31)
            drawSpark(center: CGPoint(x: c.x + cos(a) * d, y: c.y + sin(a) * d),
                      color: i % 2 == 0 ? gold.withAlphaComponent(0.76 * fade) : red.withAlphaComponent(0.88 * fade),
                      size: 4 + 9 * pulse,
                      width: 2.5)
        }
        strokePath(points: [CGPoint(x: 150, y: 184), CGPoint(x: 220, y: 238), CGPoint(x: 274, y: 206), CGPoint(x: 362, y: 342)], color: red.withAlphaComponent(0.88 * fade), width: 13)
        strokePath(points: [CGPoint(x: 164, y: 342), CGPoint(x: 232, y: 286), CGPoint(x: 300, y: 322), CGPoint(x: 360, y: 188)], color: red.withAlphaComponent(0.82 * fade), width: 12)
        if frame <= 3 {
            drawGlowOval(center: c, radiusX: 34 + 44 * flash, radiusY: 34 + 44 * flash, color: whiteGold.withAlphaComponent(0.74 * flash), layers: 7)
        }
        }
    }
    try savePNG(destroy, to: "\(outputRoot)/EnhanceDestroy_\(frame).png")
    try ensureSpriteMeta(for: "\(outputRoot)/EnhanceDestroy_\(frame).png")
}

print("Generated equipment enhancement effect assets in \(outputRoot)")
