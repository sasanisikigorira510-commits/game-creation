# ③ 実Apple/iPhone検証用の限定接続

限定QA接続は復旧・境界検証済み。一般公開はしていない。現在は実機への導入と操作検証。
初回の `run-apple-qa-mac.sh` は再実行禁止。修復は `run-apple-qa-recovery-mac.sh`。
修復結果は `reports/apple-qa-recovery.ByifQ2`。詳細は末尾の2026-09-27追記を参照。

## 反映範囲と終了条件

- 通常APIのfactory/venv/環境、署名鍵、DB schema、既存workerと監視は変更しない。
- 別service `witch-player-qa.service` をwitchplayerで起動。127.0.0.1:8790だけにbindする。
- 既存HTTPSの `/qa/` だけを別serviceへ接続。通常 `/` と `/admin` は引き続き503。
- 256-bit検証キーを追加認証に使用。既存プレイヤー認証も必須で、管理系/未知経路を拒否。
- キーはURLやcustom headerに入れず標準Authorizationへ格納。proxy側の既定の秘匿対象を使う。
  [Caddyのログ設定](https://caddyserver.com/docs/caddyfile/options#log-credentials)を参照。
- インストール時から最大24時間で全QAリクエストを拒否。自動延長/boot時の有効化はしない。
- 反映前のCaddyfile SHA-256を固定し、候補をvalidateしてから入れ替える。
  失敗時は候補の同一性を確認して旧proxyへ復帰し、新serviceを停止。証拠ファイルは残す。
- 終了条件は `APPLE_QA_GATE_VERIFIED`。正しいキーのhealth=200、キーなし/不一致=404、
  QAの管理経路=404、通常health=200、通常root/admin=503を検査する。

これはApple認証や引継ぎ成功の証拠ではない。反映後、QAアプリの導入と実Apple操作が残る。
試験中は同じ既存DB・バックアップ・解除workerを使うため、承認した実機操作はサーバーに保存される。
課金検証器は未接続で、Sandbox課金の工程完了とは扱わない。

## 保護と検証記録

コードは `reports/apple-qa-package.H3ZC81`、VPS準備領域は
`/home/ubuntu/nasus-deploy/20260926-apple-qa-01`。
manifest/installer/helperのSHA-256をMac起動スクリプトに固定。
VPSで全manifest対象の一致、専用のubuntu所有試験venvで依存関係とimportを確認済み。
この試験venvはサービスへ接続していない。

私有クライアント設定はMacの `NasusApple/qa-20260926/client.json`。
リポジトリ内の設定はBaseUrl空のまま。QA出力時のみ一時的に差し替え、finallyで元のバイト列へ戻す。
公開ビルドはApple検証flagまたはキーがあれば拒否し、通信のリダイレクトも禁止する。
VPSへは手動起動コマンドの実行時に私有設定を別送し、内容hashで紐づける。ログへキーを出力しない。
VPS稼働用gate設定にはキーのSHA-256だけを保存。準備領域の私有設定は期限切れ後も証拠として残る。

backend全503件成功、skipなし。Unity関連43件成功、skipなし。
Unity出力先は `WitchTowerGame/Builds/iOS-Apple-QA-20260926`（git対象外）。
既存開発証明書に一致するApple対応開発profileをmain targetへ明示指定した。
広告依存は既存実機版と同じGoogle-Mobile-Ads-SDK 13.8.0 / UMP 3.1.0へ固定。
端末内アプリの入替え/起動/セーブ変更はまだ行っていない。

Xcodeの最終ビルドも成功。出力は
`/Users/andou/Library/Developer/Xcode/DerivedData/NasusAppleQA20260926/Build/Products/Debug-iphoneos/ProductName.app`。
codesign --verify --deep --strictに成功。署名済みentitlementsで対象application-identifier、
Apple Sign In=[Default]、get-task-allow=true、embedded profile UUIDと接続UDIDを照合した。
結果ログは `reports/apple-qa-xcode-final-20260926.log`。実機には未導入。
自動署名が旧profileを選んだ失敗は、QA main targetへの新profile明示指定で解消。
Unity内のCocoaPods実行は環境依存で停止したため、RUBYOPT=-rloggerを使用し、生成Podfileの
旧GitHub Specsソースを除去、上記既存版へ固定してpod install --deploymentを通した。
既存Unityプロジェクトの依存設定やユーザーのRuby環境全体は変更していない。

## 続行順序

1. 上記コマンドをユーザーが一度実行しVPSの管理者認証を行う。保存レポートをこちらで確認する。
2. 署名済みQAアプリを検査して実機へ導入する。既存セーブの扱いを確認し、削除はしない。
3. 実Appleの同意/認証はユーザーが操作。連携・復旧・解除を個別に確認する。
4. QA入口を閉じ、結果と残るリリース条件を工程表へ反映する。
# 2026-09-27: 初回配置停止と修復

初回配置はproxy段階で停止。初回wrapperは再実行禁止。
失敗時の配布物 `reports/apple-qa-package.H3ZC81` はhash付きで保持する。
元設定/QA候補どちらとも一致しない現行設定は上書きしない。
`run-apple-qa-recovery-mac.sh` は元設定の復旧・caddyユーザーでの検証・正常確認後、
既存QA runtimeのhashと元の期限を照合して限定入口を再開する。
権限を明示する修正はローカルinstallerにも反映したが、旧配布物は変更していない。
公開API factory、DB、鍵、既存バックアップ/Apple定期処理は変更しない。
復旧実行は管理者認証待ち。実機アプリの置換やApple認証試験はまだ未実施。

## 2026-09-27 00:06〜 接続復旧完了・実機導入

`apple-qa-recovery.ByifQ2`: APPLE_QA_RECOVERY_VERIFIED。
Caddy reload成功、設定0644、QA service active、通常health200、キーなしQA404。
00:10にMac私有クライアント設定のキーを使ったQA healthも200を確認（キーは出力しない）。
元の有効期限は変更していない。実Apple認証はまだ未確認。

iPhoneの導入直前退避先（Mac私有領域）:
`/Users/andou/Library/Application Support/NasusBackups/iphone-pre-qa-install-20260927.dwuhZc`。
Documents/Preferences/Application Supportを取得。Documentsは再取得してバイト一致確認。
save.json SHA256: `6ebc0a40949d820c8e89142b0e6cb2e54b38d04ff61f42455a64d9be2cd0d1fc`。
既存の前日退避とも同じsave hash。Keychainを含む端末全体の退避とは異なる。
署名の厳格検査とbundle一致を再確認してQAアプリの上書き導入を開始。アンインストールはしない。

00:11: QAアプリの導入成功（`reports/apple-qa-device-install-20260927.json`）。
起動前にDocumentsを再取得し、導入直前退避との全バイト一致を確認。
起動要求はiPhoneロックにより拒否（Locked、`reports/apple-qa-device-launch-20260927.json`）。
ロック解除を迂回せず停止。ユーザーが端末を解除し、ゲームの設定（歯車）から
「アカウント管理・データ削除」→「現在のデータをAppleと連携」で実Apple認証を操作する。
アプリ起動/UI描画・実認証成功・復旧/解除/削除は引き続き未確認。

## 2026-09-27 アカウント画面の視認性修正

実機画像IMG_2451.PNGで画面の透過を確認。表示のみを濃紺の不透明下地、
既存image2設定枠の9-slice、白文字、ボタン枠、削除操作の赤系表示へ変更。
認証・削除処理は変更しない。関連EditModeテスト65件成功。
Unity iOS出力先はBuilds/iOS-Apple-QA-UI-20260927。
Xcodeの最終frameworkコピーがMac容量不足（空き117MiB）で一度停止。
ユーザーの不要ファイル削除依頼に基づく整理後、同じ出力のビルドを再開。
UI更新前退避はNasusBackups/iphone-pre-account-ui-20260927.599kJT。
Documents-retryとDocuments-verifyの2回取得が成功し、全バイト一致。
00:29: 修正版のXcodeビルド、codesign厳格検証、Apple entitlement/対象bundle確認が成功。
`account-dialog-device-install-20260927.json` に端末インストール成功を記録。
Documents-before-update/after-updateを比較し、上書き導入で保存内容が変わっていないことを確認。
00:30: 起動成功（`account-dialog-device-launch-20260927.json`）。
実機での修正後外観・Apple認証はユーザー操作/画面確認待ち。

## 2026-09-27 既存セーブの移行待ちとSandbox購入の申告

IMG_2452/2454で不透明パネルと枠の実機表示を確認。Apple連携を押すと
「処理中」が一瞬表示された後、オンライン取引のデータ確認待ちへ戻るとの申告。
コード上、AppleSignInはRunCoreの同期エラーで終了するため、Apple認証はまだ開始していない。
既存の端末退避ではLegacy=true、EconomyRevision=0、無料石1200、有償石9600。
legacy登録のmigration_requiredガードと画面の症状が一致する。

ユーザーは有償石9600について、テストアカウントで購入し実金銭の支払いはないと回答。
これはSandbox購入の申告として記録する。署名済み購入履歴の検証結果とは区別する。
現在のQAサービスは既存server.envを利用し、本番用DBから独立した課金台帳ではない。
したがって、この申告だけで9600を本番の有償残高として承認したり、移行待ちを直接解除しない。
継続には検証データと本番データの分離方法を決め、既存進行と退避を保持する必要がある。
今回の確認でサーバーDB、残高、端末セーブ、認証情報は変更していない。

## 2026-09-27 専用DB移行後の実機Apple連携成功

`qa-isolation.vsNuIm`でSandbox専用DBの切替え成功を確認した後、
ユーザー提供IMG_2455.PNGで「Apple連携が完了しました。同じAppleアカウントで
データを復旧できます。」を確認。表示上もLv9、無料1200、有償9600を維持。
この文言はサーバーのlinked応答とPlayerId一致を確認した分岐でのみ表示される。
実機連携成功として記録するが、サーバーDBの直接照合や復旧・解除試験は未実施。
次は同じAppleアカウントで復旧候補のプレビューを確認する。プレビュー取得だけでは
現在のセーブを切り替えない（切替えは別のAppleCommit操作）。アンインストール不要。

## 2026-09-27 01:07 復旧ボタンの文字あふれ修正

IMG_2456の復旧候補はLv9/無償1200/有償9600。後続IMG_2458でホームへの帰還を確認。
ユーザー依頼により、切替えボタンを「このデータに切り替える」に短縮し、
「元のデータは切り替え前に退避します。」を独立した説明へ移した。
装飾の内側に余白を取り、実際のフォント幅に応じた縮小とテキストのクリップ、
ラベル長でボタンがスクロール領域を広げないレイアウトへ変更。復旧/認証処理は不変。
関連EditMode69件成功（account-button-fit-tests-20260927.xml）、Unity出力/Xcodeビルド成功。
署名の厳格検証とbundle確認後、account-button-fit-device-install-20260927.jsonに上書き導入成功を記録。
更新前Documentsを繰り返し取得し一致を確認。導入後Documentsも導入直前と全バイト一致。
退避先はMac私有NasusBackups/iphone-pre-button-fit-20260927.U9BD9L。
退避セーブでPlayerLevel=9、FreeGachaStones=1200、PaidGachaStones=9600、EconomyRevision=1を確認。
後続IMG_2461で修正版の実機復旧プレビューを確認。切替えボタンの文字は枠内に収まり、
退避の説明はボタン外に独立表示。復旧候補Lv9/無償1200/有償9600を維持。

## 2026-09-27 同じiPhoneでの解除・再連携

IMG_2462でApple連携解除の確認画面、ユーザー操作後のIMG_2463で
「現在のデータはAppleと連携されていません。」を確認。
後続IMG_2464で「Apple連携が完了しました。同じAppleアカウントでデータを復旧できます。」
を確認。画面上のLv9・無償1200・テスト有償9600も維持されている。
先の解除操作はゲームアカウント削除とは別で、今回は再連携成功表示まで確認できた。

証跡の範囲はSandbox専用DBと同じiPhoneでの連携、復旧候補表示、ホーム帰還、解除、再連携。
ホーム帰還の画像だけで復旧処理の全段階が成功したとは断定しない。
再インストール後・別端末での復旧、サーバーDB直接照合、Apple側の失効完了は未確認。
10:47 JSTの読み取り確認ではQA service/timer active、maintenance success/Exit0、
認証付きQA health200。これは稼働確認であり、特定の失効要求の完了証明ではない。
QAのゲームアカウント削除試験は引き続き無効。画像確認に伴う追加の端末/サーバー変更は行わない。

## 2026-09-27 11:00 再連携後の端末退避・復旧切替えの確認

接続済みiPhoneからDocumentsを2回読み取り取得し、diff -rqで全バイト一致（終了0）を確認。
保存先はMac私有領域 `NasusBackups/iphone-post-relink-20260927.KZAHi5` の
Documents / Documents-verify。端末側のファイル変更やアプリ削除は実施していない。
これはDocumentsの退避であり、Keychainを含む端末全体のバックアップではない。

active-account.jsonが示すスロットの最新20桁世代ファイルを確認した結果:
- PlayerLevel=9、FreeGachaStones=1200、PaidGachaStones=9600
- EconomyRevision=1、RecoveryEpoch=1、SaveRevision=65
- 有効スロットのidentityとセーブのPlayerId一致
- apple-recovery.jsonなし（未完了の復旧journalなし）
- 元のroot/save.jsonも保持

AppleRecoveryStorage.Activateはサーバーcommit照合済みのjournalからのみ有効ポインタを書き、
その後journalを削除する。上記は同じiPhoneで復旧先スロットへの切替えが完了した証跡として記録。
画像だけのホーム帰還より強い確認だが、再インストール・別端末の復旧やApple側失効完了の証明ではない。
秘密のTokenやPlayerIdそのものは出力せず、認証情報を別用途のリクエストには使用していない。

## 2026-09-27 12:06 別端末への新規導入完了

ユーザーの明示承認後、iPhone 12 Pro（iOS 17.1.2）をApple Developerへ
`iPhone 12 Pro QA`として登録。ユーザー操作で信頼とDeveloper Mode有効化を完了。
既存QAプロファイルの証明書/機能/旧端末を維持し、2台目だけを追加した。
新プロファイルUUID: cbf437e7-4853-4e7c-a25c-c178a7d6652b。
App Store用プロファイルは変更していない。

既存の動作確認済みアプリを独立コピーし、新プロファイルと既存開発証明書で再署名。
保存先: `/Users/andou/Library/Developer/Xcode/DerivedData/NasusQASecondDevice20260927.sAMdeg/ProductName.app`。
元アプリと比較し、差分はProductNameの署名、embedded.mobileprovision、CodeResourcesのみ。
codesign厳格/deep検証、2台分UDID、team/bundle/Apple Sign In entitlement、証明書一致を確認。
QA healthは200。サーバーの設定/期限/データはこの導入作業では変更していない。

対象bundleが2台目に未導入であることを確認してから新規インストール・起動に成功。
証跡: reports/iphone12pro-qa-install-20260927.json、iphone12pro-qa-launch-20260927.json。
元のiPhoneへの操作やセーブ/資格情報のコピーは行わない。
別端末の実Apple認証・復旧候補・commit後のゲーム状態はユーザー操作待ち。導入だけで復旧成功とは扱わない。

Xcodeのデバッグ情報取得によりMac空き容量が不足したため、未使用確認済みの旧版
`~/Library/Developer/Xcode/iOS DeviceSupport/iPhone15,4 26.6.1 (23G83)`キャッシュのみ削除。
約5.6GBで、必要時に再取得可能。写真・鍵・セーブ・ソース・ゲーム素材は削除していない。
