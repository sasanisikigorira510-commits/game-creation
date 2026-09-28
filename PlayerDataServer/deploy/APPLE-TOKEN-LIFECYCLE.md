# Apple認証トークンの管理 — 開発用連携・解除に接続、本番は無効

2026-09-25。**連携解除・サーバー削除・端末消去を開発実装。保持方針や実機確認などが未完成で、公開不可。**
最新のクライアント実装と検証は[端末削除記録](ACCOUNT-DELETION-CLIENT-2026-09-25.md)を参照。
削除の現状は[サーバー側削除の検証記録](ACCOUNT-DELETION-2026-09-25.md)を参照。
実Appleサービス、Apple Developer、VPS、実アカウント、実セーブは変更していない。
本番の鍵生成・取得・設定はしていない。テストで生成する鍵は使い捨ての合成鍵のみ。

## 追加した部品

- `apple_tokens.py`: iOSの単回authorization codeを固定のApple `/auth/token`へ送信。
  native identity tokenと交換応答のidentity tokenを両方署名検証し、subject/nonceを一致確認。
  `client_secret`は明示的に渡されたP-256鍵でES256署名し、有効期間5分。
  native用のBundle IDを使用し、webのredirect URIは送らない。
- `AppleTokenClient.revoke`: `/auth/revoke`へrefresh tokenをPOST。200だけを成功と扱う。
  Appleは既に失効したtokenにも200を返すので、結果不明時の失効再試行が可能。
- HTTPS固定、リダイレクト禁止、接続/読込タイムアウト10秒、応答最大64KiB。
  認証コードは使い捨てなので交換を自動再送しない。エラー本文や秘密情報を記録しない。
- `apple_grants.py`: 明示的に渡す32バイトの別鍵でrefresh tokenをAES-256-GCM暗号化。
  毎回別のnonce、行ID・subjectハッシュ・client IDを追加認証データにする。
  DB/WALに平文token・メール・Appleの生subjectを保存しない。
  古い認証情報を上書きせず保持し、解除時にまとめて失効待ちへ変更する。
- 永続失効キュー: 呼び出し元のDBトランザクション内で`queue`し、通信はロック外。
  120秒の処理リース、失敗時30秒〜最大1時間の再試行間隔、再起動後の再開、
  古いワーカーが新しい処理を消さないリース照合。失敗時は暗号文を保持する。
  成功時は該当行を削除。履歴バックアップ内のデータが同時に消えるという意味ではない。

## 現在の接続状況

`AccountLinking(..., tokens=..., vault=...)`への明示注入により連携/復旧へ接続。
署名検証だけの旧テストモードも残るが、これは本番構成として使用しない。
Unityはnative authorizationCodeを一時的に渡し、送信後に参照を解放する。端末に保存しない。
production factory・実際の鍵・本番自動ワーカーは**未接続**。
明示設定CLI・unitテンプレート・監視判定はローカル実装済み（詳細は下記）。
追加の[公開前API接続factory](APPLE-APPLICATION-ASSEMBLY.md)もローカル検証済み。
通常の本番factoryからは呼ばれず、Apple連携と削除を勝手に有効化しない。
環境変数で暗黙有効化せず、設定が不足すれば停止する。
2026-09-26に[バッチ実行・滞留検出](APPLE-REVOCATION-WORKER.md)をローカル実装。
既存queueの再開・失敗時保全・件数だけの観測までであり、本番scheduler/鍵配置は未実施。
VPS配布、実Apple認証/失効、iPhoneインストールは行っていない。

## 接続した安全処理

- `apple_exchanges`: challengeに結び付くsubjectハッシュ・失効世代・状態・grant IDを保存。
  JWT/codeは記録しない。外部通信前にrequestedを確定し、grant暗号化保存を先に確定、
  最後にリンク/復旧プレビューを確定する。保存後に応答が失われても再交換せず再開できる。
- 外部処理中は同じsubjectの別challengeも止める。失敗が不明ならuncertainにして保持。
  プロセス停止でrequestedのままでも再送しない。期限経過で自動解除もしない。
  Appleが明示的にinvalid_grantを返した場合のみfailedとし、新規認証を許可する。
  **requested/uncertainの運営確認・解消手順は未実装。公開前の必須ゲート。**
- challenge失効や端末認証の切り替えが通信中に起きても、返ってきたgrantは暗号化保存し、
  連携成功とは扱わない。処理中に解除された場合は失効世代で検出し、遅れて届くgrantも
  失効キューへ入れる。保存前にクラッシュした場合は結果不明として止まり、成功と推測しない。
- `apple_grant_generations`: 失効開始ごとに世代を増やす。キューが一時的に空でも、古い
  認証応答を新しい連携として受け入れない。pending/leasedが残るsubjectは連携を停止。
- 解除preview→確認token→commitを分離。解除・失効queue・復旧challenge無効化を同一
  トランザクションにする。セーブ/財布/購入台帳/ゲスト認証は変更しない。
  二重confirmは24時間同じ結果を返す。再連携後に古いconfirmを押しても解除し直さない。
- Unity「認証結果を再確認」は保存したchallengeと復旧資格情報だけを送信し、
  code/JWTを再送しない。解除確認にはデータが残ることと復旧できなくなることを明記。

## 公開前の必須作業

1. ゲームアカウントの削除導線と対象/失われるデータの明示確認。
   Apple通信障害やtoken欠落だけを理由にゲームアカウントの削除を拒まない。
   クラウド/端末/履歴バックアップと購入再付与防止台帳の扱い・保持期限を決定する。
2. Apple署名付き通知、OSの資格情報失効、定期的な資格情報検証を実装。
   クライアントからの未署名の「失効しました」を根拠にサーバーのデータを削除しない。
3. Sign in with Apple専用の署名鍵と暗号鍵の安全な配置・別保管・復元試験・ローテーション。
   暗号鍵紛失や間違いでは復号失敗として停止し、鍵の自動再生成や平文保存に逃げない。
   署名鍵を課金検証キーと混同せず、どちらの秘密鍵もアプリへ含めない。
4. キューの失敗/滞留監視と保存期限、ワーカーの定期実行、バックアップからの復旧設計。
   無期限の失敗リトライを公開時の削除完了ポリシーにしない。
   requested/uncertain交換の解消手順、未使用grant/交換記録/世代記録の保持期限も必要。
5. ユーザー確認の上でApple側設定、実機の認証/失効/削除/復旧とSandbox購入の回帰試験。

## ローカル検証

初期部品114件から、交換/連携/復旧/解除の統合テストを追加。
連携/解除は[統合テスト記録](APPLE-ACCOUNT-INTEGRATION-2026-09-25.md)、
続く削除32件追加後の全172件成功は[削除検証記録](ACCOUNT-DELETION-2026-09-25.md)を参照。
実機試験は未実施。

テスト用RSA/EC/AES鍵、一時SQLite、偽HTTP応答だけを使用。
トークン交換・署名/nonce/subject・改ざん・通信切断・秘密情報非表示、
暗号文移し替え拒否、別アプリ/別ユーザー隔離、同時ワーカー、リース失効、
トランザクション巻き戻し、失効リトライを確認する。

```sh
.venv-production/bin/python -m unittest discover -s tests -p 'test_apple_tokens.py' -v
.venv-production/bin/python -m unittest discover -s tests -p 'test_apple_grants.py' -v
```

参考: [Apple Token validation](https://developer.apple.com/documentation/signinwithapplerestapi/generate-and-validate-tokens)、
[Token revocation](https://developer.apple.com/documentation/signinwithapplerestapi/revoke-tokens)、
[TN3194: Account deletion and token revocation](https://developer.apple.com/documentation/technotes/tn3194-handling-account-deletions-and-revoking-tokens-for-sign-in-with-apple)。
