# ②-D 暗号化した削除イベントと隔離復元の通し試験

## 範囲と終了条件

本番DBには接続しない。Macに新規作成した合成DBに2人を登録し、1人の削除前バックアップを取得。
その後の合成購入記録も含む削除イベントを、専用の使い捨てage鍵で暗号化する。
鍵はMacのprivate試験ディレクトリに留め、VPSへ送るのは暗号文・非秘密の転送記録・固定用途のコードのみ。

実さくらの既存privateバケット内deletion-probes/への1件のPUTとGET後、同じ暗号文をMacへ回収。
独立して保持した期待イベント一覧のhashと、取得・復号したイベントの集合/内容が一致する場合だけ復元用情報を生成。
古いバックアップを新規隔離ディレクトリに復元し、削除済みプレイヤー/セーブ/旧認証が復活しないこと、
未削除プレイヤーが変わらないこと、バックアップ後の購入も再利用できないことを検証する。
復元先はRECOVERY-PENDINGのまま。実サービスへの昇格はしない。

## ローカル検証

- 実ageバイナリで暗号化・復号を実行。通信はローカル自動テストでは模擬。
- 改変された暗号文・期待一覧、欠損/追加イベント、重複ID、異なるinstance、未来イベントは拒否。
- 既存試験ディレクトリや成功結果を上書きしない。
- クラウドhelperのテストはprivate ACL、probe以外のキー拒否、PUT失敗時GETしないこと、GET改変拒否を確認。
- backend全306テスト成功。実クラウドの結果ではない。

## 実試験の準備

Mac: `/Users/andou/Library/Application Support/NasusBackups/deletion-drill-20260925-01`

VPS staging: `/home/ubuntu/nasus-deploy/20260925-synthetic-drill-01`

実行入口: `deploy/削除復元の通し試験.command`

VPSのsudo認証が必要。恒久sudoersや認証回避は追加しない。
root所有の/run一時ディレクトリへ3ファイルをコピー後、固定SHA-256照合してから実行。
既存root所有publisherコードのhashとディレクトリ所有権を検証し、既存の設定済みcloud認証を通常利用する。
認証情報を出力・エクスポートしない。クラウドの一覧取得/削除/ACL設定変更や、本番DB・サービスの変更はしない。
PUT時のみ既存方針どおり対象合成オブジェクトをprivateにする。

実行後の証拠:

- `deploy/reports/synthetic-cloud-restore.*`: transferとローカル検証の連続ログ。
- 試験ディレクトリ`cloud-return/receipt.json`, `cloud-return/readback.age`。
- `verified.json`: SYNTHETIC_DELETION_RESTORE_VERIFIED。
- `quarantined-restore/RECOVERY-PENDING.txt`と復元照合記録。

実行は一度限り。失敗時は再実行せず保存された結果を確認する。
合成暗号文・試験鍵・隔離DB・VPS一時出力は検証用に保持する。自動削除しない。

## この試験で証明しないこと

本番の最新履歴一覧の完全性、実Apple失効、実購入、ゲームAPI公開、審査提出の完了は証明しない。
②-Eでは本番復旧時の履歴完全性確認と保存期間を手順・利用者説明に接続する必要がある。
## 実試験結果（2026-09-25 23:23 JST）

ユーザーによるsudo認証後、`deploy/reports/synthetic-cloud-restore.1iLqts`が正常完了。
実クラウドへのPUT/GET成功とMacへの回収を連続ログで確認した。
`verified.json`はSYNTHETIC_DELETION_RESTORE_VERIFIED、RemovedSyntheticPlayers=1。
SurvivorUnchanged/OldCredentialsRejected/RetiredPurchasesPreserved/
RetiredPurchaseReuseRejected/BackupUnchanged/RestoreStillQuarantinedは全てtrue。
ProductionDataUsed=false。復元レポートもRemovedPlayers=1、DELETIONS_APPLIED_OFFLINE_NOT_READY。
以上により②-Dは完了。実アカウント削除や本番公開を確認した意味ではない。
