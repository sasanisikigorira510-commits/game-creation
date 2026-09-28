# 小規模VPSへの配置手順

## 現在の段階

2026-09-25追記: VPS・DNS・TLS・外部バックアップ・監視は構築済み。healthのみ公開し、ゲーム/admin APIは閉鎖中です。
削除workerの自動実行と実さくらを使う合成削除・隔離復元試験まで確認済み。実Apple/課金・実機・保持方針等は未完了です。
最新の工程は[進捗表](deploy/RELEASE-PROGRESS.md)、復旧時の制約は[復旧・保持手順](deploy/RETENTION-AND-RECOVERY.md)を参照してください。
以下の初期構築コマンドは構築時の参考であり、既存VPSへの再実行手順ではありません。

月1,000円前後を優先し、まず **Ubuntu 24.04 LTS / メモリ1GB / 単一VPS / Caddy + Gunicorn + SQLite** を想定しています。さくらのVPSの公式仕様表では1GBが石狩880円、大阪935円、東京990円/月（税込、月払い）です。ドメインと外部バックアップは別料金です。申込時点の料金を再確認してください。年額契約は前提にしていません。

参考: [さくらのVPS仕様・料金](https://vps.sakura.ad.jp/vps_resource/pdf/spec.pdf)、[料金ページ](https://vps.sakura.ad.jp/specification/)、[Caddy公式インストール](https://caddyserver.com/docs/install)、[Gunicorn設定](https://gunicorn.org/reference/settings/)。

1台構成なのでサーバー障害・再起動・更新中はオンライン取引が停止します。通常のオフラインプレイは継続できます。利用人数の上限は未測定です。ユーザー数・DBサイズ・処理時間を見て増強を判断します。複数台や複数workerへの変更には、共有レート制限とDBの移行が必要です。

## 公開までに必要な情報

- VPSの契約先とSSH接続先。最初は1GBプランの月払いを想定。
- 公開用ドメイン（例 `api.your-domain.jp`）とDNS設定権限。
- TLS証明書の連絡先メールアドレス。
- バックアップを保管するサーバー外の保存先。課金を有効にする前に設定・復元検証する。

SSH秘密鍵・Appleキー・管理トークンをチャットやGitへ貼り付けないでください。以下の操作は、上記情報がそろった**新しい専用VPS**で行う配置手順です。この文書の作成によってサーバーの契約や変更が実行されることはありません。

## 1. 配置パッケージを作る

リポジトリのルートで実行します。

```sh
python3 PlayerDataServer/build_release.py --output /tmp/witch-player-server.tar.gz
```

許可したコード・設定・文書だけを梱包し、SHA-256一覧を同梱します。端末セーブ、`.data*`、`.venv*`、秘密鍵、管理トークン、実際の環境変数ファイルは含みません。同名の出力ファイルには上書きしません。

## 2. VPSの初期設定

Ubuntu 24.04 LTSの専用VPSを想定。公開ポートは80/443、SSHは管理者の接続元に制限し、8788は開放しません。Caddyは[公式のDebian/Ubuntu手順](https://caddyserver.com/docs/install#debian-ubuntu-raspbian)で導入し、セキュリティ更新を継続してください。以下はVPS上で実行します。

```sh
sudo apt-get update
sudo apt-get install python3-venv
sudo useradd --system --home-dir /var/lib/witch-player --shell /usr/sbin/nologin witchplayer
sudo install -d -m 755 /opt/witch-player/server
sudo install -d -o witchplayer -g witchplayer -m 700 /var/lib/witch-player /var/backups/witch-player
sudo install -d -o root -g witchplayer -m 750 /etc/witch-player
sudo tar -xzf /tmp/witch-player-server.tar.gz -C /opt/witch-player/server
sudo python3 -m venv /opt/witch-player/venv
sudo /opt/witch-player/venv/bin/pip install -r /opt/witch-player/server/requirements-production.txt
```

`/tmp/witch-player-server.tar.gz` は先にSSH/SCPで転送します。アプリコード・仮想環境はroot所有、DBとバックアップは専用ユーザー所有にします。コード更新で `/var/lib/witch-player` を消去・置換しないでください。

## 3. 空の本番DBと管理者を一度だけ作る

下記のインスタンス名と操作者名は運用時の値に置き換えます。インスタンス名は秘密情報ではなく、保存先の取り違えを検出する識別子です。ローカルの検証ユーザーは本番にコピーしません。

```sh
sudo -u witchplayer /opt/witch-player/venv/bin/python /opt/witch-player/server/maintenance.py init --data-dir /var/lib/witch-player --instance-id witch-production-001
sudo -u witchplayer /opt/witch-player/venv/bin/python /opt/witch-player/server/maintenance.py admin-add --file /var/lib/witch-player/operators-staging.json --name support-owner --role operator --token-out /var/lib/witch-player/support-owner-token
sudo install -o witchplayer -g witchplayer -m 600 /var/lib/witch-player/operators-staging.json /etc/witch-player/operators.json
sudo rm /var/lib/witch-player/operators-staging.json
sudo install -m 600 /opt/witch-player/server/deploy/server.env.example /etc/witch-player/server.env
sudo install -m 600 /opt/witch-player/server/deploy/caddy.env.example /etc/witch-player/caddy.env
sudoedit /etc/witch-player/server.env /etc/witch-player/caddy.env
```

`server.env` の `WITCH_INSTANCE_ID` は初期化で指定した名前に、`WITCH_PUBLIC_ORIGIN` は実際のHTTPS origin（末尾スラッシュなし）にします。`caddy.env` のドメインとメールも設定します。DNSのAレコードはVPSのIPv4へ向け、使用しないAAAAレコードは残さないでください。

管理トークンは作成されたファイルから担当者のパスワード管理ツールへ安全に移します。コンソール出力やログにはトークンを表示しません。各担当者に別々のトークンを発行し、共有しないでください。`operators.json` にはSHA-256ハッシュのみ保存します。

担当者追加は停止中に `maintenance.py admin-add` を実行してください（配置後の `/etc/witch-player` は書込み禁止なので、rootが一時ディレクトリにコピーして編集し、所有者 `witchplayer`・モード600で元へ原子的に置き換えます）。`viewer` は参照のみ、`operator` は凍結・補正・復旧が可能です。失効は該当エントリを削除して同様に置換します。少なくとも1人のoperatorを残してください。変更は次の管理要求から反映されます。

## 4. HTTPSとサービスを有効にする

```sh
sudo install -m 644 /opt/witch-player/server/deploy/witch-player.service /etc/systemd/system/
sudo install -m 644 /opt/witch-player/server/deploy/witch-player-backup.service /etc/systemd/system/
sudo install -m 644 /opt/witch-player/server/deploy/witch-player-backup.timer /etc/systemd/system/
sudo install -d -m 755 /etc/systemd/system/caddy.service.d
sudo install -m 644 /opt/witch-player/server/deploy/caddy-env.conf /etc/systemd/system/caddy.service.d/witch-player.conf
sudo install -m 644 /opt/witch-player/server/deploy/Caddyfile /etc/caddy/Caddyfile
sudo systemctl daemon-reload
sudo systemctl enable --now witch-player.service
sudo sh -c 'set -a; . /etc/witch-player/caddy.env; caddy validate --config /etc/caddy/Caddyfile'
sudo systemctl restart caddy
sudo systemctl enable --now witch-player-backup.timer
sudo systemctl start witch-player-backup.service
```

Caddyが証明書を取得してから `https://実際のドメイン/healthz` が200を返すこと、証明書とホスト名が正しいこと、管理画面の閲覧・権限・異常系を確認します。外部から8788へ接続できないことも確認します。Linux systemdと実DNS/TLSでの確認は配置時に必要です。

管理画面・APIは同じHTTPS originで提供します。Caddyが上書きした送信元IPだけを使用し、任意の転送ヘッダーを信用しません。HTTPのバックエンドへ直接アクセスしても本番APIは拒否します。管理トークンはUnityアプリに設定しません。

## 5. バックアップと障害時の復旧

毎時のSQLite整合バックアップを `/var/backups/witch-player` に保存します。SHA-256・サイズのmanifestとSQLite整合性検査に成功した世代のみ確定します。直近48世代と過去30日の各日の最終世代を残します。同一VPS内のコピーなので、**VPS消失への対策にはサーバー外のコピーが必要**です。

サーバー外への転送はSSH/SFTPや暗号化バックアップツールを使い、DBと対応する `.sqlite.json` を一緒に保管します。バックアップにはプレイヤーデータと認証情報のハッシュが含まれます。保存先の権限・暗号化・保持日数・容量を設定し、第三者が閲覧できるURLへ置かないでください。

```sh
sudo systemctl status witch-player-backup.timer witch-player-backup.service
sudo journalctl -u witch-player-backup.service --since today
```

任意の世代を検証・調査用ディレクトリへ復元する例です。

```sh
python maintenance.py verify --backup /secure-backups/players-TIMESTAMP.sqlite
python maintenance.py restore-copy --backup /secure-backups/players-TIMESTAMP.sqlite --new-directory /secure-recovery/incident-001
```

復元先は存在しないディレクトリを指定します。稼働DB・既存バックアップには上書きしません。復元されたDBには起動用のインスタンス識別子を作らず、`RECOVERY-PENDING.txt` を残します。**全体DBを過去へ戻してすぐ再開すると、その後の購入・ガチャ・補正台帳を失います**。障害時はオンライン取引を止め、稼働DB・WAL・ログ・バックアップを保全し、Appleの取引とバックアップ後の操作を照合してから復旧方針を決めます。バックアップだけでは最後のバックアップ以降のデータ損失を防げません。

単一ユーザーのバグ復旧には管理画面の差分確認と復旧登録を使います。DB全体の置換で対応しません。

`RECOVERY-PENDING.txt`があるデータディレクトリはproduction factoryも起動を拒否します。
この印はコピー前に作るため、途中で失敗した候補にも残ります。調査対象を公開しないでください。
独立保管した最新チェックポイントを使った[削除済みアカウントの復元除外](deploy/ACCOUNT-DELETION-RESTORE-2026-09-25.md)
を実装しています。外部outbox/workerと合成データでの通し試験は確認済みですが、最新削除集合の完全性管理などは残ります。
これだけで本番復旧・公開可能とはなりません。現行の制約は[復旧・保持手順](deploy/RETENTION-AND-RECOVERY.md)を参照してください。

## 6. 更新と運用

更新前にバックアップを作り、コードを置換する間は `witch-player.service` を停止します。DBのあるディレクトリには触れず、新しいコード・依存関係を配置してサービスを起動します。スキーマ変更時は別途移行と復元検証が必要です。

- APIログ: `journalctl -u witch-player.service`。要求ID、分類、HTTP status、所要時間のみ。ユーザーID・IP・トークン・購入レシートは記録しません。
- 基本制限: IP単位600要求/分、登録20要求/時、管理60要求/分、認証後プレイヤー120要求/分。IPv6は/64で集計します。共有回線の利用状況を見て調整します。DDoS対策を保証するものではありません。
- DB・バックアップ・ログの容量を監視します。プレイヤーの取引台帳とサーバーの保存履歴は現状自動削除しません。成長量の測定、容量警告、保持方針の合意を公開前に行います。
- バックアップ失敗、外部コピーの古さ、ディスク不足、5xx増加、HTTPS異常の外部通知は運用監視の工程で接続します。現在の設定だけで通知は送られません。
- 本番用DBの紛失、保存先の取り違え、設定不足、緩い秘密ファイル権限は起動を拒否します。稼働中にDBが消えた場合も空のDBを自動生成しません。

## 次の工程

1. VPS・ドメイン・外部バックアップ先を確定して配置、外部HTTPS・復元を検証。
2. Apple課金のSandbox実購入・再送・返金通知・本番環境への切替を検証。
3. Apple等のログイン連携、端末変更・再インストール時のアカウント復旧。
4. 障害・課金不整合・バックアップ失敗・容量警告の監視通知を接続して運用訓練。

本番で課金を有効にする前に、上記の外部バックアップ・アカウント復旧・監視を完成させます。
