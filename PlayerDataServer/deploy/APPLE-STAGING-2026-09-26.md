# Apple本番構成の下準備（有効化ではない）

入口: `bash /Users/andou/Desktop/game-creation/PlayerDataServer/deploy/run-apple-staging-mac.sh`

## 変更する範囲

- Macの非公開鍵フォルダに32バイトのtoken暗号鍵を新規生成。既存鍵は再利用し、上書きしない。
- 既存の署名鍵 `38GM3RYVD9` と暗号鍵を、ホスト確認済みSSH経由で同じVPSへ転送する。
  秘密鍵をGit・配布アーカイブ・標準出力・コマンド引数に入れない。
- `/opt/nasus-apple-20260926` にroot所有の独立コード/venvを新設する。
  既存APIのコード・venv・Gunicorn設定は触らない。
- 固定版PyJWT 2.15.0 / cryptography 50.0.1 / cffi 2.1.1 / pycparser 3.0を公式PyPIから取得済み。
  Linux x86_64/Python 3.12用wheelをSHA-256固定で配送し、root反映時はオフラインでインストールする。
- 新しい暗号化外部バックアップの転送・読み戻し検証を要求する。
  さらにサービスユーザーの私有フォルダへSQLiteの整合スナップショットを保存し、検証する。
- 既存の削除journalとinstanceを照合し、Apple用の6テーブルが一つも存在しない場合のみ、
  単一トランザクションで空のテーブル/索引を追加する。既存行の変更やDB復元はしない。
- `/etc/witch-player-apple` と `/var/lib/witch-player-apple` をwitchplayer所有700で新設。
  鍵と設定は600。実際のruntimeの読取り確認が成功し、外部health=200/root=503を確認して完了。

## 変更しない範囲

公開API、Caddy、既存systemd unit/監視、Apple service/timerの登録・有効化、実Apple通信は行わない。
sudoの恒久許可も追加しない。パスワードは既存sudoへ直接入力し、スクリプトでは受け取らない。
serviceテンプレートの実行先は独立環境へ更新したが、**この工程ではインストールしない**。

## 失敗した場合

`APPLE_STAGING_STOPPED` で終了。新規コード/鍵/検証済みスナップショットは保持する。
DB追加トランザクションの途中ならrollbackするが、commit後の異常でDBを古い状態へ戻さない。
再実行で上書きすることは拒否する。保存済みレポートと実状態を調べ、原因に限定して対応する。
成功時のみ転送用の鍵コピーを取り除く。Macの原本とVPSのサービス用コピーは残る。

## 完了判定と残り

レポートの `Status=APPLE_STAGING_COMPLETE` を確認するまでは未反映扱い。
その後も定期処理/監視統合、鍵の独立した安全なバックアップと復元確認、
既存APIへの組立て、開発署名、実Apple+iPhoneの連携/引継ぎ/解除/削除試験が必要。
Mac上の鍵保管は独立した災害対策バックアップの代わりにはならない。

ローカルでは追加14件を含む全458テスト成功（失敗0・スキップ0）。
VPSのubuntu用の隔離試験環境でwheel依存関係・import・検証器構築を確認済み。
これはroot配置後の成功やApple側認証成功の証明ではない。
