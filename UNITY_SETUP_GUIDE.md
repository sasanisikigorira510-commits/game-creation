# Unityセットアップ手順書 v0.1

## 1. 目的
この手順書は、`UNITY_PROJECT_TEMPLATE` のコードを Unity プロジェクトへ配置し、最初に動かすまでの流れをまとめたものです。

## 2. Unityプロジェクト作成

### 推奨
- Unity 2D プロジェクト
- LTS版を使用

### 作成後にやること
- プロジェクト名を決める
- `TextMeshPro` を導入する
- Build Settings に iOS と Android を追加できる状態にする

## 3. フォルダ構成を作る

`Assets` 配下に以下を用意する。

- `Assets/Scenes`
- `Assets/Scripts/Core`
- `Assets/Scripts/Managers`
- `Assets/Scripts/Battle`
- `Assets/Scripts/Home`
- `Assets/Scripts/UI`
- `Assets/Scripts/Data`
- `Assets/Scripts/Save`
- `Assets/Scripts/MasterData`
- `Assets/Resources/MasterData`
- `Assets/Prefabs`
- `Assets/Art`
- `Assets/Audio`

## 4. スクリプト配置

`UNITY_PROJECT_TEMPLATE/Assets/Scripts` の中身を Unity プロジェクトの `Assets/Scripts` へコピーする。

`MasterDataRoot` を使う場合は、作成したアセットを `Assets/Resources/MasterData/MasterDataRoot.asset` に置く。

## 5. シーン作成

以下の 4 シーンを作成する。

- `BootScene`
- `TitleScene`
- `HomeScene`
- `BattleScene`

保存先:

- `Assets/Scenes/BootScene.unity`
- `Assets/Scenes/TitleScene.unity`
- `Assets/Scenes/HomeScene.unity`
- `Assets/Scenes/BattleScene.unity`

## 6. Build Settings 登録

`File > Build Settings` を開いて、以下の順番で登録する。

1. `BootScene`
2. `TitleScene`
3. `HomeScene`
4. `BattleScene`

`BootScene` を先頭にする。

## 7. BootScene の設定

### Hierarchy
- `Bootstrap`

### 追加コンポーネント
- `Bootstrapper`

### 注意
- このシーンでは UI は不要
- 起動時初期化だけ担当する
- `MasterDataRoot` が `Resources/MasterData/MasterDataRoot` にあれば自動読込される

## 8. TitleScene の設定

### Hierarchy
- `Canvas`
- `EventSystem`
- `TitleSceneController`

### Canvas 配下の例
- `Background`
- `LogoText`
- `StartButton`
- `ContinueButton`
- `SettingsButton`

### 設定
- `TitleSceneController` を空の GameObject に追加
- `StartButton` の `OnClick` に `StartNewGame()`
- `ContinueButton` の `OnClick` に `ContinueGame()`

## 9. HomeScene の設定

### Hierarchy
- `Canvas`
- `EventSystem`
- `HomeSceneController`
- `Panels`

### Panels 配下
- `HomePanel`
- `EnhancePanel`
- `EquipmentPanel`
- `MissionPanel`

### HomePanel 内の例
- `PlayerStatusText`
- `ExpText`
- `GoldText`
- `IdleRewardText`
- `ClaimIdleRewardButton`
- `StartBattleButton`
- `OpenEnhanceButton`
- `OpenEquipmentButton`

### 設定
- `HomeSceneController` を空の GameObject に追加
- `PanelSwitcher` を同じか別オブジェクトに追加
- `HomePanelController`, `EnhancePanelController`, `EquipmentPanelController` を必要なオブジェクトへ追加
- `MissionPanelController` も必要なオブジェクトへ追加
- 各パネル参照を `PanelSwitcher` に設定
- `PlayerStatusView`, `ResourceView`, `UpgradeStatusView`, `IdleRewardView` を各パネルに配置して参照を紐づける
- 装備パネルに `EquipmentStatusView` を配置して現在装備名を表示する
- `StartBattleButton` に `StartBattle()`
- `OpenEnhanceButton` に `OpenEnhance()`
- `OpenEquipmentButton` に `OpenEquipment()`
- `OpenMissionButton` に `OpenMission()`
- `ClaimIdleRewardButton` に `ClaimIdleReward()`

### 強化パネルの例
- `AttackUpgradeButton`
- `DefenseUpgradeButton`
- `HpUpgradeButton`
- `AttackUpgradeStatus`
- `DefenseUpgradeStatus`
- `HpUpgradeStatus`

### 強化パネル設定
- `AttackUpgradeButton` に `UpgradeAttack()`
- `DefenseUpgradeButton` に `UpgradeDefense()`
- `HpUpgradeButton` に `UpgradeHp()`
- `UpgradeStatusView` を 3つ配置し、`EnhancePanelController` の参照へ設定

### 装備パネル設定
- 武器・防具・装飾の現在装備表示を `EquipmentStatusView` に紐づける
- 装備切り替えボタンを作る場合は、`EquipWeapon("equip_bronze_blade")` のようにID付きで登録する
- まずは `equip_bronze_blade / equip_iron_sword`
- `equip_guard_cloth / equip_bone_mail`
- `equip_ashen_ring / equip_quick_charm`
  のように2択ずつ置くと確認しやすい

### ミッションパネル設定
- `MissionPanelController` を `MissionPanel` に追加
- `DailyRewardView` を置いて受け取り状態を表示する
- `MissionItemView` を2つ置いてミッション表示に使う
- `ClaimDailyReward()` をデイリー報酬ボタンへ紐づける
- `ClaimMissionClear1()` と `ClaimMissionReachFloor3()` を報酬受け取りボタンへ紐づける

### 放置報酬の確認ポイント
- 初回起動では放置報酬は 0 のままでよい
- 一度終了して時間を空けて再起動すると `Idle Reward` が増える
- `ClaimIdleReward()` 実行でゴールドに反映され、表示が 0 に戻る

## 10. BattleScene の設定

### Hierarchy
- `Canvas`
- `EventSystem`
- `BattleSceneController`
- `BattleStateMachine`
- `BattleSimulator`
- `BattleHud`

### BattleHud 配下の例
- `FloorText`
- `PlayerHpBar`
- `EnemyHpBar`
- `PlayerDamageText`
- `EnemyDamageText`
- `PlayerFlash`
- `EnemyFlash`
- `SkillButton1`
- `SkillButton2`
- `SkillButton3`
- `SkillCooldownText1`
- `SkillCooldownText2`
- `SkillCooldownText3`
- `RetreatButton`
- `WinLabel`
- `LoseLabel`
- `ResultPanel`
- `ResultTitleText`
- `ResultGoldText`
- `ResultExpText`
- `ResultNextActionText`
- `NextFloorButton`
- `ReturnHomeButton`

### 設定
- `BattleSceneController` を空の GameObject に追加
- `BattleStateMachine` を追加
- `BattleSimulator` を追加
- `BattleHudController` を `BattleHud` に追加
- `BattleSceneController` の `stateMachine` に `BattleStateMachine` を紐づける
- `BattleStateMachine` の `simulator` に `BattleSimulator` を紐づける
- `BattleStateMachine` の `hudController` に `BattleHudController` を紐づける
- `BattleFeedbackController` を追加し、`BattleStateMachine` の `feedbackController` に紐づける
- `ResultPanelController` を `ResultPanel` に追加し、`BattleHudController` の `resultPanelController` に紐づける
- `RetreatButton` に `Retreat()`
- `SkillButton1` に `UseSkillStrike()`
- `SkillButton2` に `UseSkillDrain()`
- `SkillButton3` に `UseSkillGuard()`
- `SkillCooldownText1/2/3` を `BattleHudController` に紐づける
- `NextFloorButton` に `GoToNextFloor()`
- `ReturnHomeButton` に `ReturnHome()`
- `PlayerDamageText / EnemyDamageText / PlayerFlash / EnemyFlash` を `BattleFeedbackController` に紐づける

### 仮戦闘の挙動
- プレイヤーと敵は自動で一定間隔ごとに攻撃する
- `SkillButton1` は `強撃`
- `SkillButton2` は `吸命`
- `SkillButton3` は `加護`
- スキルはクールダウン中に残り秒数を表示する
- 勝敗が決まったら、`WinLabel` または `LoseLabel` を表示する
- リザルトパネルに獲得 `Gold / EXP` と次の行動を表示する

### 敵特性の確認ポイント
- `FastAttack` の敵は通常より早くHPが減る
- `HighDefense` の敵は通常攻撃ダメージが通りにくい
- `Drain` の敵は攻撃時にHPが少し戻る
- `Critical` の敵は被ダメージが大きくなりやすい

### 演出の確認ポイント
- 通常攻撃やスキル時にダメージ数値が一瞬表示される
- 被弾した側のフラッシュが短く光る
- スキルダメージは通常攻撃より目立つ色で表示される

## 11. TextMeshPro 設定

初回利用時に `TMP Essential Resources` のインポートを行う。

使用箇所:

- タイトル文字
- レベル表示
- ゴールド表示
- 階層表示
- 勝敗表示

## 12. 最初の動作確認

### 確認項目
- 再生時に `BootScene` から始まる
- 自動で `TitleScene` に進む
- `ゲーム開始` で `HomeScene` に進む
- `挑戦` で `BattleScene` に進む
- `撤退` で `HomeScene` に戻る

## 13. この時点で未実装でもよいもの
- 敵モデル
- 本格UIデザイン
- スキル演出
- 音
- マスターデータ読み込み本処理

## 14. 次にやること
- ScriptableObject を作る
- 敵・階層・スキルの初期データを入れる
- BattleScene に仮戦闘処理を追加する

関連文書:

- [SAMPLE_MASTER_DATA.md](C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/SAMPLE_MASTER_DATA.md)
- [UNITY_DATA_ENTRY_CHECKLIST.md](C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/UNITY_DATA_ENTRY_CHECKLIST.md)
- [MVP_STATUS.md](C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/MVP_STATUS.md)
