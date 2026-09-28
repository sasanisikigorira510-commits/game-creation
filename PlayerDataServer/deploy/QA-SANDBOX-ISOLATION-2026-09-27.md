# Apple実機QAのDB分離

## 目的と現在地

旧セーブの移行待ちによりApple認証が始まらない。有償石9600は、ユーザーが
Sandboxテスト購入（実金銭なし）と申告。購入署名を検証した事実とは区別する。
00:54のユーザー実行で専用DBへの反映完了。本番用DBの移行や実Apple認証成功ではない。

## 固定対象と変更範囲

- 対象は端末退避のPlayerId SHA-256で固定した単一アカウント。秘密tokenはMacから取り出さない。
- 元DBは読み取り専用。ユーザーが1人、移行待ち、revision/epoch=0、凍結なし、
  最新snapshotの無料1200/有償9600一致、オンライン取引・購入台帳・Apple連携・削除履歴なしを要求。
- 一致しない場合は停止。移行待ちの単純解除・残高の推測・初期化はしない。
- 別ディレクトリ `/var/lib/nasus-qa-sandbox-20260927/data` へWAL整合コピーし、
  専用instanceへ変更。既存の監査付きmigrationをコピー側だけに適用する。
- `SANDBOX-ONLY.json`に検証専用・本番移行未承認・購入検証未実施を記録。
- QAのみ専用コード/設定へ切替え、systemdのInaccessiblePathsで本番DBを不可視にする。
- 元の本番API・DB・Caddy・鍵・定期処理・24時間のQA期限は変更しない。
- 失敗時はQA停止を維持する。移行後の端末を本番DBへ自動で戻さない。ファイルは保持し再実行しない。

## 検証用の運用境界

- Apple signing/encryption keyは既存の当該アプリ用設定を通常どおり使用。
  Appleリンク/失効queueは専用DB、worker/stateも別。実ユーザーのApple連携情報はコピーしない。
- 専用ローカルDBバックアップと失効workerを毎分程度実行し、個別healthへ反映。
  QAバックアップはローカルのみ。既存本番の暗号化offsiteバックアップとは区別する。
- 本番のoperator credentialは使用しない。管理APIはQA gateでも遮断。
- 課金検証器は引き続き無効。新規Sandbox購入のサーバー検証は別工程。
- アカウント削除APIは専用offsite削除履歴を準備するまで503で遮断。
  連携・復旧・解除の検証を先に行う。削除試験済みとは扱わない。
- 新規timerは起動時有効化しない。VPS再起動後にQAの自動再開はしない。
- テスト用残高/進行を本番リリース用データへ自動昇格させない。

## 認証と検証

`run-qa-isolation-mac.sh`はVPS管理者認証1回を必要とする固定hash wrapper。
パスワード・秘密鍵・プレイヤーtokenをログへ出さない。レポートは`reports/qa-isolation.*`。
成功条件は`QA_SANDBOX_ISOLATION_VERIFIED`。これだけでは実Apple認証成功ではない。

ローカル関連41テスト成功。元DB不変、進行snapshot保持、cloneだけrevision1、
異なる残高/対象/複数アカウント/Apple履歴/凍結の拒否、cloneのApple challenge、
QAバックアップとworkerの失敗検知、既存QA境界とAppleアプリ構成を確認。

## 反映確認（2026-09-27 00:54）

`reports/qa-isolation.vsNuIm`で`QA_SANDBOX_ISOLATION_VERIFIED`を確認。
Free=1200、Paid=9600、ProductionMigrationApproved=false、GateExpiryUnchanged=true。
SSHによる再確認でQA service active/running、専用WorkingDirectoryと本番パスへの
InaccessiblePathsを確認。専用maintenanceは00:54:49の後続実行もsuccess/Exit0、timer active。
既存API/Apple revoke timer active。外部health200、通常root503、キーなしQA404。
実Apple認証・復旧・解除は端末操作待ち。削除APIはこのQAでは遮断を維持。
