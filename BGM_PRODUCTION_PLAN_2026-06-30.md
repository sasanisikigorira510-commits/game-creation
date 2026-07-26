# BGM制作方針 2026-06-30

## 目的

`WitchTowerGame` のBGMは、単なる背景音ではなくゲーム全体の記憶に残る主題として作る。
まずホーム曲をメインテーマとして作り、その旋律や和声をバトル、召喚、配合へ変奏して統一感を出す。

## 音楽の軸

- ダークファンタジー、塔、契約、召喚、魔導
- 重いが、周回プレイで疲れすぎない
- 低音は控えめに締め、鐘、女声風パッド、低弦、細い打楽器で神秘感を作る
- メロディは短く覚えやすく、2小節から4小節の動機を全曲で共有する

## 最初に作る曲

### 1. ホームテーマ

- Unity key: `home_theme`
- 配置: `Assets/Resources/Audio/BGM/home_theme_loop.ogg`
- 長さ: 60秒から90秒
- BPM: 70から86
- ループ: 完全ループ。終端から冒頭に戻ってもコード感が破綻しないこと
- 役割: ゲームの顔。暗いが安心できる拠点感
- 編成案: 低弦ドローン、柔らかい鐘、薄いクワイア、控えめなハープ/ピアノ

### 2. 通常バトル

- Unity key: `battle_normal`
- 配置: `Assets/Resources/Audio/BGM/battle_normal_loop.ogg`
- 長さ: 45秒から75秒
- BPM: 112から132
- 役割: 周回のテンポを作る。うるさすぎない緊張感
- ホームテーマの旋律を短調・リズム強めに変奏する

### 3. 召喚

- Unity key: `summon_chamber`
- 配置: `Assets/Resources/Audio/BGM/summon_chamber_loop.ogg`
- 長さ: 40秒から60秒
- BPM: 自由、拍感は薄め
- 役割: 契約儀式。レア演出と相性のよい浮遊感
- 鐘、逆再生風パッド、低い脈動を中心にする

### 4. 配合

- Unity key: `fusion_ritual`
- 配置: `Assets/Resources/Audio/BGM/fusion_ritual_loop.ogg`
- 長さ: 45秒から70秒
- 役割: 禁術、進化、合成の期待感
- 召喚よりも低く、少し危うい響きにする

### 5. ボス/高階層

- Unity key: `battle_boss`
- 配置: `Assets/Resources/Audio/BGM/battle_boss_loop.ogg`
- 実装は後回し。通常バトルの強化版として作る。

## ステム制作をする場合

凝る場合は1曲を以下の名前で書き出す。`AudioManager` は単一ループ曲が無ければステムを探して同時再生する。

- `home_theme_base.ogg`: 空気感、低音、和声
- `home_theme_rhythm.ogg`: 打楽器、鼓動
- `home_theme_melody.ogg`: 主旋律
- `home_theme_tension.ogg`: 緊張レイヤー

他の曲も同じ命名規則にする。

## 書き出し設定

- 形式: Ogg Vorbis 推奨
- Sample Rate: 44.1kHz
- Bitrate: 160kbpsから192kbps目安
- ループ前提なので末尾に無音を残さない
- 音量はスマホスピーカーで痛くない程度。ピークは -1dB以下、体感音量は曲同士で揃える

## 制作順

1. `home_theme` の8小節デモを作る
2. そのデモをUnityに入れて、ホーム画面で邪魔にならないか確認する
3. 60秒から90秒の完成ループへ伸ばす
4. 同じ動機から `battle_normal` を作る
5. `summon_chamber` と `fusion_ritual` を作る

まずは `home_theme_loop.ogg` だけを完成させる。

## 現在の初稿

### ホームテーマ

- 生成スクリプト: `tools/generate_home_theme_demo.py`
- 出力: `WitchTowerGame/Assets/Resources/Audio/BGM/home_theme_loop.wav`
- 長さ: 72秒
- BPM: 80
- 構成: 8小節 x 3ブロック。静かな導入、少し厚い中盤、落ち着いた戻り
- 用途: ホームテーマの長尺確認用デモ
- 注意: 最終版では60秒から90秒程度のOgg Vorbisへ作り直す

### 通常バトル

- 生成スクリプト: `tools/generate_battle_normal_demo.py`
- 出力: `WitchTowerGame/Assets/Resources/Audio/BGM/battle_normal_loop.wav`
- 長さ: 64秒
- BPM: 120
- 構成: 8小節 x 4ブロック。低い鼓動、細かいリズム、ホーム主題の短い変奏
- 用途: 通常バトルBGMの方向性確認用デモ
- 注意: 最終版ではOgg Vorbis化し、ホーム曲との音量差を実機で確認する

### ダンジョン別バトルBGM

- 生成スクリプト: `tools/generate_dungeon_bgm_demos.py`
- 方針: 既存作品の旋律や固有フレーズは使わず、儚い合唱風パッド、鐘、撥弦、ピアノ風アタック、控えめな儀式打楽器で暗い幻想感を作る
- 出力:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_blight_cavern_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_gear_crypt_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_curse_library_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_ember_drake_pass_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_star_ore_citadel_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_abyssal_grimoire_spire_loop.wav`
- 実装: `BattleDungeonCatalog.ResolveBattleBgmKey()` が現在のグローバル階層からダンジョンBGMキーを返し、`BattleSceneController` が遭遇更新時に切り替える

### ダンジョン別ボスBGM

- 生成スクリプト: `tools/generate_dungeon_boss_bgm_demos.py`
- 方針: 通常ダンジョン曲の空気感を残しながら、短い高緊張ループとして打楽器、低音、鐘の密度を上げる
- 出力:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_blight_cavern_boss_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_gear_crypt_boss_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_curse_library_boss_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_ember_drake_pass_boss_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_star_ore_citadel_boss_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_abyssal_grimoire_spire_boss_loop.wav`

### 獣影の廃工廠 再制作

- 生成スクリプト: `tools/generate_gear_crypt_bgm.py`
- 対象:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_gear_crypt_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_gear_crypt_boss_loop.wav`
- 方針: 旧ダンジョン共通生成器から分離し、廃工廠らしい金属打撃、歯車クリック、蒸気ノイズ、低い獣の唸り、機械的な非対称パルスを前面に出す

### 古契約の地下書庫 再制作

- 生成スクリプト: `tools/generate_curse_library_bgm.py`
- 対象:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_curse_library_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_curse_library_boss_loop.wav`
- 方針: 「ビッグブリッジの死闘」系の疾走感、跳ねるベース、勇ましい掛け合い、畳みかける展開をモチーフの粒度に留め、既存旋律は使わず、古契約の地下書庫らしいチェンバロ風撥弦、オルガンリード、禁書のページ音、書物打撃、儀式的な鐘で再解釈する
- ボス調整: 低音進行に対して固定上声が濁って聞こえる箇所を避けるため、ボスの上声とアルペジオを各小節のコードへ追従させる

### 紅蓮竜道 再制作

- 生成スクリプト: `tools/generate_ember_drake_pass_bgm.py`
- 対象:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_ember_drake_pass_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_ember_drake_pass_boss_loop.wav`
- 方針: Shape of My Heart系の細いナイロン弦アルペジオ、哀愁のある近い和声、抑えた低音、柔らかいパーカッションをモチーフの粒度に留め、既存旋律や固有進行は使わず、火の粉、熱風、低い竜の脈動で紅蓮竜道向けに再解釈する

### 星鉱の巨殿 再制作

- 生成スクリプト: `tools/generate_star_ore_citadel_bgm.py`
- 対象:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_star_ore_citadel_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_star_ore_citadel_boss_loop.wav`
- 方針: Ken Arai「Phonecall」系の軽い恋愛ドラマBGM感、小さいベル、柔らかいシンコペーション、電話越しの距離感をモチーフの粒度に留め、既存旋律は使わず、星鉱の巨殿らしい微細な鉱石のきらめきだけを足して再解釈する。エレピは主役ではなく薄い伴奏に下げ、主旋律はベル/電話ブリップで前に出す

### 深淵魔導回廊 再制作

- 生成スクリプト: `tools/generate_abyssal_grimoire_spire_bgm.py`
- 対象:
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_abyssal_grimoire_spire_loop.wav`
  - `WitchTowerGame/Assets/Resources/Audio/BGM/dungeon_abyssal_grimoire_spire_boss_loop.wav`
- 方針: ボサノバ調。ナイロンギターのシンコペーション、アップライトベース風の動き、リムクリック、ブラシ、ヴィブラフォン風メロディを主役にし、オルガン/合唱系の重いダークファンタジー音色は使わない。深淵要素は薄い空気音と控えめな不思議ベルだけに留める
- ボス調整: ジャズ的なテンションが濁りに聞こえやすい箇所を整理し、ボス版は四和音中心の穏やかなボサノバ和声に寄せる
