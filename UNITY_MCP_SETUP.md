# Unity MCP 接続ガイド

このワークスペースには、Unity Editor と MCP クライアントをつなぐ最小構成を追加しています。

## 追加したもの

- `WitchTowerGame/Assets/Editor/UnityMcp/UnityMcpBridge.cs`
  - Unity Editor 内で `http://127.0.0.1:8765/` を待ち受けるブリッジ
- `tools/unity_mcp_server.py`
  - MCP の標準入出力を Unity ブリッジへ中継するサーバー
- `tools/unity_mcp_server.cmd`
  - Windows 用ランチャー

## できること

- Unity との接続確認
- プロジェクト情報の取得
- シーン一覧の取得
- シーンを開く
- AssetDatabase の再読込
- Unity メニューの実行
- Play Mode の切り替え

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

## Unity 側のメニュー

- `Tools/MCP/Restart Bridge`
- `Tools/MCP/Log Bridge Status`

## トラブルシュート

- Unity が開いていないと、MCP ツールは接続エラーになります
- コンパイルエラーがあると Editor 拡張が起動しません
- ポートが競合している場合は、`UnityMcpBridge.cs` の `Prefix` と `UNITY_MCP_BASE_URL` を同じ値に変更してください

## 補足

`open-scene` は Unity 側で未保存シーンがあると確認ダイアログが出ます。自動処理中に止めたくない場合は、先に保存してから使うのが安全です。
