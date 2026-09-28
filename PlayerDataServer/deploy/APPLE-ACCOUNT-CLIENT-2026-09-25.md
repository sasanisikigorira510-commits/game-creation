# Apple連携のUnity/iOS側 — 開発用実装

## 実装

- `AppleNativeIdentity.cs` / `Plugins/iOS/NasusAppleIdentity.mm`: AuthenticationServices呼出し、
  nonce/state、キャンセル、180秒期限、古い認証応答の無視。秘密情報のログ出力なし。
- `OnlinePlayerData.Apple.cs`: 連携→本人確認、復旧検索→内容表示→明示確認→確定。
  通信断時は保存した同じ引き継ぎを再確認。Appleのidentity tokenは保存しない。
- `AppleRecoveryStorage.cs`: セーブと新認証情報を別スロットへ先行保存、サーバー確定後に
  active-accountポインターを置換。旧セーブは消さずに保全。
- `SaveManager.cs`: 起動時の確定済み復旧の完了、未確定復旧中の保存禁止、
  アカウントポインター/セーブ不整合時の安全停止。
- `HomeSceneController.cs`: 設定画面の開発用「Apple連携・データ復旧」。
  設定パネルを縦に拡張し、既存ボタン同士との重なりをテスト。
  実アプリは起動後ホームへ直行する構成。独立したタイトル画面の変更はなし。
- `AppleAccountBuildGate.cs`: 公開ビルドの検証フラグ混入を拒否、iOSのフレームワークと
  検証用entitlementsをエクスポート時に設定する処理。

## 確認結果

- Unity 6000.3.11f1: **95件成功、失敗0、スキップ0**。
  新規19件（保存/設定レイアウト18、実ローカルHTTP統合1）と、既存76件。
  保存、課金復旧、10連取引、接続先設定、リリース安全性、ホーム設定周辺の回帰を含む。
- ローカルHTTP統合: 合成Apple本人確認でゲストを連携→別端末相当の別ゲストから復旧。
  購入120個と通常召喚1体が確定したのにスナップショット未反映のケースを再現し、
  有償120/無償600/仲間1体/経済revision2を正確に復旧。元の両端末のsave.jsonは不変。
- 通信テストでUnity JSONのnull文字列を前提にしないリクエストへ修正。
  既存GrantApplierが仲間の取得順を設定するため、再生時には操作データを複製し、
  サーバーの元プレビューを変更しないよう修正。
- Python全84件も成功（失敗0、スキップ0）。本番や本番DBへの接続・変更なし。
- Xcode iPhoneOS26.5 SDK、arm64、iOS15.0対象でObjective-C++の構文/型チェック成功。
  署名付きフルビルドや実Appleの認証成功を意味しない。

Unity結果: `/tmp/nasus-apple-account-final-20260925.xml`。
ログ: `/tmp/nasus-apple-account-final-20260925.log`。

## 未確認・残作業（公開不可）

- Apple Developer Capability、実際のProvisioningと署名、iPhone上の認証と表示。
- authorizationCode交換、refresh token管理、失効通知・連携解除追跡、アカウント削除/revoke。
- 実OSの電源断/容量不足、バックグラウンド中断、期限超過後のセルフサービス復旧。
- フルXcodeエクスポート/リンクと実機インストールは未実施。
- 公開用のUI仕上げ、プライバシー説明、実Sandbox購入後の別端末復旧。

`PlayerDataService.json`と本番factoryは未変更で、機能は無効。
VPS、DNS、バックアップ、Apple Developerアカウント、実機のデータは変更していない。
次は実認証のライフサイクル対応と、ユーザー確認の上でのApple側の設定・実機検証。
