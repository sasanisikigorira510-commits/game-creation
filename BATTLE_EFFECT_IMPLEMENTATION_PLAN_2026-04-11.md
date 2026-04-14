# バトル遠距離エフェクト実装整理 2026-04-11

## 目的
- 画像置き場にある遠距離エフェクト素材を、Unity 実装へそのままつなげられるよう整理する
- 火属性と雷属性の最初の実装対象を明確にする
- 今後 `氷` `闇` `光` へ拡張しやすい構成にする

## 現在の素材対応表

### 火属性
- [発生用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/発生用.png)
  用途: 火の玉の詠唱開始、手元の溜め
  再生位置: 攻撃者の手元、杖先、口元
  タイミング: 攻撃モーション開始直後

- [飛翔本体.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/飛翔本体.png)
  用途: 火の玉本体
  再生位置: 攻撃者から対象へ移動
  タイミング: 発射フレーム

- [着弾用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/着弾用.png)
  用途: 火属性の着弾爆発
  再生位置: 対象中心か少し手前
  タイミング: ダメージ発生フレーム

- [被弾用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/被弾用.png)
  用途: 火属性の被弾、燃焼ヒット
  再生位置: 対象の中心
  タイミング: 着弾直後

### 雷属性
- [落雷頭上魔法陣.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/落雷頭上魔法陣.png)
  用途: 頭上召喚予兆
  再生位置: 対象の頭上
  タイミング: 落雷本体の少し前

- [地面マーカー.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/地面マーカー.png)
  用途: 落雷着弾位置の警告
  再生位置: 対象の足元
  タイミング: 落雷本体の少し前

- [落雷本体.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/落雷本体.png)
  用途: 落雷の縦本体
  再生位置: 対象の真上から足元へ縦方向
  タイミング: ダメージ発生フレーム

- [被弾・持続用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/被弾・持続用.png)
  用途: 感電ヒット、感電持続
  再生位置: 対象中心または足元
  タイミング: 落雷本体の直後

## 実装上の役割分担

### 火の玉
- `cast`
  [発生用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/発生用.png)
- `projectile`
  [飛翔本体.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/飛翔本体.png)
- `impact`
  [着弾用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/着弾用.png)
- `hit_overlay`
  [被弾用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/被弾用.png)

### 落雷
- `warning_air`
  [落雷頭上魔法陣.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/落雷頭上魔法陣.png)
- `warning_ground`
  [地面マーカー.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/地面マーカー.png)
- `strike`
  [落雷本体.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/落雷本体.png)
- `hit_overlay`
  [被弾・持続用.png](/Users/andou/Desktop/あ/game-creation/画像置き場/遠距離エフェクト/被弾・持続用.png)

## Unity 側で持たせたいデータ

### 共通項目
- `effectId`
- `attributeType`
  例: `Fire`, `Thunder`
- `attackPattern`
  例: `Projectile`, `SummonStrike`
- `castSprite`
- `projectileSprite`
- `impactSprite`
- `hitOverlaySprite`
- `warningAirSprite`
- `warningGroundSprite`

### 発生タイミング
- `castDelay`
  攻撃開始から発生まで
- `projectileDelay`
  発生から弾発射まで
- `impactDelay`
  発射から着弾まで
- `hitOverlayDelay`
  着弾から被弾表示まで

### 表示位置
- `spawnOffset`
  手元、杖先、口元
- `targetOffset`
  胴体、頭、足元
- `warningAirOffset`
  頭上の高さ
- `warningGroundOffset`
  足元位置

### 動き
- `travelType`
  `Straight`
  `Arc`
  `Summon`
- `travelDuration`
- `scale`
- `colorTint`
- `loopDuration`

## 攻撃種別ごとの実装イメージ

### Projectile 型
- 対象:
  火の玉
  魔法弾
  氷弾
  闇弾
- 流れ:
  1. 攻撃者に `cast`
  2. 攻撃者から対象へ `projectile`
  3. 対象に `impact`
  4. 必要なら `hit_overlay`

### SummonStrike 型
- 対象:
  落雷
  隕石
  光柱
- 流れ:
  1. 対象頭上に `warning_air`
  2. 対象足元に `warning_ground`
  3. 対象へ `strike`
  4. 必要なら `hit_overlay`

## 推奨フォルダ構成

### 画像置き場
- `game-creation/画像置き場/遠距離エフェクト/Fire`
- `game-creation/画像置き場/遠距離エフェクト/Thunder`
- `game-creation/画像置き場/遠距離エフェクト/Common`

### Unity 取り込み先
- `game-creation/WitchTowerGame/Assets/Art/Effects/Battle/Fire`
- `game-creation/WitchTowerGame/Assets/Art/Effects/Battle/Thunder`
- `game-creation/WitchTowerGame/Assets/Art/Effects/Battle/Common`

## 推奨ファイル名

### 火属性
- `fx_fire_cast_01.png`
- `fx_fire_projectile_01.png`
- `fx_fire_impact_01.png`
- `fx_fire_hit_overlay_01.png`

### 雷属性
- `fx_thunder_warning_air_01.png`
- `fx_thunder_warning_ground_01.png`
- `fx_thunder_strike_01.png`
- `fx_thunder_hit_overlay_01.png`

## 実装時の注意
- `落雷本体` は縦ライン主体にする
- `雷着弾` は地表放電主体にする
- `火着弾` と `雷着弾` を同じフォルダに混ぜない
- 被弾用は対象中央に重ねる前提でサイズを抑える
- 予兆はダメージ発生より少し早く見せる

## 先にやるべきこと
1. 現在の画像置き場を `Fire` と `Thunder` に分ける
2. Unity 側に `Assets/Art/Effects/Battle` を作る
3. 火の玉と落雷の 2 パターンだけ先にデータ化する
4. その後にコード側を `Projectile` と `SummonStrike` に分ける

## 最初の実装対象

### 火の玉
- 攻撃者の手元に発生
- 対象へ直線移動
- 着弾時に爆発
- 対象へ短い燃焼被弾

### 落雷
- 頭上と地面に予兆
- 少し遅れて真下に落雷
- 落雷後に短い感電ヒット

## 完了条件
- 火の玉と落雷の 2 系統がゲーム内で役割分担して見える
- 見ただけで `火` と `雷` が判別できる
- `発生` `飛翔` `着弾` `被弾` がコード上で差し替え可能になっている
