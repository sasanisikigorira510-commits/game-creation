import AppKit
import Foundation

let outputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/FusionPage/Effects"

func color(_ hex: UInt32, alpha: CGFloat = 1.0) -> NSColor {
    let r = CGFloat((hex >> 16) & 0xff) / 255.0
    let g = CGFloat((hex >> 8) & 0xff) / 255.0
    let b = CGFloat(hex & 0xff) / 255.0
    return NSColor(deviceRed: r, green: g, blue: b, alpha: alpha)
}

func drawGlowOval(center: CGPoint, radiusX: CGFloat, radiusY: CGFloat, color: NSColor, layers: Int) {
    for layer in stride(from: layers, through: 1, by: -1) {
        let p = CGFloat(layer) / CGFloat(layers)
        color.withAlphaComponent(color.alphaComponent * (0.07 + (1 - p) * 0.08)).setFill()
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

func drawSpark(center: CGPoint, color: NSColor, size: CGFloat) {
    strokePath(points: [CGPoint(x: center.x - size, y: center.y), CGPoint(x: center.x + size, y: center.y)], color: color, width: max(1, size * 0.18))
    strokePath(points: [CGPoint(x: center.x, y: center.y - size), CGPoint(x: center.x, y: center.y + size)], color: color, width: max(1, size * 0.18))
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
        throw NSError(domain: "FusionCeremonyEffects", code: 1, userInfo: [NSLocalizedDescriptionKey: "render failed"])
    }
    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "FusionCeremonyEffects", code: 2, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
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

try FileManager.default.createDirectory(atPath: outputRoot, withIntermediateDirectories: true)

let cyan = color(0x13f7ff)
let gold = color(0xffd46d)
let green = color(0x3cff9a)

let glow = try render(size: CGSize(width: 512, height: 512)) {
    let c = CGPoint(x: 256, y: 256)
    drawGlowOval(center: c, radiusX: 220, radiusY: 220, color: cyan.withAlphaComponent(0.9), layers: 12)
    drawGlowOval(center: c, radiusX: 165, radiusY: 165, color: green.withAlphaComponent(0.55), layers: 8)
    let ring1 = NSBezierPath(ovalIn: NSRect(x: 70, y: 70, width: 372, height: 372))
    ring1.lineWidth = 9
    cyan.withAlphaComponent(0.62).setStroke()
    ring1.stroke()
    let ring2 = NSBezierPath(ovalIn: NSRect(x: 118, y: 118, width: 276, height: 276))
    ring2.lineWidth = 5
    gold.withAlphaComponent(0.72).setStroke()
    ring2.stroke()
    for i in 0..<12 {
        let a = CGFloat(i) * .pi * 2 / 12
        drawSpark(center: CGPoint(x: 256 + cos(a) * 164, y: 256 + sin(a) * 164), color: gold.withAlphaComponent(0.9), size: 10)
    }
}
try savePNG(glow, to: "\(outputRoot)/FusionSlotGlow.png")
try ensureSpriteMeta(for: "\(outputRoot)/FusionSlotGlow.png")

for frame in 0..<8 {
    let t = CGFloat(frame) / 7
    let stream = try render(size: CGSize(width: 512, height: 160)) {
        let y = CGFloat(80)
        drawGlowOval(center: CGPoint(x: 256, y: y), radiusX: 235, radiusY: 46, color: cyan.withAlphaComponent(0.50), layers: 9)
        strokePath(points: [
            CGPoint(x: 32, y: y + sin(t * .pi * 2) * 7),
            CGPoint(x: 150, y: y + cos(t * .pi * 2) * 19),
            CGPoint(x: 278, y: y + sin(t * .pi * 2 + 1.1) * 17),
            CGPoint(x: 480, y: y + cos(t * .pi * 2 + 0.6) * 5)
        ], color: cyan.withAlphaComponent(0.76), width: 18)
        strokePath(points: [
            CGPoint(x: 48, y: y),
            CGPoint(x: 238, y: y + sin(t * .pi * 2 + 0.4) * 7),
            CGPoint(x: 464, y: y)
        ], color: gold.withAlphaComponent(0.82), width: 6)
        for i in 0..<9 {
            let x = 58 + CGFloat(i) * 48 + t * 30
            let wrappedX = x.truncatingRemainder(dividingBy: 420) + 46
            drawSpark(center: CGPoint(x: wrappedX, y: y + ((i % 2 == 0) ? -28 : 28)), color: gold.withAlphaComponent(0.85), size: 5 + t * 3)
        }
    }
    let path = "\(outputRoot)/FusionEnergyStream_\(frame).png"
    try savePNG(stream, to: path)
    try ensureSpriteMeta(for: path)
}

for frame in 0..<8 {
    let t = CGFloat(frame) / 7
    let burst = try render(size: CGSize(width: 768, height: 768)) {
        let c = CGPoint(x: 384, y: 384)
        drawGlowOval(center: c, radiusX: 120 + t * 210, radiusY: 120 + t * 210, color: cyan.withAlphaComponent(0.62 - t * 0.22), layers: 12)
        drawGlowOval(center: c, radiusX: 70 + t * 130, radiusY: 70 + t * 130, color: gold.withAlphaComponent(0.70 - t * 0.18), layers: 8)
        for i in 0..<18 {
            let a = CGFloat(i) * .pi * 2 / 18 + t * 0.28
            let inner = CGPoint(x: c.x + cos(a) * (36 + t * 36), y: c.y + sin(a) * (36 + t * 36))
            let outer = CGPoint(x: c.x + cos(a) * (170 + t * 230), y: c.y + sin(a) * (170 + t * 230))
            strokePath(points: [inner, outer], color: (i % 2 == 0 ? gold : cyan).withAlphaComponent(0.78 - t * 0.45), width: 8 - t * 4)
        }
        for i in 0..<20 {
            let a = CGFloat(i) * 0.77 + t * .pi
            drawSpark(center: CGPoint(x: c.x + cos(a) * (120 + t * 230), y: c.y + sin(a) * (120 + t * 230)), color: gold.withAlphaComponent(0.85 - t * 0.35), size: 8 + t * 8)
        }
    }
    let path = "\(outputRoot)/FusionBirthBurst_\(frame).png"
    try savePNG(burst, to: path)
    try ensureSpriteMeta(for: path)
}

print("Generated fusion ceremony effect assets in \(outputRoot)")
