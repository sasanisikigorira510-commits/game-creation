# Current Scene Role Plan

この文書は、現在の `WitchTowerGame` で採用する
`Sceneの役割` と `Sceneどうしのつながり` を整理した現行方針です。

見た目の細かいズレは、責務を固めたあとに
Unityエディタ上で手動調整する前提にします。

## 現在の前提

- タイトル画面とホーム画面は分離しない
- `HomeScene` を起動直後の統合ハブとして扱う
- `編成` は情報量が多いため `FormationScene` として独立させる
- `装備` と `合体` は当面 `HomeScene` 内のサブ画面で持つ
- UIの細かい位置合わせは、コードでの数値調整を最小限にして Unity 側で手動調整する

## 採用するScene

現時点で採用するSceneは4つです。

1. `BootScene`
2. `HomeScene`
3. `FormationScene`
4. `BattleScene`

対象ファイル:

- [BootScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/BootScene.unity)
- [HomeScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/HomeScene.unity)
- [FormationScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/FormationScene.unity)
- [BattleScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/BattleScene.unity)

## Sceneごとの役割

### `BootScene`

役割:

- アプリ起動直後の初期化
- セーブ読込
- マスターデータ初期化
- 各種マネージャ生成
- `HomeScene` への遷移

### `HomeScene`

役割:

- 起動直後に最初に見せる統合ホーム
- 世界観を見せるハブ画面
- 各メニューへの入口

このSceneで持つ導線:

- `バトル`
- `編成`
- `装備`
- `合体`

このSceneで持つサブ画面:

- `装備オーバーレイ`
- `合体オーバーレイ`

### `FormationScene`

役割:

- 保有モンスター一覧表示
- 出撃メンバー1〜5体の選択
- お気に入り登録
- ソートとフィルタ
- `HomeScene` への復帰

やらないこと:

- バトル進行
- 装備編集
- 合体処理

### `BattleScene`

役割:

- ダンジョン進行
- バトル進行
- 勝敗判定
- 報酬反映
- `HomeScene` への復帰

やらないこと:

- 編成変更
- 長期育成管理
- 装備管理

## Scene遷移

基本導線:

`BootScene -> HomeScene`

`HomeScene -> FormationScene -> HomeScene`

`HomeScene -> BattleScene -> HomeScene`

## HomeSceneの役割をさらに分解

### 1. ベース背景層

- 世界観背景
- まず最初に見せる画面

### 2. 常設メニュー層

- `バトル`
- `編成`
- `装備`
- `合体`

### 3. 一時表示オーバーレイ層

- 装備画面
- 合体画面
- 将来的な確認ダイアログ

## FormationSceneの役割

目的:

- 保有モンスターを一覧で見る
- 1〜5体編成を管理する
- お気に入り、ソート、フィルタで探しやすくする

責務:

- 保有モンスター一覧
- 出撃メンバー枠
- 編成中チェック表示
- お気に入り表示
- ソート
- フィルタ

## 手動調整に切り替える範囲

今後、以下は Unity エディタ上で手動調整を優先します。

- 編成UI画像に対する枠位置
- 枠に対するモンスター表示位置
- ボタン当たり判定
- テキストの細かい位置
- スマホ縦画面での余白

コードでやるのは:

- 画面の開閉
- データの受け渡し
- ボタン押下時の処理
- Scene遷移

## ここからの優先順

1. `FormationScene` の責務を固定する
2. `HomeScene` と `FormationScene` の導線を安定させる
3. `BattleScene` との接続を維持する
4. 最後に Unity で位置を手動調整する

## 結論

現時点の正解は、
`HomeScene をハブにしつつ、編成だけは FormationScene として分ける`
です。

そのうえで、
`見た目のズレはコードで追い込み続けず、Unityで手動調整する`
方針に切り替えます。
