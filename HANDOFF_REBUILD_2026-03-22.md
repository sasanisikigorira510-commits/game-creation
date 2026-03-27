# Witch Tower Rebuild Handoff

## 1. 目的

この引き継ぎ書は、`WitchTowerGame` を新しいセッションで作り直すための現状整理です。

今回の到達点は「MCP 経由で Unity を操作しながら、Title / Home / Battle の最低限の導線を動かした」ところまでです。

ただし、ユーザーが求めていたものは

- スマホアプリゲームとして自然に見える画面
- ファンタジー感のある世界観
- Start 画面 -> Home 画面 -> Battle 画面の分かりやすい導線
- ドット絵キャラや背景を使った実ゲームっぽい見た目

であり、現状の UI はそこから大きく外れています。

結論として、**システムは一部活かせるが、画面作りはかなり根本から作り直した方がよい**状態です。

## 2. 現在のプロジェクト情報

- Project root:
  - [WitchTowerGame](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame)
- Unity version used during most of the work:
  - `2022.3.32f1`
- User later opened project in:
  - `Unity 6.3 LTS / 6000.3.11f1`
- Current package note:
  - [manifest.json](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Packages/manifest.json)
  - `com.unity.textmeshpro` is present as `3.0.6`

## 3. 現在のシーンと導線

Scenes:

- [BootScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/BootScene.unity)
- [TitleScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/TitleScene.unity)
- [HomeScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/HomeScene.unity)
- [BattleScene.unity](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scenes/BattleScene.unity)

動作としては、最低限以下は通っていました。

- `Boot -> Title -> Home -> Battle`
- Battle の進行と Victory
- Home の装備 / 強化 / ミッション / Daily / Idle の基礎導線

ただし、**ゲーム画面としての見せ方は未完成**です。

## 4. 今の問題点

### 4-1. 最大の問題

ユーザー視点では、現在の画面は

- ゲーム画面に見えない
- 情報が多すぎる
- 文字が意味不明
- 背景画像と UI の位置関係が悪い
- スマホ画面比率で見たときに成立していない

という状態です。

### 4-2. 失敗した方向

Home 画面に「分析テキスト」を載せすぎました。

たとえば以下のような文言を増やしていました。

- Threat Read
- Confidence
- Loadout Alert
- Gold Route
- Upgrade Route
- Reward Route
- Push Window
- ROI Read
- Decision Line
- Decision Badge
- Command Stack
- Momentum Read
- Run Call
- Risk Buffer
- Enemy Tempo
- Damage Race
- Burst Read
- Kill Clock
- Crit Window
- Survival Window
- Clock Edge
- Tempo Verdict
- Pressure Call
- Reward Pace

途中で多くを `SetActive(false)` にして抑え始めましたが、設計思想自体が「分析 UI」寄りで、ゲーム体験の整理より先に情報を積み上げてしまっています。

### 4-3. 現在の Home 背景ズレ

ユーザー指定の背景画像を Home に敷いたものの、前景 UI のアンカーやサイズが古い仮レイアウトのままで、背景の中央ポータルに対して UI がずれて見えています。

そのため、Home は今の状態を微修正するより、

- 背景主導
- 主役カードを 1 つか 2 つ
- 下部ナビまたは上部の単純なタブ

くらいに **再設計した方が速い** です。

## 5. 直近でユーザーが求めていた方向

ユーザーの最新の意図はかなり明確です。

- システム面は一旦置いてよい
- まず Start 画面から作る
- 次に Home 画面
- 次に Battle 画面
- ボタン連携で順に遷移できるようにする
- ドット絵キャラや背景を使って、実際のゲーム画面らしくしたい

特にユーザーは以下を強く指摘していました。

- 「ゲーム画面がしょぼすぎて話にならない」
- 「シミュレーションの画面を見てもゲーム画面だと思わない」
- 「謎の文字列があって意味不明」

つまり次セッションでは、**UI や画面演出を最優先**にすべきです。

## 6. 活かせるもの

### 6-1. 外部ドット素材

取り込み済みの無料素材は使えます。

格納先:

- [Assets/Art/External](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External)
- [Assets/Art/External/Derived](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived)

派生で使っていた画像:

- [witch_idle.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived/witch_idle.png)
- [witch_cast.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived/witch_cast.png)
- [enemy_death_mage_elf.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived/enemy_death_mage_elf.png)
- [tree.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived/tree.png)
- [dirt_tile.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived/dirt_tile.png)
- [grass_tile.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/Derived/grass_tile.png)

ライセンスメモ:

- [README_FreeAssets.txt](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/README_FreeAssets.txt)

### 6-2. Home 背景に使いたい画像

ユーザーが Home に使いたいと指定した画像:

- 元フォルダ:
  - `C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\画像置き場`
- Unity プロジェクトへコピーしたファイル:
  - [HomeChamberBackground.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/HomeChamberBackground.png)

この画像は雰囲気がかなり良いので、Home の主背景として活かす価値があります。

### 6-3. Unity MCP の基盤

MCP ブリッジ自体はかなり整っています。

主なファイル:

- [UnityMcpBridge.cs](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpBridge.cs)
- [unity_mcp_server.py](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/tools/unity_mcp_server.py)
- [UNITY_MCP_SETUP.md](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/UNITY_MCP_SETUP.md)

MCP 経由でできることの土台はあります。

- scene open
- refresh-assets
- menu item execute
- debug tools

ただし、新セッションでは **画面制作の補助として使う** くらいの位置づけがよいです。

## 7. 要注意ファイル

### 7-1. SceneBuilder

- [UnityMcpSceneBuilder.cs](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpSceneBuilder.cs)

このファイルに Title / Home / Battle の最小シーン構築が大量に入っています。

ただし現状は

- 役割が多すぎる
- 一時的な UI 実験が多い
- 画面構築と仮説検証の履歴が混ざっている

ため、**新しく作るなら全部活かすより、必要部分だけ抜き出して新規に組み直す方が安全**です。

### 7-2. Home の分析ロジック

- [HomeActionAdvisor.cs](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scripts/Home/HomeActionAdvisor.cs)
- [PlayerStatusView.cs](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Scripts/UI/PlayerStatusView.cs)

ここには大量の「分析テキスト」生成ロジックがあります。

ゲーム的な見せ方を優先するなら、

- いったん使わない
- 使っても一部だけ
- `Danger / Reward / Push` など数個に絞る

のがよいです。

## 8. 次セッションでの推奨方針

### 方針

**Title -> Home -> Battle を、それぞれ別物として美術主導で作り直す。**

### 推奨の作り直し順

1. Title 画面を先に完成させる
2. Home 画面を背景主導で再設計する
3. Battle 画面をドットキャラ主役で組み直す
4. 最後にボタン遷移や HUD を足す

### Title 画面の目標

- ファンタジー感が一目で出る
- `Start New Run` が主役
- `Continue` は副ボタン
- 長文説明は置かない

### Home 画面の目標

- 背景は [HomeChamberBackground.png](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Art/External/HomeChamberBackground.png)
- UI は前景に絞る
- まず見せるのは
  - Gold
  - Floor
  - 主役ボタン
  - 下部ナビ
- 情報パネルを全面に敷き詰めない

### Battle 画面の目標

- `witch` と `enemy` のドット絵を大きく見せる
- 地面タイルと背景で場を作る
- HUD は最小限
  - HP
  - Floor
  - 勝敗

## 9. 具体的な再スタート案

新しいセッションで最初にやるとよいこと:

1. [UnityMcpSceneBuilder.cs](/C:/Users/sasan/OneDrive/デスクトップ/ゲーム作成/WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpSceneBuilder.cs) の既存 Home セクションを信用しすぎない
2. Home は背景画像ベースで、最小 UI を 3〜5 要素だけに絞って再構築する
3. 分析文言は一旦ほぼ全部外す
4. Battle はドットキャラのサイズをまず決める
5. スクショを見ながら少しずつ直す

## 10. いま捨ててよいもの

次のものは一旦捨ててもよいです。

- Home の大量の分析テキスト
- 画面内の細かい adviser 文言
- Game feel より先に積まれた経済判断 UI
- SceneBuilder 内の装飾の細かい試行錯誤

## 11. いま残してよいもの

- Scene 遷移の基本導線
- Battle の最低限のロジック
- 無料ドット素材
- MCP ブリッジ
- Home 背景画像

## 12. 最終メモ

今回のセッションで分かったことは、

- 「動く最低限」はもうある
- でも「見せるゲーム画面」はまだ作れていない

ということです。

新しいセッションでは、**機能を増やすより先に、1 画面ずつ見栄えを成立させる** 進め方が向いています。
