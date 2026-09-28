# Sandbox返金の明示的な接続構成（稼働環境未反映）

2026-09-28。ローカルの合成データで確認。実鍵、実Apple通知、VPS変更、iPhone配布は行っていない。

## 追加した組み立て関数

`refund_sandbox.create_sandbox_apps(environ)` は明示した既存Sandbox構成から3つのWSGI入口を返す。
環境変数だけで自動起動する `application_factory` や、公開プロキシ/インストーラは追加していない。
通常production/現在のQA factoryは変更せず、過去の署名固定済み配布物も変更していない。

- `device`: 既存の期限付きQaGatewayと個別プレイヤー認証。管理者操作・Apple通知は404。
- `review`: 名前付き管理者認証で返金preview/commitだけを受付。一般の管理者API/端末APIは404。
- `notifications`: POST `/v1/store/apple/notifications` だけを受付。
  QAトークンを要求する代わりに、既存のApple署名・環境・アプリ・所有者・最新取引検証を使う。
  単なるJSON指定で残高は変更できない。通知以外の経路は404。

3入口は同じStoreを共有し、購入検証、返金台帳、審査、Appleアカウント連携も同じDBに接続する。
別プロセスとして組み立てても設定上同じDB/instanceとなることを検査する。
期限切れでは端末入口は従来通り404、審査/通知は503。期限切れ通知を処理済みとして返さない。
管理者入口は将来のプロキシで私有ネットワークに限定すること。公開の通知URLから転送してはいけない。
すべての入口で既存ApplicationのHTTPS・Host・Origin・信頼プロキシ条件を維持する。

## 必須設定と起動時の停止条件

明示有効化 `WITCH_REFUND_SANDBOX_ENABLED=1` に加え、以下を指定する。

- `WITCH_REFUND_WORKER_CONFIG`: 既存の返金ワーカー用私有JSON。
- `WITCH_QA_GATE_CONFIG`: 有効な期限付きQAゲートJSON。自動延長しない。
- `WITCH_APPLE_CONFIG`: 既存のAppleアカウント連携/失効ワーカー用私有JSON。
- `WITCH_DATA_DIR`, `WITCH_INSTANCE_ID`, `WITCH_PUBLIC_ORIGIN`
- `WITCH_OPERATORS_FILE`, `WITCH_BACKUP_HEALTH_FILE`

返金ワーカー設定のEnvironmentとAppleConfigFile内はSandbox必須。
DBディレクトリ/instanceとアカウント連携構成のDataDirectory/InstanceId/ClientIdを照合する。
継承環境に食い違うApple購入設定があれば、黙って上書きせず停止する。
データディレクトリ内の私有 `SANDBOX-ONLY.json` は次を満たす必要がある。

```
Purpose = SANDBOX_ONLY_NOT_PRODUCTION
InstanceId = 設定したインスタンスID
ProductionMigrationApproved = false
```

署名鍵/プロバイダを構築する前に期限とDB/台帳を検査する。台帳が欠けていれば作り直さない。
`RECOVERY-PENDING.txt` のある候補は起動拒否。移行リハーサル成功だけで起動許可は与えない。
返金ワーカーとApple連携失効ワーカーの状態ディレクトリは別々で、入れ子も禁止する。
バックアップ成功記録と各ワーカーの `status.json` の共用も禁止する。
購入と通知の検証器は同じ明示設定から `sandbox_only=True` で構築する。

起動時には通信・購入・返金適用・DB変更をしないが、明示構成の署名鍵は読み込む。
したがって稼働環境の設定を使った起動試験は、このローカル検証の代わりに無断実行しない。

## 監視と未有効化機能

端末入口のhealthは既存バックアップ/アカウント失効の監視に加え、返金ワーカーの
systemd状態・期限内成功・未処理件数を検査する。返金の監視失敗だけで通常のプレイヤー読取を止めない。
現行 `check_systemd` は固定ユニット名を使用するため、配布時にSandbox専用の
ユニット/実行構成との一致を必ず確認する。実行中の一時503を含め外部監視の運用設計は未完了。

アカウント削除はSandboxの外部削除journalが未準備のため、端末入口では従来通り503。
一般管理画面、データ移行、QA期限延長、通知URL登録、タイマー有効化をこの関数は行わない。

## 検証と次の境界

合成環境で構成照合、無通信/DB無変更の起動、入口分離、署名拒否、認証、期限切れ、
監視障害とプレイヤー読取の分離を確認する。
購入650→有償召喚300→返金→無料召喚→不足300免除→別ワーカーによる返金取消を実行し、
同じDBで有償350/不足0/無料600を確認する。実Apple署名の成功を示す試験ではない。

次の配布工程では専用のテストDB/アカウント、私有管理者入口、Sandbox通知URLとその転送先、
期限、ワーカー名、監視先を確定し、インストーラ/プロキシ構成を別途審査する。
現在の実機セーブや公開サーバーを流用せず、実環境の設定・反映にはユーザー確認が必要。
バックアップ後の独立した取引/免除履歴保全、復旧照合の残件も維持する。

検証結果: 追加12件成功、サーバー全体693件中684成功・9スキップ。
配布アーカイブ展開先から、隔離Pythonでこの構成と依存先を読み込めることも確認。
ログ: `/tmp/nasus-refund-sandbox.KCAbmJ/backend-all.log`（一時ファイル）。
9スキップは明示的なage暗号化試験環境を必要とする既存試験。
