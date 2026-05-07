import AppKit
import Foundation

enum EffectKind {
    case flameProjectile
    case abyssProjectile
    case plasmaProjectile
    case laserProjectile
    case punchImpact
    case stoneImpact
    case swordSlash
    case magicProjectile
    case magicCircle
    case holyBurst
    case natureProjectile
    case cosmicImpact
}

struct EffectSpec {
    let key: String
    let kind: EffectKind
    let primary: NSColor
    let secondary: NSColor
}

let projectRoot = "/Users/andou/Desktop/あ/game-creation/WitchTowerGame"
let outputRoot = "\(projectRoot)/Assets/Resources/BattleEffects/Monster"
let canvasSize = 256

func color(_ hex: UInt32, alpha: CGFloat = 1.0) -> NSColor {
    let r = CGFloat((hex >> 16) & 0xff) / 255.0
    let g = CGFloat((hex >> 8) & 0xff) / 255.0
    let b = CGFloat(hex & 0xff) / 255.0
    return NSColor(deviceRed: r, green: g, blue: b, alpha: alpha)
}

let specs: [EffectSpec] = [
    EffectSpec(key: "dragon_whelp", kind: .flameProjectile, primary: color(0xff7a18), secondary: color(0xfff2a8)),
    EffectSpec(key: "flare_drake", kind: .flameProjectile, primary: color(0xff3d00), secondary: color(0xffdd55)),
    EffectSpec(key: "abyss_dragon", kind: .abyssProjectile, primary: color(0x12d8ff), secondary: color(0x09113d)),
    EffectSpec(key: "chibi_gear", kind: .punchImpact, primary: color(0x7ef7ff), secondary: color(0xffffff)),
    EffectSpec(key: "armed_droid", kind: .punchImpact, primary: color(0x31a8ff), secondary: color(0xe6fbff)),
    EffectSpec(key: "omega_leon", kind: .punchImpact, primary: color(0xff3158), secondary: color(0xffffff)),
    EffectSpec(key: "rock_golem", kind: .punchImpact, primary: color(0xb28a52), secondary: color(0xffdf9b)),
    EffectSpec(key: "ore_giant_garm", kind: .punchImpact, primary: color(0xd2a85f), secondary: color(0x8affd2)),
    EffectSpec(key: "cosmic_ore_fortress_golem", kind: .punchImpact, primary: color(0x8058ff), secondary: color(0xffdf7a)),
    EffectSpec(key: "apprentice_swordsman", kind: .swordSlash, primary: color(0xdbe9ff), secondary: color(0xffffff)),
    EffectSpec(key: "holy_armor_leon", kind: .swordSlash, primary: color(0xffdf6f), secondary: color(0xffffff)),
    EffectSpec(key: "sword_saint_alvarez", kind: .swordSlash, primary: color(0x8feaff), secondary: color(0xffffff)),
    EffectSpec(key: "apprentice_mage", kind: .magicProjectile, primary: color(0xb05cff), secondary: color(0xffffff)),
    EffectSpec(key: "dark_robe_curse_mage_noah", kind: .magicProjectile, primary: color(0x8134ff), secondary: color(0xff7dff)),
    EffectSpec(key: "abyss_grand_mage_seraphis", kind: .magicCircle, primary: color(0x6d35ff), secondary: color(0xf3d0ff)),
    EffectSpec(key: "mecha_dragon_valdrake", kind: .plasmaProjectile, primary: color(0x28f5ff), secondary: color(0xff5c2b)),
    EffectSpec(key: "drag_gaia", kind: .punchImpact, primary: color(0xb88d4c), secondary: color(0x38ffbb)),
    EffectSpec(key: "dragon_sword_saint_agito", kind: .swordSlash, primary: color(0xff6532), secondary: color(0xffefc0)),
    EffectSpec(key: "abyss_dragon_mage_valflare", kind: .abyssProjectile, primary: color(0x26e7ff), secondary: color(0xbf35ff)),
    EffectSpec(key: "fortress_machine_gigafort", kind: .laserProjectile, primary: color(0x45d5ff), secondary: color(0xffffff)),
    EffectSpec(key: "mecha_sword_saint_gransaber", kind: .swordSlash, primary: color(0x6eeeff), secondary: color(0xfff19c)),
    EffectSpec(key: "dark_magic_machine_god_merchion", kind: .plasmaProjectile, primary: color(0x9b35ff), secondary: color(0x39f6ff)),
    EffectSpec(key: "rock_knight_gaius", kind: .punchImpact, primary: color(0xd5aa63), secondary: color(0xffffff)),
    EffectSpec(key: "astral_eclipse_golem", kind: .cosmicImpact, primary: color(0x7855ff), secondary: color(0xf6e38a)),
    EffectSpec(key: "magic_sword_saint_luciel", kind: .swordSlash, primary: color(0xb346ff), secondary: color(0xffffff)),
    EffectSpec(key: "seraph_michael", kind: .holyBurst, primary: color(0xffef88), secondary: color(0xffffff)),
    EffectSpec(key: "spirit_queen_titania", kind: .natureProjectile, primary: color(0x5cff9a), secondary: color(0xfff5a3))
]

func drawGlowOval(center: CGPoint, radiusX: CGFloat, radiusY: CGFloat, color: NSColor, layers: Int = 5) {
    guard layers > 0 else { return }
    for layer in stride(from: layers, through: 1, by: -1) {
        let progress = CGFloat(layer) / CGFloat(layers)
        let alpha = color.alphaComponent * 0.11 * (1.0 - progress + 0.22)
        color.withAlphaComponent(alpha).setFill()
        let rect = NSRect(
            x: center.x - radiusX * progress,
            y: center.y - radiusY * progress,
            width: radiusX * progress * 2.0,
            height: radiusY * progress * 2.0)
        NSBezierPath(ovalIn: rect).fill()
    }

    color.withAlphaComponent(min(1.0, color.alphaComponent)).setFill()
    NSBezierPath(ovalIn: NSRect(x: center.x - radiusX * 0.28, y: center.y - radiusY * 0.28, width: radiusX * 0.56, height: radiusY * 0.56)).fill()
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
    strokePath(points: [CGPoint(x: center.x - size, y: center.y), CGPoint(x: center.x + size, y: center.y)], color: color, width: max(1.0, size * 0.22))
    strokePath(points: [CGPoint(x: center.x, y: center.y - size), CGPoint(x: center.x, y: center.y + size)], color: color, width: max(1.0, size * 0.22))
}

func drawProjectile(frame: Int, spec: EffectSpec, isFlame: Bool, isLaser: Bool = false) {
    let t = CGFloat(frame) / 3.0
    let headX = isLaser ? 160 + (t * 22) : 116 + (t * 36)
    let y = CGFloat(canvasSize) * 0.52 + sin(t * .pi) * 8
    let tailX: CGFloat = isLaser ? 44 : 46
    let wave = sin((t + 0.18) * .pi)

    if isLaser {
        strokePath(points: [CGPoint(x: tailX, y: y), CGPoint(x: headX + 42, y: y + wave * 3)], color: spec.primary.withAlphaComponent(0.25), width: 34)
        strokePath(points: [CGPoint(x: tailX + 8, y: y), CGPoint(x: headX + 36, y: y + wave * 2)], color: spec.primary.withAlphaComponent(0.62), width: 18)
        strokePath(points: [CGPoint(x: tailX + 16, y: y), CGPoint(x: headX + 32, y: y)], color: spec.secondary.withAlphaComponent(0.92), width: 6)
        drawGlowOval(center: CGPoint(x: headX + 42, y: y), radiusX: 28, radiusY: 18, color: spec.primary.withAlphaComponent(0.82), layers: 6)
        return
    }

    strokePath(points: [
        CGPoint(x: tailX, y: y - 9),
        CGPoint(x: (tailX + headX) * 0.50, y: y + wave * 15),
        CGPoint(x: headX, y: y)
    ], color: spec.primary.withAlphaComponent(0.28), width: isFlame ? 34 : 24)
    strokePath(points: [
        CGPoint(x: tailX + 12, y: y - 3),
        CGPoint(x: (tailX + headX) * 0.55, y: y + wave * 8),
        CGPoint(x: headX, y: y)
    ], color: spec.primary.withAlphaComponent(0.72), width: isFlame ? 17 : 13)
    strokePath(points: [
        CGPoint(x: tailX + 24, y: y),
        CGPoint(x: headX, y: y + wave * 2)
    ], color: spec.secondary.withAlphaComponent(0.88), width: isFlame ? 6 : 5)

    drawGlowOval(center: CGPoint(x: headX + 14, y: y), radiusX: isFlame ? 30 : 24, radiusY: isFlame ? 23 : 22, color: spec.primary.withAlphaComponent(0.86), layers: 6)
    drawGlowOval(center: CGPoint(x: headX + 14, y: y), radiusX: isFlame ? 13 : 10, radiusY: isFlame ? 10 : 10, color: spec.secondary.withAlphaComponent(1.0), layers: 3)

    for i in 0..<5 {
        let offset = CGFloat(i)
        drawSpark(
            center: CGPoint(x: headX - 54 + offset * 18, y: y + ((i % 2 == 0) ? -23 : 22) * (0.65 + t * 0.3)),
            color: spec.primary.withAlphaComponent(0.58),
            size: 3.5 + t * 2.0)
    }
}

func drawStoneImpact(frame: Int, spec: EffectSpec, cosmic: Bool = false) {
    let t = CGFloat(frame) / 3.0
    let center = CGPoint(x: 128, y: 124)
    let radius = 42 + t * 42
    drawGlowOval(center: center, radiusX: radius, radiusY: radius * 0.55, color: spec.primary.withAlphaComponent(0.42), layers: 5)

    let ring = NSBezierPath(ovalIn: NSRect(x: center.x - radius, y: center.y - radius * 0.5, width: radius * 2, height: radius))
    ring.lineWidth = 5 + t * 4
    spec.secondary.withAlphaComponent(0.64 - t * 0.28).setStroke()
    ring.stroke()

    for i in 0..<9 {
        let angle = (CGFloat(i) / 9.0) * .pi * 2.0 + t * 0.4
        let inner = CGPoint(x: center.x + cos(angle) * (16 + t * 18), y: center.y + sin(angle) * (10 + t * 12))
        let outer = CGPoint(x: center.x + cos(angle) * (42 + t * 45), y: center.y + sin(angle) * (24 + t * 26))
        strokePath(points: [inner, outer], color: (cosmic ? spec.secondary : spec.primary).withAlphaComponent(0.76 - t * 0.34), width: 4.0 - t)
    }

    if cosmic {
        for i in 0..<7 {
            let angle = CGFloat(i) * 0.9 + t
            drawSpark(center: CGPoint(x: center.x + cos(angle) * (52 + t * 22), y: center.y + sin(angle) * (34 + t * 16)), color: spec.secondary.withAlphaComponent(0.8), size: 5 + t * 3)
        }
    }
}

func drawPunchImpact(frame: Int, spec: EffectSpec) {
    let t = CGFloat(frame) / 3.0
    let center = CGPoint(x: 118 + t * 8, y: 126)
    let forward = CGPoint(x: 176 + t * 12, y: 126 + sin(t * .pi) * 5)

    strokePath(points: [
        CGPoint(x: 42, y: 120),
        CGPoint(x: center.x - 18, y: center.y - 2),
        forward
    ], color: spec.primary.withAlphaComponent(0.18), width: 46 - t * 8)
    strokePath(points: [
        CGPoint(x: 58, y: 126),
        CGPoint(x: center.x + 6, y: center.y),
        forward
    ], color: spec.primary.withAlphaComponent(0.48), width: 20 - t * 4)
    strokePath(points: [
        CGPoint(x: center.x + 16, y: center.y),
        forward
    ], color: spec.secondary.withAlphaComponent(0.76), width: 6)

    drawGlowOval(center: forward, radiusX: 24 + t * 38, radiusY: 18 + t * 26, color: spec.primary.withAlphaComponent(0.58 - t * 0.12), layers: 6)
    drawGlowOval(center: forward, radiusX: 9 + t * 10, radiusY: 8 + t * 9, color: spec.secondary.withAlphaComponent(0.90 - t * 0.18), layers: 3)

    for i in 0..<8 {
        let angle = (CGFloat(i) / 8.0) * .pi * 2.0 + t * 0.22
        let inner = CGPoint(x: forward.x + cos(angle) * (10 + t * 10), y: forward.y + sin(angle) * (8 + t * 8))
        let outer = CGPoint(x: forward.x + cos(angle) * (34 + t * 42), y: forward.y + sin(angle) * (23 + t * 32))
        strokePath(points: [inner, outer], color: spec.secondary.withAlphaComponent(0.72 - t * 0.38), width: 4.0 - t * 1.4)
    }

    for i in 0..<5 {
        let angle = CGFloat(i) * 1.27 + t * .pi
        drawSpark(
            center: CGPoint(x: forward.x + cos(angle) * (38 + t * 18), y: forward.y + sin(angle) * (25 + t * 15)),
            color: spec.primary.withAlphaComponent(0.72),
            size: 4.0 + t * 2.5)
    }
}

func drawSlash(frame: Int, spec: EffectSpec) {
    let t = CGFloat(frame) / 3.0
    let center = CGPoint(x: 126, y: 126)
    let startAngle = CGFloat(215 - frame * 8)
    let endAngle = CGFloat(334 + frame * 10)
    for layer in 0..<4 {
        let radius = CGFloat(58 + layer * 11) + t * 16
        let path = NSBezierPath()
        path.appendArc(withCenter: center, radius: radius, startAngle: startAngle, endAngle: endAngle)
        path.lineCapStyle = .round
        path.lineWidth = CGFloat(18 - layer * 3)
        let tint = layer == 0 ? spec.secondary : spec.primary
        tint.withAlphaComponent(CGFloat(0.24 + Double(layer) * 0.12) * (1.0 - t * 0.35)).setStroke()
        path.stroke()
    }

    strokePath(points: [CGPoint(x: 74 - t * 8, y: 78 + t * 4), CGPoint(x: 182 + t * 12, y: 178 - t * 6)], color: spec.secondary.withAlphaComponent(0.82 - t * 0.22), width: 5)
    drawSpark(center: CGPoint(x: 184 + t * 12, y: 176 - t * 4), color: spec.secondary.withAlphaComponent(0.85), size: 7 + t * 5)
}

func drawMagicCircle(frame: Int, spec: EffectSpec, holy: Bool = false, nature: Bool = false) {
    let t = CGFloat(frame) / 3.0
    let center = CGPoint(x: 128, y: 126)
    let baseRadius = 42 + t * 22
    drawGlowOval(center: center, radiusX: baseRadius * 1.18, radiusY: baseRadius * 0.74, color: spec.primary.withAlphaComponent(0.42), layers: 7)

    for i in 0..<3 {
        let radius = baseRadius + CGFloat(i * 14)
        let path = NSBezierPath(ovalIn: NSRect(x: center.x - radius, y: center.y - radius * 0.58, width: radius * 2, height: radius * 1.16))
        path.lineWidth = 3.2 - CGFloat(i) * 0.6
        (i == 0 ? spec.secondary : spec.primary).withAlphaComponent(0.78 - t * 0.22).setStroke()
        path.stroke()
    }

    for i in 0..<10 {
        let angle = CGFloat(i) * (.pi * 2.0 / 10.0) + t * .pi
        let p1 = CGPoint(x: center.x + cos(angle) * 20, y: center.y + sin(angle) * 13)
        let p2 = CGPoint(x: center.x + cos(angle) * (74 + t * 14), y: center.y + sin(angle) * (43 + t * 8))
        strokePath(points: [p1, p2], color: spec.secondary.withAlphaComponent(0.46 - t * 0.12), width: holy ? 4.5 : 2.5)
    }

    if holy {
        for i in 0..<5 {
            let x = 82 + CGFloat(i) * 23
            strokePath(points: [CGPoint(x: x, y: 34), CGPoint(x: x + sin(t * .pi + CGFloat(i)) * 8, y: 214)], color: spec.secondary.withAlphaComponent(0.42), width: 8 - CGFloat(i % 2) * 2)
        }
    }

    if nature {
        for i in 0..<7 {
            let angle = CGFloat(i) * 0.85 + t * 1.6
            let point = CGPoint(x: center.x + cos(angle) * (42 + t * 22), y: center.y + sin(angle) * (30 + t * 13))
            drawGlowOval(center: point, radiusX: 7 + t * 2, radiusY: 4 + t, color: spec.primary.withAlphaComponent(0.78), layers: 2)
        }
    }
}

func drawEffect(frame: Int, spec: EffectSpec) {
    switch spec.kind {
    case .flameProjectile:
        drawProjectile(frame: frame, spec: spec, isFlame: true)
    case .abyssProjectile:
        drawProjectile(frame: frame, spec: spec, isFlame: true)
        drawMagicCircle(frame: min(frame, 2), spec: spec)
    case .plasmaProjectile:
        drawProjectile(frame: frame, spec: spec, isFlame: false)
    case .laserProjectile:
        drawProjectile(frame: frame, spec: spec, isFlame: false, isLaser: true)
    case .punchImpact:
        drawPunchImpact(frame: frame, spec: spec)
    case .stoneImpact:
        drawStoneImpact(frame: frame, spec: spec)
    case .swordSlash:
        drawSlash(frame: frame, spec: spec)
    case .magicProjectile:
        drawProjectile(frame: frame, spec: spec, isFlame: false)
    case .magicCircle:
        drawMagicCircle(frame: frame, spec: spec)
    case .holyBurst:
        drawMagicCircle(frame: frame, spec: spec, holy: true)
    case .natureProjectile:
        drawProjectile(frame: frame, spec: spec, isFlame: false)
        drawMagicCircle(frame: min(frame, 2), spec: spec, nature: true)
    case .cosmicImpact:
        drawStoneImpact(frame: frame, spec: spec, cosmic: true)
    }
}

func render(spec: EffectSpec, frame: Int) throws -> NSBitmapImageRep {
    let image = NSImage(size: NSSize(width: canvasSize, height: canvasSize))
    image.lockFocus()
    NSColor.clear.setFill()
    NSRect(x: 0, y: 0, width: canvasSize, height: canvasSize).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    drawEffect(frame: frame, spec: spec)
    image.unlockFocus()

    guard let data = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "GenerateMonsterAttackEffects", code: 1, userInfo: [NSLocalizedDescriptionKey: "Failed to render \(spec.key) frame \(frame)"])
    }

    return rep
}

func savePNG(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let data = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "GenerateMonsterAttackEffects", code: 2, userInfo: [NSLocalizedDescriptionKey: "Failed to encode \(path)"])
    }

    try data.write(to: URL(fileURLWithPath: path), options: .atomic)
}

func guid() -> String {
    UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
}

func ensureSpriteMeta(for pngPath: String) throws {
    let metaPath = "\(pngPath).meta"
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
  cookieLightType: 0
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

for spec in specs {
    for frame in 0..<4 {
        let rep = try render(spec: spec, frame: frame)
        let path = "\(outputRoot)/fx_\(spec.key)_attack_\(frame).png"
        try savePNG(rep, to: path)
        try ensureSpriteMeta(for: path)
    }
}

print("Generated \(specs.count * 4) monster attack effect frames in \(outputRoot)")
