import AppKit
import Foundation

let outputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/EquipmentEnhance"

func color(_ hex: UInt32, alpha: CGFloat = 1.0) -> NSColor {
    let r = CGFloat((hex >> 16) & 0xff) / 255.0
    let g = CGFloat((hex >> 8) & 0xff) / 255.0
    let b = CGFloat(hex & 0xff) / 255.0
    return NSColor(deviceRed: r, green: g, blue: b, alpha: alpha)
}

func render(size: CGSize, draw: () -> Void) throws -> NSBitmapImageRep {
    let image = NSImage(size: size)
    image.lockFocus()
    NSColor.clear.setFill()
    NSRect(origin: .zero, size: size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    draw()
    image.unlockFocus()

    guard let data = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 1, userInfo: [NSLocalizedDescriptionKey: "render failed"])
    }

    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "EquipmentEnhancementEffects", code: 2, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
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

let runeCircle = try render(size: CGSize(width: 512, height: 512)) {
    let c = CGPoint(x: 256, y: 256)
    drawGlowOval(center: c, radiusX: 230, radiusY: 230, color: cyan.withAlphaComponent(0.62), layers: 16)
    drawGlowOval(center: c, radiusX: 170, radiusY: 170, color: violet.withAlphaComponent(0.36), layers: 10)
    drawRing(center: c, radius: 208, color: cyan.withAlphaComponent(0.58), width: 8)
    drawRing(center: c, radius: 168, color: gold.withAlphaComponent(0.66), width: 5, dash: [18, 10])
    drawRing(center: c, radius: 118, color: whiteGold.withAlphaComponent(0.48), width: 3)
    for i in 0..<16 {
        let a = CGFloat(i) * .pi * 2 / 16
        drawSpark(center: CGPoint(x: c.x + cos(a) * 186, y: c.y + sin(a) * 186), color: gold.withAlphaComponent(0.78), size: 10, width: 2)
    }
    for i in 0..<8 {
        let a = CGFloat(i) * .pi * 2 / 8 + .pi / 8
        strokePath(points: [
            CGPoint(x: c.x + cos(a) * 132, y: c.y + sin(a) * 132),
            CGPoint(x: c.x + cos(a) * 154, y: c.y + sin(a) * 154)
        ], color: cyan.withAlphaComponent(0.72), width: 4)
    }
}
try savePNG(runeCircle, to: "\(outputRoot)/EnhanceRuneCircle.png")
try ensureSpriteMeta(for: "\(outputRoot)/EnhanceRuneCircle.png")

for frame in 0..<8 {
    let t = CGFloat(frame) / 7
    let success = try render(size: CGSize(width: 512, height: 512)) {
        let c = CGPoint(x: 256, y: 256)
        drawGlowOval(center: c, radiusX: 80 + 150 * t, radiusY: 80 + 150 * t, color: gold.withAlphaComponent(0.78 * (1 - t * 0.32)), layers: 12)
        drawRing(center: c, radius: 52 + 155 * t, color: whiteGold.withAlphaComponent(0.82 * (1 - t * 0.18)), width: 10 - 5 * t)
        drawRing(center: c, radius: 96 + 92 * t, color: cyan.withAlphaComponent(0.55), width: 5, dash: [16, 9])
        for i in 0..<20 {
            let a = CGFloat(i) * .pi * 2 / 20 + t * .pi * 0.45
            let d = 52 + 188 * t + CGFloat(i % 3) * 8
            drawSpark(center: CGPoint(x: c.x + cos(a) * d, y: c.y + sin(a) * d), color: i % 2 == 0 ? whiteGold.withAlphaComponent(0.94) : cyan.withAlphaComponent(0.78), size: 8 + 8 * t, width: 2.2)
        }
        strokePath(points: [CGPoint(x: 118, y: 248), CGPoint(x: 214, y: 182), CGPoint(x: 384, y: 342)], color: whiteGold.withAlphaComponent(0.88), width: 16)
        strokePath(points: [CGPoint(x: 118, y: 248), CGPoint(x: 214, y: 182), CGPoint(x: 384, y: 342)], color: gold.withAlphaComponent(0.86), width: 8)
    }
    try savePNG(success, to: "\(outputRoot)/EnhanceSuccess_\(frame).png")
    try ensureSpriteMeta(for: "\(outputRoot)/EnhanceSuccess_\(frame).png")

    let fail = try render(size: CGSize(width: 512, height: 512)) {
        let c = CGPoint(x: 256, y: 256)
        drawGlowOval(center: c, radiusX: 105 + 80 * t, radiusY: 75 + 55 * t, color: smoke.withAlphaComponent(0.62 * (1 - t * 0.25)), layers: 10)
        drawGlowOval(center: CGPoint(x: c.x - 14 + sin(t * .pi) * 24, y: c.y + 4), radiusX: 88 + 70 * t, radiusY: 58 + 40 * t, color: violet.withAlphaComponent(0.28 * (1 - t * 0.1)), layers: 8)
        drawRing(center: c, radius: 70 + 115 * t, color: red.withAlphaComponent(0.42 * (1 - t * 0.18)), width: 7)
        strokePath(points: [
            CGPoint(x: 207, y: 340),
            CGPoint(x: 245, y: 274),
            CGPoint(x: 232, y: 230),
            CGPoint(x: 292, y: 174)
        ], color: red.withAlphaComponent(0.82), width: 9)
        for i in 0..<10 {
            let a = CGFloat(i) * .pi * 2 / 10 + 0.25
            let d = 64 + 120 * t + CGFloat(i % 2) * 12
            drawSpark(center: CGPoint(x: c.x + cos(a) * d, y: c.y + sin(a) * d), color: smoke.withAlphaComponent(0.62), size: 7 + 6 * t, width: 2)
        }
    }
    try savePNG(fail, to: "\(outputRoot)/EnhanceFail_\(frame).png")
    try ensureSpriteMeta(for: "\(outputRoot)/EnhanceFail_\(frame).png")

    let destroy = try render(size: CGSize(width: 512, height: 512)) {
        let c = CGPoint(x: 256, y: 256)
        drawGlowOval(center: c, radiusX: 108 + 120 * t, radiusY: 108 + 120 * t, color: red.withAlphaComponent(0.40 * (1 - t * 0.22)), layers: 12)
        drawGlowOval(center: c, radiusX: 92 + 74 * t, radiusY: 92 + 74 * t, color: black.withAlphaComponent(0.72), layers: 8)
        drawRing(center: c, radius: 72 + 130 * t, color: red.withAlphaComponent(0.76 * (1 - t * 0.22)), width: 10)
        for i in 0..<18 {
            let a = CGFloat(i) * .pi * 2 / 18 + CGFloat(i % 5) * 0.08
            let d = 35 + 198 * t + CGFloat((i * 17) % 29)
            drawShard(center: c, angle: a, distance: d, size: 8 + CGFloat(i % 4) * 3 + t * 5, color: i % 3 == 0 ? red.withAlphaComponent(0.88) : smoke.withAlphaComponent(0.74))
        }
        strokePath(points: [CGPoint(x: 178, y: 190), CGPoint(x: 334, y: 334)], color: red.withAlphaComponent(0.82), width: 12)
        strokePath(points: [CGPoint(x: 178, y: 334), CGPoint(x: 334, y: 190)], color: red.withAlphaComponent(0.82), width: 12)
    }
    try savePNG(destroy, to: "\(outputRoot)/EnhanceDestroy_\(frame).png")
    try ensureSpriteMeta(for: "\(outputRoot)/EnhanceDestroy_\(frame).png")
}

print("Generated equipment enhancement effect assets in \(outputRoot)")
