import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

struct CropJob {
    let source: String
    let output: String
    let rect: CGRect
}

let projectRoot = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let assetRoot = projectRoot.appendingPathComponent("WitchTowerGame/Assets/Resources/UI/HomeRedesign")

let bottomSource = "HomeBottomNavBar.png"
let topSource = "HomeTopHudFrame.png"

let jobs: [CropJob] = [
    CropJob(source: bottomSource, output: "HomeBottomNavShop.png", rect: CGRect(x: 0, y: 0, width: 216, height: 190)),
    CropJob(source: bottomSource, output: "HomeBottomNavGacha.png", rect: CGRect(x: 216, y: 0, width: 216, height: 190)),
    CropJob(source: bottomSource, output: "HomeBottomNavDex.png", rect: CGRect(x: 432, y: 0, width: 216, height: 190)),
    CropJob(source: bottomSource, output: "HomeBottomNavEquipment.png", rect: CGRect(x: 648, y: 0, width: 216, height: 190)),
    CropJob(source: bottomSource, output: "HomeBottomNavFusion.png", rect: CGRect(x: 864, y: 0, width: 216, height: 190)),

    CropJob(source: topSource, output: "HomeTopHudProfile.png", rect: CGRect(x: 0, y: 0, width: 190, height: 132)),
    CropJob(source: topSource, output: "HomeTopHudGold.png", rect: CGRect(x: 180, y: 0, width: 232, height: 132)),
    CropJob(source: topSource, output: "HomeTopHudFreeStone.png", rect: CGRect(x: 392, y: 0, width: 224, height: 132)),
    CropJob(source: topSource, output: "HomeTopHudPaidStone.png", rect: CGRect(x: 602, y: 0, width: 226, height: 132)),
    CropJob(source: topSource, output: "HomeTopHudExp.png", rect: CGRect(x: 810, y: 0, width: 230, height: 132)),
]

func loadImage(_ url: URL) throws -> CGImage {
    guard
        let source = CGImageSourceCreateWithURL(url as CFURL, nil),
        let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
    else {
        throw NSError(domain: "split-home-assets", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not load \(url.path)"])
    }

    return image
}

func writePNG(_ image: CGImage, to url: URL) throws {
    guard let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil) else {
        throw NSError(domain: "split-home-assets", code: 2, userInfo: [NSLocalizedDescriptionKey: "Could not create \(url.path)"])
    }

    CGImageDestinationAddImage(destination, image, nil)
    guard CGImageDestinationFinalize(destination) else {
        throw NSError(domain: "split-home-assets", code: 3, userInfo: [NSLocalizedDescriptionKey: "Could not write \(url.path)"])
    }
}

var cache: [String: CGImage] = [:]

for job in jobs {
    let sourceImage: CGImage
    if let cached = cache[job.source] {
        sourceImage = cached
    } else {
        sourceImage = try loadImage(assetRoot.appendingPathComponent(job.source))
        cache[job.source] = sourceImage
    }

    guard let cropped = sourceImage.cropping(to: job.rect) else {
        throw NSError(domain: "split-home-assets", code: 4, userInfo: [NSLocalizedDescriptionKey: "Could not crop \(job.output)"])
    }

    try writePNG(cropped, to: assetRoot.appendingPathComponent(job.output))
    print("wrote \(job.output)")
}
