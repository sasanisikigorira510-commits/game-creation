# 返金試験専用VPS環境の準備記録

ユーザーが既存VPSへの専用Sandbox追加と、ターミナルによる管理者認証を承認。
2026-09-28の管理者読み取り診断は成功。既存公開/QA/Caddyはactive、配置先は未作成、
127.0.0.1の候補ポート8791〜8793に競合なし。
記録: `deploy/reports/refund-host-check.e3NQ5C`。

## 現在地

ローカルの新規環境初期化・期限停止・私有ファイル・パッケージ境界テスト8件成功。
サーバー全体701件中692成功、9スキップ（明示的なage暗号化試験環境が必要な既存試験）。
ログ: `/tmp/nasus-refund-lab-install.0mH79h/backend-all.log`。

コード74ファイルと導入スクリプトだけを、所有者専用の準備領域へ転送しSHA-256一致を確認。
準備領域: `/home/ubuntu/nasus-deploy/20260928-refund-lab-01`。

- `code.tar.gz`: `5a094dda90e920a9324a8e74cf7ff22f14e8ae32b7f9178bedc39cd70e5786ea`
- `install_refund_lab.py`: `d21917f5ed0d46e1b3e9deedb184338dfeeaa149267ff2c9207961b758f7354d`

ユーザーによる管理者認証後、導入の全4段階が成功。
記録: `deploy/reports/refund-lab-install.6QgeoZ`。
結果は `REFUND_LAB_INTERNAL_READY_NOT_PUBLIC`。内部health、経路/認証拒否、
未署名通知拒否、空DB、既存設定ハッシュ不変の検証を通過した。
続いて別SSH接続の読み取りで以下を独立確認した。

- 専用のdevice/review/notificationsサービス3個はactive/running・enabled。
- 8791/8792/8793の全リスナーは127.0.0.1限定。
- maintain/refundsのoneshotは終了成功（Result=success、ExecMainStatus=0）。
  両timerはactive/waiting・enabled。oneshotのinactive/deadは実行後の正常状態。
- 既存公開/QA/Caddyはactive/runningを維持。

公開設定、既存QA設定、既存DB、iPhoneには変更を加えていない。
新しいQAゲートは導入時から24時間のみで、自動延長しない。

## 承認済み導入の範囲

- 新しいユーザー `nasusrefund`。既存 `witchplayer` とは別UID。
- 新しい `/opt/nasus-refund-sandbox-20260928`、`/var/lib/nasus-refund-sandbox-20260928`、
  `/etc/nasus-refund-sandbox-20260928` のみ。既存パスは上書きしない。
- 新しい空のDB・独立した暗号化鍵・管理者トークン・24時間のQAゲート。
  現在のセーブ・購入・Appleトークン・旧暗号化鍵・旧管理者トークンはコピーしない。
- 同じアプリで既に設定済みのApple署名鍵/IAP署名鍵/Appleルート証明書を新しい私有領域へ配置。
  秘密鍵・パスワードの内容はログ/配布物/チャットに出さない。
- 既存のroot管理下にある固定依存lockとwheelから、新しいvenvへオフライン導入。
- device/review/notificationsの3入口をループバック8791/8792/8793だけに配置。
  公開プロキシ・ファイアウォール・DNS・App Store Connectの通知URLは変更しない。
- ローカルバックアップ/アカウント失効と返金再確認の専用ジョブ・タイマーを追加。
  期限切れではバックアップ/Apple処理を実行せず停止する。期限は自動延長しない。
- 内部health成功、入口の認証/経路拒否、未署名通知の拒否、空のplayer/purchaseテーブルを確認。
  本物の購入・召喚・返金・Apple API通信を起動確認の代わりに実行しない。

導入前後に既存の設定ハッシュと公開/QA/Caddyのactive状態を照合する。
導入途中の失敗時は今回作成した新ユニットのみ停止・無効化し、新規ファイルを調査用に残す。
削除・元DBへの差し戻し・既存サービス停止は行わない。再実行は既存候補を上書きせず拒否する。
既存設定不一致、ポート競合、配置済み候補、空きメモリ不足は書き込み前に停止する。

## 未完了の範囲

導入スクリプト全体のLinux/VPS上での成功と内部稼働を確認済み。
公開通知URLへの接続・Apple実通知・実機購入/返金の試験は別工程で未実施。
公開経路の追加とApp Store ConnectのSandbox通知URL変更は、別途承認してから行う。
削除の外部journalが未準備なので、このSandboxではアカウント削除APIは503を維持する。
実サービス用の独立した取引/免除履歴保全・復旧照合・外部監視は未完了。
再確認実行中はhealthが一時503となる設計。通知監視を本番運用へ流用しない。
