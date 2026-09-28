# Apple構成: 独立保管と監視への接続

## 2026-09-26の現在地

本番のApple定期処理・統合監視は16:55の再開処理で有効化し、自動実行を確認済み。
APIのApple対応入口への切替・ゲーム公開・実Appleアカウントでの試験はまだ行っていない。
既存APIはactive/success、外部 `/healthz` は200、`/` は503。
監視のdrop-inは40-reason-logging、60-integrated-health、70-backup-retention、80-apple-worker。

最終反映結果: `reports/apple-activation-resume.5lgWub`。
Apple timer enabled/active、service success/Exit0、後続の自動health更新と外部200を確認。
workerは `/opt/nasus-apple-worker-20260926`、統合監視は
`/usr/local/lib/nasus-apple-health-20260926-v2`。v1配置物は変更せず保存した。
実Apple通信ではなく、空queueの自動処理と監視を確認した段階である。

## 鍵の復旧

`backup_apple_keys.py` は既定の署名鍵と32-byte token暗号鍵の2ファイルだけを読み、
既存Nasusバックアップrecipient宛にage暗号化する。recipientと既存identityの対応を
確認し、保存前・保存後にメモリ内で復号して元の内容と完全一致することを検証する。
平文アーカイブ・秘密情報のログ・鍵の上書き・クラウドへの転送は行わない。

実行済みの暗号化ファイルの保存フォルダ:

`/Users/andou/Library/Application Support/NasusBackups/apple-keys-4ap0esb1/recovery`

Ciphertext SHA256:
`17de7cf9f3304515c28481c2b28e9dca9e3b05530e2503a9b27ce95d798ac358`

これはMac内での復号検証まで完了したもの。独立保管の完了ではない。
以前のUSB（NO NAME、UUID `37B86F31-7DB2-3D0A-9C18-568993DDB539`）は現在未接続。
再接続時はUUIDを照合し、既存ファイルを変更せず新しい専用フォルダへ暗号文・説明・
checksum・検証receiptをコピーする。USBから読み戻し、既存USB identityで復号比較して
初めて独立保管完了と記録する。Mac側のreceiptは独立保管未検証のまま改ざんしない。

2026-09-26追記: 上記UUIDのUSB再接続を確認。新規フォルダ
`Nasus-Apple-Recovery-2026-09-26` に4ファイルを追加し、sync後に読み戻して
Mac側との全バイト一致を検証。既存USB identityを使ったメモリ内復号で、
署名鍵とtoken暗号鍵の2点が元の鍵と一致した。独立保管・復号検証は完了。
既存USBファイルは変更なし。`USB-VERIFIED.txt` とMac側の別receipt
`NasusBackups/apple-keys-4ap0esb1/usb-verified.json` に結果を記録。

USBには既存の平文age identityもある。同じUSB上の暗号化ファイルは、そのUSBを
入手した者に対して秘密を守れない。従来どおりオフラインで物理的に厳重管理する。
この2鍵だけで全サーバーを復元できるわけではなく、DB、削除履歴の完全性確認、
対応したコード・設定も必要。復旧隔離を自動解除したり本番DBを上書きしない。

## ローカル実装済み、未反映の監視

`apple_integrated_health.py` は既存retention collectorを呼び、バックアップ、
削除worker、30日保存期限のいずれかが異常ならその異常を保持する。
既存チェックをすべて通過した場合だけAppleの私有markerとsystemd状態を追加検査する。
鍵・DB・token・providerレスポンスを監視へ読み出さない。

正常な定期実行の途中で誤警報が出ることを避けるため、runtimeのrunning markerには
直前の「完了した正常実行」の時刻だけを引き継ぐ。running/失敗/中断からは引き継がない。
監視はtimer有効、serviceの実際の起動状態、同一実行のmonotonic時刻、90秒未満の
実行時間、180秒以内の完了実績をすべて要求する。終了失敗・timer停止・期限切れは異常。
running marker自体を成功として書き換えるわけではない。

このruntime変更も現在はMacだけ。本番 `/opt/nasus-apple-20260926` の変更は未実施。
既存stagingインストーラーは再実行不可。反映時には独立保管完了を確認したうえで、
配置済みコードのhash照合、限定された更新・unit追加、既存collectorのhash固定コピー、
初回正常実行、監視追加、自動timer実行、外部health正常・公開ルート閉鎖を検証する。
失敗時は新timerを停止して既存監視へ戻し、鍵やDBを巻き戻さない。

## 確認済み

実ageによる合成鍵の往復・改ざん拒否、宛先違い・権限・symlink・上書き拒否、
秘密を含まないエラー、監視の既存異常維持、停止/実行中/タイムアウト/古いmarkerを
含む追加15テストと既存回帰テスト、全473件成功（skipなし）。
本番のApple通信・定期実行・監視統合・iPhone連携成功を意味しない。

## 認証1回の本番反映パッケージ（準備完了・未実行）

実行入口: `run-apple-activation-mac.sh`。USB復号検証receipt、installer、
manifestのhashを固定。VPSへはコードとunitだけを転送済みで、Linux上の
hash照合・Python構文・監視moduleのimport確認まで成功した。

この反映では既存 `/opt/nasus-apple-20260926` を上書きしない。
更新版workerは `/opt/nasus-apple-worker-20260926` に新設し、検証済みの既存venvを使う。
監視は `/usr/local/lib/nasus-apple-health-20260926` に分離し、現行retention/baseの
hash一致を確認してコピーする。追加drop-inは `80-apple-worker.conf` のみ。

開始前に既存worker未登録、既存監視の構成一致、既存Apple runtime、空の6テーブル、
外部health正常・公開root閉鎖を確認する。既存の鍵・schema・API入口を変更しない。
初回の空queue実行を確認し、timer有効化後は手動起動せず自動workerと自動healthの
更新を最大180秒待つ。完了時に外部応答・空table・保護した設定hashを再検査する。
失敗時は新timer停止と既存監視への復帰を試み、停止失敗があっても監視復帰を試す。
処理結果・配置物は残し、失敗したinstallerを自動再実行しない。

追加8件を含む全481テスト成功（skipなし）。Linux上での実service動作は認証後の
反映処理内で検証するため、現時点では未確認。本番反映・定期処理有効化はまだ未実施。
