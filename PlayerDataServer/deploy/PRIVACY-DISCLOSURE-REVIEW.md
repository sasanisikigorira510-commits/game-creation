# オンライン版の実装・保存・申告照合表

2026-09-26。ローカル実装の調査であり、App Store Connectの申告や公開ポリシーを変更していない。
対象ビルドを確定するまで、以下の分類は提出回答ではなく監査候補。

## 現在の機能設定

- `WitchTowerGame/ProjectSettings/ProjectSettings.asset`のiPhone設定に
  `WITCHTOWER_ADS_ENABLED;WITCHTOWER_IAP_ENABLED`がある。
- `MonetizationFeatureFlags.cs`は上記defineで広告/課金を有効化。
  `AdMobBannerService.cs`は広告フラグを初期化条件に使用。実広告配信の成功を意味しない。
- `Assets/Resources/PlayerDataService.json`のBaseUrlは空。
- `PlayerDataServer/production.py`は現時点でApple連携/削除サービスをApplicationに注入していない。
  課金検証は別の環境設定経路であり、サーバーのゲーム/admin外部ルートは閉じたまま。
- 8月の「端末内のみ」「広告/課金なし」「Data Not Collected」はオンライン版の根拠として再利用しない。

## データと目的の対応

| データ/機能 | コード上の根拠 | 目的 | App Privacy確認候補と残確認 |
|---|---|---|---|
| Player ID、認証資格情報のhash | store.py: players/register | 認証、保存、引継ぎ | User ID、App Functionality、利用者との関連ありを検討 |
| セーブ、所持品、通貨 | store.py: snapshots/players | プレイ継続、復元 | Gameplay Content、App Functionality、関連ありを検討 |
| 操作、報酬受領、監査/不正フラグ | store.py: operations/claims/audit/flags | 二重処理・不正防止、障害対応 | Gameplay Content/Product Interaction等を実payloadで確定 |
| 商品・取引ID・購入者との対応 | store.py: purchases、apple_verifier.py | 購入照合、二重付与防止 | Purchase History、App Functionality、関連ありを検討。カード情報とは区別 |
| Apple subject hash、暗号化refresh token、交換/失効状態 | account_linking.py、apple_grants.py | 連携、引継ぎ、失効 | User ID等。メール/氏名の一時処理もnative要求と実応答で確認 |
| HTTP Request ID、route分類、status、応答時間 | application.pyのproductionログ | 障害調査 | Diagnosticsの細分類、関連付け、保存期間を確認 |
| 削除hash/日時、購入再利用防止hash、外部イベント/一覧 | account_deletion.py、deletion_journal.py、deletion_inventory.py | 復活・再付与防止 | hashは自動的な匿名化ではない。目的・保存期限の確定が必要 |
| 広告SDK | AdMobBannerService.cs、iPhone define | 広告/同意処理 | SDKバージョン、ID・広告/利用/診断情報・概算位置等をGoogle資料と実ビルドで確認 |
| 問い合わせメール/添付 | 既存窓口 | サポート | Customer Support等。収集経路と保持期間、任意開示条件を確認 |

アプリのproductionログはURL・IP・認証・本文・例外本文を出さない実装、Gunicorn accesslogは無効。
これはOS・プロキシ・サービス提供者・SDKの全ログを調査した結果ではない。通信先がIPを処理しないという意味でもない。
「トラッキングなし」「第三者収集なし」「メールは一切扱わない」は現段階では確定しない。

## 保持/消去の境界

| 対象 | 確認済みの動作 | 確定/実装の残り |
|---|---|---|
| 外部db/バックアップ | 30日超を毎時判定、異常時停止 | 実期限切れDELETEは候補発生時の確認対象 |
| VPS内バックアップ | 直近48世代＋過去30日の各UTC日最終世代 | 一律30日保証ではない。外部と統一するか運用判断が必要 |
| 削除記録と一覧 | 現在は自動期限切れなし | 復元対象コピーの消去確認と連動した整理手順が必要 |
| 購入再付与防止hash | 現在は自動期限切れなし | 復旧・再購入照合への影響と保持根拠を確認。DBの30日方針は流用しない |
| Apple未解決交換/失効 | 成功した失効のgrant行削除、失敗/不明は保持 | ③の自動worker/滞留監視・解消手順と併せて期限を決める |
| 削除結果proof | 確認後最低24時間、期限切れを次回previewで掃除 | 24時間で必ず消去とは説明しない |
| Mac/USB・調査コピー | 自動削除なし | 保存先と責任者、廃棄条件の台帳が必要。バックアップと復号鍵は分ける |
| サポート/運用ログ | 全体の期間は未監査 | サーバー/提供者/メールごとの保存設定を確認 |

この文書は新しい期限や削除権限を決定しない。とくに購入再付与防止記録を日数だけで消す変更は行わない。
説明文だけで未実装の消去処理を「対応済み」にしない。

## 終了条件と担当工程

1. **②-E**: DB喪失時の保証範囲を決め、記録種別の保持/消去条件と実装を一致させる。
   現状は、最新削除集合を確認できなければ復元候補を公開しない。
2. **③・④**: Apple実認証/失効、未解決交換の解消、購入/返金とデータ分類を確定。
   この実装調査は②-Eと並行できる。未完了のまま公開ルートは開けない。
3. **⑤**: 提出予定ビルドで広告SDK・同意/追跡・実通信、削除/解除の表示、プライバシーリンクを確認。
4. **⑥**: 未確定箇所を除いたポリシー、アプリ内説明、App Privacy回答を同じビルドに揃えて公開/提出。

## 公式資料（2026-09-26確認）

- [Apple App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/): 組込み第三者の取扱いも申告対象として確認。機能提供目的の収集も対象。
- [Apple Account Deletion](https://developer.apple.com/support/offering-account-deletion-in-your-app/): アプリ内削除開始、状況案内、Apple token失効の確認。法的保持義務は別途専門家確認。
- [Google Mobile Ads開示](https://developers.google.com/admob/ios/privacy/data-disclosure): SDKが扱い得るIP・端末ID・広告/操作/診断情報を提示。最終回答は導入版と設定に合わせる。
