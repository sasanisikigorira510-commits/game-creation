# Sandbox課金準備の確認

2026-09-28、サインイン済みApp Store Connectを読み取り確認。購入・再提出なし。

## 最新状況：01:52 Sandbox接続完了（以下は経過記録）

- `reports/iap-sandbox-connect.Vd7QhW` は全4工程完了、`SANDBOX_IAP_CONNECTED`。
  Environment=Sandbox、ProductionChanged=false、GateExpiryChanged=false。
- 01:54の `iap-sandbox-connect.qg6bes` は既存配置先を検出した事前確認（121行）で停止。
  成功後の二重実行を防止したもので、初回成功を取り消す失敗ではない。再実行不要。
- SSHで新実行領域 `/opt/nasus-iap-sandbox-20260928`、95-iap-sandbox.conf、
  QA active/running/Exit0を確認。本番サービスとQA maintenance timerもactive。
- 独立した外部GET確認: 通常health200、通常root/admin503、認証なしQA404、
  QA認証付きhealth200、QA認証付きadmin404。設定変更リクエストなし。
- 残件は実機Sandbox購入、再送/通信断、返金・取消対応と商品情報の照合。
  Apple API資格や実取引の成功は未確認。通常公開/本番課金は有効化していない。
  テストは復旧済みの2台目で行い、旧端末の無効化済み認証を再有効化しない。

## 01:31以降の更新（以下の初回確認より優先）

- ユーザーが「Nasus IAP Server」を作成し、秘密鍵をダウンロード。
  管理画面のアクティブ(1)、ダウンロード済み表示とMacのファイル存在を確認。
- 鍵本文を出力せず、EC P-256形式とローカル署名/検証の成功を確認。
  ダウンロードファイルの権限を所有者のみ読み書き（0600）に制限。
- 秘密鍵はリポジトリに格納していない。Apple API呼び出し、QA購入検証の有効化は未実施。
  鍵の存在だけではSandbox課金確認完了と扱わない。

## ユーザー許可後のVPS配置

- 新しい課金用秘密鍵を既存さくらVPSへSandbox購入検証用として配置する許可を得た。
- 既存SSH接続で `/home/ubuntu/.nasus-iap-sandbox-20260928/SubscriptionKey_2LX8L872Z8.p8`
  へ転送。新規専用ディレクトリ0700、鍵0600、所有者ubuntu、通常ファイル257 bytes。
  Mac側とのSHA-256一致を確認。鍵本文・ハッシュ値はログに出力していない。
- これは私有の配置準備領域であり、実行中のQAサービスにはまだ接続していない。
  サービス設定・DB・一般公開・本番課金は変更せず、sudoも使用していない。
- 残件: Sandbox限定の購入検証構成を実装・テストし、管理者権限で実行用領域へ配置する。
  現在のQA guardは購入検証用bundle設定を拒否するため、環境変数の追加だけで有効化しない。

## Sandbox専用接続パッケージ準備済み（管理者反映待ち）

- `qa_purchase_runtime.py` を追加。既存非課金QA/maintenanceのguardは変更せず再利用。
  同じ隔離DB、固定アプリID、Sandbox明示設定、所有者私有の鍵/設定/証明書、
  既存24時間QA gateを要求。管理API・削除APIの遮断は保持。
- `apple_verifier.py` は渡された設定を使用し、環境省略/誤記から本番へ暗黙移行しない。
  新鮮なApple応答の取引ID・アプリ・環境も照合し、失敗時は付与せず保留。
  `production.py` の設定引数伝達も修正したが、稼働中本番のコードは更新していない。
- Apple公式PKIのルート3種類を取得、CA属性・発行者・有効期間を検査。
  ダウンロード済み実鍵とルートによる公式ライブラリ構築を**ネットワーク禁止で**確認。
  実Apple取引・API資格の検証成功を意味しない。
- 独立Python 3.12環境で公式ライブラリ3.1.2と全依存を固定。
  全体571件実行、562件成功/9件スキップ。課金/分離/配置の選択テスト105件は全成功。
  実機購入・返金通知・通信断の実環境検証は未実施。
- VPS `/home/ubuntu/nasus-deploy/20260928-iap-sandbox-01` に秘密鍵を含まないパッケージを配置。
  全ファイルのSHA-256、サーバーPython構文検査を確認。
  manifest: `75fd8ec0cdf42088cdb83682a4f817d3cb21881ac684b0642497b6d790f08035`
  installer: `f7bd0010d16fea6f931aa64a577355859c55512130e776b751de9179e9d39e07`
- `run-iap-sandbox-mac.sh` は上記ハッシュを固定し、sudoによる1回限りの反映を行う。
  `/opt/nasus-iap-sandbox-20260928` に別実行環境を作成し、現QA DBの整合性確認済みスナップショットを
  保持してからQAだけを切替える。QA期限/公開ルート/DB構造/本番/maintenance設定を変更しない。
  切替え後失敗なら新drop-inを退避し旧非課金QAへ戻す。DBは巻き戻さない。自動再実行しない。
- **未実行**: 管理者パスワード入力待ち。`SANDBOX_IAP_CONNECTED` 出力と外部状態確認までは有効化済みと扱わない。

## 01:45反映試行は事前確認で停止

- ユーザー実行結果 `reports/iap-sandbox.07P8ug` は `SANDBOX_IAP_FAILED`。
  1/4の書き込み開始表示より前に停止し、理由の詳細は旧スクリプトに記録されていなかった。
- SSHで新実行領域/鍵の実行用領域/drop-inが未作成、QAが旧実行領域のままactive/running、
  通常サービスとQA maintenance timerもactiveであることを確認。
- 管理者所有の環境ファイルは通常SSHから読めないため、権限を迂回せず診断をユーザーへ引き継ぐ。
  **元の反映コマンドを再実行しない**。課金接続は未完了。
- `install_iap_sandbox.py` を読み取り専用preflightと反映に分離し、`--check` を追加。
  失敗時は例外型/コード位置/固定ファイルの所有者・権限のみ記録し、秘密の値を出力しない。
  診断モードで書き込み/サービス操作しない検査を含む配置関連7件成功。
- `run-iap-sandbox-check-mac.sh` を用意。ユーザーの管理者入力が必要。
  旧配置パッケージと旧反映ラッパーは保存し、未解明のまま再反映しない。

## 01:49読み取り診断に合格、接続の反映待ち

- ユーザー実行 `reports/iap-sandbox-check.VFdy6I` は `SANDBOX_IAP_PREFLIGHT_PASSED`。
  初回停止の原因は再現しておらず未確定。設定や鍵の誤りが原因だったとは断定しない。
- QAは旧実行領域でactive、QA maintenanceはResult=success/Exit0。
  VPS上の所有者私有の追加検査venvに、固定Linux wheelsだけをオフライン導入。
  `pip check` 成功、実鍵/Appleルート/Sandbox指定による公式SDK構築も通信禁止で成功。
  既存実行環境・サービス・DBに変更なし。Apple API呼び出し/購入もなし。
- 合格済みのチェック版installer（SHA-256 `2ce15b94dd658163f12dcdfe858da3d466828d94bce82337dc8ff01eb69198c4`）
  を固定する `run-iap-sandbox-connect-mac.sh` を用意。対象パッケージmanifestは変更なし。
  反映直前にも全事前確認を行い、不一致なら停止する。失敗時は安全なコード位置を記録。
  **接続用ラッパーはまだ未実行**。管理者入力が必要。

実装参照: https://github.com/apple/app-store-server-library-python/blob/main/README.md
ルート証明書配布元: https://www.apple.com/certificateauthority/

## 初回確認

- 対象アプリ: ダンジョンモンスターローグライク、数値ID 6804616218。
- 既存提出物に有償クリスタル120/650/2000/4200/8600/15000個の6商品あり。
  いずれも表示は「審査準備完了」。商品ID/価格/配信地域の一致はまだ未確認。
- ユーザとアクセス → 統合 → アプリ内購入: **アクティブ(0)**。
  課金検証用キー未作成。Appleログイン用キーとは別用途。
- 一般のApp Store Connect API画面はアクセス権リクエスト表示だったが、
  アプリ内購入画面にはキー生成ボタンがある。一般APIのアクセス申請は行っていない。
- 秘密鍵の新規発行・保存はユーザー操作へ引き継ぐ。キー本文はチャットに貼らない。
- サーバーの現QAは購入検証器を明示的に禁止しているため、鍵作成だけでは購入を有効化しない。
  Sandbox限定設定/証明書/公式ライブラリ/取引照合/返金取消対応の検証が必要。

## 既存審査指摘（2026-08-28、提出ビルド1.0(2)）

2.1 Information Needed。現在の開発ビルドの判定ではない。
Appleは実機で起動から主要機能を示す録画、試験機種とOS、機能と対象者、操作手順、
外部サービス一覧、地域差、必要な権利証明、購入対象と購入画面への導線の説明を要求。
録画は該当するログイン/削除/購入/権限許可も含める要求。返信・再提出は未実施。
最新版で課金・削除・主要動作を確認後、録画と審査説明を揃える。

管理画面: https://appstoreconnect.apple.com/access/integrations/api/subs
公式説明: https://developer.apple.com/help/app-store-connect/configure-in-app-purchase-settings/generate-keys-for-in-app-purchases
