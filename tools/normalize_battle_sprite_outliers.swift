import AppKit
import Foundation

struct AuditRow {
    let assetPath: String
    let characterKey: String
    let pose: String
    let ratio: Double
}

struct Pixel {
    var r: UInt8
    var g: UInt8
    var b: UInt8
    var a: UInt8
}

struct Bounds {
    var minX: Int
    var minY: Int
    var maxX: Int
    var maxY: Int

    var width: Int { maxX - minX + 1 }
    var height: Int { maxY - minY + 1 }
    var midX: Double { (Double(minX) + Double(maxX)) * 0.5 }
    var bottomDistanceFromCanvas: Int
}

func usage() -> Never {
    fputs("usage: normalize_battle_sprite_outliers.swift <project-root> [audit-csv] [low-threshold] [high-threshold] [--apply] [--characters=a,b] [--poses=idle,move,attack]\n", stderr)
    exit(1)
}

func parseCSV(_ text: String) -> [AuditRow] {
    var rows: [AuditRow] = []
    let lines = text.components(separatedBy: .newlines)
    guard lines.count > 1 else {
        return rows
    }

    for rawLine in lines.dropFirst() {
        let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
        if line.isEmpty {
            continue
        }

        let columns = line.components(separatedBy: ",")
        if columns.count < 10 {
            continue
        }

        let assetPath = columns[0].replacingOccurrences(of: "\u{feff}", with: "")
        guard let ratio = Double(columns[9]) else {
            continue
        }

        rows.append(AuditRow(
            assetPath: assetPath,
            characterKey: columns[2],
            pose: columns[3],
            ratio: ratio))
    }

    return rows
}

func shouldProcess(_ row: AuditRow, lowThreshold: Double, highThreshold: Double) -> Bool {
    guard row.ratio < lowThreshold || row.ratio > highThreshold else {
        return false
    }

    guard row.assetPath.hasPrefix("Assets/Resources/MonsterBattle/"),
          row.assetPath.hasSuffix(".png") else {
        return false
    }

    let fileName = URL(fileURLWithPath: row.assetPath).lastPathComponent
    let pattern = #"_(idle|move|attack)_\d+\.png$"#
    return fileName.range(of: pattern, options: .regularExpression) != nil
}

func parseFilterSet(prefix: String, from args: [String]) -> Set<String>? {
    guard let rawValue = args.first(where: { $0.hasPrefix(prefix) }) else {
        return nil
    }

    let value = String(rawValue.dropFirst(prefix.count))
    let items = value
        .split(separator: ",")
        .map { String($0).trimmingCharacters(in: .whitespacesAndNewlines) }
        .filter { !$0.isEmpty }

    return items.isEmpty ? nil : Set(items)
}

func normalizedPose(_ pose: String) -> String {
    return pose.components(separatedBy: "+").first ?? pose
}

func matchesFilters(_ row: AuditRow, characterFilter: Set<String>?, poseFilter: Set<String>?) -> Bool {
    if let characterFilter, !characterFilter.contains(row.characterKey) {
        return false
    }

    if let poseFilter, !poseFilter.contains(normalizedPose(row.pose)) {
        return false
    }

    return true
}

func loadRGBA(_ path: String) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOfFile: path) else {
        throw NSError(domain: "normalize_battle_sprite_outliers", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to load \(path)"])
    }

    guard let data = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "normalize_battle_sprite_outliers", code: 2, userInfo: [NSLocalizedDescriptionKey: "failed to decode \(path)"])
    }

    return rep
}

func makeTransparentRGBA(width: Int, height: Int) throws -> NSBitmapImageRep {
    guard let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: width,
        pixelsHigh: height,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bitmapFormat: .alphaNonpremultiplied,
        bytesPerRow: 0,
        bitsPerPixel: 0),
        let data = rep.bitmapData else {
        throw NSError(domain: "normalize_battle_sprite_outliers", code: 4, userInfo: [NSLocalizedDescriptionKey: "failed to create output buffer"])
    }

    data.initialize(repeating: 0, count: rep.bytesPerRow * height)
    return rep
}

func pixel(in rep: NSBitmapImageRep, x: Int, y: Int) -> Pixel {
    guard let data = rep.bitmapData else {
        return Pixel(r: 0, g: 0, b: 0, a: 0)
    }

    let bytesPerPixel = max(1, rep.bitsPerPixel / 8)
    let offset = y * rep.bytesPerRow + x * bytesPerPixel
    let r = data[offset]
    let g = bytesPerPixel > 1 ? data[offset + 1] : r
    let b = bytesPerPixel > 2 ? data[offset + 2] : r
    let a = bytesPerPixel > 3 ? data[offset + 3] : UInt8.max
    return Pixel(r: r, g: g, b: b, a: a)
}

func setPixel(_ pixel: Pixel, in rep: NSBitmapImageRep, x: Int, y: Int) {
    guard let data = rep.bitmapData else {
        return
    }

    let bytesPerPixel = max(1, rep.bitsPerPixel / 8)
    let offset = y * rep.bytesPerRow + x * bytesPerPixel
    data[offset] = pixel.r
    if bytesPerPixel > 1 {
        data[offset + 1] = pixel.g
    }
    if bytesPerPixel > 2 {
        data[offset + 2] = pixel.b
    }
    if bytesPerPixel > 3 {
        data[offset + 3] = pixel.a
    }
}

func contentBounds(in rep: NSBitmapImageRep, alphaThreshold: UInt8 = 8) -> Bounds? {
    var minX = rep.pixelsWide
    var minY = rep.pixelsHigh
    var maxX = -1
    var maxY = -1

    for y in 0..<rep.pixelsHigh {
        for x in 0..<rep.pixelsWide {
            if pixel(in: rep, x: x, y: y).a > alphaThreshold {
                minX = min(minX, x)
                minY = min(minY, y)
                maxX = max(maxX, x)
                maxY = max(maxY, y)
            }
        }
    }

    if maxX < minX || maxY < minY {
        return nil
    }

    return Bounds(
        minX: minX,
        minY: minY,
        maxX: maxX,
        maxY: maxY,
        bottomDistanceFromCanvas: rep.pixelsHigh - 1 - maxY)
}

func normalizedImage(from source: NSBitmapImageRep, ratio: Double) throws -> (NSBitmapImageRep, Double, Bounds, Bounds) {
    guard let sourceBounds = contentBounds(in: source) else {
        return (source, 1.0, Bounds(minX: 0, minY: 0, maxX: 0, maxY: 0, bottomDistanceFromCanvas: 0), Bounds(minX: 0, minY: 0, maxX: 0, maxY: 0, bottomDistanceFromCanvas: 0))
    }

    let scale = max(0.25, min(2.25, 1.0 / ratio))
    let scaledWidth = max(1, Int((Double(sourceBounds.width) * scale).rounded()))
    let scaledHeight = max(1, Int((Double(sourceBounds.height) * scale).rounded()))
    let horizontalPadding = 12
    let topPadding = 12

    let requiredWidth = scaledWidth + horizontalPadding * 2
    let targetWidth = max(source.pixelsWide, requiredWidth)
    let requiredHeight = scaledHeight + sourceBounds.bottomDistanceFromCanvas + topPadding
    let targetHeight = max(source.pixelsHigh, requiredHeight)

    let horizontalShift = (targetWidth - source.pixelsWide) / 2
    let centerX = sourceBounds.midX + Double(horizontalShift)
    var targetMinX = Int((centerX - Double(scaledWidth) * 0.5).rounded())
    targetMinX = max(0, min(targetMinX, targetWidth - scaledWidth))

    let targetBottomY = targetHeight - 1 - sourceBounds.bottomDistanceFromCanvas
    var targetMinY = targetBottomY - scaledHeight + 1
    targetMinY = max(0, min(targetMinY, targetHeight - scaledHeight))

    let output = try makeTransparentRGBA(width: targetWidth, height: targetHeight)

    for y in 0..<scaledHeight {
        let sourceY = sourceBounds.minY + min(sourceBounds.height - 1, Int((Double(y) / scale).rounded(.down)))
        for x in 0..<scaledWidth {
            let sourceX = sourceBounds.minX + min(sourceBounds.width - 1, Int((Double(x) / scale).rounded(.down)))
            let p = pixel(in: source, x: sourceX, y: sourceY)
            if p.a > 0 {
                setPixel(p, in: output, x: targetMinX + x, y: targetMinY + y)
            }
        }
    }

    let targetBounds = Bounds(
        minX: targetMinX,
        minY: targetMinY,
        maxX: targetMinX + scaledWidth - 1,
        maxY: targetMinY + scaledHeight - 1,
        bottomDistanceFromCanvas: targetHeight - 1 - (targetMinY + scaledHeight - 1))

    return (output, scale, sourceBounds, targetBounds)
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "normalize_battle_sprite_outliers", code: 5, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

let args = CommandLine.arguments
let positionalArgs = Array(args.dropFirst().filter { !$0.hasPrefix("--") })
if positionalArgs.count < 1 || positionalArgs.count > 4 {
    usage()
}

let projectRoot = positionalArgs[0]
let auditPath = positionalArgs.count >= 2 ? positionalArgs[1] : "\(projectRoot)/../tools/reports/battle_sprite_visual_audit.csv"
let lowThreshold = positionalArgs.count >= 3 ? Double(positionalArgs[2]) ?? 0.82 : 0.82
let highThreshold = positionalArgs.count >= 4 ? Double(positionalArgs[3]) ?? 1.18 : 1.18
let applyChanges = args.contains("--apply")
let characterFilter = parseFilterSet(prefix: "--characters=", from: args)
let poseFilter = parseFilterSet(prefix: "--poses=", from: args)

let auditText = try String(contentsOfFile: auditPath, encoding: .utf8)
let rows = parseCSV(auditText)
let targets = rows.filter {
    shouldProcess($0, lowThreshold: lowThreshold, highThreshold: highThreshold) &&
    matchesFilters($0, characterFilter: characterFilter, poseFilter: poseFilter)
}

let reportPath = "\(projectRoot)/../tools/reports/battle_sprite_outlier_normalization.csv"
try FileManager.default.createDirectory(atPath: URL(fileURLWithPath: reportPath).deletingLastPathComponent().path, withIntermediateDirectories: true)
var report = "mode,assetPath,characterKey,pose,oldRatio,scale,oldCanvasWidth,oldCanvasHeight,newCanvasWidth,newCanvasHeight,oldOpaqueWidth,oldOpaqueHeight,newOpaqueWidth,newOpaqueHeight\n"

for row in targets {
    let fullPath = "\(projectRoot)/\(row.assetPath)"
    let source = try loadRGBA(fullPath)
    let (normalized, scale, oldBounds, newBounds) = try normalizedImage(from: source, ratio: row.ratio)

    if applyChanges {
        try save(normalized, to: fullPath)
    }

    report += [
        applyChanges ? "apply" : "dry-run",
        row.assetPath,
        row.characterKey,
        row.pose,
        String(format: "%.3f", row.ratio),
        String(format: "%.3f", scale),
        "\(source.pixelsWide)",
        "\(source.pixelsHigh)",
        "\(normalized.pixelsWide)",
        "\(normalized.pixelsHigh)",
        "\(oldBounds.width)",
        "\(oldBounds.height)",
        "\(newBounds.width)",
        "\(newBounds.height)"
    ].joined(separator: ",") + "\n"
}

try report.write(toFile: reportPath, atomically: true, encoding: .utf8)

print("\(applyChanges ? "Applied" : "Dry-run") \(targets.count) battle sprite frame normalizations.")
print("Report: \(reportPath)")
