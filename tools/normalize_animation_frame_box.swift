import AppKit
import Foundation

struct Pixel {
    let r: UInt8
    let g: UInt8
    let b: UInt8
    let a: UInt8
}

struct Bounds {
    let minX: Int
    let minY: Int
    let maxX: Int
    let maxY: Int

    var width: Int { maxX - minX + 1 }
    var height: Int { maxY - minY + 1 }
    var midX: Double { (Double(minX) + Double(maxX)) * 0.5 }
}

struct Frame {
    let index: Int
    let path: String
    let rep: NSBitmapImageRep
    let bounds: Bounds
}

func usage() -> Never {
    fputs("usage: normalize_animation_frame_box.swift <project-root> <character-key> <pose> <target-width> <target-height> [--frames=0,1] [--apply]\n", stderr)
    exit(1)
}

func loadRGBA(_ path: String) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOfFile: path),
          let data = image.tiffRepresentation,
          let rep = NSBitmapImageRep(data: data) else {
        throw NSError(domain: "normalize_animation_frame_box", code: 1, userInfo: [NSLocalizedDescriptionKey: "failed to load \(path)"])
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
        throw NSError(domain: "normalize_animation_frame_box", code: 2, userInfo: [NSLocalizedDescriptionKey: "failed to create output image"])
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

    return maxX >= minX && maxY >= minY
        ? Bounds(minX: minX, minY: minY, maxX: maxX, maxY: maxY)
        : nil
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "normalize_animation_frame_box", code: 3, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

func parseFrameFilter(_ args: [String]) -> Set<Int>? {
    guard let raw = args.first(where: { $0.hasPrefix("--frames=") }) else {
        return nil
    }

    let value = String(raw.dropFirst("--frames=".count))
    let frames = value
        .split(separator: ",")
        .compactMap { Int($0.trimmingCharacters(in: .whitespacesAndNewlines)) }

    return frames.isEmpty ? nil : Set(frames)
}

func frameIndex(from url: URL, pose: String) -> Int? {
    let name = url.deletingPathExtension().lastPathComponent
    let marker = "_\(pose)_"
    guard let range = name.range(of: marker) else {
        return nil
    }

    return Int(name[range.upperBound...])
}

func normalizedImage(from frame: Frame, targetWidth: Int, targetHeight: Int) throws -> NSBitmapImageRep {
    let source = frame.rep
    let bounds = frame.bounds
    let output = try makeTransparentRGBA(width: source.pixelsWide, height: source.pixelsHigh)
    let bottomDistance = source.pixelsHigh - 1 - bounds.maxY
    let targetBottomY = source.pixelsHigh - 1 - bottomDistance
    let targetMinY = max(0, min(targetBottomY - targetHeight + 1, source.pixelsHigh - targetHeight))
    let targetMinX = max(0, min(Int((bounds.midX - Double(targetWidth) * 0.5).rounded()), source.pixelsWide - targetWidth))

    for y in 0..<targetHeight {
        let sourceY = bounds.minY + min(bounds.height - 1, Int((Double(y) / Double(targetHeight) * Double(bounds.height)).rounded(.down)))
        for x in 0..<targetWidth {
            let sourceX = bounds.minX + min(bounds.width - 1, Int((Double(x) / Double(targetWidth) * Double(bounds.width)).rounded(.down)))
            let p = pixel(in: source, x: sourceX, y: sourceY)
            if p.a > 0 {
                setPixel(p, in: output, x: targetMinX + x, y: targetMinY + y)
            }
        }
    }

    return output
}

let args = CommandLine.arguments
let positionalArgs = Array(args.dropFirst().filter { !$0.hasPrefix("--") })
if positionalArgs.count != 5 {
    usage()
}

let projectRoot = positionalArgs[0]
let characterKey = positionalArgs[1]
let pose = positionalArgs[2]
guard let targetWidth = Int(positionalArgs[3]), let targetHeight = Int(positionalArgs[4]), targetWidth > 0, targetHeight > 0 else {
    usage()
}

let applyChanges = args.contains("--apply")
let frameFilter = parseFrameFilter(args)
let folder = URL(fileURLWithPath: projectRoot).appendingPathComponent("Assets/Resources/MonsterBattle")
let files = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil)
let prefix = "mon_\(characterKey)_\(pose)_"
let frames = try files
    .filter { $0.pathExtension == "png" && $0.lastPathComponent.hasPrefix(prefix) }
    .compactMap { url -> Frame? in
        guard let index = frameIndex(from: url, pose: pose),
              frameFilter == nil || frameFilter!.contains(index) else {
            return nil
        }

        let rep = try loadRGBA(url.path)
        guard let bounds = contentBounds(in: rep) else {
            return nil
        }

        return Frame(index: index, path: url.path, rep: rep, bounds: bounds)
    }
    .sorted { $0.index < $1.index }

if frames.isEmpty {
    print("No matching frames.")
    exit(0)
}

let reportPath = "\(projectRoot)/../tools/reports/animation_frame_box_normalization.csv"
try FileManager.default.createDirectory(atPath: URL(fileURLWithPath: reportPath).deletingLastPathComponent().path, withIntermediateDirectories: true)
var report = "mode,characterKey,pose,frame,path,oldWidth,oldHeight,newWidth,newHeight,xScale,yScale\n"

for frame in frames {
    let normalized = try normalizedImage(from: frame, targetWidth: targetWidth, targetHeight: targetHeight)
    if applyChanges {
        try save(normalized, to: frame.path)
    }

    report += [
        applyChanges ? "apply" : "dry-run",
        characterKey,
        pose,
        "\(frame.index)",
        frame.path,
        "\(frame.bounds.width)",
        "\(frame.bounds.height)",
        "\(targetWidth)",
        "\(targetHeight)",
        String(format: "%.3f", Double(targetWidth) / Double(frame.bounds.width)),
        String(format: "%.3f", Double(targetHeight) / Double(frame.bounds.height))
    ].joined(separator: ",") + "\n"
}

try report.write(toFile: reportPath, atomically: true, encoding: .utf8)
print("\(applyChanges ? "Applied" : "Dry-run") \(frames.count) frame box normalizations.")
print("Report: \(reportPath)")
