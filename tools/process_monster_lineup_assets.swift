import AppKit
import Foundation

struct Pixel {
    var r: UInt8
    var g: UInt8
    var b: UInt8
    var a: UInt8
}

struct MonsterEntry {
    let raceFolder: String
    let sourceFolder: String
    let resourceFolder: String
    let outputKey: String
    let monsterId: String
    let displayName: String
    let raceId: String
    let classRank: Int
    let encyclopediaNumber: Int
    let element: Int
    let rangeType: Int
    let damageType: Int
    let maxHp: Int
    let attack: Int
    let magicAttack: Int
    let defense: Int
    let magicDefense: Int
    let attackSpeed: String
    let attackRange: String
    let normalAttackTargetCount: Int
    let description: String
    let fusionExclusive: Int

    init(
        raceFolder: String,
        sourceFolder: String,
        resourceFolder: String,
        outputKey: String,
        monsterId: String,
        displayName: String,
        raceId: String,
        classRank: Int,
        encyclopediaNumber: Int,
        element: Int,
        rangeType: Int,
        damageType: Int,
        maxHp: Int,
        attack: Int,
        magicAttack: Int,
        defense: Int,
        magicDefense: Int,
        attackSpeed: String,
        attackRange: String,
        normalAttackTargetCount: Int,
        description: String,
        fusionExclusive: Int = 0)
    {
        self.raceFolder = raceFolder
        self.sourceFolder = sourceFolder
        self.resourceFolder = resourceFolder
        self.outputKey = outputKey
        self.monsterId = monsterId
        self.displayName = displayName
        self.raceId = raceId
        self.classRank = classRank
        self.encyclopediaNumber = encyclopediaNumber
        self.element = element
        self.rangeType = rangeType
        self.damageType = damageType
        self.maxHp = maxHp
        self.attack = attack
        self.magicAttack = magicAttack
        self.defense = defense
        self.magicDefense = magicDefense
        self.attackSpeed = attackSpeed
        self.attackRange = attackRange
        self.normalAttackTargetCount = normalAttackTargetCount
        self.description = description
        self.fusionExclusive = fusionExclusive
    }
}

struct IntRect {
    let x: Int
    let y: Int
    let width: Int
    let height: Int

    var maxX: Int { x + width }
    var maxY: Int { y + height }
}

let entries: [MonsterEntry] = [
    MonsterEntry(raceFolder: "ドラゴン", sourceFolder: "ドラゴン1", resourceFolder: "Dragon", outputKey: "dragon_whelp", monsterId: "monster_dragon_whelp", displayName: "ヒナドラ", raceId: "dragon", classRank: 1, encyclopediaNumber: 1, element: 3, rangeType: 1, damageType: 1, maxHp: 58, attack: 13, magicAttack: 12, defense: 9, magicDefense: 8, attackSpeed: "1.08", attackRange: "1.55", normalAttackTargetCount: 1, description: "炎の息を覚え始めた幼いドラゴン。素早く動き、序盤の万能アタッカーとして扱いやすい。", fusionExclusive: 0),
    MonsterEntry(raceFolder: "ドラゴン", sourceFolder: "ドラゴン2", resourceFolder: "Dragon", outputKey: "flare_drake", monsterId: "monster_flare_drake", displayName: "フレアドレイク", raceId: "dragon", classRank: 2, encyclopediaNumber: 2, element: 3, rangeType: 1, damageType: 1, maxHp: 86, attack: 21, magicAttack: 23, defense: 15, magicDefense: 14, attackSpeed: "0.98", attackRange: "1.85", normalAttackTargetCount: 1, description: "火炎をまとって飛ぶ成長期の竜。中距離からブレスで敵を焼き払う。", fusionExclusive: 0),
    MonsterEntry(raceFolder: "ドラゴン", sourceFolder: "ドラゴン3", resourceFolder: "Dragon", outputKey: "abyss_dragon", monsterId: "monster_abyss_dragon", displayName: "蒼黒竜アビス", raceId: "dragon", classRank: 3, encyclopediaNumber: 3, element: 5, rangeType: 1, damageType: 1, maxHp: 126, attack: 30, magicAttack: 38, defense: 24, magicDefense: 25, attackSpeed: "0.86", attackRange: "2.25", normalAttackTargetCount: 2, description: "蒼黒の炎を操る上位竜。広い射程と重い一撃で戦線を支配する。", fusionExclusive: 0),

    MonsterEntry(raceFolder: "ロボット", sourceFolder: "ロボット1", resourceFolder: "Robot", outputKey: "chibi_gear", monsterId: "monster_chibi_gear", displayName: "チビギア", raceId: "robot", classRank: 1, encyclopediaNumber: 4, element: 4, rangeType: 0, damageType: 0, maxHp: 62, attack: 16, magicAttack: 4, defense: 13, magicDefense: 7, attackSpeed: "1.02", attackRange: "1.05", normalAttackTargetCount: 1, description: "小さな歯車機兵。軽快な動きで前線を支えるロボット系の下位個体。"),
    MonsterEntry(raceFolder: "ロボット", sourceFolder: "ロボット2", resourceFolder: "Robot", outputKey: "armed_droid", monsterId: "monster_armed_droid", displayName: "アームドロイド", raceId: "robot", classRank: 2, encyclopediaNumber: 5, element: 4, rangeType: 0, damageType: 0, maxHp: 92, attack: 25, magicAttack: 6, defense: 22, magicDefense: 13, attackSpeed: "0.92", attackRange: "1.15", normalAttackTargetCount: 1, description: "武装を増設した中位ドロイド。耐久と物理攻撃のバランスに優れる。"),
    MonsterEntry(raceFolder: "ロボット", sourceFolder: "ロボット3", resourceFolder: "Robot", outputKey: "omega_leon", monsterId: "monster_omega_leon", displayName: "機皇オメガレオン", raceId: "robot", classRank: 3, encyclopediaNumber: 6, element: 4, rangeType: 0, damageType: 0, maxHp: 138, attack: 39, magicAttack: 10, defense: 32, magicDefense: 20, attackSpeed: "0.82", attackRange: "1.30", normalAttackTargetCount: 1, description: "獅子型の機皇。重装甲と高火力で敵陣を押し返すロボット系上位個体。"),

    MonsterEntry(raceFolder: "ゴーレム", sourceFolder: "ゴーレム1", resourceFolder: "Golem", outputKey: "rock_golem", monsterId: "monster_rock_golem", displayName: "ロックゴーレム", raceId: "golem", classRank: 1, encyclopediaNumber: 7, element: 1, rangeType: 0, damageType: 0, maxHp: 82, attack: 16, magicAttack: 3, defense: 22, magicDefense: 10, attackSpeed: "0.78", attackRange: "0.95", normalAttackTargetCount: 1, description: "岩でできた下位ゴーレム。動きは遅いが、前線で敵を受け止める。"),
    MonsterEntry(raceFolder: "ゴーレム", sourceFolder: "ゴーレム2", resourceFolder: "Golem", outputKey: "ore_giant_garm", monsterId: "monster_ore_giant_garm", displayName: "鉱石巨人ガルム", raceId: "golem", classRank: 2, encyclopediaNumber: 8, element: 1, rangeType: 0, damageType: 0, maxHp: 124, attack: 24, magicAttack: 5, defense: 34, magicDefense: 17, attackSpeed: "0.70", attackRange: "1.05", normalAttackTargetCount: 1, description: "鉱石を核に成長した巨人。高い防御力で長く戦場に残る。"),
    MonsterEntry(raceFolder: "ゴーレム", sourceFolder: "ゴーレム3", resourceFolder: "Golem", outputKey: "cosmic_ore_fortress_golem", monsterId: "monster_cosmic_ore_fortress_golem", displayName: "宇宙鉱石要塞ゴーレム", raceId: "golem", classRank: 3, encyclopediaNumber: 9, element: 5, rangeType: 0, damageType: 0, maxHp: 184, attack: 35, magicAttack: 8, defense: 50, magicDefense: 28, attackSpeed: "0.62", attackRange: "1.15", normalAttackTargetCount: 1, description: "宇宙鉱石で構成された要塞級ゴーレム。圧倒的な耐久力を誇る。"),

    MonsterEntry(raceFolder: "剣士", sourceFolder: "剣士1", resourceFolder: "Swordsman", outputKey: "apprentice_swordsman", monsterId: "monster_apprentice_swordsman", displayName: "見習い剣士", raceId: "swordsman", classRank: 1, encyclopediaNumber: 10, element: 4, rangeType: 0, damageType: 0, maxHp: 56, attack: 18, magicAttack: 3, defense: 10, magicDefense: 7, attackSpeed: "1.18", attackRange: "1.00", normalAttackTargetCount: 1, description: "剣を学び始めた下位剣士。手数が多く、扱いやすい近接アタッカー。"),
    MonsterEntry(raceFolder: "剣士", sourceFolder: "剣士2", resourceFolder: "Swordsman", outputKey: "holy_armor_leon", monsterId: "monster_holy_armor_leon", displayName: "聖鎧剣士レオン", raceId: "swordsman", classRank: 2, encyclopediaNumber: 11, element: 4, rangeType: 0, damageType: 0, maxHp: 84, attack: 29, magicAttack: 6, defense: 18, magicDefense: 13, attackSpeed: "1.08", attackRange: "1.08", normalAttackTargetCount: 1, description: "聖鎧をまとった中位剣士。攻守のバランスがよく、前線で安定して戦える。"),
    MonsterEntry(raceFolder: "剣士", sourceFolder: "剣士3", resourceFolder: "Swordsman", outputKey: "sword_saint_alvarez", monsterId: "monster_sword_saint_alvarez", displayName: "剣聖アルヴァレス", raceId: "swordsman", classRank: 3, encyclopediaNumber: 12, element: 4, rangeType: 0, damageType: 0, maxHp: 118, attack: 45, magicAttack: 10, defense: 28, magicDefense: 20, attackSpeed: "1.00", attackRange: "1.18", normalAttackTargetCount: 2, description: "極めた剣技で敵を斬り伏せる上位剣士。高い物理火力を持つ。"),

    MonsterEntry(raceFolder: "魔法使い", sourceFolder: "魔法使い1", resourceFolder: "Mage", outputKey: "apprentice_mage", monsterId: "monster_apprentice_mage", displayName: "見習い魔導士", raceId: "mage", classRank: 1, encyclopediaNumber: 13, element: 5, rangeType: 1, damageType: 1, maxHp: 44, attack: 5, magicAttack: 19, defense: 7, magicDefense: 13, attackSpeed: "1.04", attackRange: "2.00", normalAttackTargetCount: 1, description: "魔力の扱いを覚えた下位魔導士。遠距離から魔法攻撃を放つ。"),
    MonsterEntry(raceFolder: "魔法使い", sourceFolder: "魔法使い2", resourceFolder: "Mage", outputKey: "dark_robe_curse_mage_noah", monsterId: "monster_dark_robe_curse_mage_noah", displayName: "黒衣の呪術師ノア", raceId: "mage", classRank: 2, encyclopediaNumber: 14, element: 5, rangeType: 1, damageType: 1, maxHp: 66, attack: 8, magicAttack: 32, defense: 11, magicDefense: 22, attackSpeed: "0.96", attackRange: "2.25", normalAttackTargetCount: 1, description: "黒衣をまとった中位呪術師。高い魔力で後衛から敵を削る。"),
    MonsterEntry(raceFolder: "魔法使い", sourceFolder: "魔法使い3", resourceFolder: "Mage", outputKey: "abyss_grand_mage_seraphis", monsterId: "monster_abyss_grand_mage_seraphis", displayName: "深淵大魔導セラフィス", raceId: "mage", classRank: 3, encyclopediaNumber: 15, element: 5, rangeType: 1, damageType: 1, maxHp: 94, attack: 12, magicAttack: 52, defense: 18, magicDefense: 35, attackSpeed: "0.88", attackRange: "2.50", normalAttackTargetCount: 2, description: "深淵の魔法を操る上位魔導士。広い射程と強力な魔法攻撃を持つ。"),

    MonsterEntry(raceFolder: "4クラス", sourceFolder: "機竜ヴァルドレイク", resourceFolder: "Class4", outputKey: "mecha_dragon_valdrake", monsterId: "monster_mecha_dragon_valdrake", displayName: "機竜ヴァルドレイク", raceId: "special", classRank: 4, encyclopediaNumber: 16, element: 4, rangeType: 1, damageType: 1, maxHp: 176, attack: 42, magicAttack: 54, defense: 36, magicDefense: 30, attackSpeed: "0.82", attackRange: "2.35", normalAttackTargetCount: 2, description: "竜の魔力と機械装甲が融合した特殊個体。中距離から機械竜の砲撃を放つ。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "竜岩巨兵ドラグガイア", resourceFolder: "Class4", outputKey: "drag_gaia", monsterId: "monster_drag_gaia", displayName: "竜岩巨兵ドラグガイア", raceId: "special", classRank: 4, encyclopediaNumber: 17, element: 1, rangeType: 0, damageType: 0, maxHp: 252, attack: 48, magicAttack: 20, defense: 66, magicDefense: 38, attackSpeed: "0.58", attackRange: "1.25", normalAttackTargetCount: 2, description: "竜骨と巨岩が一体化した特殊巨兵。巨大な体で敵群を受け止める。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "竜剣聖アギト", resourceFolder: "Class4", outputKey: "dragon_sword_saint_agito", monsterId: "monster_dragon_sword_saint_agito", displayName: "竜剣聖アギト", raceId: "special", classRank: 4, encyclopediaNumber: 18, element: 3, rangeType: 0, damageType: 0, maxHp: 164, attack: 68, magicAttack: 18, defense: 38, magicDefense: 26, attackSpeed: "1.04", attackRange: "1.32", normalAttackTargetCount: 2, description: "竜の闘気をまとった剣聖。素早い斬撃で前線を切り開く。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "深淵竜魔導ヴァルフレア", resourceFolder: "Class4", outputKey: "abyss_dragon_mage_valflare", monsterId: "monster_abyss_dragon_mage_valflare", displayName: "深淵竜魔導ヴァルフレア", raceId: "special", classRank: 4, encyclopediaNumber: 19, element: 5, rangeType: 1, damageType: 1, maxHp: 156, attack: 24, magicAttack: 76, defense: 30, magicDefense: 52, attackSpeed: "0.78", attackRange: "2.75", normalAttackTargetCount: 3, description: "深淵竜と大魔導の力が重なった特殊魔導竜。広範囲に暗黒炎を放つ。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "要塞機兵ギガフォート", resourceFolder: "Class4", outputKey: "fortress_machine_gigafort", monsterId: "monster_fortress_machine_gigafort", displayName: "要塞機兵ギガフォート", raceId: "special", classRank: 4, encyclopediaNumber: 20, element: 4, rangeType: 0, damageType: 0, maxHp: 270, attack: 50, magicAttack: 12, defense: 72, magicDefense: 34, attackSpeed: "0.55", attackRange: "1.18", normalAttackTargetCount: 2, description: "要塞級の装甲を持つ機械兵。圧倒的な防御力で戦線を固定する。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "機甲剣聖グランセイバー", resourceFolder: "Class4", outputKey: "mecha_sword_saint_gransaber", monsterId: "monster_mecha_sword_saint_gransaber", displayName: "機甲剣聖グランセイバー", raceId: "special", classRank: 4, encyclopediaNumber: 21, element: 4, rangeType: 0, damageType: 0, maxHp: 168, attack: 72, magicAttack: 12, defense: 42, magicDefense: 24, attackSpeed: "0.98", attackRange: "1.28", normalAttackTargetCount: 2, description: "機械装甲で剣技を増幅する特殊剣聖。鋭い連撃で敵を削る。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "暗黒魔導機神メルキオン", resourceFolder: "Class4", outputKey: "dark_magic_machine_god_merchion", monsterId: "monster_dark_magic_machine_god_merchion", displayName: "暗黒魔導機神メルキオン", raceId: "special", classRank: 4, encyclopediaNumber: 22, element: 5, rangeType: 1, damageType: 1, maxHp: 178, attack: 22, magicAttack: 74, defense: 42, magicDefense: 48, attackSpeed: "0.74", attackRange: "2.65", normalAttackTargetCount: 3, description: "暗黒魔導を内蔵した機神。遠距離から敵群を魔力砲で焼き払う。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "巨岩騎士ガイアス", resourceFolder: "Class4", outputKey: "rock_knight_gaius", monsterId: "monster_rock_knight_gaius", displayName: "巨岩騎士ガイアス", raceId: "special", classRank: 4, encyclopediaNumber: 23, element: 1, rangeType: 0, damageType: 0, maxHp: 230, attack: 58, magicAttack: 8, defense: 64, magicDefense: 30, attackSpeed: "0.66", attackRange: "1.22", normalAttackTargetCount: 2, description: "岩の肉体に騎士の剣技を宿した特殊個体。重い一撃で敵を粉砕する。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "星蝕魔像アストラルゴーレム", resourceFolder: "Class4", outputKey: "astral_eclipse_golem", monsterId: "monster_astral_eclipse_golem", displayName: "星蝕魔像アストラルゴーレム", raceId: "special", classRank: 4, encyclopediaNumber: 24, element: 5, rangeType: 1, damageType: 1, maxHp: 212, attack: 18, magicAttack: 66, defense: 58, magicDefense: 58, attackSpeed: "0.64", attackRange: "2.45", normalAttackTargetCount: 3, description: "星蝕の魔力を宿した魔像。高耐久と範囲魔法を両立する。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "魔剣聖ルシエル", resourceFolder: "Class4", outputKey: "magic_sword_saint_luciel", monsterId: "monster_magic_sword_saint_luciel", displayName: "魔剣聖ルシエル", raceId: "special", classRank: 4, encyclopediaNumber: 25, element: 5, rangeType: 0, damageType: 1, maxHp: 148, attack: 52, magicAttack: 58, defense: 34, magicDefense: 42, attackSpeed: "1.02", attackRange: "1.36", normalAttackTargetCount: 2, description: "剣技と魔導を極めた特殊剣聖。近接から魔力をまとった斬撃を放つ。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "熾天使ミカエル", resourceFolder: "Class4", outputKey: "seraph_michael", monsterId: "monster_seraph_michael", displayName: "熾天使ミカエル", raceId: "angel", classRank: 4, encyclopediaNumber: 26, element: 4, rangeType: 1, damageType: 1, maxHp: 168, attack: 28, magicAttack: 72, defense: 40, magicDefense: 60, attackSpeed: "0.84", attackRange: "2.55", normalAttackTargetCount: 3, description: "天使系の神話級個体。聖なる光で敵を浄化する。", fusionExclusive: 1),
    MonsterEntry(raceFolder: "4クラス", sourceFolder: "精霊女王ティターニア", resourceFolder: "Class4", outputKey: "spirit_queen_titania", monsterId: "monster_spirit_queen_titania", displayName: "精霊女王ティターニア", raceId: "spirit", classRank: 4, encyclopediaNumber: 27, element: 1, rangeType: 1, damageType: 1, maxHp: 152, attack: 18, magicAttack: 70, defense: 34, magicDefense: 62, attackSpeed: "0.90", attackRange: "2.60", normalAttackTargetCount: 3, description: "精霊系の神話級個体。自然の魔力を操り、後衛から戦場を支配する。", fusionExclusive: 1)
]

func normalizedName(_ value: String) -> String {
    value
        .precomposedStringWithCanonicalMapping
        .replacingOccurrences(of: "１", with: "1")
        .replacingOccurrences(of: "２", with: "2")
        .replacingOccurrences(of: "３", with: "3")
}

func findChildDirectory(in parent: String, named targetName: String) throws -> String {
    let target = normalizedName(targetName)
    let children = try FileManager.default.contentsOfDirectory(atPath: parent)
    for child in children {
        let path = "\(parent)/\(child)"
        var isDirectory: ObjCBool = false
        if FileManager.default.fileExists(atPath: path, isDirectory: &isDirectory),
           isDirectory.boolValue,
           normalizedName(child) == target {
            return path
        }
    }

    throw NSError(domain: "process_monster_lineup_assets", code: 1, userInfo: [NSLocalizedDescriptionKey: "directory not found: \(targetName) in \(parent)"])
}

func findImage(in directory: String, preferredName: String) throws -> String {
    let preferredPath = "\(directory)/\(preferredName)"
    if FileManager.default.fileExists(atPath: preferredPath) {
        return preferredPath
    }

    if preferredName == "待機.png" {
        let spriteSheetPath = "\(directory)/待機スプライト.png"
        if FileManager.default.fileExists(atPath: spriteSheetPath) {
            return spriteSheetPath
        }
    }

    if preferredName == "姿絵.png" {
        let reserved = Set(["待機.png", "待機スプライト.png", "移動.png", "攻撃.png", "エフェクト.png", ".DS_Store"])
        let candidates = try FileManager.default.contentsOfDirectory(atPath: directory)
            .filter { $0.lowercased().hasSuffix(".png") && !reserved.contains($0) && !$0.hasPrefix("待機") }
            .sorted()
        if let fallback = candidates.first {
            return "\(directory)/\(fallback)"
        }
    }

    if preferredName == "攻撃.png" {
        let reserved = Set(["姿絵.png", "待機.png", "移動.png", "エフェクト.png", ".DS_Store"])
        let candidates = try FileManager.default.contentsOfDirectory(atPath: directory)
            .filter { $0.lowercased().hasSuffix(".png") && !reserved.contains($0) }
            .sorted()
        if let fallback = candidates.first {
            return "\(directory)/\(fallback)"
        }
    }

    throw NSError(domain: "process_monster_lineup_assets", code: 2, userInfo: [NSLocalizedDescriptionKey: "image not found: \(preferredName) in \(directory)"])
}

func loadImage(_ path: String) throws -> NSImage {
    guard let image = NSImage(contentsOfFile: path) else {
        throw NSError(domain: "process_monster_lineup_assets", code: 3, userInfo: [NSLocalizedDescriptionKey: "failed to load \(path)"])
    }

    return image
}

func bitmap(from image: NSImage, rect: NSRect) -> NSBitmapImageRep {
    let target = NSImage(size: rect.size)
    target.lockFocus()
    NSColor.clear.set()
    NSRect(origin: .zero, size: rect.size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    image.draw(in: NSRect(origin: .zero, size: rect.size), from: rect, operation: .copy, fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func image(from rep: NSBitmapImageRep) -> NSImage {
    let source = NSImage(size: NSSize(width: rep.pixelsWide, height: rep.pixelsHigh))
    source.addRepresentation(rep)
    return source
}

func bitmap(from rep: NSBitmapImageRep, rect: NSRect) -> NSBitmapImageRep {
    let source = image(from: rep)
    let target = NSImage(size: rect.size)
    target.lockFocus()
    NSColor.clear.set()
    NSRect(origin: .zero, size: rect.size).fill()
    NSGraphicsContext.current?.imageInterpolation = .none
    source.draw(in: NSRect(origin: .zero, size: rect.size), from: rect, operation: .copy, fraction: 1.0)
    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func isBackgroundCandidate(_ pixel: Pixel) -> Bool {
    if pixel.a == 0 {
        return false
    }

    let minRGB = min(pixel.r, min(pixel.g, pixel.b))
    let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
    return minRGB >= 180 && (maxRGB - minRGB) <= 72
}

func clearEdgeBackground(_ rep: NSBitmapImageRep) {
    guard let data = rep.bitmapData else {
        return
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    let bytesPerRow = rep.bytesPerRow

    if bytesPerPixel < 4 {
        return
    }

    func readPixel(_ x: Int, _ y: Int) -> Pixel {
        let offset = y * bytesPerRow + x * bytesPerPixel
        return Pixel(r: data[offset], g: data[offset + 1], b: data[offset + 2], a: data[offset + 3])
    }

    func clear(_ x: Int, _ y: Int) {
        let offset = y * bytesPerRow + x * bytesPerPixel
        data[offset] = 0
        data[offset + 1] = 0
        data[offset + 2] = 0
        data[offset + 3] = 0
    }

    var visited = Array(repeating: false, count: width * height)
    func mark(_ x: Int, _ y: Int) { visited[y * width + x] = true }
    func hasVisited(_ x: Int, _ y: Int) -> Bool { visited[y * width + x] }

    var queue: [(Int, Int)] = []
    queue.reserveCapacity((width * 2) + (height * 2))

    func enqueue(_ x: Int, _ y: Int) {
        if x < 0 || y < 0 || x >= width || y >= height || hasVisited(x, y) {
            return
        }

        if isBackgroundCandidate(readPixel(x, y)) {
            mark(x, y)
            queue.append((x, y))
        }
    }

    for x in 0..<width {
        enqueue(x, 0)
        enqueue(x, height - 1)
    }

    for y in 0..<height {
        enqueue(0, y)
        enqueue(width - 1, y)
    }

    var index = 0
    while index < queue.count {
        let (x, y) = queue[index]
        index += 1
        clear(x, y)
        for neighborY in max(0, y - 1)...min(height - 1, y + 1) {
            for neighborX in max(0, x - 1)...min(width - 1, x + 1) {
                if neighborX == x && neighborY == y {
                    continue
                }

                enqueue(neighborX, neighborY)
            }
        }
    }

    func touchesTransparent(_ x: Int, _ y: Int, radius: Int) -> Bool {
        for neighborY in max(0, y - radius)...min(height - 1, y + radius) {
            for neighborX in max(0, x - radius)...min(width - 1, x + radius) {
                if neighborX == x && neighborY == y {
                    continue
                }

                if readPixel(neighborX, neighborY).a <= 8 {
                    return true
                }
            }
        }

        return false
    }

    func isDetachedBackgroundCandidate(_ pixel: Pixel) -> Bool {
        if pixel.a <= 8 {
            return false
        }

        let minRGB = min(pixel.r, min(pixel.g, pixel.b))
        let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
        let saturation = maxRGB - minRGB
        return (minRGB >= 220 && saturation <= 52) ||
            (minRGB >= 205 && saturation <= 22)
    }

    func collectDetachedComponent(startX: Int, startY: Int, visited componentVisited: inout [Bool]) -> (pixels: [(Int, Int)], touchesTransparent: Bool, averageMinRGB: Int, averageSaturation: Int) {
        var component: [(Int, Int)] = []
        var queue: [(Int, Int)] = [(startX, startY)]
        componentVisited[startY * width + startX] = true
        var index = 0
        var sumMinRGB = 0
        var sumSaturation = 0
        var componentTouchesTransparent = false

        while index < queue.count {
            let (x, y) = queue[index]
            index += 1
            component.append((x, y))

            let pixel = readPixel(x, y)
            let minRGB = Int(min(pixel.r, min(pixel.g, pixel.b)))
            let maxRGB = Int(max(pixel.r, max(pixel.g, pixel.b)))
            sumMinRGB += minRGB
            sumSaturation += maxRGB - minRGB
            componentTouchesTransparent = componentTouchesTransparent || touchesTransparent(x, y, radius: 2)

            for neighborY in max(0, y - 1)...min(height - 1, y + 1) {
                for neighborX in max(0, x - 1)...min(width - 1, x + 1) {
                    if neighborX == x && neighborY == y {
                        continue
                    }

                    let neighborIndex = neighborY * width + neighborX
                    if componentVisited[neighborIndex] || !isDetachedBackgroundCandidate(readPixel(neighborX, neighborY)) {
                        continue
                    }

                    componentVisited[neighborIndex] = true
                    queue.append((neighborX, neighborY))
                }
            }
        }

        let count = max(1, component.count)
        return (
            pixels: component,
            touchesTransparent: componentTouchesTransparent,
            averageMinRGB: sumMinRGB / count,
            averageSaturation: sumSaturation / count)
    }

    var componentVisited = visited
    let minimumDetachedArea = max(12, (width * height) / 20_000)
    for y in 0..<height {
        for x in 0..<width {
            let index = y * width + x
            if componentVisited[index] || !isDetachedBackgroundCandidate(readPixel(x, y)) {
                continue
            }

            let component = collectDetachedComponent(startX: x, startY: y, visited: &componentVisited)
            let clearlyWhiteIsland = component.pixels.count >= minimumDetachedArea &&
                component.averageMinRGB >= 238 &&
                component.averageSaturation <= 32
            let checkerboardIsland = component.pixels.count >= minimumDetachedArea &&
                component.averageMinRGB >= 214 &&
                component.averageSaturation <= 14

            if component.touchesTransparent || clearlyWhiteIsland || checkerboardIsland {
                for (componentX, componentY) in component.pixels {
                    clear(componentX, componentY)
                }
            }
        }
    }

    func isWhiteMatteFringe(_ pixel: Pixel) -> Bool {
        if pixel.a <= 8 {
            return false
        }

        let minRGB = min(pixel.r, min(pixel.g, pixel.b))
        let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
        let saturation = maxRGB - minRGB
        return (minRGB >= 230 && saturation <= 70) || (minRGB >= 204 && saturation <= 32 && pixel.a <= 245)
    }

    for _ in 0..<4 {
        var pixelsToClear: [(Int, Int)] = []
        for y in 0..<height {
            for x in 0..<width {
                let pixel = readPixel(x, y)
                if isWhiteMatteFringe(pixel) && touchesTransparent(x, y, radius: 2) {
                    pixelsToClear.append((x, y))
                }
            }
        }

        if pixelsToClear.isEmpty {
            break
        }

        for (x, y) in pixelsToClear {
            clear(x, y)
        }
    }

    for y in 0..<height {
        for x in 0..<width {
            let offset = y * bytesPerRow + x * bytesPerPixel
            let alpha = data[offset + 3]
            if alpha <= 8 {
                data[offset] = 0
                data[offset + 1] = 0
                data[offset + 2] = 0
                data[offset + 3] = 0
                continue
            }

            let pixel = Pixel(r: data[offset], g: data[offset + 1], b: data[offset + 2], a: alpha)
            let minRGB = min(pixel.r, min(pixel.g, pixel.b))
            let maxRGB = max(pixel.r, max(pixel.g, pixel.b))
            if alpha <= 24 && minRGB >= 215 && (maxRGB - minRGB) <= 60 {
                data[offset] = 0
                data[offset + 1] = 0
                data[offset + 2] = 0
                data[offset + 3] = 0
            }
        }
    }
}

func save(_ rep: NSBitmapImageRep, to path: String) throws {
    guard let png = rep.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "process_monster_lineup_assets", code: 4, userInfo: [NSLocalizedDescriptionKey: "failed to encode \(path)"])
    }

    try png.write(to: URL(fileURLWithPath: path))
}

func contentBounds(in rep: NSBitmapImageRep, xRange: Range<Int>? = nil) -> IntRect? {
    guard let data = rep.bitmapData else {
        return nil
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    let bytesPerRow = rep.bytesPerRow

    if bytesPerPixel < 4 {
        return nil
    }

    let range = xRange ?? 0..<width
    var minX = width
    var minY = height
    var maxX = -1
    var maxY = -1

    for y in 0..<height {
        for x in range {
            let offset = y * bytesPerRow + x * bytesPerPixel
            if data[offset + 3] <= 12 {
                continue
            }

            minX = min(minX, x)
            minY = min(minY, y)
            maxX = max(maxX, x)
            maxY = max(maxY, y)
        }
    }

    if maxX < minX || maxY < minY {
        return nil
    }

    return IntRect(x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1)
}

func columnHasContent(in rep: NSBitmapImageRep, minimumPixels: Int) -> [Bool] {
    guard let data = rep.bitmapData else {
        return []
    }

    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let bytesPerPixel = rep.bitsPerPixel / 8
    let bytesPerRow = rep.bytesPerRow

    if bytesPerPixel < 4 {
        return []
    }

    var result = Array(repeating: false, count: width)
    for x in 0..<width {
        var count = 0
        for y in 0..<height {
            let offset = y * bytesPerRow + x * bytesPerPixel
            if data[offset + 3] > 12 {
                count += 1
                if count >= minimumPixels {
                    result[x] = true
                    break
                }
            }
        }
    }

    return result
}

func bridgedColumns(_ columns: [Bool], maximumGap: Int) -> [Bool] {
    if columns.isEmpty || maximumGap <= 0 {
        return columns
    }

    var bridged = columns
    var x = 0
    while x < bridged.count {
        if bridged[x] {
            x += 1
            continue
        }

        let start = x
        while x < bridged.count && !bridged[x] {
            x += 1
        }

        let end = x
        if start > 0 && end < bridged.count && (end - start) <= maximumGap {
            for index in start..<end {
                bridged[index] = true
            }
        }
    }

    return bridged
}

func spans(from columns: [Bool], minimumWidth: Int) -> [Range<Int>] {
    var result: [Range<Int>] = []
    var x = 0
    while x < columns.count {
        while x < columns.count && !columns[x] {
            x += 1
        }

        let start = x
        while x < columns.count && columns[x] {
            x += 1
        }

        let end = x
        if end > start && (end - start) >= minimumWidth {
            result.append(start..<end)
        }
    }

    return result
}

func mergeNearestSpans(_ input: [Range<Int>], expectedCount: Int) -> [Range<Int>] {
    var merged = input
    while merged.count > expectedCount {
        var bestIndex = 0
        var bestGap = Int.max
        for index in 0..<(merged.count - 1) {
            let gap = merged[index + 1].lowerBound - merged[index].upperBound
            if gap < bestGap {
                bestGap = gap
                bestIndex = index
            }
        }

        let combined = merged[bestIndex].lowerBound..<merged[bestIndex + 1].upperBound
        merged.remove(at: bestIndex + 1)
        merged[bestIndex] = combined
    }

    return merged
}

func padFrame(_ rep: NSBitmapImageRep, targetWidth: Int, targetHeight: Int) -> NSBitmapImageRep {
    let source = image(from: rep)
    let target = NSImage(size: NSSize(width: targetWidth, height: targetHeight))
    target.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: targetWidth, height: targetHeight).fill()
    NSGraphicsContext.current?.imageInterpolation = .none

    let drawX = max(0, (targetWidth - rep.pixelsWide) / 2)
    let drawY = max(0, (targetHeight - rep.pixelsHigh) / 2)
    source.draw(
        in: NSRect(x: drawX, y: drawY, width: rep.pixelsWide, height: rep.pixelsHigh),
        from: NSRect(x: 0, y: 0, width: rep.pixelsWide, height: rep.pixelsHigh),
        operation: .copy,
        fraction: 1.0)

    target.unlockFocus()
    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func extractContentBasedFrames(from sheet: NSBitmapImageRep, expectedFrameCount: Int) -> [NSBitmapImageRep]? {
    let width = sheet.pixelsWide
    let height = sheet.pixelsHigh
    let frameWidth = width / max(1, expectedFrameCount)
    let minimumColumnPixels = max(3, height / 256)
    let minimumSpanWidth = max(12, frameWidth / 12)
    let bridgeGap = min(max(24, frameWidth / 12), 96)
    let horizontalPadding = max(16, frameWidth / 32)

    let columns = bridgedColumns(columnHasContent(in: sheet, minimumPixels: minimumColumnPixels), maximumGap: bridgeGap)
    var detectedSpans = spans(from: columns, minimumWidth: minimumSpanWidth)

    if detectedSpans.count > expectedFrameCount {
        detectedSpans = mergeNearestSpans(detectedSpans, expectedCount: expectedFrameCount)
    }

    guard detectedSpans.count == expectedFrameCount else {
        return nil
    }

    var cropRects: [IntRect] = []
    for span in detectedSpans {
        guard let bounds = contentBounds(in: sheet, xRange: span) else {
            return nil
        }

        let x = max(0, bounds.x - horizontalPadding)
        let maxX = min(width, bounds.maxX + horizontalPadding)
        cropRects.append(IntRect(x: x, y: 0, width: maxX - x, height: height))
    }

    let targetWidth = max(frameWidth, cropRects.map { $0.width }.max() ?? frameWidth)
    let targetHeight = height

    return cropRects.map { rect in
        let cropped = bitmap(
            from: sheet,
            rect: NSRect(x: rect.x, y: rect.y, width: rect.width, height: rect.height))
        let padded = padFrame(cropped, targetWidth: targetWidth, targetHeight: targetHeight)
        clearEdgeBackground(padded)
        return padded
    }
}

func detectCorrectedFrameCount(from sheet: NSBitmapImageRep, estimatedFrameCount: Int) -> Int {
    if estimatedFrameCount >= 4 {
        return estimatedFrameCount
    }

    let width = sheet.pixelsWide
    let height = sheet.pixelsHigh
    let minimumColumnPixels = max(3, height / 256)
    let minimumSpanWidth = max(16, min(width / 18, height / 10))
    let bridgeGap = min(max(18, height / 16), 72)
    let columns = bridgedColumns(columnHasContent(in: sheet, minimumPixels: minimumColumnPixels), maximumGap: bridgeGap)
    let detectedSpans = spans(from: columns, minimumWidth: minimumSpanWidth)

    // Some generated idle sheets have four clearly separated poses on a tall 2:1 canvas.
    // Aspect-ratio slicing reads them as two frames, which makes two monsters appear in one frame.
    if detectedSpans.count == 4 {
        return 4
    }

    return estimatedFrameCount
}

func removeExistingFrames(in outputDirectory: String, prefix: String) throws {
    for index in 0..<16 {
        let path = "\(outputDirectory)/\(prefix)_\(index).png"
        if FileManager.default.fileExists(atPath: path) {
            try FileManager.default.removeItem(atPath: path)
        }
    }
}

func processPortrait(source: String, destination: String) throws {
    let image = try loadImage(source)
    let rep = bitmap(from: image, rect: NSRect(x: 0, y: 0, width: image.size.width, height: image.size.height))
    clearEdgeBackground(rep)
    try save(rep, to: destination)
}

func resizedBitmap(from rep: NSBitmapImageRep, canvasSize: Int) -> NSBitmapImageRep {
    let source = NSImage(size: NSSize(width: rep.pixelsWide, height: rep.pixelsHigh))
    source.addRepresentation(rep)

    let target = NSImage(size: NSSize(width: canvasSize, height: canvasSize))
    target.lockFocus()
    NSColor.clear.set()
    NSRect(x: 0, y: 0, width: canvasSize, height: canvasSize).fill()
    NSGraphicsContext.current?.imageInterpolation = .high
    source.draw(
        in: NSRect(x: 0, y: 0, width: canvasSize, height: canvasSize),
        from: NSRect(x: 0, y: 0, width: source.size.width, height: source.size.height),
        operation: .copy,
        fraction: 1.0)
    target.unlockFocus()

    return NSBitmapImageRep(data: target.tiffRepresentation!)!
}

func processCardPortrait(source: String, destination: String) throws {
    let image = try loadImage(source)
    let rep = bitmap(from: image, rect: NSRect(x: 0, y: 0, width: image.size.width, height: image.size.height))
    clearEdgeBackground(rep)

    let cardRep = resizedBitmap(from: rep, canvasSize: 768)
    clearEdgeBackground(cardRep)
    try save(cardRep, to: destination)
}

func processSheet(source: String, outputDirectory: String, prefix: String) throws {
    let image = try loadImage(source)
    let width = Int(image.size.width.rounded())
    let height = Int(image.size.height.rounded())

    try FileManager.default.createDirectory(atPath: outputDirectory, withIntermediateDirectories: true)
    try removeExistingFrames(in: outputDirectory, prefix: prefix)

    let sheet = bitmap(from: image, rect: NSRect(x: 0, y: 0, width: width, height: height))
    clearEdgeBackground(sheet)
    let estimatedFrameCount = max(1, Int((Double(width) / Double(max(1, height))).rounded()))
    let frameCount = detectCorrectedFrameCount(from: sheet, estimatedFrameCount: estimatedFrameCount)
    let frameWidth = width / frameCount

    if let contentFrames = extractContentBasedFrames(from: sheet, expectedFrameCount: frameCount) {
        for (index, frame) in contentFrames.enumerated() {
            try save(frame, to: "\(outputDirectory)/\(prefix)_\(index).png")
        }

        return
    }

    for index in 0..<frameCount {
        let frame = bitmap(from: sheet, rect: NSRect(x: index * frameWidth, y: 0, width: frameWidth, height: height))
        clearEdgeBackground(frame)
        try save(frame, to: "\(outputDirectory)/\(prefix)_\(index).png")
    }
}

func yamlString(_ value: String) -> String {
    let escaped = value
        .replacingOccurrences(of: "\\", with: "\\\\")
        .replacingOccurrences(of: "\"", with: "\\\"")
    return "\"\(escaped)\""
}

func guidFromMeta(_ metaPath: String) -> String? {
    guard let text = try? String(contentsOfFile: metaPath, encoding: .utf8) else {
        return nil
    }

    for line in text.components(separatedBy: .newlines) {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        if trimmed.hasPrefix("guid:") {
            return trimmed.replacingOccurrences(of: "guid:", with: "").trimmingCharacters(in: .whitespaces)
        }
    }

    return nil
}

func createGuid() -> String {
    UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
}

func ensureAssetMeta(assetPath: String) throws -> String {
    let metaPath = "\(assetPath).meta"
    if let existingGuid = guidFromMeta(metaPath), !existingGuid.isEmpty {
        return existingGuid
    }

    let guid = createGuid()
    let meta = """
fileFormatVersion: 2
guid: \(guid)
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 

"""
    try meta.write(toFile: metaPath, atomically: true, encoding: .utf8)
    return guid
}

func monsterAssetYaml(for entry: MonsterEntry) -> String {
    let rarity = max(1, min(6, entry.classRank))
    let plusHp = entry.classRank == 1 ? 1 : 2
    let plusAttack = entry.classRank == 1 ? 1 : 2
    let plusMagic = entry.damageType == 1 ? max(1, entry.classRank) : 0
    let plusDefense = max(1, entry.classRank)
    let plusMagicDefense = entry.classRank >= 2 ? 1 : 0
    let attackSpeedGrowth = entry.classRank == 1 ? "0.003" : "0.002"

    return """
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: eec78acb125d463d8a56eb9594bb2330, type: 3}
  m_Name: \(entry.monsterId)
  m_EditorClassIdentifier: 
  monsterId: \(entry.monsterId)
  monsterName: \(yamlString(entry.displayName))
  encyclopediaNumber: \(entry.encyclopediaNumber)
  raceId: \(entry.raceId)
  classRank: \(entry.classRank)
  rarity: \(rarity)
  element: \(entry.element)
  rangeType: \(entry.rangeType)
  damageType: \(entry.damageType)
  baseStats:
    maxHp: \(entry.maxHp)
    attack: \(entry.attack)
    magicAttack: \(entry.magicAttack)
    defense: \(entry.defense)
    magicDefense: \(entry.magicDefense)
    attackSpeed: \(entry.attackSpeed)
  attackRange: \(entry.attackRange)
  normalAttackTargetCount: \(entry.normalAttackTargetCount)
  normalAttackAppliesKnockback: 0
  normalAttackKnockbackDuration: 0.18
  plusValueCap: 99
  plusGrowth:
    maxHpPerPlus: \(plusHp)
    attackPerPlus: \(plusAttack)
    magicAttackPerPlus: \(plusMagic)
    defensePerPlus: \(plusDefense)
    magicDefensePerPlus: \(plusMagicDefense)
    attackSpeedPerPlus: \(attackSpeedGrowth)
  fusionExclusive: \(entry.fusionExclusive)
  fusionRecipes: []
  portraitSprite: {fileID: 0}
  illustrationSprite: {fileID: 0}
  portraitResourcePath: FamilyMonsterCards/\(entry.resourceFolder)/\(entry.outputKey)
  illustrationResourcePath: FamilyMonsters/\(entry.resourceFolder)/\(entry.outputKey)
  battleIdleResourcePath: MonsterBattle/mon_\(entry.outputKey)_idle
  battleIdleFacing: 0
  battleMoveResourcePath: MonsterBattle/mon_\(entry.outputKey)_move
  battleMoveFacing: 0
  battleAttackResourcePath: MonsterBattle/mon_\(entry.outputKey)_attack
  battleAttackFacing: 0
  description: \(yamlString(entry.description))

"""
}

func updateMasterDataRoot(rootPath: String, monsterGuids: [String]) throws {
    var text = try String(contentsOfFile: rootPath, encoding: .utf8)
    guard let start = text.range(of: "  monsterDataList:\n"),
          let end = text.range(of: "  enemyDataList:\n") else {
        throw NSError(domain: "process_monster_lineup_assets", code: 5, userInfo: [NSLocalizedDescriptionKey: "failed to locate monsterDataList in MasterDataRoot"])
    }

    var replacement = "  monsterDataList:\n"
    for guid in monsterGuids {
        replacement += "  - {fileID: 11400000, guid: \(guid), type: 2}\n"
    }

    text.replaceSubrange(start.lowerBound..<end.lowerBound, with: replacement)
    try text.write(toFile: rootPath, atomically: true, encoding: .utf8)
}

func updateFloorRecruitment(projectRoot: String) throws {
    let floorRecruitment: [Int: [String]] = [
        1: ["monster_dragon_whelp", "monster_chibi_gear", "monster_rock_golem"],
        2: ["monster_apprentice_swordsman", "monster_apprentice_mage", "monster_dragon_whelp"],
        3: ["monster_dragon_whelp", "monster_chibi_gear", "monster_rock_golem", "monster_apprentice_swordsman", "monster_apprentice_mage"],
        4: ["monster_flare_drake", "monster_armed_droid", "monster_ore_giant_garm"],
        5: ["monster_holy_armor_leon", "monster_dark_robe_curse_mage_noah", "monster_flare_drake"],
        6: ["monster_flare_drake", "monster_armed_droid", "monster_ore_giant_garm", "monster_holy_armor_leon", "monster_dark_robe_curse_mage_noah"],
        7: ["monster_abyss_dragon", "monster_omega_leon", "monster_cosmic_ore_fortress_golem"],
        8: ["monster_sword_saint_alvarez", "monster_abyss_grand_mage_seraphis", "monster_abyss_dragon"],
        9: ["monster_abyss_dragon", "monster_omega_leon", "monster_cosmic_ore_fortress_golem", "monster_sword_saint_alvarez"],
        10: ["monster_abyss_dragon", "monster_omega_leon", "monster_cosmic_ore_fortress_golem", "monster_sword_saint_alvarez", "monster_abyss_grand_mage_seraphis"]
    ]

    for (floor, monsterIds) in floorRecruitment {
        let path = "\(projectRoot)/Assets/MasterData/Floor/Floor_\(floor).asset"
        var text = try String(contentsOfFile: path, encoding: .utf8)
        guard let range = text.range(of: #"  recruitableMonsterIds:\n(?:  - .+\n?)*"#, options: .regularExpression) else {
            throw NSError(domain: "process_monster_lineup_assets", code: 6, userInfo: [NSLocalizedDescriptionKey: "failed to locate recruitableMonsterIds in Floor_\(floor)"])
        }

        var replacement = "  recruitableMonsterIds:\n"
        for monsterId in monsterIds {
            replacement += "  - \(monsterId)\n"
        }

        text.replaceSubrange(range, with: replacement)
        try text.write(toFile: path, atomically: true, encoding: .utf8)
    }
}

let args = CommandLine.arguments
if args.count != 3 {
    fputs("usage: process_monster_lineup_assets.swift <source-root> <unity-project-root>\n", stderr)
    exit(1)
}

let sourceRoot = args[1]
let projectRoot = args[2]
let portraitsRoot = "\(projectRoot)/Assets/Resources/FamilyMonsters"
let cardPortraitsRoot = "\(projectRoot)/Assets/Resources/FamilyMonsterCards"
let battleRoot = "\(projectRoot)/Assets/Resources/MonsterBattle"
let dragonEffectRoot = "\(projectRoot)/Assets/Resources/BattleEffects/Dragon"
let monsterAssetRoot = "\(projectRoot)/Assets/MasterData/Monster"

try FileManager.default.createDirectory(atPath: portraitsRoot, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: cardPortraitsRoot, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: battleRoot, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: dragonEffectRoot, withIntermediateDirectories: true)
try FileManager.default.createDirectory(atPath: monsterAssetRoot, withIntermediateDirectories: true)

var monsterGuids: [String] = []

for entry in entries {
    let raceDirectory = try findChildDirectory(in: sourceRoot, named: entry.raceFolder)
    let monsterDirectory = try findChildDirectory(in: raceDirectory, named: entry.sourceFolder)
    let portraitDirectory = "\(portraitsRoot)/\(entry.resourceFolder)"
    let cardPortraitDirectory = "\(cardPortraitsRoot)/\(entry.resourceFolder)"
    try FileManager.default.createDirectory(atPath: portraitDirectory, withIntermediateDirectories: true)
    try FileManager.default.createDirectory(atPath: cardPortraitDirectory, withIntermediateDirectories: true)

    let portraitSource = try findImage(in: monsterDirectory, preferredName: "姿絵.png")
    try processPortrait(
        source: portraitSource,
        destination: "\(portraitDirectory)/\(entry.outputKey).png")
    try processCardPortrait(
        source: portraitSource,
        destination: "\(cardPortraitDirectory)/\(entry.outputKey).png")
    try processSheet(
        source: try findImage(in: monsterDirectory, preferredName: "待機.png"),
        outputDirectory: battleRoot,
        prefix: "mon_\(entry.outputKey)_idle")
    try processSheet(
        source: try findImage(in: monsterDirectory, preferredName: "移動.png"),
        outputDirectory: battleRoot,
        prefix: "mon_\(entry.outputKey)_move")
    try processSheet(
        source: try findImage(in: monsterDirectory, preferredName: "攻撃.png"),
        outputDirectory: battleRoot,
        prefix: "mon_\(entry.outputKey)_attack")

    let effectPath = "\(monsterDirectory)/エフェクト.png"
    if entry.raceId == "dragon", FileManager.default.fileExists(atPath: effectPath) {
        try processSheet(
            source: effectPath,
            outputDirectory: dragonEffectRoot,
            prefix: "fx_\(entry.outputKey)_attack")
    }

    let assetPath = "\(monsterAssetRoot)/\(entry.monsterId).asset"
    try monsterAssetYaml(for: entry).write(toFile: assetPath, atomically: true, encoding: .utf8)
    let guid = try ensureAssetMeta(assetPath: assetPath)
    monsterGuids.append(guid)
}

try updateMasterDataRoot(
    rootPath: "\(projectRoot)/Assets/Resources/MasterData/MasterDataRoot.asset",
    monsterGuids: monsterGuids)
try updateFloorRecruitment(projectRoot: projectRoot)

print("Processed \(entries.count) monster lineup entries.")
