# Unityクラス設計・データ設計書 v0.1

## 1. 目的
この文書は、`SPEC.md` と `UNITY_SCENE_DESIGN.md` をもとに、MVP実装に必要なクラス責務とデータ構造を定義する。

目的は以下の 2 つ。

- スクリプト作成の順番を明確にする
- データとロジックの分離を保つ

## 2. 全体方針

### 設計方針
- `MonoBehaviour` は表示と進行制御を担当する
- ステータス計算や報酬計算は純粋クラスへ寄せる
- マスターデータは `ScriptableObject` を基本にする
- セーブデータは `JSON` で保存する

### MVPで避けること
- 過剰なDI構成
- 複雑なイベントバス
- ネットワーク前提設計

## 3. フォルダ構成案

- `Assets/Scripts/Core`
- `Assets/Scripts/Managers`
- `Assets/Scripts/Battle`
- `Assets/Scripts/Home`
- `Assets/Scripts/UI`
- `Assets/Scripts/Data`
- `Assets/Scripts/Save`
- `Assets/Scripts/MasterData`

## 4. マネージャ系クラス

### GameManager

#### 役割
- ゲーム全体の実行状態保持
- 現在挑戦中の階層管理
- 各画面で共有する軽量な状態を持つ

#### 主な責務
- 現在階層
- 最高到達階層
- プレイヤープロファイル参照
- シーン遷移時の共有状態

### SaveManager

#### 役割
- セーブ/ロード処理

#### 主な責務
- `PlayerSaveData` の保存
- JSONシリアライズ
- 初回データ生成
- セーブ破損時の保護

### MasterDataManager

#### 役割
- マスターデータ読み込みと参照窓口

#### 主な責務
- プレイヤー初期データ取得
- 敵データ取得
- スキルデータ取得
- 階層データ取得
- 装備データ取得

### AudioManager

#### 役割
- BGM/SE再生管理

## 5. シーン制御クラス

### Bootstrapper

#### 役割
- `BootScene` で初期化開始

#### 主な処理
- マネージャ生成
- セーブ読込
- タイトル遷移

### TitleSceneController

#### 役割
- タイトル画面入力処理
- 開始ボタン制御

### HomeSceneController

#### 役割
- ホーム画面全体の制御
- 各パネル開閉制御

### BattleSceneController

#### 役割
- バトル全体進行
- 状態遷移
- 勝敗判定開始

## 6. Home関連クラス

### PanelSwitcher

#### 役割
- Home内のパネル切替

### HomePanelController

#### 役割
- ホームトップUI制御

### EnhancePanelController

#### 役割
- 強化UI制御
- 強化実行

### EquipmentPanelController

#### 役割
- 装備一覧表示
- 装備変更

### MissionPanelController

#### 役割
- MVPではダミー表示でも可
- 将来拡張用に分離

## 7. Battle関連クラス

### BattleStateMachine

#### 役割
- バトル状態遷移管理

#### 想定状態
- `Init`
- `Ready`
- `Fighting`
- `Result`
- `Transition`

### BattleUnitController

#### 役割
- ユニットの見た目とアニメーション制御
- 攻撃演出再生

### PlayerBattleController

#### 役割
- プレイヤー側の戦闘ロジック制御

### EnemyBattleController

#### 役割
- 敵側の戦闘ロジック制御

### BattleHudController

#### 役割
- HP表示
- スキルボタン状態表示
- 階層表示

### SkillButtonView

#### 役割
- 個別スキルボタンの表示と入力受付

### ResultPanelController

#### 役割
- 勝敗表示
- 報酬表示
- 次へ/ホームへ制御

## 8. バトルロジック純粋クラス

### BattleUnitStats

#### 役割
- バトル中に使う能力値を保持

#### 主な項目
- MaxHp
- CurrentHp
- Attack
- Defense
- AttackSpeed
- CritRate
- CritDamage

### DamageCalculator

#### 役割
- ダメージ計算を担当

### BattleRewardCalculator

#### 役割
- 勝利報酬計算

### BattleProgressService

#### 役割
- 勝利時の次階層更新
- 到達記録更新

### SkillExecutor

#### 役割
- スキル効果適用

## 9. プレイヤーデータ関連

### PlayerProfile

#### 役割
- プレイヤーの現在状態をメモリ上で保持

#### 主な項目
- Level
- Exp
- Gold
- HighestFloor
- EquippedWeaponId
- EquippedArmorId
- EquippedAccessoryId
- SkillLevels
- Inventory

### InventoryData

#### 役割
- 素材、装備所持情報

### EquipmentInstanceData

#### 役割
- 個別装備の所持状態
- 強化値保持

## 10. セーブデータ構造

### PlayerSaveData

#### 保存項目
- PlayerLevel
- PlayerExp
- Gold
- HighestFloor
- CurrentFloor
- OwnedMaterials
- OwnedEquipments
- EquippedWeaponId
- EquippedArmorId
- EquippedAccessoryId
- SkillLevels
- SettingsData

### SettingsData

#### 保存項目
- BgmVolume
- SeVolume
- Language

## 11. マスターデータ構造

### PlayerBaseDataSO

#### 役割
- プレイヤー初期値
- レベル成長値

### EnemyDataSO

#### 主な項目
- EnemyId
- Name
- MaxHp
- Attack
- Defense
- AttackSpeed
- CritRate
- CritDamage
- RewardGold
- RewardExp
- DropTableId
- EnemyTrait

### SkillDataSO

#### 主な項目
- SkillId
- Name
- Description
- Cooldown
- PowerRate
- HealRate
- BuffType
- BuffValue
- BuffDuration

### EquipmentDataSO

#### 主な項目
- EquipmentId
- Name
- SlotType
- BaseAttack
- BaseDefense
- BaseHp
- BonusCritRate
- BonusAttackSpeed
- Rarity

### FloorDataSO

#### 主な項目
- FloorNumber
- EnemyId
- FirstClearReward
- RepeatRewardTableId

### DropTableDataSO

#### 主な項目
- DropTableId
- GoldRange
- MaterialDrops

## 12. UI表示用クラス

### PlayerStatusView

#### 役割
- プレイヤー戦力やレベル表示

### ResourceView

#### 役割
- ゴールドと素材表示

### HpBarView

#### 役割
- HPゲージ更新

### RewardItemView

#### 役割
- リザルト報酬1件の表示

## 13. 処理の依存関係

### 起動時
`Bootstrapper -> SaveManager -> MasterDataManager -> GameManager`

### ホーム表示時
`HomeSceneController -> PlayerProfile -> PlayerStatusView / ResourceView`

### バトル開始時
`BattleSceneController -> MasterDataManager -> FloorDataSO / EnemyDataSO -> BattleStateMachine`

### ダメージ発生時
`PlayerBattleController or EnemyBattleController -> DamageCalculator -> BattleUnitStats`

### 勝利時
`BattleSceneController -> BattleRewardCalculator -> PlayerProfile -> SaveManager`

## 14. 実装順

### Phase 1
- `GameManager`
- `SaveManager`
- `MasterDataManager`
- `PlayerSaveData`
- `PlayerProfile`

### Phase 2
- `TitleSceneController`
- `HomeSceneController`
- `PanelSwitcher`
- `PlayerStatusView`
- `ResourceView`

### Phase 3
- `BattleSceneController`
- `BattleStateMachine`
- `BattleUnitStats`
- `DamageCalculator`
- `BattleHudController`

### Phase 4
- `SkillExecutor`
- `BattleRewardCalculator`
- `EnhancePanelController`
- `EquipmentPanelController`

## 15. MVPでの簡略化ルール

### ルール
- 敵AIは複雑にしない
- スキルは 3種固定
- 装備生成ランダムは後回し
- まずは固定ドロップ中心
- バフの種類は最小限

## 16. 将来の拡張を見越した注意

### 注意点
- `SkillExecutor` はスキル追加しやすい形にする
- `EnemyTrait` は enum またはIDで分離する
- `EquipmentInstanceData` を持たせて強化値を管理する
- `PlayerProfile` にUI参照を持たせない

## 17. 次に作るもの
この次は、実装に入る前の最終準備として以下を作ると良い。

- Unityフォルダ構成テンプレート
- ScriptableObject一覧
- 初期クラス雛形
- バトル状態遷移図
