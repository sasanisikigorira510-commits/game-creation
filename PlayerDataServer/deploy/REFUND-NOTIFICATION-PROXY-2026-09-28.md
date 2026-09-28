# Sandbox通知URLの追加準備

ユーザーが専用公開受信経路とApp Store ConnectのSandbox通知URLだけの設定を承認。
本番通知URL・既存QA/公開DB・iPhone・購入/返金操作は変更対象外。

## 現在地

関連32テスト成功。受信経路の反映は成功。
レポート: `reports/refund-notification-proxy.I0zG6n`。
外部HTTPSの未署名POSTが400、既存公開/QA/Caddy/通知サービスactiveを独立確認。
反映後Caddyfile SHA-256: `48e529384d7691d9551a504b060f713f3f4e17840ab9e4619f673982c30957f3`。
2026-09-29 JST、App Store Connectのアプリ情報で本番/Sandboxの未設定を確認後、
Sandbox欄だけに下記URLを保存。保存後のURL表示と本番未設定を確認済み。
現在の入力ダイアログにはVersion選択は表示されなかったため、Version選択を行ったとは記録しない。
証跡: `reports/sandbox-notification-url-20260929.png`。
Apple TEST通知の送信/配信結果確認スクリプトを準備（追加4テスト成功）。
実行は管理者認証待ち。TESTの署名・環境・種別・Apple側SUCCESSを揃えて成功判定する。
API/通知tokenや秘密鍵は出力しない。1回の送信後は私有記録を保存し、無断再送を拒否。
まだApple TEST通知の受信成功は未確認。

- 受信予定: `https://api.nasus-games.com/sandbox-refund-20260928/apple/notifications`
- POSTと完全一致パスだけを127.0.0.1:8793の通知専用サービスへ転送。
- 内部パス: `/v1/store/apple/notifications`。
- device/reviewの8791/8792は公開しない。署名検証は既存Sandbox専用設定を使用。
- 現行CaddyfileのSHA-256を固定し、追加以外の変更を拒否。
- root専用`/var/lib/nasus-refund-proxy-20260928`に元の設定と結果を保存。
- caddyユーザーとして候補をvalidate後、原子的置換とreload。再起動はしない。
- 未署名POSTの400、隣接経路/管理経路の503、既存healthとQAゲート拒否を確認。
- 失敗時は他の管理者変更がない場合だけ元の設定を復元・reloadする。
- 専用Sandboxの24時間期限は延長しない。期限後は通知を成功扱いしない。

導入スクリプトSHA-256:
`01cd6429d09fbda3c76c8cae68f95f332b02e08942f77415621e7afa27984c71`

## 反映後の工程

1. 端末レポートと外部HTTPS応答を独立確認。
2. 対象アプリ6804616218の本番/Sandbox通知欄を確認し、SandboxのみVersion 2で設定。
   既存Sandbox URLがあれば置換の影響を確認し、必要ならユーザーへ相談。
3. 既存の課金検証キーを通常の設定経由で使用し、SandboxのRequest a Test Notificationを実行。
   応答tokenは私有ファイルに保存し、Get Test Notification Statusで結果を確認する。
   購入・返金は実行しない。TESTの200は経済台帳の返金反映試験ではない。
4. Apple側の保存結果はスクリーンショットで提示。まだ通知受信成功と断定しない。

公式手順:
https://developer.apple.com/help/app-store-connect/configure-in-app-purchase-settings/enter-server-urls-for-app-store-server-notifications
