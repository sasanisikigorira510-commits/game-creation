# iPhone公開までの工程と現在地

更新: 2026-09-28。記録と確認結果に基づく。実装済みと実機確認済みを区別する。

2026-09-29 JST追記: Sandbox通知の専用公開経路反映に成功。外部HTTPSで未署名POST400を確認。
App Store ConnectのSandbox通知URLだけ保存済み、本番URLは未設定を維持。
保存証跡あり。Apple TEST通知の送信/配信確認を準備し、追加4テスト成功。
TEST送信は専用ユーザーでの実行に必要な管理者認証待ち。実通知成功は未確認。
以下の「反映/保存待ち」はこの追記より前の履歴。

Sandbox通知経路追記: 専用公開受信URLとSandbox通知URL設定をユーザー承認済み。
完全一致POSTだけを通知専用8793へ転送する設定・退避/検証/失敗時復元スクリプトを準備。
関連32テスト成功、VPS私有準備領域への転送とハッシュ一致確認済み。
公開経路の反映は端末での管理者認証待ち。Apple側の通知URL保存と実通知試験はまだ未実施。
詳細: [Sandbox通知経路の準備](REFUND-NOTIFICATION-PROXY-2026-09-28.md)。

返金専用Sandbox追加: ユーザーからVPSへの分離環境追加を承認済み。
管理者認証後、専用の空DB/別UID/内部限定サービスの導入が成功。
内部health・認証/経路拒否・未署名通知拒否・既存設定ハッシュ不変を確認。
別SSH接続で専用3サービスactive、8791〜8793の127.0.0.1限定待受、
両定期ジョブの終了成功とtimer稼働、既存公開/QA/Caddyの稼働維持を独立確認。
結果: REFUND_LAB_INTERNAL_READY_NOT_PUBLIC。公開通知経路とApple実通知試験は未実施。
追加8テスト成功、全体701件中692成功・9スキップ。既存公開/QA設定とDB、iPhoneは変更なし。
詳細: [返金試験環境の導入準備](REFUND-LAB-INSTALL-2026-09-28.md)。

以下の課金返金対応追記は導入前の履歴。現在の導入状態は上記を優先する。

課金返金対応追記: ユーザー方針承認後、返金取消数・不足数の台帳とApple署名検証、
有償消費だけの制限をローカル実装。通常factory/稼働環境は未有効化。
続いて不足審査・解除と通知取り逃し再照合を内部サービスとして実装。
購入ごとの免除相殺で返金取消時の二重付与を防止。再照合は失敗待機・占有期限を保存。
返金台帳のバックアップ検証と削除反映付き隔離復旧を追加。欠けた台帳は拒否し、
復旧先の再照合を必須化。バックアップ後の審査履歴等を自動復元する機能ではない。
名前付き管理者認証の審査API、明示実行ワーカー、タイマーテンプレート、鮮度/実行状態監視を追加。
通常factoryは無効のまま。稼働環境への接続・タイマー有効化はしていない。
追加: 完成バックアップの新しい隔離コピーだけに返金台帳を導入するリハーサルを実装。
元残高/認証/取引履歴の不変と失敗時の隔離を確認。稼働DBの直接移行・昇格機能は追加していない。
追加: Sandbox用の購入・返金通知・審査・再確認を同じDBへ結ぶ明示的な組み立て関数を追加。
端末/審査/通知の入口を分離し、期限・環境・instance・アプリ不一致は拒否。実環境への接続は未実施。
最新サーバー全体693件中684成功・9スキップ。Unity関連107件は前回成功、今回はコード未変更。
管理画面/外部通知、取得後履歴の独立保全/照合、明示的な移行・配布と
Sandbox実通知試験が残る。実購入・実返金・VPS/実DB更新・実機配布は行っていない。
詳細: [審査解除・再照合の仕様](REFUND-OPERATIONS-2026-09-28.md)。
詳細: [返金台帳のバックアップ・隔離復旧](REFUND-RESTORE-2026-09-28.md)。
詳細: [管理者認証・ワーカー・監視](REFUND-CONTROLS-2026-09-28.md)。
詳細: [隔離移行リハーサル・Sandbox結合試験準備](REFUND-MIGRATION-2026-09-28.md)。
詳細: [Sandbox返金の接続構成](REFUND-SANDBOX-ASSEMBLY-2026-09-28.md)。
詳細: [返金対応の実装・検証と残件](IAP-REFUND-REVIEW-2026-09-28.md)。

21:48追記（通信通知の最新結果）: iPhone 12 Proの開発限定HTTP診断で、通信断ではなく
登録APIの429を確認。同期のたびに登録を繰返し、登録用20回/時の制限に達していた。
既存端末は認証済みheadを先に取得するよう修正。429表示/待機も区別。サーバー制限は不変。
関連95テスト成功、修正版上書き前後Documents一致。実機head/snapshotsとも200へ回復。
召喚/購入せず、無料600/有償8820/所持207/EconomyRevision7の不変を確認。
ユーザーから通知が「消えた」と報告あり、画面上の消去も確認完了。
続く実機確認で再起動後もhead/snapshots 200、経済状態不変を確認。
機内モード/Wi-Fiオフでエラー表示→通信復帰後、召喚せず通知が消えたとユーザー報告あり。
通信断からの通知自動解除も確認完了。
iOS更新/ストレージ削除は中止したまま。
詳細: [通信通知の実機診断・修正](ONLINE-NOTICE-2026-09-28.md)。

通信通知追記: IMG_0338でオフライン時の残高9420/所持203保持を確認。
ユーザー報告では再接続後も古い通知が残り、追加召喚成功で消えた。追加後の残高は未確認。
取引なし正常同期で古いエラーを消す修正、大きな中央配置の通知枠/文字と独立した閉じる領域、
復帰時の同期促進を実装。ローカル実HTTPの停止/復帰を含む関連87件成功。
20:22にUnity/Xcodeビルド・署名検証成功後、iPhone 12 Proへの上書き更新完了。
前後Documents全ファイル一致。退避: NasusBackups/iphone12pro-online-notice-20260928.SppUNS。
見た目/復帰の実機再確認待ち。詳細: [通信通知修正](ONLINE-NOTICE-2026-09-28.md)。

購入・召喚再送テスト追記（以下より優先）: IMG_2471で旧iPhone15の認証無効案内と
ボタン無効化を実機確認。IMG_0336/0337で引き継ぎ先の有償9720→9420、所持202→203、
無償1200保持を確認。ユーザーから再起動後も同値と報告あり。
隔離一時DBの購入/召喚同時実行6テストを追加。関連47件成功、追加6件の25回反復も成功。
同一購入の8件同時再送、異なるRequestIdの同一購入、別アカウントの同一購入、
残高300の同時消費、同一召喚再送、確定後再起動/検証失敗再試行を確認。
本番/検証VPS・実機残高は変更せず、サーバー実装や配布も変更なし。
実Apple通知/実レシート再送、返金取消の継続反映は未確認のまま。
詳細: [隔離再送テスト](ECONOMY-REPLAY-TEST-2026-09-28.md)。

19:48追記: 旧端末の召喚画面で認証拒否と「契約可能」が併存する表示を修正。
OnlinePlayerDataに取引可否の表示状態を追加し、確認中/同期失敗/復旧待ちは召喚を無効化。
401および既存アカウントへの初回接続(/v1/accounts)403を認証無効として案内。
他の403は操作権限の拒否として区別。正常同期後は所持枠・残高に応じてボタンを復帰。
サーバーの取引判定、残高、認証情報、QA期限は変更なし。
最終関連EditMode74件成功、Unity書出し/Xcodeビルド/署名検証成功。
元のiPhone15だけに上書き導入成功。前後Documents全ファイル一致。
バックアップ: NasusBackups/iphone15-gacha-auth-ui-20260928.7X6tED。
テスト記録: /tmp/nasus-gacha-auth-ui.00OYfk/tests-final.xml。
自動起動は端末ロックで拒否。ユーザーによる起動・召喚画面の実機表示確認待ち。

19:38追記（以下より優先）: IMG_0334でSandbox購入後の有償9600→9720と120個受取表示を確認。
ユーザーからアプリ削除後も9720保持、購入キャンセル後も9720・操作可能との報告あり。
これらは画面/ユーザー申告による基本確認であり、購入台帳の独立確認や再送試験とは別。
現在の旧iPhone15の保存済み認証をユーザー許可のもと読み取り診断し、QA health200・
旧認証のplayer head401を再確認。IMG_2470の「契約可能」はローカル残高判定による表示で、
認証成功の証拠ではない。旧端末の実ガチャは実行せず、二重付与/同時消費・返金取消は未確認。

最新状況（以下の9/27記録より優先）: 2台目でLv9/無償1200/テスト有償9600への
復旧と、ユーザーによる再起動後の保持を確認。旧端末の認証でのGETはHTTP401、
同じQA gateのhealthは200。旧端末用認証の無効化を実応答で確認した。
詳細は[9/28検証結果](QA-RENEWAL-2026-09-28.md)。QA期限は9/29 00:57:40 JST。
③のApple側トークン失効結果、②の削除関連の残件は継続。③全体の完了ではない。
並行して④Sandbox課金の準備状況を確認中。現QAはpurchase verifierを明示的に無効化。
課金キー・証明書・Sandbox専用構成と、返金/取消の継続反映は未確認または未実装。
Store関連21テスト成功。ユーザーがApp Store Connectで課金用キーを作成・ダウンロード。
アクティブ(1)とMacの鍵の形式/ローカル署名検証成功を確認、ファイル権限0600。
ユーザー許可後、課金用秘密鍵を既存VPSの専用私有準備領域へ転送しSHA-256一致を確認。
ディレクトリ0700/鍵0600。実行中QAへの接続、実購入、サーバー設定変更は未実施。
Sandbox専用factoryと固定依存の反映パッケージを用意し、VPSへの転送・整合性確認まで完了。
全体571件中562成功/9スキップ、関連105件成功。
01:45のユーザー反映は書き込み開始前の事前確認で停止。旧QA/本番は稼働を保持。
01:49の読み取り診断は全事前確認に合格。初回停止原因は再現せず未確定。
VPS上の別検査環境で公式SDK/実鍵/Appleルートのオフライン構築・依存確認も成功。
01:52、`iap-sandbox-connect.Vd7QhW` でSandbox接続完了。新QA実行領域の稼働と
外部GETによるhealth200/公開root・admin503/認証なしQA404を独立確認。
01:54の再実行は既存配置の二重作成を防ぐ事前確認で停止。再実行不要。
**現在④はサーバーのSandbox接続まで完了、実機購入・再送・返金/取消は未確認。**
一般公開と本番課金は変更なし。
詳細は[IAP準備記録](IAP-READINESS-2026-09-28.md)。

02:08追記: 実機のApple購入シート（IMG_0329）でSandbox・120個160円・請求なし表示を確認。
アプリの商品一覧は古いUSD表示が残っていたため、ショップ表示/復帰/購入終了後の
商品再取得を追加。JPYはストア取得額を「160円」の形式で表示し、他通貨を円に偽装しない。
購入中・商品取得中の並行リクエストを避け、変更前価格は再取得中に表示しない。
関連EditModeテスト53件成功、Unity書出し/Xcode Debugビルド成功。
ユーザーの購入シート閉鎖後、2台目iPhone12Proに上書きインストール完了。
更新前後のDocumentsは全ファイル一致。バックアップはMacのNasusBackups内
`iphone12pro-iap-price-20260928.78feOt`。円価格の実機表示は再確認待ち。
購入確定・付与・再送・返金/取消の確認は未完了のまま。

02:15追記: ユーザーが円表記への更新を実機確認。追加要望で宝晶個数19→32pt、
価格24→34pt、商品枠400×300→420×330へ拡大し、6商品の間隔・価格枠も調整。
既存の枠素材を使用。53件の関連テストと実描画で長い価格/個数、文字の収まり、
カード間の非重複を確認。描画記録は `/tmp/nasus-iap-large-20260928/ready.png`。
02:17、iPhone12Proへの上書きインストール完了。前後のDocuments全ファイル一致。
バックアップ: NasusBackups/iphone12pro-iap-large-20260928.uRTqSU。実購入は未実施。

直近の現在地: ③ Apple実機連携。Sandbox専用DBで同じiPhoneの連携・復旧切替え・解除・再連携を確認。
画面の透過は実機で改善確認。連携が開始しない原因は旧セーブの移行確認待ち。
ユーザー申告のテスト購入石9600を本番残高として承認しないため、専用DBへコピーし、
進行・無料石1200・テスト有償石9600をQA内だけで移行する実装を用意した。
関連41テスト成功。00:54の`qa-isolation.vsNuIm`でQA_SANDBOX_ISOLATION_VERIFIEDを確認。
専用DBへの切替え完了。QA service稼働、専用maintenanceの後続実行success/Exit0、timer active。
通常health200・通常root503・キーなしQA404を再確認。本番DBへの移行は未承認・未実施。
IMG_2455.PNGで実機の「Apple連携が完了しました」表示を確認。クライアントは
サーバー応答Status=linkedとPlayerId一致を検査してからこの表示を出す。
IMG_2456/2461で復旧候補Lv9・無償1200・テスト有償9600を確認。IMG_2458はホーム帰還の証跡。
IMG_2463で解除後の未連携表示、IMG_2464で再連携成功表示を確認。
同じiPhoneでの操作確認であり、再インストール後・別端末での復旧成功とは区別する。
11:00の再連携後Documents退避2回は全バイト一致。有効復旧スロット/identity一致、
RecoveryEpoch=1、未完了journalなし、Lv9/無償1200/テスト有償9600と元セーブ保持を確認。
同じiPhoneでの復旧切替え完了を端末の保存状態でも確認できた。
12:06に別のiPhone 12 ProへQAアプリを新規導入し起動成功。旧端末データはコピーしていない。
現在は2台目で同じAppleアカウントによる復旧操作待ち。別端末での復旧成功はまだ未確認。
12:29: 2台目のプロローグが下部広告に重なる報告（IMG_0320）に対応。
ネイティブ広告の実高さと画面上の安全領域を換算し、本文・枠・ページ表示・タップ案内を
一緒に上へ移動。背景と文字サイズは維持。広告の遅延表示/高さ変更/非表示と全6ページの
安定表示を含むEditMode 8件成功、Unity/Xcodeビルドと署名検証成功。
iPhone 12 Proだけへ上書き導入済み。導入前後Documentsの全バイト一致を確認。
退避: Mac私有NasusBackups/iphone12pro-pre-prologue-20260927.l6r7AA。
実機で広告との重なりが解消したかはユーザー確認待ち。Apple別端末復旧成功とは扱わない。
12:29の自動起動は端末ロック（Locked）で拒否。更新は完了しており、ロック解除後の起動待ち。
12:30〜: 2台目で召喚画面が「読込待ち/エディタプレビュー」のままという報告（IMG_0321）。
端末の保存状態はT02・初回召喚0・無料石900・有償石0で保持されている。
GachaSceneControllerの全Canvas検索が永続OnlineInputBlockerを選び、実行用パネルを
非表示の通信ブロッカー下へ作成し得る問題をテストで再現（修正前2件失敗）。
自分のシーンのGachaCanvasだけを選択・正規化するよう修正。通信ブロック自体は維持。
修正後はCanvas分離・初回召喚会話・遷移・プロローグの21件成功。実機更新/進行確認は別途記録。
端末ログとDocumentsはMac私有NasusBackups/iphone12pro-gacha-20260927.u507o8へ退避。
12:39: Unity/Xcodeビルド・署名検証成功後、iPhone 12 Proだけへ上書き更新・起動成功。
更新直前/直後のDocumentsが全バイト一致。実機で召喚画面に入り会話が進むことは確認待ち。
18:28〜: IMG_0323で初回3体の召喚とチーム編成完了を確認。端末セーブもT05・初回召喚3・
所持3体で一致。Apple別端末復旧とは別の、新規端末チュートリアル進行の確認。
「ホームへ戻る」の点滅を覆っていたのは上部のIMGUI「データを反映しました」通知。
Canvasの兄弟順を変えてもIMGUIより前には出ないため、実際の戻るボタンの画面座標から
枠と下の案内の保護範囲を計算し、通知を右側へ避けるよう修正。狭い場合は案内下へ配置。
通知の閉じる機能・通信中の入力遮断・ゲーム進行は変更しない。
通知配置/編成回帰10件成功、Unity/Xcodeビルド・署名確認成功。
更新用の端末Documents退避はMac私有NasusBackups/iphone12pro-formation-toast-20260927.ZKy0eR。
18:32: iPhone 12 Proへの上書き更新成功、更新前後Documents全バイト一致。
18:33: 起動は端末ロックで拒否。ユーザーの解除/起動と通知位置の実機確認待ち。
18:36〜: 冒険開始後に黒画面になる報告。端末ログではHomeSceneに留まりBattleScene未到達。
DungeonSelectionPanelController.EnsurePanelの全Canvas検索が、Homeが正しく作成した選択画面を
OnlineInputBlocker下へ移動させる不具合を再現（修正前2件失敗）。非表示ブロッカー下では
ホームを隠したあと選択画面も見えなくなる。HomeCanvasをシーン所有者経由で選ぶよう修正。
修正後、Canvas分離/シーン遷移/バトル関連24件成功。通信中の入力遮断は変更していない。
端末退避: Mac私有NasusBackups/iphone12pro-battle-black-20260927.ThjNCZ/Documents-complete と
Documents-verify。差分はSaveRevision/SavedAtUtcだけ。T06・初回召喚3・階層1/未クリアを保持。
実機更新とバトル到達の確認は別途記録する。
18:44: Unity/Xcodeビルド・署名検証成功後、iPhone 12 Proだけに修正版を上書き導入。
同退避先のDocuments-before-install/after-installを比較。save.jsonとbakの差分は
SaveRevision/SavedAtUtcだけで、履歴67が追加。進行・仲間など他フィールドは一致。
18:45: 更新版の起動成功。実機で探索先の表示・バトル到達を確認する操作待ち。
22:28〜: 味方ロックゴーレムの接敵が速すぎるとの報告に対応。中衛にも適用される
近接移動1.6倍×クラス1補正1.18倍、および後衛接敵1.35倍の対象からロックゴーレムのみ除外。
全配置で通常近接の移動速度0.1564 anchor/秒に統一（中衛は従来比約47％減速）。
攻撃速度・攻撃範囲・他の味方/敵の移動設定は変更なし。
移動11件を含む関連58テスト成功、Unity/Xcodeビルド・署名検証成功。
Mac私有NasusBackups/iphone12pro-golem-speed-20260927.OPwTEzへ端末Documentsを退避。
22:33: ロックゴーレム限定版を2台目へ更新・起動、前後Documents全バイト一致。
その後ユーザー指定により近接キャラ全体の加速廃止へ変更。中/後衛の1.6倍、系統/クラスの
移動補正、後衛の接敵1.35倍を削除し、通常近接速度を全配置で使用。ゴーレム限定例外も削除。
攻撃速度・射程・遠隔/敵の移動速度は維持。全モンスターの基本移動速度検査および
戦闘/難易度など関連86テスト成功。全近接版の実機反映は別途記録する。
22:41: 全近接加速廃止版をビルド・署名検証後、iPhone 12 Proへ上書き導入成功。
上記退避先Documents-before-all-melee-retry/after-all-melee-installの全バイト一致。
それ以前のbefore-all-melee-install/stoppedは自動保存の履歴ローテーション中に転送失敗した
不完全コピーであり、確認済み退避として使用しない。保存データの削除・書き戻しは未実施。
22:42: 更新後の起動は端末ロック（Locked）で拒否。インストールは完了、実機の速度確認待ち。
22:45〜: 2台目の最新セーブを読み取り確認。HasCompletedTutorial=true、TutorialStepId=Complete、
初回召喚3、Lv4、最高クリア階10、現在階11、無償/有償石0。チュートリアルと戦闘の進行を
保存状態で確認。加速変更後の見た目の評価とは区別する。認証付きQA health=200。
既存QA期限は2026-09-27 23:57:58 JST（延長していない）。
同退避先Documents-before-cross-device-previewにDocuments取得成功。
別端末復旧候補を実Apple認証で取得するユーザー操作待ち。現在のLv4データの切替え、
元端末の操作、サーバー設定/データ変更は実施していない。
23:20〜: IMG_0324で2台目のApple復旧候補Lv9/無償1200/テスト有償9600を確認。
端末退避のapple-recovery.jsonもPhase=preview。切替え完了とは扱わず、こちらではcommitしない。
ユーザー依頼で歯車から開く設定画面を拡大。枠760×742→960×1360（Apple機能有効時）、
主要ボタン高さ64/68→112、音量スライダー操作高54→96、主要文字22→36。
ミュート/解除は横2列、アカウント/物語/広告設定/閉じるは幅740の独立行。
既存image2枠に9-slice境界を設定し角の歪みを防止。画面の安全領域と実広告高さに合わせて
全体を収める。アカウント復旧処理・音量保存処理は変更なし。
レイアウト/復旧/プロローグ関連31テスト成功。生成プレビューを確認。
退避先はMac私有NasusBackups/iphone12pro-settings-large-20260927.xvjXgY/Documents-before-build。
23:35: Unity/Xcodeビルド・署名検証後、2台目のiPhone 12 Proへ設定拡大版を上書き導入成功。
同退避先Documents-before-install/after-installの全バイト一致、復旧journalもpreviewのまま保持。
23:37: 起動は端末ロック（Locked）で拒否。更新完了、ロック解除後の設定サイズ確認待ち。
容量確保のため未使用のiPhone15,4 26.6.2開発シンボルキャッシュ約5GBのみ削除。
再取得可能なキャッシュであり、ソース・素材・鍵・セーブ・写真は削除していない。
サーバーDB直接照合、Apple側の失効完了も未確認。正常なmaintenance/healthだけでは失効完了と扱わない。
残りは端末内の既存認証に依存しない復旧と失効結果の確認。削除・課金試験は別ゲート。
詳細: [QA Sandbox分離](QA-SANDBOX-ISOLATION-2026-09-27.md)。

最終ゴール: App Storeで一般公開し、プレイ・セーブ・購入・引継ぎが動き、障害時に復旧できる。
Androidは別工程。各工程の大きさが異なるため、段階数から完成率を計算しない。

| 工程 | 終了条件 | 現在 |
|---|---|---|
| ① サーバー基盤 | ドメイン、HTTPS、バックアップ、異常通知が動く | 基本構築・確認済み |
| ② 削除・復旧対策 | 削除履歴を保管し復旧時の復活を防ぐ。手順と保持方針を整える | 作業中 |
| ③ 連携・引継ぎ | 実Apple認証で連携、復旧、解除・失効を確認 | 別端末復旧・再起動保持・旧端末認証拒否まで確認済み。Apple側トークン失効結果などは残る |
| ④ 課金 | Sandbox購入、再送、通信断、返金/取消対応を確認 | 実装あり、実購入試験/設定など残る |
| ⑤ 実機総合確認 | 接続・セーブ・戦闘・召喚・引継ぎ・削除をiPhoneで通す | 最終確認前 |
| ⑥ 審査提出 | 提出ビルド、紹介素材、説明、プライバシー申告を揃える | 状況確認と仕上げが残る |
| ⑦ 公開 | 審査対応と公開直後の動作確認 | 最終ゴール |

## ②の内訳

- ②-A 開発用削除/履歴/復元実装とローカル試験: 実施済み。公開の証明ではない。
- ②-B サーバー反映: 完了。限定権限で実workerを再開し正常終了を確認。
- ②-C timer自動実行と監視統合: 完了。後続worker自動完了2回と後続監視更新、HTTPS正常を確認。
- ②-D 合成テストデータで外部履歴保存〜復元後の削除再適用を通す: 完了。23:23の実さくらPUT/GET・回収・隔離復元で確認。
- **②-E 保存期間・復旧手順・利用者向け説明: 作業中。外部DBバックアップの30日保持は反映・初回実行・監視更新を確認済み。最新削除履歴の完全性管理などが残る。**

22:48台にユーザー実行の`deletion-integration-resume.DgFxVY`がDELETION_INTEGRATION_VERIFIEDで完了。
LaterWorkerCompletions=2、LaterHealthRunVerified=true、GameRoutesClosed=true。
反映処理の終了後もSSHでworkerのResult=success/Exit0、timer enabled/active、統合health正常を確認。
22:51台の監視ログもHEALTHY。外部GET/HEAD /healthz=200、GET /=503。
既存APIとhealth/offsite/backup timerもactive。新しいsudoers許可は追加していない。

正常な同時更新を未来時刻扱いする競合はローカルで再現・修正し、修正版を反映済み。
過去の503の唯一の原因だったかは未確定だが、今回の連携確認の終了条件は達成。
記録の鮮度上限・実際の未来時刻拒否・失敗検出は維持し、異常理由を固定コードで記録する。
②-Dの終了条件は、合成データで外部削除履歴保存と復元後の再適用を通し、本番データを変更しないこと。
②-B/Cの結果はRealDeletionTestComplete=false。空queueの確認と②-Dの合成データによる通し試験を区別する。
②全体や公開準備の完了ではない。ゲーム/admin APIは引き続き非公開。

②-Dの追加実装: 独立した期待履歴一覧とSHA-256で、取得した削除イベント集合を照合して復元用情報に変換する。
一覧自体が最新であることの証明とは別。ダウンロード結果や古いバックアップから期待一覧を作ってはいけない。
ローカルでは実age暗号化・復号、削除前バックアップの隔離復元、削除アカウント/認証情報の拒否、
削除していないアカウントの維持、購入履歴の再利用拒否を確認。バックエンド全306テスト成功。
このテストの通信部分は模擬であり、実さくら転送成功とは扱わない。
実転送用には専用の使い捨て試験鍵と596バイトの暗号文を準備。秘密鍵はMacから出さず、
既存の非公開バケットのdeletion-probes/に合成データ1件のみPUT/GETする。
「削除復元の通し試験.command」はVPSのsudo認証1回で転送・回収・Macでの復号/隔離復元まで行う。
本番DB、公開ルート、サービス、監視設定は変更していない。

23:23の`synthetic-cloud-restore.1iLqts`でSYNTHETIC_PROBE_READBACK_VERIFIEDと
SYNTHETIC_DELETION_RESTORE_VERIFIEDを確認。Macのverified.jsonと隔離復元レポートも一致。
削除済み合成プレイヤー1件を除外し、未削除プレイヤー不変、旧資格情報拒否、
購入再付与拒否、元バックアップ不変、隔離維持を確認。②-Dの終了条件を達成。

②-Eの現状と未確定事項は[復旧・保持の運用手順](RETENTION-AND-RECOVERY.md)に集約。
8月のPRIVACY_POLICY.md/APP_STORE_SUBMISSION.mdは端末内のみの旧構成が前提であり、
オンライン機能を有効化するビルドへそのまま流用しない。公開ページやストア申告は本工程で変更していない。
復旧用イベント照合のオフラインCLIも追加し、私有ファイル権限・symlink・余分な入力・上書きを拒否。
追加後のbackend全309テスト成功。外部health=200、公開対象外のroot=503を再確認。
外部バックアップはユーザー指定で30日保持と決定。削除せずに期限超過候補を判定するツールと10件の試験を追加。
対象はdb/の暗号化DB世代のみ。削除履歴・復号鍵・Mac/USBには適用しない。
再ログイン後、管理画面にprivateのDBバックアップ27件を確認。表示範囲は全て30日以内。
管理画面で保持設定/世代状態は確認できず、読み取り専用API調査を実施。
この読み取り調査の段階では期限切れ削除、bucket設定変更、自動削除の有効化は行っていない。
API調査の7件を含めbackend全326テスト成功。VPSへ転送した2ファイルのSHA-256一致も確認。
ユーザー実行の`retention-inspection.ti1Ndv`を確認。2026-09-25 23:53 JST時点で
GET全ページ取得完了、Versioning=NeverEnabled、db/27件、30日超過候補0件。
直近23:15 JSTの読戻し検証済みバックアップと一致し、RETENTION_REVIEW_READY、Blockers=[]。
ObjectsDeleted=0、設定変更なし。これは候補判定までであり、自動削除の反映完了ではない。
30日選択時には「回答だけでは既存ファイルを削除しない」と説明済み。
2026-09-26、対象と不可逆な効果を明示した確認へのユーザー「ok」で、30日超の外部DB世代の自動削除を承認。
固定db/・書込み側との共通lock・新しいバックアップ検証・対象再確認・削除監査・異常停止を実装。
失敗やtimer停止/成功記録の鮮度切れは、既存の削除worker監視を保ったままhealthに統合する。
新しい恒久sudo権限は追加せず、専用.commandで本番反映・初回実行・監視確認まで実施。
古いバックアップの実DELETEは、期限超過候補がないため未実施。
backend全348テスト成功。期限切れ/対象外prefix/異常停止/監視に加え、固定URL・認証失敗時の無再試行・配布hashを検証。
7ファイルを`/home/ubuntu/nasus-deploy/20260926-retention-01`へ転送し、全SHA-256一致を確認。
「バックアップ30日保持の有効化.command」が実行入口。成功結果はreports/retention-install.*へ自動保存する。
現状の候補0件では実期限切れDELETEの権限・プロバイダー挙動までは実証しない。初回期限切れ時の失敗は監視対象。

2026-09-26 00:11 JST、ユーザー実行の`retention-install.Jdwwgt`でRETENTION_INSTALL_VERIFIEDを確認。
RetentionDays=30、CandidateCount=0、ObjectsDeleted=0、RetainedCount=27、AutomaticHealthUpdate=true。
続くSSH確認でもretention timerはenabled/active、service Result=success/Exit0。
次の定期実行予定は00:40:09 JST（この確認時点ではまだ後続retention実行の観測前）。
既存health/deletion/offsite timerはactive。外部GET /healthz=200、GET /=503で非公開状態を維持。
この作業はVPSで動作しMacの常時起動を必要としない。②-E全体・リリース完了ではない。

②-Eの削除履歴欠落対策: `deletion_inventory.py`を追加し、合成DBで18件の試験に成功。
稼働DBの単一read snapshotから全outbox状態を含む期待一覧を生成し、player/購入の削除記録と照合する。
以前の独立保管一覧/hashに対する欠落・置換・巻き戻りを拒否。元DBの行は不変、上書き/復元候補は拒否。
生成した一覧は既存のオフライン復元照合ツールと互換。本番workerやDB、クラウドへの変更はなし。
この一覧生成小工程の実装・試験は完了。下記で外部保存とACK連動のローカル実装を追加した。最新世代の独立確認は残る。
生成しただけでは外部保管や最新性の証明にならないため、②-Eを完了扱いにしない。
追加後のバックエンド全366テスト成功、失敗0・スキップ0。今回の変更はローカルコードと運用記録のみ。

②-E追加: 暗号化一覧の外部PUT/GET照合と削除完了ACKの連動をローカル実装。全390テスト成功。
送信前に暗号文・世代・送信予算を永続化し、応答喪失後も同じ内容で再開する。
当該イベントを含む一覧の確認前には削除完了を返さない。既存headの欠落・巻き戻りは上書きせず停止。
実ageでの暗号化・復号と復元用照合、workerのACK順序・失敗時pending維持も試験済み。
本番用15ファイルを`20260926-inventory-01`へ配置し、全SHA-256一致を確認。その後、下記のユーザー実行で有効化済み。
`削除履歴一覧の外部保存.command`が固定hashの反映入口。ユーザーのsudo認証1回が必要。
空baselineのみ許可し、外部読戻し・後続自動worker実行・統合health・非公開ルート維持を確認する。
失敗時は旧workerへ戻し、新しい証跡を保持。実アカウントの削除やDB schema変更は行わない。
認証後の結果はreports/inventory-install.*へ自動保存。成功未確認の段階で②-Eを完了扱いにしない。
クラウドheadの保存だけでDB喪失時の「最新」完全性を証明したとは扱わず、復元候補の隔離を維持する。

2026-09-26 00:31 JST、`reports/inventory-install.B5FFM8`のINVENTORY_WORKER_INSTALLEDを確認。
外部一覧の初期保存・読み戻し照合、AutomaticWorkerRun=true、HealthHealthy=true、GameRoutesClosed=true。
Sequence=1、InventoryEvents=0、RealAccountDeleted=false。実アカウントの削除試験ではなく空baselineでの有効化。
別途SSHで新しい60-inventory.confの適用、worker success/Exit0、timer enabled/activeを確認。
外部health=200、root=503。health/retention timerもactive。
この小工程（一覧外部保存とACK連動の反映）は完了。LatestDisasterCompletenessProven=falseは維持。
②-Eの残りはDB喪失時の最新集合の確認、記録種別ごとの保持/消去方針、復旧手順と利用者向け説明の整合。
retentionの初回後続定期実行は00:40台予定で、この確認時点ではまだ観測していない。

②-Eの復旧資料照合を接続: `deletion_recovery_evidence.py`を追加。
独立したhead hashから暗号文・復号一覧・全イベントを照合し、隔離復元用checkpointを生成する。
8件の追加試験、実age暗号化/復号との接続を含めbackend全398テスト成功（失敗0・スキップ0）。
旧世代・改変・入力権限・欠落・余分なイベントを拒否。新規ファイルのみ生成し、DBや本番設定を変更しない。
配布許可リストと復旧手順を更新。ユーザーの追加認証操作は不要。
最新性そのものの証明とは区別し、DB喪失で未送信履歴を否定できなければ公開せず停止する判断表を追加した。
復旧資料の照合処理は完成。②-E全体の残り（最新集合の保証範囲と保持/消去方針の確定、説明の整合）は継続中。

2026-09-26 00:42 JST追記: retentionの後続定期実行を確認。
timer LAST=00:40:56、service Result=success/Exit0。その後health=00:42:08 success/Exit0、外部health=200/root=503。
手動でtimerを起動せず観測した。実期限切れDELETEの検証とは区別する。
オンライン版プライバシー説明案と`PRIVACY-DISCLOSURE-REVIEW.md`を追加。公開・ストア申告は未変更。
現在のiPhone defineは広告/課金有効、オンラインBaseUrlは空。8月の「収集なし」は流用不可と明示した。
Apple/Googleの公式案内とコードを照合し、保存期間未確定の項目や広告収集を断定しない別案にした。
②-Eの文書作成と実装対応整理は完了。方針確定・対象ビルドでの整合確認は未完了。
③の実装調査・検証は並行可能。Apple未解決交換/失効の運用条件は③と共通の公開前ゲートとして扱う。
今回は文書のみの変更で、直前の全398テスト成功を継承（新たなコードテストの実行ではない）。

## ③の追加進捗（2026-09-26）

`apple_revocation_worker.py`で既存の暗号化失効queueをまとめて処理し、件数のみの監視結果を返す処理を追加。
provider失敗・誤鍵・クラッシュ後のリース回収、時間/件数枠、結果不明交換の保全、復元隔離拒否を確認。
単回認証コードの再送・未解決fenceの解除は行わず、要確認状態を明示する。
新規17件を含むbackend全415テスト成功、失敗0・スキップ0。
詳しくは[Apple解除worker](APPLE-REVOCATION-WORKER.md)参照。

③-A 解除処理のバッチ化・滞留検出のローカル検証は完了。
③の残りは、本番鍵/定期起動/監視への接続、結果不明交換と通知/資格情報失効の扱い、実Apple+iPhoneの通し試験。
②-Eの方針確定と公開前説明の整合も未完了のまま追跡する。
実Appleアカウント・VPS・本番DB・公開ルート・端末セーブへの変更はなし。今回はユーザーの認証操作不要。

現在地: **③-B 明示設定・実行入口・定期起動テンプレートのローカル実装/検証**。
既存DBだけを開き、鍵/アプリ/instance不整合・復元隔離・schema欠落を拒否する入口を追加。
通常は読取り確認のみ、明示的な実行オプションで失効queueを処理する。
私有lock/結果ファイル、実行時間上限のunit、service/timerを併せた監視判定と配布対象を用意。
監視判定の本番統合とLinux上のunit動作確認は未実施。秘密情報は生成・転送・表示していない。
③の本番反映・実Apple/iPhoneの確認が終わったという意味ではない。
追加14件を含むbackend全429テスト成功（失敗0・スキップ0）。実ageの既存復旧試験も含む。
③-Bの上記ローカル実装/検証は完了。本番の設定・鍵配置と監視統合は未実施。

③-B追記: Apple連携・解除・アカウント削除と外部履歴ACKを組み立てる別factoryを追加。
通常の本番入口は無効のまま。DB初期化をせず、既存schema/instance/鍵の一致を検査する。
解除HTTP→永続queue→別worker→模擬providerの通し試験、削除の外部ACK待ちなど9件を追加。
詳細は[Apple API接続の検証](APPLE-APPLICATION-ASSEMBLY.md)。
VPS反映・Appleの設定変更・実Apple通信・公開ルートの変更はなし。
backend全438テスト成功（失敗0・スキップ0）。③-BのAPI接続部分のローカル検証まで完了。
SSHによる読取り確認でApple解除service/timerはLoadState=not-found、
`/etc/witch-player-apple`と`/var/lib/witch-player-apple`も未作成だった。
予定された場所には本番設定・workerがまだ存在しない。別の場所に鍵があるかは探索していない。

## ③-C Apple Developer側の設定（2026-09-26）

Team ID `687D767B8W`、Bundle ID `com.nasus.dungeonmonsterroguelike`がUnity設定と一致することを確認。
ユーザー承認後、当該App IDのみSign In with Appleをprimary App IDとして有効化し保存した。
Appleの「関連する既存provisioning profileが無効となり再生成が必要」という警告についても
別途ユーザー承認を受けてConfirmを実行。保存後に一覧から再度開き、チェックON/Save無効で永続化を確認。
署名鍵の作成・ダウンロード、profile再生成、本番設定/DB、公開ルートは変更していない。
現在地は③-CのApp ID有効化完了。専用鍵の安全な用意/配置、profile更新と実機確認は未完了。

2026-09-26 12:01 JST追記: ③-Cの専用キー発行・Mac保管を完了。
最初の未使用キー `2MMYR94Y3W` はダウンロード実体を確認できなかったため、ユーザー承認を受けて失効。
Safariから再発行した `38GM3RYVD9` はSign In with Appleのみ、上記primary App IDに関連付け。
Apple側のキー一覧への登録と、Macへの実ファイル保存を確認した。
保存先は `/Users/andou/Library/Application Support/NasusApple/keys/AuthKey_38GM3RYVD9.p8`。
Git管理外、親フォルダ700/秘密鍵600。内容を表示せずP-256形式とローカル署名/検証を確認。
秘密鍵はVPSへ未転送。本番設定・DB・公開ルートは未変更。
③-Cの残り: provisioning profile更新、専用鍵の本番配置/設定、実AppleとiPhoneの通し確認。
キーの独立した安全なバックアップは未実施。キー保管完了を③全体の完了とは扱わない。

③-C追記: 既存App Storeプロファイル `Dungeon Monster Roguelike App Store` のInvalid状態を確認し、
同じApp ID・選択済みDistribution証明書のまま再生成。証明書の追加/失効は行っていない。
Safariで取得した実ファイルのCMS内容を解析し、Team/Bundle一致、Apple Sign In=Default、
get-task-allow=false、有効期限2027-08-24を確認した。
新UUID `ed73b6b4-1830-48d0-9bee-0a21741f88ec` をXcode UserData/Provisioning Profilesへ配置、
ダウンロード元とのバイト一致を確認。既存ローカルprofileを上書き・削除していない。
これはApp Store配布用profileの更新であり、開発用profileの更新や署名付き実機ビルドの成功ではない。
iPhoneはxctraceでOffline。VPSの既存serviceはactive/success、Apple workerは未配置。
SSH接続は成功したがsudo -nはパスワード要求で停止。本番鍵転送・DB/schema・serviceの変更はなし。
③-Cの残りは本番側の認証を伴う設定/配置と開発署名・実機接続テスト。管理者認証を回避しない。

本番Apple設定の読取り確認コマンド `run-apple-preflight-mac.sh` を用意。
ユーザーがターミナルで一度sudo認証し、rootではなくwitchplayerとして検査する。
検査コードのSHA-256固定、Python隔離モード、SQLite mode=ro/query_only、出力量を限定。
キー/環境変数/プレイヤー内容を読み出さず、定義済みtableの列有無・件数・整合性、
既定パスの所有権/権限、service状態のみ報告する。恒久sudo許可・鍵転送・schema移行はなし。
結果は `deploy/reports/apple-configuration.*` に自動保存。合成DBの新規6テスト成功。
本番検査の実行・成功はユーザー認証待ちで、まだ確認していない。

2026-09-26 12:16 JST追記: `reports/apple-configuration.pWqSNE` を読み、
APPLE_CONFIGURATION_INSPECTEDを確認。DBのquick_check/外部キー検査は成功、view/triggerなし。
Apple用6tableの必要列検査はすべてfalse（table不存在と列不足はこの出力だけでは区別しない）。
既存deletion_journal_metaは1行、deletion_outboxは0行で必要列あり。
既定の署名鍵・token暗号鍵・worker設定・状態ディレクトリはPresent=false。
Apple service/timerはLoadState=not-found。既存witch-player.serviceはloaded/active/success。
検査自体は成功したが、Apple連携の稼働確認ではない。本番側の構成追加が必要と確定した。
AppleRequests=0、DatabaseChanged=false、ConfigurationChanged=false。
次の反映は検証済みバックアップを前提に、必要なschema・秘密鍵/暗号鍵・worker/監視を
まとめて準備し、ゲーム公開ルートは閉じたまま検証する。署名鍵を通常の配布アーカイブに含めない。

③-Cの本番下準備パッケージを作成。詳細は[配置範囲と停止時の扱い](APPLE-STAGING-2026-09-26.md)。
追加14件を含む全458テスト成功。VPS既存venvにはcryptographyがなく、OSのPyJWTも旧版だったため、
既存APIを更新せず `/opt/nasus-apple-20260926` に独立環境を配置する構成とした。
署名鍵/暗号鍵は別送・私有配置。暗号化外部バックアップとローカルsnapshot検証後、
空の6テーブルだけを単一トランザクションで追加し、読取りruntime確認までを認証1回で実行する。
配布先 `/home/ubuntu/nasus-deploy/20260926-apple-stage-01` へコードとwheelのみ転送しhash一致を確認。
ubuntu所有の試験venvで依存関係・import・検証器構築に成功。実Apple通信/本番DB変更なし。
この時点では本番鍵の転送・暗号鍵生成・本番配置は未実行。`run-apple-staging-mac.sh` のユーザー認証待ち。
serviceテンプレートを独立環境向けに更新したが、service/timerの配置・起動と監視統合は別の有効化工程。

2026-09-26 15:46 JST: `reports/apple-staging.3THAO4` の `APPLE_STAGING_COMPLETE` を確認。
AddedEmptyTables=6、BackupVerified=true、ConfiguredRuntimeChecked=true。
既存行・既存API環境・公開ルート設定は変更なし。AppleRequests=0、WorkerEnabled=false。
本番の空schema・署名鍵/暗号鍵/設定・独立実行環境の配置と読取り検証が完了した。
その後のSSH確認で既存witch-player.serviceはloaded/active/success。
Apple service/timerはnot-found/inactiveで、意図どおり自動処理は未登録・未有効化。
現在地は③-C「本番構成の下準備完了」。残りは鍵の独立保管・復元確認、定期処理/監視統合、
APIの非公開検証接続、開発署名と実Apple/iPhoneの通し試験。③全体の完了とは扱わない。

進捗報告では現在地・終了条件・確認できた結果を示す。「次は〜します」だけで止めず実作業を続ける。
新たな不具合や工程の追加は、どの終了条件に影響するかと合わせて説明する。
ユーザー要望: 通常の修正・診断・検証で「進めてよいか」を繰り返さず続行する。
こちらで代行できない認証など、実際にユーザー操作が必要な場合のみ依頼する。

③-C追記: 署名鍵とtoken暗号鍵を既存Nasus recipientへ暗号化し、Mac内で保存前後の
復号・内容一致を検証。平文アーカイブ作成・鍵上書き・外部送信はなし。
保存先は `NasusBackups/apple-keys-4ap0esb1/recovery`。USB未接続につき独立保管は未完了。
既存バックアップ/削除/retentionの判定を保ったApple監視collectorと、正常な実行中を
期限付きで判別するmarkerをローカル実装。追加15件を含む全473テスト成功、skipなし。
本番runtime・service/timer・監視には未反映。既存API active/success、healthz=200、root=503。
詳細と反映の終了条件は[鍵復旧と監視接続](APPLE-RECOVERY-AND-MONITORING.md)。

③-C追記: ユーザーがUSBを接続。既存の承認済みUUIDを照合し、専用新規フォルダへ
暗号文・検証情報の4ファイルをコピー、sync後の全バイト一致とUSB内の既存identity
による復号・元の2鍵との一致を確認。鍵の独立保管と復号検証が完了。
既存USBファイル・本番設定・DBへの変更なし。定期処理/監視の本番反映は未実施。

③-C追記: 本番の旧コードhashとApple unit未登録・既存監視drop-inを再照合。
`activate_apple_worker.py` と `run-apple-activation-mac.sh` を作成し、別ディレクトリへの
worker配置、既存監視の維持、自動起動の確認、停止時復帰を一度の認証で実行する構成にした。
空のApple 6テーブルを開始前後に確認し、公開ルート・API設定・鍵・schemaを変更しない。
全481テスト成功。コードのみ `/home/ubuntu/nasus-deploy/20260926-apple-activation-01`
へ転送し、Linux上でmanifest hash・構文・import一致を確認。本番配置はまだ行っていない。
ユーザーによるVPS管理者認証が必要。既知のsudo制限を回避せず、固定範囲のコマンドを案内する。

2026-09-26 16:42 JST: `reports/apple-activation.3Qkx2F` で初回空queue実行まで通過し、
step3（timer有効化・監視合成）で停止。`APPLE_ACTIVATION_ROLLBACK_VERIFIED` を確認。
現在の読取り確認: Apple service loaded/inactive、timer disabled/inactive、既存health成功、
drop-inは40/60/70のみ。外部healthz=200、root=503。新しい定期処理は稼働していない。
終了理由の詳細は保存されておらず、原因は未確定。元installerの再実行はしない。
root保護されたmarkerと既存collectorの読取り診断を用意し、コードのみ別領域へ配置済み。
`run-apple-activation-diagnostic-mac.sh` は起動/有効化/DB変更/health publishを行わず、
私有markerのメタ情報、固定reason、既存collectorの判定とhook有無を収集する。
停止後timerの状態と、仮にenabledだった場合のmarker単体分類を明確に区別する。
追加3件を含む全484テスト成功。診断自体の実行はユーザー認証待ち。

2026-09-26 16:46 JST: `reports/apple-activation-diagnostic.BM72gK` を確認。
配置コードhash一致、Apple markerは正常完了・権限正常、既存監視Healthy=true。
新hookの有効化もrollback時退避もなし。停止はhook作成前のstep3内。
停止後timer disabledと古いmarkerによる現在のAPPLE_RUNTIME_REJECTEDは期待される状態で、
当時の停止理由そのものを特定した証拠ではない。仮定したenabled状態での過去marker分類は成功。

起動/終了の途中でunitとmarkerの観測が混在して失敗するケースを合成テストで再現。
`apple_integrated_health_v2.py` は前後のservice/timer情報を照合し、遷移中のみ上限3回の
読取り再観測を行う。安定した異常・無効timer・期限切れを正常に変更しない。
専用再開手順は配置済みv1全hash・停止状態を検証し、自動worker完了後に候補監視を
publishせず検査してから別ディレクトリのv2を接続する。v1配置物・API・鍵・schemaは維持。
失敗時は固定stage/reasonを出力し既存監視へ戻す。元installerの再実行ではない。
追加5件を含む全489テスト成功、Linux構文/hash照合済み。
`run-apple-activation-resume-mac.sh` のユーザー認証待ち。本番へのv2反映は未実施。

2026-09-26 16:55 JST: `reports/apple-activation-resume.5lgWub` が
APPLE_WORKER_ACTIVATION_VERIFIEDで完了。AutomaticWorkerRun=true、
AutomaticCombinedHealth=true、AppleTablesStillEmpty=true、GameRootClosed=true。
SchemaChanged/KeysChanged/LiveApiConfigurationChangedはすべてfalse。
続くSSH確認でもApple timer enabled/active、service Result=success/Exit0、health成功、
80-apple-worker.confが既存40/60/70に追加されていることを確認。外部healthz=200、root=503。
③-Cのサーバー定期処理/統合監視接続は完了。空queueでの実行であり実Apple失効成功ではない。
残りは非公開APIの検証接続、実Apple認証・iPhone引継ぎ/解除/削除の通し試験など。
xctraceではiPhoneがOffline。UnityのBaseUrlは空、ExperimentalAppleLinkingは有効化されていない。
公開ビルド/本番APIの切替は未実施。iPhone接続と開発用設定・署名の準備が必要。

2026-09-26 17:11 JST: iPhone 15がdevicectlでpaired/wired/connected、Developer Mode有効と確認。
端末には対象bundleの1.0(2)が導入済み。アプリの入替え・起動・アンインストールは行っていない。
Documents（現行save.json、bak、historyを含む）、Library/Preferences、Library/Application Supportを
Macの私有ディレクトリ `NasusBackups/iphone-before-apple-20260926.l5VUhY` に退避。
Documentsを再取得して全ファイルのバイト一致をdiffで確認した。端末への書込みなし。
これはアプリファイルの退避であり、Keychainを含む端末全体バックアップや復元試験ではない。

旧開発profileにはApple Sign In entitlementがなかったため、既存App ID・既存Mac開発証明書・
登録済みiPhone1台だけを使用する `Dungeon Monster Roguelike Apple QA 20260926` を生成。
UUID `51fa7b36-f48d-4578-9f06-851e7688982e`。ダウンロード後、CMS内容を読取り、
対象team/bundle、get-task-allow=true、Apple Sign In=[Default]、接続UDID、証明書SHA1を照合。
Xcode UserData/Provisioning Profilesへ新規UUID名でコピーし、元ファイルとの一致を確認。
既存証明書/profileは失効・上書きせず、新しい秘密鍵や端末登録も作成していない。

Unity 6000.3.11f1でAppleAccountRecoveryTests/AppleAccountTransportTests/
OnlineBuildConfigurationTestsの35件成功（失敗0、skip0）。結果は
`reports/apple-device-preparation-tests-20260926.xml`。
③の端末接続・セーブ退避・開発署名準備まで完了。実機QAビルドの生成/導入、
非公開APIの検証接続、実Apple認証/引継ぎ/解除/削除はまだ未確認。
BaseUrlは空、ExperimentalAppleLinking未有効の状態を維持。本番API/ゲーム公開設定への変更なし。

2026-09-26追記: ③の実機接続用に、24時間上限・追加キー必須・管理経路遮断のQA gatewayと
開発限定クライアント接続を実装。通常factoryは変更せず、別service/loopback portへ分離する。
サーバー配布コードとwheelのhash/importをubuntu所有の隔離準備領域で確認。
backend全503件、Unity関連43件成功。実Apple通信・本番反映・端末アプリの入替えは未実施。
`run-apple-qa-mac.sh` がVPS管理者認証1回で配置/失敗時復帰/HTTPS境界検証まで行う。
現在はユーザー認証待ち。[限定API接続の範囲と手順](APPLE-DEVICE-QA-2026-09-26.md)に詳細を記録。
Xcodeの実機用ビルド成功、codesignの厳格検証とApple entitlement/開発profile/接続UDIDの一致も確認。
検証アプリはMac内に完成済み。VPS反映・実機導入・実Apple操作とは区別する。

2026-09-27追記（③ 限定QA API接続で停止）:
`reports/apple-qa-install.ESGYAk` でstage=proxy、rollback=False。
新QA serviceはinactive、既存APIとCaddyはactiveだがCaddy ReloadResult=exit-code。
原因となるコード不備を確認: umask077下のos.open(mode=0644)は0600になり、
root所有Caddyfileをサービスユーザーcaddyが読めない。rollback側も同じ不備。
実ファイルmode=0600 root:rootを確認。保護された現行内容のhashは未確認。
外部healthzは一度503を観測、その後200へ回復。これを権限修復済みとは扱わない。
初回配置wrapperは再実行禁止に変更。失敗した配布物・reportはそのまま保存。
修復専用処理は旧設定の既知hash/現行内容照合→0644明示→caddy権限でvalidate→reload→
既存health/root/admin確認。その後だけ配置済み全コードhash・service・gate期限を照合し
QA再開とHTTPS境界試験を実施する。失敗時は元設定へ復帰しQAを停止する。
DB/鍵/パッケージ再配置/期限延長/ゲーム公開は行わない。実行はユーザーのsudo認証待ち。
現在地: ③ Apple実機検証の入口復旧。次の完了条件は限定API接続→実機認証/引継ぎ/解除/削除。
以降は④課金Sandbox、⑤実機総合、⑥ストア申請、⑦公開。署名済み検証アプリは準備済み。
修復コード/旧helperを別のubuntu私有領域へ配置し、Linuxで3ファイルの構文と固定hashを照合済み。
mode回帰・未知設定拒否・期限切れ拒否などを含むbackend全509件成功。
最後の外部health=200。修復そのものは未実行で、caddy設定の権限はまだ変更していない。

2026-09-27 00:06〜追記: `apple-qa-recovery.ByifQ2` でAPPLE_QA_RECOVERY_VERIFIED。
後続SSH確認でcaddy reload成功/設定0644、QA active、health200、キーなしQA404。
00:10の認証付きQA healthも200。SchemaChanged/KeysChanged=false、PublicGameClosed=true。
③の限定API接続は完了。実Apple認証/引継ぎ/解除/削除は未確認。
iPhoneの現行セーブをMac私有領域 `iphone-pre-qa-install-20260927.dwuhZc` に追加退避し、
Documents再取得の一致を確認。署名済みQAアプリの上書き導入中（既存アプリ削除なし）。
00:11: 上書き導入成功。導入直後・初回起動前のDocumentsも退避と一致し、セーブ保持を確認。
起動は端末ロックで拒否されたため、ユーザーのロック解除待ち。実Apple認証は未実施。
2026-09-27: ユーザー実機画像でアカウント画面の表示を確認。背景透過の改善依頼に対応。
濃紺の不透明下地と既存image2枠、白文字、削除操作の赤系表示へ変更。関連65テスト成功。
iOS出力は成功したがXcode最終コピーはMac容量不足で停止。
続く不要ファイル削除依頼で古い中間生成物3箇所と導入済みアプリのインストーラー4個を削除。
空き116MiB→3.9GiB（約4.2GB）。写真ライブラリはOS保護で未アクセス・未削除。
ソース、ゲーム素材、完成アプリ、セーブ、バックアップは保持。中間生成物は再ビルドで再生成可能。
画面修正版のXcodeビルドを再開。端末への更新と外観の確認は別途記録する。
00:29〜00:30: 画面修正版のビルド/署名確認、iPhoneへの上書き導入、起動が成功。
更新前後Documentsの全バイト一致を確認。修正後の実機外観と実Apple認証は未確認。
更新後のMac空きは3.8GiB（約4.1GB）。
