# Unityプロジェクト雛形

このフォルダは、Unityプロジェクトへ移植しやすい形で初期構成をまとめたものです。

## 想定配置

- `Assets/Scenes`
- `Assets/Scripts/Core`
- `Assets/Scripts/Managers`
- `Assets/Scripts/Battle`
- `Assets/Scripts/Home`
- `Assets/Scripts/UI`
- `Assets/Scripts/Data`
- `Assets/Scripts/Save`

## 含まれる内容

- ゲーム全体管理の基礎クラス
- セーブデータ雛形
- バトル制御の基礎クラス
- ホーム画面制御の基礎クラス
- ScriptableObject の初期定義
- 仮戦闘ループ
- 強化値の保存と戦闘反映
- 勝利報酬とレベルアップの基礎処理
- 装備変更と装備補正の戦闘反映
- リザルト表示と次アクション導線
- 3種スキルとクールダウン表示
- 敵特性の戦闘反映
- ダメージ表示と簡易ヒット演出
- デイリー報酬と基本ミッション
- 放置報酬
- Unity実データ投入前提のMVP土台一式
- `Resources/MasterData/MasterDataRoot` からの自動読込対応

## 進め方

1. Unityで新規 2D プロジェクトを作成する
2. `Assets/Scripts` 以下にこの雛形をコピーする
3. `BootScene`, `TitleScene`, `HomeScene`, `BattleScene` を作る
4. 各シーンに対応するControllerをアタッチする
5. ScriptableObjectマスターデータを後から追加する
6. `BattleSimulator` と `BattleHudController` を接続して勝敗確認を行う
