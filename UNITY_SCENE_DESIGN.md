# Unityシーン設計書 v0.1

## 1. 目的
この文書は、`SPEC.md` を Unity で実装するためのシーン構成と画面遷移を定義する。

MVPでは、実装のしやすさと管理のしやすさを優先し、シーン数を増やしすぎない。

## 2. 採用方針

### 基本方針
- 画面単位で大きくシーンを分ける
- 複雑なサブ画面は `Panel` 切り替えで対応する
- MVPではロード管理を単純にする

### MVPのシーン数
以下の 4 シーン構成を採用する。

1. `Boot`
2. `Title`
3. `Home`
4. `Battle`

## 3. シーン一覧

### 1. Boot

#### 役割
- アプリ起動時の初期化
- セーブデータ読み込み
- マスターデータ読み込み
- 各種マネージャ初期化
- 次シーンへの遷移判定

#### 主な処理
- セーブデータがあれば読み込む
- 必須データが不足していないか確認する
- 初回起動なら新規データ作成
- 完了後に `Title` へ遷移

#### 配置するもの
- `Bootstrapper`
- `GameManager`
- `SaveManager`
- `MasterDataManager`

### 2. Title

#### 役割
- タイトル表示
- 新規開始または続きから開始
- 設定画面へ移動

#### 主なUI
- ロゴ
- `ゲーム開始`
- `続きから`
- `設定`

#### 主な処理
- 新規開始時はデータ初期化確認
- 続きからで `Home` へ遷移
- 設定はポップアップまたはパネル表示

### 3. Home

#### 役割
- メインハブ画面
- プレイヤー状態の確認
- 強化、装備、塔挑戦への導線

#### 主なUI
- プレイヤー情報
- 所持ゴールド
- 所持素材
- 最高到達階層
- `挑戦`
- `強化`
- `装備`
- `ミッション`

#### 実装方針
MVPでは `Home` シーン内でパネルを切り替える。

#### Homeシーン内パネル
- `HomePanel`
- `EnhancePanel`
- `EquipmentPanel`
- `MissionPanel`
- `SettingsPanel`

#### メリット
- シーン遷移の数を減らせる
- UI状態管理がしやすい
- データ再読込を減らせる

### 4. Battle

#### 役割
- 階層バトルを進行する
- 勝敗判定
- 報酬計算
- 次階層遷移またはホーム復帰

#### 主なUI
- プレイヤーHP
- 敵HP
- 階層表示
- スキルボタン 3つ
- 撤退ボタン
- 勝敗表示
- リザルト表示

#### Battle内の表示単位
- `BattleHudPanel`
- `PausePanel`
- `ResultPanel`

## 4. シーン遷移

### 基本遷移
`Boot -> Title -> Home -> Battle -> Home`

### 詳細遷移

#### 起動時
- アプリ起動
- `Boot` 読み込み
- 初期化
- `Title` へ遷移

#### ゲーム開始
- `Title` で開始
- `Home` へ遷移

#### 挑戦開始
- `Home` で `挑戦`
- `Battle` へ遷移

#### バトル勝利時
- リザルト表示
- `次へ` を押す
- 同一 `Battle` シーン内で次階層をロード

#### バトル敗北時
- リザルト表示
- `ホームへ` を押す
- `Home` へ遷移

#### 撤退時
- 確認ダイアログ
- `Home` へ遷移

## 5. Battleシーンの内部状態

`Battle` シーンでは、シーン遷移を増やさず状態で管理する。

### 状態一覧
- `Init`
- `Ready`
- `Fighting`
- `Result`
- `Transition`

### 状態説明

#### Init
- 階層データ読み込み
- プレイヤーと敵の生成

#### Ready
- 開始演出
- UI初期化

#### Fighting
- 通常攻撃進行
- スキル入力受付
- 勝敗判定監視

#### Result
- 勝利または敗北の結果表示
- 報酬反映

#### Transition
- 次階層へ進むか、ホームへ戻るかを処理

## 6. Homeシーンの内部構成

### メイン構成
- `Canvas`
- `HomeUIRoot`
- `PanelSwitcher`
- `PlayerStatusView`
- `ResourceView`

### パネル運用ルール
- 常時表示するのは `HomePanel`
- 他パネルは重ねて開く
- 戻る操作で前のパネルを閉じる
- Android の戻るキー対応を意識する

## 7. シーンごとの責務分離

### Bootで持つ責務
- 起動時初期化だけ
- ゲームロジックを持ちすぎない

### Titleで持つ責務
- 開始導線
- 設定導線

### Homeで持つ責務
- データ確認
- 強化
- 装備変更
- 挑戦開始

### Battleで持つ責務
- バトル進行
- 階層攻略
- 報酬反映

## 8. DontDestroyOnLoad対象
MVPでは以下を `DontDestroyOnLoad` 候補とする。

- `GameManager`
- `SaveManager`
- `AudioManager`
- `MasterDataManager`

必要以上に増やさないこと。

## 9. シーン命名ルール

### シーン名
- `BootScene`
- `TitleScene`
- `HomeScene`
- `BattleScene`

### フォルダ案
- `Assets/Scenes/BootScene.unity`
- `Assets/Scenes/TitleScene.unity`
- `Assets/Scenes/HomeScene.unity`
- `Assets/Scenes/BattleScene.unity`

## 10. ロード方針

### MVP方針
- 非同期ロードは後回しでもよい
- まずは通常の `LoadScene` で成立させる

### 将来拡張
- ロード演出追加
- Addressables対応
- 軽量アセット差し替え

## 11. エラー時の扱い

### Bootでのエラー
- セーブ破損時は新規データとして再生成する導線を出す
- マスターデータ不足時はログに出す

### Battleでのエラー
- 階層データ取得失敗時は `Home` へ戻す

## 12. MVP完成時の判断基準
- 4シーンで全導線が成立している
- Home内パネル切り替えで主要機能が触れる
- Battle内だけで連続階層攻略ができる
- アプリ再起動後も `Boot` から正常復帰できる

## 13. 次工程
次はこのシーン設計を前提に、以下を定義する。

- クラス設計
- データ設計
- セーブデータ構造
- バトル進行クラス
