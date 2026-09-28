# ゲームアカウント削除：サーバー側の開発実装

2026-09-25。本書はサーバー実装段階の記録。後続で[アプリ内確認・端末消去](ACCOUNT-DELETION-CLIENT-2026-09-25.md)を追加した。
**本番無効。保持方針・バックアップ復元時の削除適用などが残り、公開できる完成状態ではない。**
テスト用一時DBのみ削除した。VPS、実アカウント、実機セーブ、Apple設定、鍵には変更なし。

## 変更したファイル

- `account_deletion.py`（追加）: 認証付きpreview/commit/status、原子的なライブデータ削除。
- `store.py`: 削除済みPlayerIdの再登録拒否、過去に付与した購入の再付与防止。
- `application.py`: 明示的な依存注入でのみ有効になる3つのPOSTルート。
- `account_linking.py`: 期限切れでも未解決の交換challengeを保持し、削除時の失効対象を追跡。
- `tests/test_account_deletion.py`（追加）: 32件の削除テスト。
- `tests/test_production.py`: production factoryでは削除APIが503であることを追加確認。
- `build_release.py`と関連説明: 新スクリプト・本書を明示許可リストに追加。
- Unityコード・画像・プレハブの追加/変更はない。

## API（開発用のみ）

全ルートで現在のゲームのBearer資格情報を使用する。Appleへの新規サインインは要求しない。
`Application(..., account_deletion=AccountDeletion(store, account_linking=accounts))`を
開発fixtureなどから明示注入する。Appleを構成しないゲスト専用DBではaccount_linkingを省略できる。
Appleテーブルがあるのに失効vault依存を省略した構成は拒否する。
環境変数・クライアントフラグで本番を有効にする経路はない。

1. `POST /v1/account-deletion/preview`, `{ "PlayerId": "..." }`
   - `Status=confirm_delete`, `ConfirmationToken`, `ExpiresIn=300`。
   - PlayerId、Epoch、EconomyRevision、Free、Paidを返す。セーブ本体は返さない。
   - この呼び出しだけでは削除しない。凍結/移行待ち/未同期のゲストも確認可能。
2. アプリ側で不可逆な削除対象を表示し、ユーザーが明示確認する必要がある（UIは次の作業）。
3. `POST /v1/account-deletion/commit`, `{ "PlayerId": "...", "ConfirmationToken": "..." }`
   - 現在の認証、確認期限、アカウント/保存/財布/Appleリンクの変更有無を再照合。
   - 同一トランザクションでApple失効予約、リンク・保存・台帳削除、再送防止記録を確定。
   - `Status=deleted`, `PlayerId`, `RevocationPending`, `ManualRevocationRequired`。
   - これは**ライブDBの論理削除結果**。端末/バックアップの物理消去完了を意味しない。
4. `POST /v1/account-deletion/status`, commitと同じbody/資格情報。
   - 読み取り専用。応答欠落後に再削除せず結果を照会できる。
   - 削除前の現認証なら`not_deleted`。成功したcommitの正しい組合せなら`deleted`。
   - commitとstatusの成功結果再取得は24時間。異なる秘密情報・確認tokenでは取得不可。
   - 24時間後の401を「未削除」と見なして新規登録/保存を再開してはいけない。
   - 返却する失効フラグは確定時点の記録であり、現在のAppleキュー状態ではない。

## 削除・保持の範囲

削除するライブ行:
`players`, `snapshots`, `operations`, `claims`, `purchases`, `flags`, `audit`の当該プレイヤー分。
Appleリンク、関連challenge（復旧preview内のセーブを含む）、unlink確認も取り除く。
別プレイヤーのセーブ・財布には触れない。

少量の再送防止記録を別に残す:

- `deleted_players`: ドメイン分離したPlayerIdのSHA-256、削除日時。
  同じPlayerIdは古い資格情報でも新しい資格情報でも410となる。
- `retired_purchases`: ストア名と取引IDを組み合わせたSHA-256のみ。
  生の取引ID・商品ID・PlayerId・残高は残さず、他アカウントでの再付与も拒否する。
- `deletion_receipts`: PlayerId/資格情報/確認tokenの組合せのSHA-256と期限、最小結果。
  生の秘密情報やPlayerIdは残さない。期限切れ行は次のpreview時に削除する。
  現時点では専用の定期削除ジョブはない。
- Apple失効用の暗号化tokenと交換/世代記録は、失効・未解決処理のため別途残る。
  `requested/uncertain/stored`の未完了challengeは期限切れで利用不能になっても、
  通常のchallenge掃除では消さない。削除時に拾い、遅れて届くtokenも失効待ちにする。

**ハッシュ化は匿名化や法的保持根拠を保証しない。** 保持目的/期限、消去方針とユーザー向け説明は
運用前に確定する。SQLiteの空きページ/WAL、既存の暗号化バックアップまで即時消去する実装ではない。
削除防止記録も現DB内にあるため、過去DB全体の復元だけで削除が巻き戻る問題は未解決。
別管理の削除記録と、復元後の再削除・購入再付与防止の適用が公開前に必須。

## Appleとの関係

- Apple連携解除とゲーム削除は別処理。unlinkはゲームを残すが、このcommitはゲームを消す。
- tokenが欠けてもライブゲーム削除は完了し、手動失効の案内が必要な結果を返す。
- Apple通信は削除トランザクション内で行わない。障害中も削除を確定し、失効キューは保持。
- 昔のunlink確認が残っていても、そのApple IDが既に別ゲームアカウントへ連携済みなら、
  古いゲームの削除を理由に新しいアカウントの連携/認証情報を失効させない。
- 実Apple失効の定期ワーカー・滞留対応・OS/サーバー署名通知は未接続。

## 確認結果

- Python全172件成功、失敗0、スキップ0（削除テスト32件を追加）。`pip check`成功。
- Unityの既存連携/復旧/購入/召喚/保存/UI関連95件成功、失敗0、スキップ0。
  `/tmp/nasus-account-deletion-backend-20260925.xml`。
  Unityの削除フローを試験したという意味ではない（クライアントは未実装）。
- 確認無し・誤認証・別アカウント・期限切れ・確認後の財布/保存/連携変更を拒否。
- 連打/並行確定、応答欠落/再起動、DB失敗時の全巻き戻しを確認。
- 削除後の古い端末の登録/保存/購入応答、同じ購入の別アカウントへの再付与を拒否。
- 認証中/期限切れ/結果不明の削除、失効キューとの原子性、別リンク所有者の保全を確認。

## 次に必要な作業

1. アプリ内の明示確認、送信前の永続削除ジャーナル、通信断/再起動後の結果照会。
2. 同じPlayerIdの端末コピー・保存履歴・資格情報・保留処理の安全な消去。
   引き継ぎ時に退避した別ゲストのデータを巻き込まないこと。削除中/結果不明時は保存・購入を止める。
3. バックアップ保持期間、復元時の削除適用、再送防止記録の保持方針、運営解消手順。
4. 実Apple認証/失効、iPhoneの中断/容量不足/再起動、Sandbox購入の回帰確認。
5. 上記完了後に公開可否を判断。本書追加だけで本番接続やリリース承認とはしない。

参考: [Appleのアカウント削除案内](https://developer.apple.com/support/offering-account-deletion-in-your-app)、
[TN3194](https://developer.apple.com/documentation/technotes/tn3194-handling-account-deletions-and-revoking-tokens-for-sign-in-with-apple)。
