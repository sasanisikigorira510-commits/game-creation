# アカウント削除：ゲーム内確認と端末消去（開発用）

2026-09-25。**本番無効・実機未確認。実アカウントを削除する作業は行っていない。**
前段の[サーバー実装](ACCOUNT-DELETION-2026-09-25.md)へクライアントを接続した。
テストでは一時ディレクトリ・使い捨てDB・合成Apple認証のみ使用。

## 変更ファイル

追加:
- `WitchTowerGame/Assets/Scripts/Save/AccountDeletionStorage.cs`とmeta。
- `WitchTowerGame/Assets/Scripts/Save/OnlinePlayerData.Deletion.cs`とmeta。
- `WitchTowerGame/Assets/Tests/EditMode/AccountDeletionStorageTests.cs`とmeta。

変更:
- `OnlinePlayerData.Apple.cs`: 管理画面から削除確認へ。縦スクロール、削除状態の再読込。
- `OnlinePlayerData.cs`: 削除ジャーナル中の自動同期・保存取引・購入・運営復旧の停止。
- `AppleRecoveryStorage.cs`: 削除中の引き継ぎ禁止、新規ゲストは別slotを作成。
- `SaveManager.cs` / `GameManager.cs`: 削除中の起動・保存を停止し、現在のプロフィールを解除。
- `HomeSceneController.cs`: 管理ボタンの名称と停止中のUpdateガード。
- `SceneTransitionGuard.cs`: 削除結果不明/消去未完了の間は画面移動を止める。
- `AppleAccountTransportTests.cs`: 既存復旧/解除テストに削除取り消し・通信断・端末消去・新規開始を追加。
- `PlayerDataServer/account_deletion.py` / `application.py`: サーバー確定付きcancelを追加。
- `tests/test_account_deletion.py` / `tests/test_production.py` / `tests/apple_unity_fixture.py`。
- 説明文書・配布許可リスト。

新規画像・プレハブは追加していない。開発管理画面は既存のIMGUIスタイルを使う。
他の作業から存在していた変更は残し、まとめて置換・取り消ししていない。

## 操作の流れ

1. 開発機能を有効にした設定画面の「アカウント管理・データ削除」から管理画面を開く。
2. 「ゲームアカウントを削除（次に確認）」でサーバーのpreviewだけを取得する。
   この操作では登録・購入処理・報酬受取を自動実行しない。
3. 仲間・進行・石・永続強化を失うこと、連携解除とは異なること、返金手続きではないこと、
   過去バックアップは未対応であることを表示。確定前なら「削除せず戻る」が可能。
4. 明示確定後、現在の進行を端末へ保存し、削除対象ファイルの一覧・内容ハッシュ・確認token・
   接続先を`account-deletion.json`へアトミックに記録してからcommitを送る。
5. `deleted`を確認してから端末消去へ進む。通信断・401・不正応答では消去しない。
   ジャーナルがある間は起動時も保存/取引/購入/別アカウントへの切替を停止する。
6. 消去が完了しても`completed`の小さな停止記録を残し、元ゲストへ自動復帰しない。
   「新しいデータで最初から始める」を選ぶと、新PlayerId/資格情報/別slotを作成して再開する。

初回のオンライン登録/認証がまだない端末は、サーバーpreviewを取得できない。
未登録オフライン専用データの削除導線は別途必要。本番公開用の導線とは扱わない。

## 通信断・取り消し

- 再起動後はジャーナルの`requested`/`confirmed`/`completed`から再開。
- `requested`ではstatusで結果を確認し、`not_deleted`でも勝手にプレイへ戻らない。
  初回commitがまだ処理中かもしれないため、同じ確認で再送するかcancelをサーバーへ依頼する。
- 追加API: `POST /v1/account-deletion/cancel`。body・Bearerはcommitと同じ。
  DB書込ロックでcommitと直列化し、未削除なら確認tokenを無効化して`cancelled`。
  先に削除が確定していれば`deleted`を返し、端末側は消去へ進む。
- `cancelled`を受け取った場合だけ端末ジャーナルを外し、保存しておいた進行に戻る。
- 設定の接続先が開始時と異なる場合は秘密情報を送らない。
- サーバーの成功結果照会期限は24時間。期限後の401を未削除と判断しない。
  運営による解消手順はまだ公開前の必須作業。

## 端末で消すもの / 残すもの

対象PlayerIdのrootまたは`linked-accounts/<32桁slot>`内にある既知の保存ファイル:
`save.json`、バックアップ/一時ファイル/破損退避、既知形式の保存履歴、
`online-identity.json`、`online-request.json`と一時ファイル、当該active-accountポインター。
旧資格情報で残っている同じPlayerIdのslotも対象。

ディレクトリの再帰削除はしない。別PlayerIdのゲスト、音量などの設定、無関係なファイルは保持。
複数PlayerIdのファイルが混在する場合、リンク経由のパス、途中で内容が変わった場合は停止する。
既知の対象が増えても勝手に消去対象へ追加しない。部分消去後の再試行では、既に消したファイルは許容。
消去完了後に10連召喚の表示用PlayerPrefsだけを消し、秘密情報と一覧をジャーナルから除去する。

これは論理ファイル削除。OS/iCloudの端末バックアップ、他端末、サーバーの古いバックアップ、
ストレージ上の残存領域まで消去した保証ではない。正規保存場所以外へ手動でコピーしたファイルは対象外。
実StoreKitの未確認注文は勝手に確認済みにしない。購入処理中は開始を止め、遅い購入応答にも付与しない。
実際の保留注文・削除済みアカウント・返金の運営対応は実Sandboxで確認が必要。

## 検証

- Python176件成功・失敗0・スキップ0（cancel関連4件追加）。
- Unity関連115件成功・失敗0・スキップ0を確認。追加のストレージ20件と、既存の実HTTP統合を拡張。
  結果: `/tmp/nasus-account-deletion-client-20260925.xml`。
- 保全対象の別ゲスト、同PlayerIdの複数slot、履歴・資格情報・保留リクエスト消去、内容変更時の停止、
  不正パス・シンボリックリンク拒否、途中消去、破損/一時ファイルだけのジャーナル、起動停止を確認。
- 実ループバックHTTPで、送信前クラッシュ相当→status→cancel→復帰、
  サーバーcommit後503→端末再読込→接続先不一致の停止→status→消去→明示新規開始を確認。
- 別物理端末を模したディレクトリは残ることも確認。リモート端末消去を実装したわけではない。

## 本番ゲート・残作業

- `PlayerDataService.json`のBaseUrlは空、ExperimentalAppleLinkingはfalse相当のまま。
  production factoryは削除/連携依存を注入せず、削除4ルートは503。
- 既存の削除ジャーナルがある場合の結果確認/安全停止は、機能フラグがOFFでも維持する。
- VPS配置・実Appleキー取得・実Apple認証/失効・iPhoneインストールは未実施。
- iPhoneの見た目/タップ/VoiceOver、実容量不足・強制終了・バックグラウンド、保留中のStoreKit注文は未確認。
- 本番の保持期間/ユーザー向け説明、バックアップ復元後に削除を巻き戻さない仕組み、
  Apple失効ワーカー/通知/滞留監視、資格情報のKeychain移行、運営解消手順が必要。
- 合成fixtureが返す「失効待ち」は実Appleでの失効完了ではない。
