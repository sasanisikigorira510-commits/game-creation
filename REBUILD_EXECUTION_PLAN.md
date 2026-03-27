# Witch Tower やり直し実行プラン

この文書は、`HANDOFF_REBUILD_2026-03-22.md` をもとに、
Unity プロジェクトを見た目優先で順番に作り直すための実行チェックリストです。

## 目的

- 「ゲーム画面らしく見える」ことを最優先にする
- `Title -> Home -> Battle` の順で1画面ずつ完成度を上げる
- 既存ロジックは必要最低限だけ残し、画面をシンプルに再構築する

## 現在の前提

- Unity バージョンは `6000.3.11f1`
- 既存シーンはすでに存在する
  - `WitchTowerGame/Assets/Scenes/BootScene.unity`
  - `WitchTowerGame/Assets/Scenes/TitleScene.unity`
  - `WitchTowerGame/Assets/Scenes/HomeScene.unity`
  - `WitchTowerGame/Assets/Scenes/BattleScene.unity`
- 既存 Home には分析系 UI が多く残っている
- Battle の最低限ロジックと Scene 遷移の土台は利用価値がある

## 残すもの

- `Boot -> Title -> Home -> Battle` の基本導線
- Battle の最低限の進行ロジック
- ドット絵素材
- `HomeChamberBackground.png`
- MCP ブリッジ

## 一旦信用しすぎないもの

- `UnityMcpSceneBuilder.cs` の Home 構築
- `HomeActionAdvisor.cs` の大量の分析テキスト
- `PlayerStatusView.cs` の分析表示前提
- 現在の `HomeScene.unity` 上の密度の高い UI 配置

## 実行順

### Phase 0: 足場確認

- [ ] Unity で `WitchTowerGame` を開く
- [ ] コンパイルエラーがないか確認する
- [ ] `Boot -> Title -> Home -> Battle` の現状遷移を一度確認する
- [ ] 現在の各画面スクリーンショットを保存して比較基準にする

### Phase 1: Title を先に作り直す

目標:

- 一目でファンタジーゲームだと分かる
- `Start New Run` を主役にする
- `Continue` は副ボタンにする
- 説明文は短くする

作業:

- [ ] 既存 `TitleScene` の要素を整理する
- [ ] 背景、ロゴ、主ボタン、副ボタンの4要素に絞る
- [ ] 文字量を最小化する
- [ ] `Start New Run -> HomeScene` の導線を確認する
- [ ] スマホ縦画面での見え方を確認する

完了条件:

- [ ] スクショ1枚で「タイトル画面」と分かる
- [ ] 主要ボタンが迷わず押せる

### Phase 2: Home を背景主導で再設計する

目標:

- `HomeChamberBackground.png` を主役にする
- UI は 3〜5 要素に絞る
- 分析文言をほぼ消す

最初に置く UI:

- [ ] Gold
- [ ] Floor
- [ ] 主役ボタン
- [ ] 下部ナビ、または単純な上部タブ

外す候補:

- [ ] Threat Read
- [ ] Confidence
- [ ] Loadout Alert
- [ ] Gold Route
- [ ] Upgrade Route
- [ ] Reward Route
- [ ] そのほか分析系テキスト

作業:

- [ ] `HomeScene` の情報過多 UI を削るか非表示化する
- [ ] 背景に対して主役ボタンの位置を合わせる
- [ ] ナビゲーションを単純化する
- [ ] 画面中央の見せ場を1つに絞る
- [ ] `Home -> BattleScene` の導線を確認する

完了条件:

- [ ] 背景と前景 UI が喧嘩していない
- [ ] 初見でも何を押せばよいか分かる
- [ ] 謎の英語分析文が前面に出ていない

### Phase 3: Battle をドット絵主役で再構築する

目標:

- `witch` と `enemy` が主役に見える
- 地面と背景で戦闘の場が成立している
- HUD は最小限にする

最小 HUD:

- [ ] HP
- [ ] Floor
- [ ] 勝敗表示

使う候補素材:

- [ ] `witch_idle.png`
- [ ] `witch_cast.png`
- [ ] `enemy_death_mage_elf.png`
- [ ] `tree.png`

作業:

- [ ] キャラ表示サイズを先に決める
- [ ] 地面タイルや背景を整える
- [ ] 視線誘導がキャラ中央に集まるようにする
- [ ] 勝利後の `Return Home` 導線を確認する

完了条件:

- [ ] スクショ1枚で戦闘画面に見える
- [ ] HUD が邪魔せず、必要情報だけ読める

### Phase 4: 最後に導線と磨き込み

- [ ] `Boot -> Title -> Home -> Battle -> Home` を通し確認する
- [ ] 各画面でボタン文言を統一する
- [ ] 余分な説明文を削る
- [ ] スマホ縦画面で UI がはみ出していないか確認する
- [ ] 比較スクショを取って改善を見える化する

## 進め方のルール

- 1画面ずつ完成させる
- システム追加より先に見た目を整える
- 画面に置く情報は「必要最小限」から始める
- Home の分析 UI は再採用するとしても最後にごく一部だけ戻す
- `UnityMcpSceneBuilder.cs` は必要箇所の参考にとどめる

## 次の着手点

次に始める作業は `Phase 1: Title を先に作り直す`。
この順番なら、画面品質の基準を最初に作ってから Home と Battle に横展開できます。
