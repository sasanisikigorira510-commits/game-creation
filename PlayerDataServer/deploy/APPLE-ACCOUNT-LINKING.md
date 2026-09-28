# Apple連携・復旧（実装途中、公開禁止）

2026-09-25。ユーザーの承認方針: 普段はゲストプレイ、設定から任意のApple連携、
再インストール・機種変更時に同じAppleアカウントで復旧する。別のゲームデータとの合算はしない。

## この段階で実装したこと

- `apple_identity.py`: Apple固定JWKSによるRS256署名検証。issuer、単一のaudience、
  exp、iat、subject、サーバー発行nonceを検証。メールアドレスでは紐付けない。
  クライアント指定の鍵URL・署名方式は使わない。鍵取得タイムアウト5秒。
- `account_linking.py`: 連携、復旧プレビュー、明示確認後の引き継ぎ確定。
  SQLiteトランザクションで紐付けの一意性、認証情報更新、世代更新を保証。
- `application.py`: 連携/復旧3つ・解除2つのPOST API。`account_linking` 未注入は503。
  IPベースで30リクエスト/5分。本文、Apple subject、JWT、認証情報をログに出さない。
- `store.py`: HTTP経由のhead/snapshot/operationはDBトランザクション内でも認証する。
  特にApple購入検証中に引き継ぎが成立しても、旧端末の購入処理を確定させない。
- 確定済み抽選・購入の末尾差分を復旧データに含め、台帳や残高自体を変更しない。

**`production.create_app` はこの機能を構築しない。環境変数やクライアント要求で
有効化する入口もまだない。公開中のVPS・DB・Caddy・Apple Developer設定は未変更。**
ゲーム内ボタン、iOSネイティブ認証、端末上の引き継ぎ保存処理は開発用として追加済み。
Unity/ローカルHTTP検証済み、Appleの実認証・実機UI・署名付きXcodeビルドは未確認。
このファイルをリリース完了記録として扱わないこと。

## API契約（開発テスト用）

全APIはHTTPS、JSON、`Authorization: Bearer <credential>`。
linkでは現在のインストール認証情報を使う。recoverでは新しいランダム32バイトの
小文字hex認証情報を端末で生成・永続化してから使う。Apple JWTをBearerに使わない。

1. `POST /v1/apple/challenge`
   - 連携: `{ "Mode": "link", "PlayerId": "既存PlayerId" }`
   - 復旧: `{ "Mode": "recover" }`。復旧先のPlayerIdをクライアントに選ばせない。
   - 応答: `ChallengeId`, `Nonce`, `ExpiresIn`（300秒）。
2. iOSのASAuthorizationAppleIDRequestに`nonce=Nonce`を設定。
   `state=ChallengeId`を設定し、ネイティブの応答時にstateの一致を確認すること。
   nonceのハッシュ化をクライアントだけに追加しない。名前・メールの要求は不要。
3. `POST /v1/apple/verify`
   - `{ "ChallengeId": "...", "IdentityToken": "Appleの署名済みJWT", "AuthorizationCode": "単回コード" }`
   - token clientと暗号化vaultを対で注入すると、コード交換とrefresh token保管が必須。
     保存済み結果の再取得は`{ "ChallengeId": "..." }`だけを送る。コードの再送をしない。
   - link: `Status=linked`, `PlayerId`。既存クラウド保存がない場合は失敗。
   - recover: `Status=preview`, `PlayerId`, `Epoch`, `EconomyRevision`, `Free`, `Paid`,
     `Snapshot`, `Operations`, `PreviewToken`。この段階では旧端末は無効化されない。
4. 復旧のみ: ユーザーに置き換え対象と進行状況を表示。自動合算しないことを説明。
   `OnlineGrantApplier.Stage`でSnapshotにOperationsを順番に適用し、最終カーソル・
   財布が応答と完全一致することを検証。ガチャ再抽選や購入再送をしない。
5. 確認されたら`POST /v1/apple/commit`:
   `{ "ChallengeId": "...", "PreviewToken": "..." }`。
   プレビュー後に保存・経済台帳・世代が変わっていれば409でやり直し。
   成功時は`Status=committed`。全体を一度だけ確定し、同じ資格情報で24時間再取得可能。
   さらに別端末へ移行済みの場合、昔のcommit応答再取得でも旧認証を復活させない。

### Apple連携解除（ゲームアカウント削除とは別）

1. `POST /v1/apple/unlink/preview`: `{ "PlayerId": "現在のPlayerId" }`、現在のBearer必須。
   `Status=confirm_unlink`, `ConfirmationToken`, `ExpiresIn=300`、未連携は`Status=not_linked`。
   この要求だけでは解除しない。
2. ゲームデータ・石・購入履歴が残るがAppleでの復旧ができなくなることを確認表示。
3. `POST /v1/apple/unlink/commit`: `{ "PlayerId": "...", "ConfirmationToken": "..." }`。
   `Status=unlinked`, `RevocationPending`, `ManualRevocationRequired`を返す。
   リンクの除去・既存復旧challenge無効化・失効queueへの追加をアトミックに確定。
   token欠落/結果不明でもApple側の失効成功とは表示しない。実Apple失効ワーカーは未稼働。
   同じconfirmationは24時間再取得できるが、資格情報の変化・再連携後は拒否する。

復旧差分が1000件超、台帳に欠落、財布不一致、凍結、旧データ移行待ち、
現在世代のスナップショット未作成は安全側で停止する。運営確認を案内し、空のデータで代替しない。
クラウドスナップショットの非経済情報は依然`client_unverified`であり、これを
サーバー検証済みゲーム進行と呼ばないこと。

## 次に実装・検証する必須項目

### Unity/iOS（2026-09-25 追加実装）

- 設定画面と復旧停止画面のApple連携・データ復旧。任意連携。
  現在のアプリはBootSceneからHomeSceneへ直行するため、存在しないタイトル画面には追加しない。
- AuthenticationServicesブリッジ、システムのAppleサインインUI、nonce/state検証、
  キャンセル・連打・180秒タイムアウト。JWTをログ/ファイルに保存しない。
  実際のOS認証UIとバックグラウンド復帰は実機未確認。
- **別ディレクトリに新しいセーブ世代と認証情報を先に保存し、最後にactive-account
  ポインターをアトミックに切り替える**。現在のsaveとidentityを別々に上書きしない。
  旧ローカルデータは退避。新認証情報・challenge・preview・確定状態のジャーナルで、
  commit直後に通信/電源が落ちた場合も再試行できること。復旧途中の通常保存/購入を禁止。
- SaveManagerの起動時アカウント選択と破損時の安全停止を追加。
  開発用の連携解除確認と結果再確認を追加。続いて[端末のゲーム削除](ACCOUNT-DELETION-CLIENT-2026-09-25.md)も開発追加。
  OS側の失効通知は未実装。
  サーバー側の削除APIは別途開発追加済み。本番は無効。詳細は
  [削除検証記録](ACCOUNT-DELETION-2026-09-25.md)を参照。
- 保存不整合、確認前確定の拒否、再起動相当の再読込、台帳末尾再生をUnityで検証。
  OSの実際の容量不足・電源断・物理端末間の同時操作は未確認。

### 開発機能のゲートと保存仕様

- `Assets/Resources/PlayerDataService.json` の `ExperimentalAppleLinking` がtrue、
  かつEditorまたはDevelopment Buildの場合のみ表示する。現在の設定は未変更でfalse相当。
- 公開ビルドにtrueの設定が混入すると`AppleAccountBuildGate`がビルドを拒否。
- iOS向けはAuthenticationServicesをリンク。検証有効時のみエクスポート先のentitlementsへ
  Sign in with Appleを追加する。Developerポータルや配布用署名は自動変更しない。
- 元のsave.json/履歴/identityは残し、復旧先を`linked-accounts/<random-slot>/`に保存。
  `active-account.json`の置換で切り替える。ポインター不正時に新規ゲストへ戻さない。
- `apple-recovery.json`はstarted → preview → requested → committedの順で永続化。
  requested以降は結果不明でも記録を捨てず、同じcommitを再確認する。
  committedの再起動は保存を再検証して切り替えを完了する。
- 確定済み結果の再取得期限24時間を超えた場合や、確定後に別端末へ移行した場合は
  自動復旧を止めてサポートを案内する。無条件キャンセルや旧認証への戻しはしない。
- コピー途中のフォルダーは残す。認証情報は既存インストール認証と同様にローカルファイルで保存。
  Apple JWT・Appleパスワード・署名秘密鍵は保存しない。
- 開発画面は既存の設定ボタンスタイルと標準GUIを使用。新規の画像素材は追加していない。
- Unity統合テストの認証代替はEditor限定。Pythonの合成認証fixtureはtests配下のみで、
  配布パッケージに含めない。実Apple認証の代替として本番で有効化しない。

### Apple側のリリース前提（未実施）

- トークン交換・暗号化保管・失効キューを開発用連携API/Unityへ接続。
  **本番には未接続**。競合対策・鍵管理・削除方針と残作業は
  [APPLE-TOKEN-LIFECYCLE.md](APPLE-TOKEN-LIFECYCLE.md)を参照。削除機能の完成とは扱わない。
- 対象の正確なBundle IDでSign in with Apple Capability、entitlements、署名を設定。
- 認証コード交換/refresh token保管/解除の実Apple試験、失効ワーカーの運用、
  アプリ内アカウント削除、Appleの資格情報失効/通知対応を実装・試験。
  Apple IDのパスワードやサーバー署名秘密鍵をゲームへ組み込まない。
- 実機で通常連携、別Appleアカウントの衝突、再インストール、機種変更、
  Apple連携解除/削除、Sandbox購入後復旧を確認してからproduction factoryへ接続。
- Apple Developerの操作や秘密鍵発行は別途ユーザー確認。秘密鍵をチャットに貼らせない。

## 検証

`requirements-identity.txt`は任意の追加依存。テスト環境に導入済み。
テストは一時SQLiteと合成RSA鍵のみで、実Appleログイン・実購入は行わない。

```sh
.venv-production/bin/python -m unittest discover -s tests -p 'test_account_linking.py' -v
.venv-production/bin/python -m unittest discover -s tests -p 'test_apple_identity.py' -v
```

参考: [Appleの本人確認](https://developer.apple.com/documentation/signinwithapple/verifying-a-user)、
[認証セッション](https://developer.apple.com/documentation/signinwithapple/authenticating-users-with-sign-in-with-apple)、
[公開鍵](https://appleid.apple.com/auth/keys)、
[PyJWT API](https://pyjwt.readthedocs.io/en/latest/api.html)。
