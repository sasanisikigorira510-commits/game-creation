# Unity実データ投入チェックリスト v0.1

## 1. 目的
この文書は、`UNITY_PROJECT_TEMPLATE` のコードを Unity 上で実際に動かすために、必要なデータ作成と接続作業を順番に整理したものです。

## 2. 最初に作るアセット

以下の `ScriptableObject` を作る。

### 必須
- `PlayerBaseDataSO` 1件
- `EnemyDataSO` 3件以上
- `SkillDataSO` 3件
- `EquipmentDataSO` 6件
- `DropTableDataSO` 3件
- `FloorDataSO` 3件以上

### 任意
- `MasterDataRoot` 1件

`MasterDataRoot` を作る場合は `Assets/Resources/MasterData/MasterDataRoot.asset` に置くと自動で読まれる。

## 3. 作成順

作成順はこの通りにすると迷いにくい。

1. `PlayerBaseDataSO`
2. `EquipmentDataSO`
3. `EnemyDataSO`
4. `SkillDataSO`
5. `DropTableDataSO`
6. `FloorDataSO`
7. `MasterDataManager` への登録
8. `MasterDataRoot` へまとめる

## 4. 作成場所のおすすめ

`Assets/MasterData` 配下に以下のように分ける。

- `Assets/MasterData/Player`
- `Assets/MasterData/Enemy`
- `Assets/MasterData/Skill`
- `Assets/MasterData/Equipment`
- `Assets/MasterData/Floor`
- `Assets/MasterData/DropTable`

## 5. プレイヤー初期データ入力

`PlayerBaseDataSO`

- HP: 100
- Attack: 15
- Defense: 5
- AttackSpeed: 1.0
- CritRate: 0.05
- CritDamage: 1.5

## 6. 装備データ入力

最低でも以下を作る。

### Weapon
- `equip_bronze_blade`
- `equip_iron_sword`

### Armor
- `equip_guard_cloth`
- `equip_bone_mail`

### Accessory
- `equip_ashen_ring`
- `equip_quick_charm`

数値は [SAMPLE_MASTER_DATA.md](C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/SAMPLE_MASTER_DATA.md) を参照する。

## 7. 敵データ入力

最低でも以下を作る。

- `enemy_slime_dark`
- `enemy_hound_blood`
- `enemy_witch_guard`

特性:

- 1階: `None`
- 2階: `FastAttack`
- 3階: `HighDefense`

## 8. スキルデータ入力

以下を作る。

- `skill_strike`
- `skill_drain`
- `skill_guard`

注意:

- 現在の MVP コードではスキルの実動作はコード固定
- ただし将来のために `ScriptableObject` は先に作る

## 9. ドロップテーブル入力

以下を作る。

- `drop_floor_1`
- `drop_floor_2`
- `drop_floor_3`

まずは素材1種類ずつでもよい。

## 10. 階層データ入力

### floor 1
- Enemy: `enemy_slime_dark`
- FirstClearRewardGold: 30
- RepeatRewardTableId: `drop_floor_1`

### floor 2
- Enemy: `enemy_hound_blood`
- FirstClearRewardGold: 40
- RepeatRewardTableId: `drop_floor_2`

### floor 3
- Enemy: `enemy_witch_guard`
- FirstClearRewardGold: 60
- RepeatRewardTableId: `drop_floor_3`

## 11. MasterDataManager への登録

`BootScene` か、`DontDestroyOnLoad` の `MasterDataManager` を持つオブジェクトで以下を設定する。

- `playerBaseData`
- `enemyDataList`
- `skillDataList`
- `equipmentDataList`
- `floorDataList`
- `dropTableDataList`

漏れがあると戦闘でフォールバック値が使われる。

`MasterDataRoot` を使う場合:

- 各データ参照を `MasterDataRoot` にまとめる
- `Assets/Resources/MasterData/MasterDataRoot.asset` に保存する
- 実行時に `MasterDataManager` が自動読込する

## 12. シーン側の接続チェック

### BootScene
- `Bootstrapper`

### TitleScene
- `TitleSceneController`

### HomeScene
- `HomeSceneController`
- `PanelSwitcher`
- `HomePanelController`
- `EnhancePanelController`
- `EquipmentPanelController`
- `MissionPanelController`
- `PlayerStatusView`
- `ResourceView`
- `IdleRewardView`
- `UpgradeStatusView`
- `EquipmentStatusView`
- `DailyRewardView`
- `MissionItemView` x2

### BattleScene
- `BattleSceneController`
- `BattleStateMachine`
- `BattleSimulator`
- `BattleHudController`
- `BattleFeedbackController`
- `ResultPanelController`

## 13. 最初の動作確認順

1. 新規開始できる
2. ホームに入れる
3. 放置報酬表示が出る
4. 強化画面でゴールド消費できる
5. 装備画面で装備を切り替えられる
6. ミッション画面を開ける
7. デイリー報酬を受け取れる
8. バトルに入り自動攻撃する
9. 3つのスキルボタンが押せる
10. 勝敗後にリザルトが出る
11. ホームへ戻るとゴールドとEXPが増えている

## 14. 不具合が出た時に見る場所

### データ参照系
- `MasterDataManager`
- `PlayerProfile`
- `PlayerSaveData`

### ホーム系
- `HomeSceneController`
- `HomePanelController`
- `MissionPanelController`

### バトル系
- `BattleSceneController`
- `BattleStateMachine`
- `BattleSimulator`
- `BattleHudController`

## 15. 今の段階で未接続でもよいもの
- 実際の敵画像
- アニメーション
- BGM/SE
- 本格的な装備一覧UI
- スキル定義の完全データ駆動

## 16. 完了条件

以下が通れば、Unity上でのMVP確認として十分。

- 新規開始からホームまで問題なく進む
- 1戦して報酬を持ち帰れる
- 強化や装備変更が次の戦闘に反映される
- デイリー報酬、ミッション、放置報酬が確認できる
