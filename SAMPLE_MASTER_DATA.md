# サンプルマスターデータ設計書 v0.1

## 1. 目的
この文書は、Unity の `ScriptableObject` に最初に入力するサンプルデータを決めるためのものです。

MVPでは、まず以下があれば十分です。

- プレイヤー初期値 1件
- 敵 3体
- スキル 3件
- 装備 3件
- 階層 3件
- ドロップテーブル 3件

## 2. プレイヤー初期値

### PlayerBaseData
- HP: 100
- Attack: 15
- Defense: 5
- AttackSpeed: 1.0
- CritRate: 0.05
- CritDamage: 1.5

## 3. 敵データ

### enemy_slime_dark
- Name: 闇の粘体
- MaxHp: 40
- Attack: 8
- Defense: 2
- AttackSpeed: 0.8
- CritRate: 0.03
- CritDamage: 1.3
- RewardGold: 10
- RewardExp: 5
- DropTableId: drop_floor_1
- Trait: None

### enemy_hound_blood
- Name: 血牙の猟犬
- MaxHp: 65
- Attack: 12
- Defense: 4
- AttackSpeed: 1.1
- CritRate: 0.08
- CritDamage: 1.5
- RewardGold: 18
- RewardExp: 9
- DropTableId: drop_floor_2
- Trait: FastAttack
  - 効果: 攻撃間隔が短くなる

### enemy_witch_guard
- Name: 魔女塔の衛兵
- MaxHp: 110
- Attack: 17
- Defense: 8
- AttackSpeed: 0.9
- CritRate: 0.05
- CritDamage: 1.6
- RewardGold: 30
- RewardExp: 16
- DropTableId: drop_floor_3
- Trait: HighDefense
  - 効果: 防御力が高い

### 追加候補の敵特性メモ
- `None`: 基本敵
- `FastAttack`: 通常より早く攻撃する
- `HighDefense`: 防御力が高い
- `Drain`: 与えたダメージの一部を回復する
- `Critical`: 会心が出やすい

## 4. スキルデータ

### skill_strike
- Name: 強撃
- Description: 単体に高威力ダメージ
- Cooldown: 6
- PowerRate: 2.0
- HealRate: 0
- BuffType: None
- BuffValue: 0
- BuffDuration: 0

### skill_drain
- Name: 吸命
- Description: ダメージの一部を回復に変える
- Cooldown: 8
- PowerRate: 1.2
- HealRate: 0.5
- BuffType: Heal
- BuffValue: 0
- BuffDuration: 0

### skill_guard
- Name: 加護
- Description: 一定時間、防御力を上げる
- Cooldown: 10
- PowerRate: 0
- HealRate: 0
- BuffType: DefenseUp
- BuffValue: 5
- BuffDuration: 5

## 5. 装備データ

### equip_bronze_blade
- Name: 青銅の短剣
- SlotType: Weapon
- BaseAttack: 5
- BaseDefense: 0
- BaseHp: 0
- BonusCritRate: 0.02
- BonusAttackSpeed: 0
- Rarity: Common

### equip_guard_cloth
- Name: 守りの外套
- SlotType: Armor
- BaseAttack: 0
- BaseDefense: 4
- BaseHp: 15
- BonusCritRate: 0
- BonusAttackSpeed: 0
- Rarity: Common

### equip_ashen_ring
- Name: 灰燼の指輪
- SlotType: Accessory
- BaseAttack: 0
- BaseDefense: 0
- BaseHp: 0
- BonusCritRate: 0.03
- BonusAttackSpeed: 0.1
- Rarity: Uncommon

### equip_iron_sword
- Name: 鉄の長剣
- SlotType: Weapon
- BaseAttack: 8
- BaseDefense: 0
- BaseHp: 0
- BonusCritRate: 0
- BonusAttackSpeed: -0.05
- Rarity: Uncommon

### equip_bone_mail
- Name: 骨の鎧
- SlotType: Armor
- BaseAttack: 0
- BaseDefense: 7
- BaseHp: 25
- BonusCritRate: 0
- BonusAttackSpeed: -0.05
- Rarity: Uncommon

### equip_quick_charm
- Name: 俊足の護符
- SlotType: Accessory
- BaseAttack: 0
- BaseDefense: 0
- BaseHp: 0
- BonusCritRate: 0.01
- BonusAttackSpeed: 0.2
- Rarity: Uncommon

## 6. 階層データ

### floor_1
- FloorNumber: 1
- Enemy: enemy_slime_dark
- FirstClearRewardGold: 30
- RepeatRewardTableId: drop_floor_1

### floor_2
- FloorNumber: 2
- Enemy: enemy_hound_blood
- FirstClearRewardGold: 40
- RepeatRewardTableId: drop_floor_2

### floor_3
- FloorNumber: 3
- Enemy: enemy_witch_guard
- FirstClearRewardGold: 60
- RepeatRewardTableId: drop_floor_3

## 7. ドロップテーブル

### drop_floor_1
- MinGold: 8
- MaxGold: 12
- Material 1:
  - Id: mat_bone_fragment
  - Amount: 1
  - DropRate: 0.7

### drop_floor_2
- MinGold: 14
- MaxGold: 20
- Material 1:
  - Id: mat_dark_fur
  - Amount: 1
  - DropRate: 0.6

### drop_floor_3
- MinGold: 24
- MaxGold: 32
- Material 1:
  - Id: mat_witch_ash
  - Amount: 1
  - DropRate: 0.5

## 8. Unity入力時の作成順

1. `PlayerBaseDataSO` を 1件作る
2. `EnemyDataSO` を 3件作る
3. `SkillDataSO` を 3件作る
4. `EquipmentDataSO` を 3件作る
5. `DropTableDataSO` を 3件作る
6. `FloorDataSO` を 3件作る
7. `MasterDataManager` に各配列を登録する

## 9. 最初の確認基準
- 1階は無強化で勝ちやすい
- 2階は少し危ない
- 3階は初回だと負ける可能性がある

このバランスを作れれば、MVPとして十分に手応えが出る。
