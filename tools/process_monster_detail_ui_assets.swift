import AppKit
import CoreGraphics
import Foundation

struct CropSpec {
    let name: String
    let x: Int
    let y: Int
    let width: Int
    let height: Int
}

let arguments = CommandLine.arguments
guard arguments.count >= 3 else {
    fputs("Usage: swift process_monster_detail_ui_assets.swift <source.png> <output-dir>\n", stderr)
    exit(2)
}

let sourcePath = arguments[1]
let outputDirectory = arguments[2]
let sourceURL = URL(fileURLWithPath: sourcePath)
guard
    let sourceImage = NSImage(contentsOf: sourceURL),
    let cgImage = sourceImage.cgImage(forProposedRect: nil, context: nil, hints: nil)
else {
    fputs("Failed to load source image: \(sourcePath)\n", stderr)
    exit(1)
}

let width = cgImage.width
let height = cgImage.height
let colorSpace = CGColorSpaceCreateDeviceRGB()
let bytesPerPixel = 4
let bytesPerRow = width * bytesPerPixel
var pixels = [UInt8](repeating: 0, count: height * bytesPerRow)
guard let context = CGContext(
    data: &pixels,
    width: width,
    height: height,
    bitsPerComponent: 8,
    bytesPerRow: bytesPerRow,
    space: colorSpace,
    bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
else {
    fputs("Failed to create bitmap context.\n", stderr)
    exit(1)
}

context.draw(cgImage, in: CGRect(x: 0, y: 0, width: width, height: height))

for y in 0..<height {
    for x in 0..<width {
        let offset = (y * bytesPerRow) + (x * bytesPerPixel)
        let r = Int(pixels[offset])
        let g = Int(pixels[offset + 1])
        let b = Int(pixels[offset + 2])

        let isChromaGreen = g > 35 && (g - r) > 25 && (g - b) > 20
        if isChromaGreen {
            pixels[offset] = 0
            pixels[offset + 1] = 0
            pixels[offset + 2] = 0
            pixels[offset + 3] = 0
        }
    }
}

try FileManager.default.createDirectory(
    at: URL(fileURLWithPath: outputDirectory),
    withIntermediateDirectories: true)

func savePNG(name: String, crop: CGRect) throws {
    guard
        let baseContext = CGContext(
            data: &pixels,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: bytesPerRow,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue),
        let fullImage = baseContext.makeImage(),
        let cropped = fullImage.cropping(to: crop)
    else {
        throw NSError(domain: "MonsterDetailAssetProcessing", code: 1)
    }

    let bitmap = NSBitmapImageRep(cgImage: cropped)
    guard let data = bitmap.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "MonsterDetailAssetProcessing", code: 2)
    }

    let url = URL(fileURLWithPath: outputDirectory).appendingPathComponent(name)
    try data.write(to: url)
    print(url.path)
}

// The generated sheet is fixed at 1254x1254. These crops keep generous edge padding
// so the ornate corners do not get clipped when Unity scales the UI.
let specs = [
    CropSpec(name: "MonsterDetailPanel.png", x: 70, y: 34, width: 1068, height: 900),
    CropSpec(name: "MonsterDetailStatRow.png", x: 76, y: 1010, width: 770, height: 168),
    CropSpec(name: "MonsterDetailCloseButton.png", x: 910, y: 966, width: 280, height: 260)
]

try savePNG(name: "MonsterDetailAssetSheet.png", crop: CGRect(x: 0, y: 0, width: width, height: height))
for spec in specs {
    try savePNG(
        name: spec.name,
        crop: CGRect(x: spec.x, y: spec.y, width: spec.width, height: spec.height))
}
