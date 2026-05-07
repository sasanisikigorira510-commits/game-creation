import AppKit
import Foundation

let outputRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/DungeonSelect"

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
    NSGraphicsContext.current?.imageInterpolation = .high
    draw()
    image.unlockFocus()

    guard let data = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "DungeonSelectionAssets", code: 1, userInfo: [NSLocalizedDescriptionKey: "render failed"])
    }

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

func drawDungeonCard(fileName: String, accent: NSColor, mood: Int) throws {
    let rep = try render(size: CGSize(width: 760, height: 360)) {
        let rect = NSRect(x: 0, y: 0, width: 760, height: 360)
        drawRoundedRect(rect.insetBy(dx: 12, dy: 12), radius: 42, fill: color(0x071019, alpha: 0.96), stroke: accent.withAlphaComponent(0.72), strokeWidth: 5)
        drawGlowOval(center: CGPoint(x: 380, y: 178), radiusX: 350, radiusY: 140, color: accent.withAlphaComponent(0.70), layers: 16)
        drawRoundedRect(NSRect(x: 38, y: 42, width: 684, height: 276), radius: 30, fill: color(0x0a1018, alpha: 0.62), stroke: color(0xffffff, alpha: 0.10), strokeWidth: 2)

        switch mood {
        case 0:
            for i in 0..<7 {
                let x = CGFloat(70 + i * 92)
                strokeLine(CGPoint(x: x, y: 52), CGPoint(x: x + CGFloat((i % 2 == 0) ? 16 : -20), y: 276), color: color(0x26130d, alpha: 0.76), width: CGFloat(18 + i % 3 * 3))
                drawCrystal(CGPoint(x: x + 14, y: CGFloat(96 + (i % 4) * 42)), size: CGFloat(14 + i % 3 * 4), fill: accent.withAlphaComponent(0.72), stroke: color(0xffffff, alpha: 0.28))
            }
            drawGlowOval(center: CGPoint(x: 500, y: 115), radiusX: 132, radiusY: 48, color: accent.withAlphaComponent(0.50), layers: 8)
        case 1:
            for i in 0..<8 {
                let x = CGFloat(72 + i * 84)
                strokeLine(CGPoint(x: x, y: 54), CGPoint(x: x, y: 280), color: color(0x27313b, alpha: 0.72), width: 14)
                strokeLine(CGPoint(x: x - 26, y: CGFloat(82 + i % 4 * 48)), CGPoint(x: x + 32, y: CGFloat(118 + i % 3 * 44)), color: accent.withAlphaComponent(0.52), width: 5)
            }
            for i in 0..<5 {
                drawCrystal(CGPoint(x: CGFloat(160 + i * 104), y: CGFloat(108 + (i % 2) * 76)), size: 18, fill: accent.withAlphaComponent(0.82), stroke: color(0xffffff, alpha: 0.22))
            }
        default:
            for i in 0..<7 {
                let cx = CGFloat(96 + i * 92)
                let cy = CGFloat(104 + (i % 3) * 58)
                drawGlowOval(center: CGPoint(x: cx, y: cy), radiusX: 44, radiusY: 26, color: accent.withAlphaComponent(0.36), layers: 7)
                drawCrystal(CGPoint(x: cx, y: cy), size: CGFloat(12 + i % 4 * 3), fill: accent.withAlphaComponent(0.86), stroke: color(0xffffff, alpha: 0.28))
            }
            strokeLine(CGPoint(x: 70, y: 74), CGPoint(x: 688, y: 288), color: accent.withAlphaComponent(0.32), width: 8)
            strokeLine(CGPoint(x: 86, y: 286), CGPoint(x: 700, y: 78), color: accent.withAlphaComponent(0.24), width: 6)
        }
    }

    let path = "\(outputRoot)/\(fileName).png"
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
try writeFolderMeta(outputRoot)

let background = try render(size: CGSize(width: 1080, height: 1920)) {
    fillGradient(NSRect(x: 0, y: 0, width: 1080, height: 1920), top: color(0x071320), bottom: color(0x020407))
    for i in 0..<18 {
        let x = CGFloat((i * 97) % 1080)
        let top = CGFloat(1920 - ((i * 137) % 380))
        strokeLine(CGPoint(x: x, y: 0), CGPoint(x: x + CGFloat((i % 2 == 0) ? 180 : -120), y: top), color: color(0x16364a, alpha: 0.22), width: CGFloat(2 + i % 4))
    }
    drawGlowOval(center: CGPoint(x: 540, y: 1180), radiusX: 420, radiusY: 540, color: color(0x236d91, alpha: 0.45), layers: 22)
    drawGlowOval(center: CGPoint(x: 540, y: 560), radiusX: 480, radiusY: 220, color: color(0x4b1630, alpha: 0.32), layers: 16)
}
let backgroundPath = "\(outputRoot)/DungeonSelectBackground.png"
try savePNG(background, to: backgroundPath)
try writeSpriteMeta(for: backgroundPath)

try drawDungeonCard(fileName: "DungeonCard_BlightCavern", accent: color(0xff5b2f), mood: 0)
try drawDungeonCard(fileName: "DungeonCard_GearCrypt", accent: color(0x39c8ff), mood: 1)
try drawDungeonCard(fileName: "DungeonCard_CurseLibrary", accent: color(0xae5cff), mood: 2)
try drawFloorNode(fileName: "FloorNodeUnlocked", fill: color(0x101821, alpha: 0.96), stroke: color(0xd0a44d, alpha: 0.95), core: color(0xffd15c))
try drawFloorNode(fileName: "FloorNodeSelected", fill: color(0x10251e, alpha: 0.96), stroke: color(0x7cffc8, alpha: 0.95), core: color(0x4dffb0))
try drawFloorNode(fileName: "FloorNodeLocked", fill: color(0x101218, alpha: 0.76), stroke: color(0x55606c, alpha: 0.86), core: color(0x8d98a6, alpha: 0.60))

print("Generated dungeon selection assets in \(outputRoot)")
