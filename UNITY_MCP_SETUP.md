# Unity MCP 接続ガイド

このワークスペースには、Unity Editor と MCP クライアントをつなぐ最小構成を追加しています。

## 追加したもの

- `WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpBridge.cs`
  - Unity Editor 内で `http://127.0.0.1:8765/` を待ち受けるブリッジ
  - 起動時に `tools/unity_bridge_state.json` へ現在の接続先情報を書き出す
- `tools/unity_mcp_server.py`
  - MCP の標準入出力を Unity ブリッジへ中継するサーバー
- `tools/unity_mcp_server.cmd`
  - Windows 用ランチャー
- `tools/unity_bridge_status.cmd`
  - Windows 用診断ランチャー
- `tools/unity_bridge_status.ps1`
  - `unity_bridge_state.json` と実際の `/ping` をまとめて確認する PowerShell 診断スクリプト
- `tools/unity_reports_status.cmd`
  - 保存済みレポート一覧を見る Windows 用ランチャー
- `tools/unity_reports_status.ps1`
  - `tools/reports` の最新 smoke / bridge-status レポート一覧を返す PowerShell スクリプト
- `tools/unity_report_summary.cmd`
  - 最新レポートの要点だけを見る Windows 用ランチャー
- `tools/unity_report_summary.ps1`
  - 最新 smoke / bridge-status レポートの要点サマリーを返す PowerShell スクリプト
- `tools/unity_status_overview.cmd`
  - bridge 状態、最新要約、直近レポート一覧をまとめて返す Windows 用ランチャー
- `tools/unity_status_overview.ps1`
  - bridge と reports の総合ステータスを返す PowerShell スクリプト
- `tools/unity_compare_reports.cmd`
  - 最新 2 件のレポート差分を見る Windows 用ランチャー
- `tools/unity_compare_reports.ps1`
  - 最新 2 件の smoke / bridge-status レポート差分を返す PowerShell スクリプト
- `tools/unity_status_brief.cmd`
  - 今の bridge / smoke 状態を短文で見る Windows 用ランチャー
- `tools/unity_status_brief.ps1`
  - bridge と latest smoke の要点を数行で返す PowerShell スクリプト
- `tools/unity_compile_health.cmd`
  - 再コンパイル滞留の状態だけを短く見る Windows 用ランチャー
- `tools/unity_compile_health.ps1`
  - `UnityMcpBridge.cs` と `Assembly-CSharp-Editor.dll` の差分を返す PowerShell スクリプト
- `tools/unity_compile_health_snapshot.cmd`
  - compile health の latest と履歴 JSON を保存する Windows 用ランチャー
- `tools/unity_compile_health_snapshot.ps1`
  - `tools/reports` に compile health スナップショットを保存する PowerShell スクリプト
- `tools/unity_compile_health_history.cmd`
  - compile health の履歴一覧を見る Windows 用ランチャー
- `tools/unity_compile_health_history.ps1`
  - 保存済み compile health レポート一覧を返す PowerShell スクリプト
- `tools/unity_refresh_and_wait_compile.cmd`
  - Asset refresh 後に compile stale が 0 になるまで待つ Windows 用ランチャー
- `tools/unity_refresh_and_wait_compile.ps1`
  - refresh と compile health poll をまとめて実行する PowerShell スクリプト

## できること

- Unity との接続確認
- MCP から bridge 状態ファイルと ping を直接確認
- MCP から最新の smoke / bridge-status レポートを確認
- MCP から最新レポートの要点サマリーを確認
- MCP から短文の総合ステータスを確認
- プロジェクト情報の取得
- シーン一覧の取得
- シーンを開く
- AssetDatabase の再読込
- Unity メニューの実行
- Play Mode の切り替え
- Home / Battle のスモークチェック実行

## 使い方

1. Unity Hub から `WitchTowerGame` を開く
2. コンパイル完了後、Unity Console に `Listening on http://127.0.0.1:8765/` が出ることを確認する
3. MCP クライアント側で `tools/unity_mcp_server.cmd` をサーバーとして登録する

## Codex / MCP 設定例

コマンド:

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_mcp_server.cmd
```

必要に応じて環境変数を追加できます。

```text
UNITY_MCP_BASE_URL=http://127.0.0.1:8765
```

環境変数を指定しない場合は、`tools/unity_bridge_state.json` があればその `baseUrl` を優先して使います。

## スモークチェック

Unity を開いた状態なら、次のコマンドで MCP ブリッジ経由の最小動作確認をまとめて回せます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_smoke_check.cmd
```

このチェックでは、`ping`、Home シーン再生成、Home 直入り Play、Equipment パネル表示、Battle 開始、BootScene への復帰までを確認します。

Python が使いにくい環境では、PowerShell 版も使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_smoke_check_runner.cmd
```

新規開始の装備ロック状態まで見たい場合は、次のように `-FreshStart` を付けます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_smoke_check_runner.cmd -FreshStart
```

装備解放の段階進行まで確認したい場合は、さらに `-UnlockProgression` を付けます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_smoke_check_runner.cmd -FreshStart -UnlockProgression
```

PowerShell 版は実行ごとにレポートを `tools/reports` へ保存します。`latest` と時刻付きの両方が出るので、直近結果の参照と履歴比較の両方に使えます。
また、PowerShell 版スモークは実行後に `unity-bridge-status-<scenario>-latest.json` も併せて保存するので、ブリッジ不調時の切り分けにも使えます。

接続状態だけを手早く確認したい場合は、次のコマンドで `unity_bridge_state.json` と `/ping` の両方を見られます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_bridge_status.cmd
```

MCP 側では `unity_bridge_status` ツールでも同じ情報を取れます。Unity 本体が落ちていても、このツールだけは state file と ping の両方を返せます。
保存済みレポートを見たいときは `unity_latest_report` で `kind = smoke` または `bridge-status` を指定すると、`tools/reports` の最新 JSON を返せます。
要点だけ見たいときは `unity_report_summary` で `kind = smoke` または `bridge-status` を指定すると、成功可否や最後のエラー、解決 URL などを短く返します。
bridge-status の要約には、`portListeningCount` と `portCloseWaitCount` も含まれるので、待受はあるのに応答が詰まっている状態も短く追えます。
さらに `ownerPid / ownerProcessName / ownerResponding` も含まれるので、state file が出ていないときでもどの Unity プロセスがポートを握っているかを確認できます。
ローカルの bridge 診断には `compileStatus` も含まれ、`UnityMcpBridge.cs` と `Assembly-CSharp-Editor.dll` の時刻差から「スクリプト更新がまだ再コンパイルされていない」状態も追えます。
まとめて見たいときは `unity_status_overview` を使うと、bridge 状態、最新要約、直近レポート一覧を 1 回で返せます。
直近 2 件の変化だけ見たいときは `unity_compare_reports` を使うと、成功可否やエラー文、ping 結果の差分を返せます。
とにかく短文だけ欲しいときは `unity_status_brief` を使うと、bridge 到達可否、base URL、最新 smoke の失敗箇所まで数行で返せます。
復旧の当たりをすぐ見たいときは `unity_recovery_hint.cmd` を使うと、いまの診断コードと推奨アクションを返せます。
一覧で見たいときは、ローカルでは次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_reports_status.cmd -Kind smoke -Limit 5
```

MCP 側では `unity_list_reports` で同じように直近レポート一覧を取れます。
ローカルで要点だけ見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_report_summary.cmd -Kind bridge-status
```

ローカルでまとめて見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_status_overview.cmd
```

ローカルで直近 2 件の差分を見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_compare_reports.cmd -Kind smoke
```

短文だけで今の状態を見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_status_brief.cmd
```

復旧のヒントだけ見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_recovery_hint.cmd
```

再コンパイルが進んでいるかだけ見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_compile_health.cmd
```

履歴も残したいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_compile_health_snapshot.cmd
```

保存済み compile health の履歴一覧を見たいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_compile_health_history.cmd -Limit 5
```

最新変更を Unity に取り込ませたいときは、次のコマンドが使えます。

```text
C:\Users\sasan\OneDrive\デスクトップ\ゲーム作成\tools\unity_refresh_and_wait_compile.cmd
```

## Unity 側のメニュー

- `Tools/MCP/Restart Bridge`
- `Tools/MCP/Log Bridge Status`

## トラブルシュート

- Unity が開いていないと、MCP ツールは接続エラーになります
- コンパイルエラーがあると Editor 拡張が起動しません
- ポートが競合している場合は、`UnityMcpBridge.cs` の `Prefix` と `UNITY_MCP_BASE_URL` を同じ値に変更してください
- どの Unity が待ち受けているか分からないときは、`tools/unity_bridge_state.json` の `projectPath / processId / baseUrl` を確認すると追いやすいです
- MCP サーバーの接続エラーには `unity_bridge_state.json` の内容も含まれるので、失敗時はその URL と `running` 状態を見ると切り分けしやすいです

## 補足

`open-scene` は Unity 側で未保存シーンがあると確認ダイアログが出ます。自動処理中に止めたくない場合は、先に保存してから使うのが安全です。
