# Apple連携・削除APIの接続 — 公開前のローカル検証

2026-09-26。③-Bの続き。`apple_application.create_apple_app`を追加した。
通常の`production.application_factory`やGunicorn/service/Caddyは変更していない。
**この別factoryは本番未配置・未起動。公開許可や実Apple認証成功を意味しない。**

## 接続した範囲

既存の明示設定から、同じDB・アプリID・暗号鍵を使う連携、失効queue、削除API、削除履歴を組み立てる。
`WITCH_APPLE_CONFIG`に加え、既存のDATA_DIR/INSTANCE_ID/PUBLIC_ORIGIN/OPERATORS_FILEと
BACKUP_HEALTH_FILEを必須とする。`deploy/apple-worker.json.example`の構成を共有する。
通常の本番factoryは、この追加変数があってもApple連携を有効化しない。

- Storeの初期化処理を呼ばず、既存DBだけを開く。必要なtable/columnが欠けたら停止する。
- AccountLinking/AccountDeletionの`initialize=False`で起動時のtable作成を禁止する。
- 既存のDeletionJournalを`initialize=False`で接続し、instance一致を要求する。
  journal未設定で削除成功を返す代替経路を作らない。
- Apple解除HTTPから永続queueへ渡し、別workerで模擬providerの失効を完了する通し試験を追加。
  ゲームのプレイヤー/資格情報は解除で消さない。
- `/healthz`はバックアップとApple workerの両方の新鮮な正常結果を要求する。
  markerだけではtimer停止直後を判定できないため、systemd状態を読む既存監視への接続は別途必須。
  実行中の異常markerによる一時的通知の扱いも本番統合時に確認する。
- 課金用Bundle IDが設定されている場合、Apple連携用IDと一致し、課金検証器が明示注入されていることを要求する。
  課金検証器なしでは購入は無効。実Sandbox購入検証や検証器の本番組立ては未完了。

## 確認結果と残り

`tests/test_apple_application.py`の9件は一時DB、使い捨て鍵、模擬providerのみを使用。
起動時のDB行/schema不変、schema欠落、instance不一致/復元隔離、通常factoryの無効状態、
連携challenge、削除の外部ACK待ち、解除からworkerへの接続、監視、課金設定の誤接続拒否を確認。

実Appleの設定/署名鍵・暗号鍵の安全な配置と復旧、本番schema移行のレビュー、worker/監視の反映、
Apple通知・資格情報失効/結果不明交換の運用、iPhone実機の通し試験は残る。
試験用鍵を本番に流用せず、鍵未設定のまま有効化しない。公開前条件はRELEASE-PROGRESS.mdを参照。
