import AppKit
import Foundation

let unityRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/GachaPage"
let homeMenuRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame/Assets/Resources/UI/HomeMenu"
let sourceRoot = "/Users/andou/Desktop/あ/アセット画像/ガチャ関係画像/ChatGPT_2026-05-11"

func color(_ hex: UInt32, alpha: CGFloat = 1.0) -> NSColor {
    let r = CGFloat((hex >> 16) & 0xff) / 255.0
    let g = CGFloat((hex >> 8) & 0xff) / 255.0
    let b = CGFloat(hex & 0xff) / 255.0
    return NSColor(deviceRed: r, green: g, blue: b, alpha: alpha)
}

func guid() -> String {
    UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
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

func oval(_ rect: NSRect, fill: NSColor, stroke: NSColor? = nil, lineWidth: CGFloat = 1) {
    let path = NSBezierPath(ovalIn: rect)
    fill.setFill()
    path.fill()
    if let stroke {
        path.lineWidth = lineWidth
        stroke.setStroke()
        path.stroke()
    }
}

func glow(center: CGPoint, radiusX: CGFloat, radiusY: CGFloat, color: NSColor, layers: Int) {
    for layer in stride(from: layers, through: 1, by: -1) {
        let p = CGFloat(layer) / CGFloat(layers)
        oval(NSRect(x: center.x - radiusX * p, y: center.y - radiusY * p, width: radiusX * p * 2, height: radiusY * p * 2),
             fill: color.withAlphaComponent(color.alphaComponent * (0.045 + (1 - p) * 0.12)))
    }
}

func spark(center: CGPoint, size: CGFloat, color: NSColor) {
    line([CGPoint(x: center.x - size, y: center.y), CGPoint(x: center.x + size, y: center.y)], color: color, width: max(1, size * 0.16))
    line([CGPoint(x: center.x, y: center.y - size), CGPoint(x: center.x, y: center.y + size)], color: color, width: max(1, size * 0.16))
}

func drawText(_ text: String, in rect: NSRect, size: CGFloat, color: NSColor, weight: NSFont.Weight = .bold) {
    let paragraph = NSMutableParagraphStyle()
    paragraph.alignment = .center
    let font = NSFont.systemFont(ofSize: size, weight: weight)
    let attrs: [NSAttributedString.Key: Any] = [
        .font: font,
        .foregroundColor: color,
        .paragraphStyle: paragraph
    ]
    NSString(string: text).draw(in: rect, withAttributes: attrs)
}

func render(size: CGSize, opaque: Bool = false, draw: () -> Void) throws -> NSBitmapImageRep {
    let image = NSImage(size: size)
    image.lockFocus()
    (opaque ? color(0x101420) : NSColor.clear).setFill()
    NSRect(origin: .zero, size: size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    draw()
    image.unlockFocus()

    guard let data = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "GachaUiAssets", code: 1, userInfo: [NSLocalizedDescriptionKey: "render failed"])
    }
    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    try FileManager.default.createDirectory(atPath: URL(fileURLWithPath: path).deletingLastPathComponent().path, withIntermediateDirectories: true)
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "GachaUiAssets", code: 2, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
    }
    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func ensureSpriteMeta(for path: String, pixelsPerUnit: Int = 100) throws {
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
    enableMipMap: 1
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
  spritePixelsToUnits: \(pixelsPerUnit)
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

func saveAsset(_ name: String, rep: NSBitmapImageRep, effects: Bool = false) throws {
    let relative = effects ? "Effects/\(name).png" : "\(name).png"
    let unityPath = "\(unityRoot)/\(relative)"
    let sourcePath = "\(sourceRoot)/\(relative)"
    try savePNG(rep, to: unityPath)
    try savePNG(rep, to: sourcePath)
    try ensureSpriteMeta(for: unityPath)
}

let ink = color(0x141722)
let deep = color(0x20263a)
let purple = color(0x6354d9)
let cyan = color(0x35dfff)
let gold = color(0xffd76a)
let rose = color(0xff6c9c)

try FileManager.default.createDirectory(atPath: unityRoot, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: "\(unityRoot)/Effects", withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: sourceRoot, withIntermediateDirectories: true)

let background = try render(size: CGSize(width: 1080, height: 1920), opaque: true) {
    rounded(NSRect(x: 0, y: 0, width: 1080, height: 1920), radius: 0, fill: color(0x121827))
    for y in stride(from: 0, through: 1920, by: 96) {
        rounded(NSRect(x: 0, y: CGFloat(y), width: 1080, height: 2), radius: 0, fill: color(0x2a3658, alpha: 0.32))
    }
    glow(center: CGPoint(x: 540, y: 1190), radiusX: 520, radiusY: 520, color: purple.withAlphaComponent(0.95), layers: 18)
    glow(center: CGPoint(x: 540, y: 1185), radiusX: 350, radiusY: 350, color: cyan.withAlphaComponent(0.65), layers: 12)
    for i in 0..<42 {
        let x = CGFloat((i * 173) % 1020 + 30)
        let y = CGFloat((i * 257) % 1740 + 90)
        spark(center: CGPoint(x: x, y: y), size: CGFloat(4 + (i % 5)), color: (i % 3 == 0 ? gold : cyan).withAlphaComponent(0.48))
    }
}
try saveAsset("GachaBackground", rep: background)

let mainFrame = try render(size: CGSize(width: 960, height: 1680)) {
    rounded(NSRect(x: 18, y: 18, width: 924, height: 1644), radius: 28, fill: ink.withAlphaComponent(0.93), stroke: gold.withAlphaComponent(0.68), lineWidth: 8)
    rounded(NSRect(x: 42, y: 42, width: 876, height: 1596), radius: 22, fill: deep.withAlphaComponent(0.72), stroke: cyan.withAlphaComponent(0.22), lineWidth: 3)
    rounded(NSRect(x: 70, y: 78, width: 820, height: 8), radius: 4, fill: gold.withAlphaComponent(0.55))
    rounded(NSRect(x: 70, y: 1594, width: 820, height: 8), radius: 4, fill: cyan.withAlphaComponent(0.34))
}
try saveAsset("GachaMainFrame", rep: mainFrame)

let banner = try render(size: CGSize(width: 780, height: 210)) {
    rounded(NSRect(x: 10, y: 18, width: 760, height: 174), radius: 24, fill: color(0x2a2143, alpha: 0.94), stroke: gold.withAlphaComponent(0.82), lineWidth: 6)
    glow(center: CGPoint(x: 390, y: 106), radiusX: 330, radiusY: 70, color: rose.withAlphaComponent(0.55), layers: 8)
    line([CGPoint(x: 70, y: 56), CGPoint(x: 710, y: 56)], color: cyan.withAlphaComponent(0.45), width: 4)
    line([CGPoint(x: 70, y: 154), CGPoint(x: 710, y: 154)], color: gold.withAlphaComponent(0.55), width: 4)
    drawText("召喚祭", in: NSRect(x: 0, y: 72, width: 780, height: 76), size: 56, color: color(0xffe29a))
}
try saveAsset("GachaBannerFrame", rep: banner)

let portal = try render(size: CGSize(width: 680, height: 680)) {
    let c = CGPoint(x: 340, y: 340)
    glow(center: c, radiusX: 300, radiusY: 300, color: purple.withAlphaComponent(0.88), layers: 18)
    glow(center: c, radiusX: 210, radiusY: 210, color: cyan.withAlphaComponent(0.72), layers: 12)
    for ring in 0..<4 {
        let inset = CGFloat(80 + ring * 44)
        oval(NSRect(x: inset, y: inset, width: 680 - inset * 2, height: 680 - inset * 2),
             fill: NSColor.clear, stroke: (ring % 2 == 0 ? gold : cyan).withAlphaComponent(0.72), lineWidth: CGFloat(8 - ring))
    }
    for i in 0..<18 {
        let a = CGFloat(i) * .pi * 2 / 18
        let p1 = CGPoint(x: c.x + cos(a) * 105, y: c.y + sin(a) * 105)
        let p2 = CGPoint(x: c.x + cos(a + 0.16) * 255, y: c.y + sin(a + 0.16) * 255)
        line([p1, p2], color: (i % 2 == 0 ? gold : cyan).withAlphaComponent(0.5), width: 3)
    }
}
try saveAsset("GachaPortal", rep: portal)

let capsule = try render(size: CGSize(width: 280, height: 280)) {
    glow(center: CGPoint(x: 140, y: 140), radiusX: 125, radiusY: 125, color: gold.withAlphaComponent(0.62), layers: 10)
    oval(NSRect(x: 54, y: 40, width: 172, height: 200), fill: color(0xf5efff), stroke: color(0xffdf7a), lineWidth: 8)
    rounded(NSRect(x: 58, y: 40, width: 164, height: 98), radius: 42, fill: color(0x5538bd), stroke: color(0xffdf7a), lineWidth: 5)
    rounded(NSRect(x: 78, y: 134, width: 124, height: 12), radius: 6, fill: color(0xffdf7a))
    spark(center: CGPoint(x: 184, y: 194), size: 18, color: color(0xffffff, alpha: 0.92))
}
try saveAsset("GachaCapsule", rep: capsule)

let resultSlot = try render(size: CGSize(width: 760, height: 230)) {
    rounded(NSRect(x: 12, y: 12, width: 736, height: 206), radius: 22, fill: color(0x171b29, alpha: 0.92), stroke: cyan.withAlphaComponent(0.48), lineWidth: 5)
    rounded(NSRect(x: 40, y: 38, width: 680, height: 154), radius: 14, fill: color(0x222a42, alpha: 0.62), stroke: gold.withAlphaComponent(0.24), lineWidth: 2)
}
try saveAsset("GachaResultSlot", rep: resultSlot)

let pullButton = try render(size: CGSize(width: 360, height: 116)) {
    rounded(NSRect(x: 8, y: 8, width: 344, height: 100), radius: 20, fill: color(0xa95b19), stroke: gold.withAlphaComponent(0.88), lineWidth: 5)
    rounded(NSRect(x: 24, y: 72, width: 312, height: 18), radius: 9, fill: color(0xffe094, alpha: 0.28))
    rounded(NSRect(x: 24, y: 24, width: 312, height: 10), radius: 5, fill: color(0x5b2d13, alpha: 0.45))
}
try saveAsset("GachaPullButton", rep: pullButton)

let smallButton = try render(size: CGSize(width: 300, height: 88)) {
    rounded(NSRect(x: 8, y: 8, width: 284, height: 72), radius: 18, fill: color(0x25314f), stroke: cyan.withAlphaComponent(0.68), lineWidth: 4)
    rounded(NSRect(x: 28, y: 56, width: 244, height: 8), radius: 4, fill: color(0x9beaff, alpha: 0.24))
}
try saveAsset("GachaSmallButton", rep: smallButton)

let ticket = try render(size: CGSize(width: 128, height: 128)) {
    rounded(NSRect(x: 20, y: 26, width: 88, height: 76), radius: 12, fill: color(0xf7d277), stroke: color(0x7a4820), lineWidth: 5)
    rounded(NSRect(x: 32, y: 42, width: 64, height: 44), radius: 8, fill: color(0x5d43c5, alpha: 0.78), stroke: color(0xfff0aa), lineWidth: 3)
    spark(center: CGPoint(x: 64, y: 64), size: 17, color: color(0xffffff, alpha: 0.94))
}
try saveAsset("GachaTicketIcon", rep: ticket)

for frame in 0..<6 {
    let t = CGFloat(frame) / 5
    let sparkle = try render(size: CGSize(width: 720, height: 720)) {
        let c = CGPoint(x: 360, y: 360)
        glow(center: c, radiusX: 210 + t * 80, radiusY: 210 + t * 80, color: cyan.withAlphaComponent(0.48 - t * 0.14), layers: 10)
        for i in 0..<22 {
            let a = CGFloat(i) * .pi * 2 / 22 + t * .pi * 0.35
            let distance = CGFloat(120 + (i % 5) * 38) + t * 48
            spark(center: CGPoint(x: c.x + cos(a) * distance, y: c.y + sin(a) * distance),
                  size: CGFloat(7 + (i % 4) * 3), color: (i % 2 == 0 ? gold : cyan).withAlphaComponent(0.9 - t * 0.18))
        }
    }
    try saveAsset("GachaSparkle_\(frame)", rep: sparkle, effects: true)
}

let homeButton = try render(size: CGSize(width: 500, height: 330)) {
    rounded(NSRect(x: 12, y: 18, width: 476, height: 294), radius: 30, fill: color(0x302157, alpha: 0.96), stroke: gold.withAlphaComponent(0.8), lineWidth: 7)
    glow(center: CGPoint(x: 250, y: 178), radiusX: 190, radiusY: 105, color: purple.withAlphaComponent(0.76), layers: 10)
    oval(NSRect(x: 182, y: 110, width: 136, height: 136), fill: color(0x211934, alpha: 0.52), stroke: cyan.withAlphaComponent(0.76), lineWidth: 6)
    oval(NSRect(x: 210, y: 138, width: 80, height: 80), fill: color(0xffdf7a, alpha: 0.92), stroke: color(0xffffff, alpha: 0.82), lineWidth: 3)
    drawText("ガチャ", in: NSRect(x: 0, y: 36, width: 500, height: 76), size: 54, color: color(0xffe29a))
}
let homePath = "\(homeMenuRoot)/GachaButton.png"
try savePNG(homeButton, to: homePath)
try savePNG(homeButton, to: "\(sourceRoot)/GachaButton.png")
try ensureSpriteMeta(for: homePath)

print("Generated gacha UI assets in \(unityRoot)")
