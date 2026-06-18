import AppKit
import Foundation

let projectRoot = FileManager.default.currentDirectoryPath
let outputRoot = "\(projectRoot)/WitchTowerGame/Assets/Resources/BattleEffects/Defeat"
let sourceSheetPath = "\(projectRoot)/tools/generated_equipment_enhancement_image2_sources/rejected_destroy_image2_sheet_has_monster.png"

func loadBitmap(_ path: String) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOfFile: path),
          let data = try? Data(contentsOf: URL(fileURLWithPath: path)),
          let sourceRep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "EnemyDefeatEffectAssets", code: 1, userInfo: [NSLocalizedDescriptionKey: "image2 sheet load failed: \(path)"])
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
        throw NSError(domain: "EnemyDefeatEffectAssets", code: 2, userInfo: [NSLocalizedDescriptionKey: "image2 sheet normalize failed: \(path)"])
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

func transparentImage2Frame(
    from sheet: NSBitmapImageRep,
    frameIndex: Int,
    alphaBoost: CGFloat,
    alphaMultiplier: CGFloat) throws -> NSBitmapImageRep {
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
        throw NSError(domain: "EnemyDefeatEffectAssets", code: 3, userInfo: [NSLocalizedDescriptionKey: "frame bitmap allocation failed"])
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
            guard let sourceColor = sheet.colorAt(x: sourceX, y: sourceY)?.usingColorSpace(.deviceRGB) else {
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

            let alpha = min(1, pow(maxChannel, 0.72) * alphaBoost) * a * alphaMultiplier
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

func allyPaletteFrame(from source: NSBitmapImageRep, frameIndex: Int, frameCount: Int) throws -> NSBitmapImageRep {
    guard let out = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: source.pixelsWide,
        pixelsHigh: source.pixelsHigh,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0) else {
        throw NSError(domain: "EnemyDefeatEffectAssets", code: 5, userInfo: [NSLocalizedDescriptionKey: "ally frame bitmap allocation failed"])
    }

    let progress = frameCount > 1 ? CGFloat(frameIndex) / CGFloat(frameCount - 1) : 1
    let lateFade = max(0.35, 1 - progress * 0.20)
    for y in 0..<source.pixelsHigh {
        for x in 0..<source.pixelsWide {
            guard let sourceColor = source.colorAt(x: x, y: y)?.usingColorSpace(.deviceRGB) else {
                out.setColor(NSColor.clear, atX: x, y: y)
                continue
            }

            var r: CGFloat = 0
            var g: CGFloat = 0
            var b: CGFloat = 0
            var a: CGFloat = 0
            sourceColor.getRed(&r, green: &g, blue: &b, alpha: &a)
            if a <= 0.001 {
                out.setColor(NSColor.clear, atX: x, y: y)
                continue
            }

            let luminance = max(0, min(1, (r * 0.30) + (g * 0.45) + (b * 0.25)))
            let warmth = max(0, r - max(g, b) * 0.72)
            let sparkle = min(1, pow(max(r, max(g, b)), 1.45) + warmth * 1.1)
            let smoke = 1 - sparkle
            let blueWhiteR = min(1, 0.48 + luminance * 0.58)
            let blueWhiteG = min(1, 0.76 + luminance * 0.35)
            let blueWhiteB = min(1, 0.96 + luminance * 0.18)
            let ash = 0.50 + luminance * 0.42
            let cyanWeight = min(1, sparkle * 0.82 + (1 - smoke) * 0.18)
            let red = ash * smoke * 0.92 + blueWhiteR * cyanWeight
            let green = ash * smoke * 0.98 + blueWhiteG * cyanWeight
            let blue = ash * smoke + blueWhiteB * cyanWeight
            let alpha = min(1, a * (0.86 + sparkle * 0.18) * lateFade)
            out.setColor(NSColor(
                deviceRed: min(1, red),
                green: min(1, green),
                blue: min(1, blue),
                alpha: alpha),
                atX: x,
                y: y)
        }
    }

    return out
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "EnemyDefeatEffectAssets", code: 4, userInfo: [NSLocalizedDescriptionKey: "encode failed: \(path)"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func guid() -> String {
    UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
}

func ensureFolderMeta(for path: String) throws {
    let metaPath = "\(path).meta"
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
try ensureFolderMeta(for: outputRoot)

let sourceSheet = try loadBitmap(sourceSheetPath)
let frameMap = [4, 5, 6, 7, 7, 7]
let alphaMultipliers: [CGFloat] = [1.00, 1.00, 0.92, 0.78, 0.56, 0.34]
for frame in 0..<frameMap.count {
    let rep = try transparentImage2Frame(
        from: sourceSheet,
        frameIndex: frameMap[frame],
        alphaBoost: 1.34,
        alphaMultiplier: alphaMultipliers[frame])
    let path = "\(outputRoot)/EnemyDefeat_\(frame).png"
    try savePNG(rep, to: path)
    try ensureSpriteMeta(for: path)

    let allyRep = try allyPaletteFrame(from: rep, frameIndex: frame, frameCount: frameMap.count)
    let allyPath = "\(outputRoot)/AllyDefeat_\(frame).png"
    try savePNG(allyRep, to: allyPath)
    try ensureSpriteMeta(for: allyPath)
}

print("Generated enemy defeat effect assets in \(outputRoot)")
