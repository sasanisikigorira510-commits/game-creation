# 削除履歴ワーカー：追加導入の準備

## 最新：②-B/②-Cの連携確認完了（22:48台）

ユーザー実行結果`reports/deletion-integration-resume.DgFxVY`で
Status=DELETION_INTEGRATION_VERIFIED、LaterWorkerCompletions=2、LaterHealthRunVerified=true、
GameRoutesClosed=true、VerifiedUnix=1790344126.185721を確認。
終了後のSSH確認でもworkerはResult=success/ExecMainStatus=0、timer enabled/active。
healthは40番baselineと60番integratedのdrop-in構成、Result=success/Exit0。
22:47〜22:51の理由ログはHEALTHY。公開マーカーも更新されHealthy=true。
外部GET/HEAD /healthz=200、GET /=503。既存API/health/offsite/backup timerはactive。
以上から②-B/②-Cの終了条件を達成と記録。過去の全503の原因解明を意味しない。
RealDeletionTestComplete=falseであり、②-D合成データによる外部履歴〜復元再適用試験は次工程。
本番データの復元や実アカウント削除、ゲームAPI公開は行っていない。以下は準備・過去の経緯。

## ②-Cの再開準備：同時更新の誤判定を再現・修正（本番未反映）

現在のbaseline理由記録は継続してHEALTHY。削除timerは停止中、workerのResult=signalは
前回rollbackで停止したときの値で、今回新たに起動して失敗したものではない。
ローカルの2つの再現テストは修正前に失敗:
- collect開始1000、バックアップの原子的更新が1000.5、読み取り後1001。
- collect開始1000、unit照会中にworkerが1000.5で完了、マーカー読取後1001。
旧コードは開始1000と比較して両方を未来時刻と誤判定。新コードは各マーカー読取後の
現在時刻で比較する。テストで指定するnowは固定のまま。公開CheckedUnixは開始時刻を維持。
実際の未来、古い/false/privateでないマーカー、停止timer、失敗serviceは引き続き異常。
理由ログは固定コードと固定unit名だけ。公開JSONはHealthy/CheckedUnixの2項目のみ。
この再現は実装上の不具合の証明だが、21:49の503の唯一の原因である証明ではない。

`resume_deletion_integration.py`は手動sudo認証1回で有限の検証まで行う。sudoers追加なし。
旧helperのhash固定スナップショットから検査・empty queue確認・限定cap設定関数だけ再利用。
期限付き許可を復活・延長するものではなく、apply/authorizeの旧処理は呼ばない。
旧API/旧publisher/Caddy/workerコードは変更せず、新しいroot-owned監視スクリプトと60番drop-inを追加。
40番baseline観測drop-inは残す。再開するworker権限はCAP_SETUID/CAP_SETGIDのみ。
210秒を上限に後続worker完了2回と後続health更新を確認。90秒未満の完了扱いを禁止。
各確認で外部GET/HEAD200・root503、fresh private worker status、timer activeを確認。
最終的にコードhash・追加hook・empty queueを再検査しroot専用verified.jsonへ保存する。
失敗・中断時はworker timer停止と自分の完全一致drop-inだけを退避し、baseline理由記録へ戻す。
部分導入の再実行は拒否。DB巻戻し、実ユーザー削除、公開、秘密情報出力はしない。

新規13テストを含め全292件成功・スキップ0。古いfixtureのdatetime端数丸めによる
不安定さも、テスト時刻を整数秒に固定して解消（本番の未来時刻拒否は緩和しない）。
本番反映はまだ。ユーザー認証後にprivateのreports/deletion-integration-resume.*を読む。
転送先`/home/ubuntu/nasus-deploy/20260925-integration-resume-01/`の3ファイルhash一致と
Linux Python構文検査、Mac用bash構文検査を確認。実行用.commandをFinderで表示。
coordinator: a518fddbc71b525a7ba1016522767071508c954b094a9e2eb5ee4ce00bcf19de
publisher: a42a6d7cb7eb49064b95babc7360d0927c18a2d192a320cc5dd180dc54decafb
helper: 51698a01a881d73b4b2b4a646038a4f48d003f2e7e6ed4b8bf4c52b4b3c32c83

## 22:22：理由記録の導入成功

ユーザー実行後の`reports/health-reason-install.SyIDw2`に
`HEALTH_REASON_LOGGING_INSTALLED`を確認。SSHでhealthの専用drop-in、Result=success/Exit0、
22:22:18の`NASUS_HEALTH_REASON={"Reason":"HEALTHY"}`と新しいHealthy=trueマーカーを確認。
外部からGET/HEAD /healthzは200、GET /は503。APIとhealth/offsite/backup timerはactive。
削除timerはdisabledのまま。ログ追加の完了であり、前回の異常原因の修正完了ではない。
22:23:18の次回timer自動実行でもHEALTHY/Exit0とマーカー更新を確認。
旧publisher・Caddy・API application.pyは固定hashと一致し、一時sudo許可も不在。

## 22:14画面受領後：読取診断正常、理由記録の追加を準備

保存された`backup-health-diagnostic.DQnfBG`を確認。normal-root/service-sandboxともに
OriginalCollect.Healthy=true、OriginalUnitsHealthy=true、PublishedMarker.Healthy=true。
レポートはroot0600、親0711、検証済みバックアップの経過約2810秒（2時間以内）。
全unit照会成功、SANDBOX_DIAGNOSTIC_EXIT=0。恒常的な権限障害はこの実行では再現しなかった。
追加のSSH読取でもhealth200、既存APIと3timerはactive、削除timerはdisabled/inactive。
21:45〜22:04のhealth journalは正常終了だけで、Healthy=falseの理由は記録していない。
今回の正常結果を、以前の503の原因解明・修正完了と扱わない。

`observe_backup_health.py`は旧publisherの固定hash・root所有権を確認し、同じcollectを1回呼ぶ。
元のrecent_successとsystemctl結果をそのまま返し、判定・時間・鮮度上限を変更しない。
public statusの項目は変更せず、元publishを使用する。journalには固定理由コードと対象unit名だけ。
理由はHEALTHY / BACKUP_REPORT_REJECTED / UNIT_QUERY_FAILED / UNIT_STATE_REJECTED /
REPORT_READ_SIZE_OR_PARSE_FAILED。例外本文、環境、オブジェクト名、DB内容、資格情報なし。
旧publisher異変・observer失敗時はexit1でマーカー更新を止め、既存の鮮度判定で異常となる。

`install_health_observer.py`は固定hashの新規root-owned observerと専用ExecStart drop-inのみ追加。
既存publisher・API・Caddy・DB・バックアップ設定を上書きしない。削除timerは停止を維持。
現在の正常状態・旧コードhash・drop-in不在を前提にし、再実行/部分導入は拒否。
反映失敗時は自分の完全一致drop-inだけを退避して元コマンドへ戻す。生成物は調査用に残す。
sudoersは追加せず、ユーザーが`監視の理由記録を追加.command`で一度認証する。
この時点では本番設定変更なし。異常の理由を観測可能にする準備であり、原因の修正ではない。
全279テスト成功・スキップ0。理由記録5件と導入/巻戻し5件を追加。
転送先`/home/ubuntu/nasus-deploy/20260925-health-observer-01/`の2ファイルhashとLinux構文検査成功。
observer SHA-256: fb715fe7d0e80e91543c4d91ddf5f6ac0f06184716951fbe7b39943bbab32218
installer SHA-256: 29ec0def53d3630ecbd15e8050b26a8c16b0f73525f0bdb374c37792260d696c
Finderで実行用ファイルを表示。実行結果はprivateの`reports/health-reason-install.*`に保存される。

## 監視診断の準備（管理者認証待ち）

21:57台に元のhealthがHealthy=true、HTTPS 200へ復帰したことを確認。
恒常障害とは限らず、権限・状態読み取り・鮮度のどの判定が失敗したかは未確定。
ユーザー承認に基づき、`diagnose_backup_health.py`で一括読取診断を準備。
元のpublishスクリプトは固定hashを照合しcollect/units_healthyだけ呼び出す。publishは呼ばない。
backup成功レポートは最大4097バイトを読み、status一致・時刻の妥当性・経過秒のみ出力。
オブジェクト名、hash、DB内容、curl資格情報、環境変数は出力しない。
systemctlはallowlist済み4項目のみ。stderrは本文を出さずbus/permission/memoryの有無に変換する。
通常rootと、現health serviceのNoNewPrivileges/PrivateTmp/ProtectSystem/ProtectHome/
AF_UNIX/64MiB制限を再現した一時unitを比較。DB・クラウド送信・永続unit変更なし。

`監視の読み取り診断.command`が固定hashを検証して単発実行し、
`deploy/reports/backup-health-diagnostic.*`へprivate保存する。今回はsudoersを追加しない。
この記録時点では診断は未実行。診断用の5テストに成功。

## 21:48–21:52 実行結果：②-B起動成功、②-C監視503で撤回済み

ユーザーの許可設定結果`deletion-maintenance-authorization.A1jr6h`を確認。
root helperのハッシュ一致と専用sudoers存在を確認した後、承認済み`apply`を実行。
実worker正常終了、空queue、timer有効化、health drop-in配置、初回のGET/HEAD200・root503と
root helper内の監視判定に成功。初回WorkerCheckedUnix=1790340510.049741。

次回timer実行（21:49:30）はResult=success/ExecMainStatus=0。
ただし`verify`は2回ともHTTP_CHECK_FAILED。GET/HEAD `/healthz`が503、
`/run/witch-player-health/status.json`はHealthy=false。healthサービス自体はexit0。
既存backup/offsiteのtimerはactive、serviceはResult=success/ExecMainStatus=0。

承認済み`rollback`を実行。最後のHTTP確認は失敗したが、
両drop-inがなくなり、health ExecStartが元のnasus-backupスクリプトへ戻ったこと、
削除timer disabled/inactiveをSSHで確認。API/health/offsite/backup timerは引き続き稼働。
元の監視でもHealthy=false/503が継続。新規監視だけが原因と断定しない。
旧healthスクリプトのハッシュは検査済み値のまま。バックアップ失敗/データ破損と断定する証拠なし。

承認済み`revoke`に成功、`/etc/sudoers.d/nasus-deletion-maintenance-20260925`の不在を確認。
追加許可は解除済み、設定は削除せずroot専用領域へ保管され復元可能。
本番DBの復元/置換、実アカウント削除、ゲーム公開は行っていない。
次: 元のhealth監視がfalseになる理由を、秘密情報を出さない読み取り診断で確認する。
現行helperの許可にその診断操作はなく、別途限定した管理者認証が必要。

## 以下は今回の反映前の準備記録

19:32の`deletion-minimal-capabilities.xTiIV2`で読取テスト成功を確認。
schema/queue確認とも成功、切替後UID=999/GID=988、CapEff/CapPrm/CapAmb=0。
DB変更・クラウド送信なし。永続設定はまだ未反映。

ユーザーは反復するコマンド貼付けを減らすため、今回の作業に限定したsudo許可を承認。
`run-authorize-deletion-maintenance-mac.sh`で最初の1回だけ手動認証する。
これは汎用root/shell権限ではない。root所有・SHA-256固定の
`/usr/local/sbin/nasus-deletion-maintenance-20260925`に対する
`apply / verify / rollback / revoke`の完全一致だけを許可する。
apply/verifyは許可作成から2時間で停止。期限後も安全な巻戻しと許可解除だけ可能。
パスワードを保存・取得しない。MacのTerminal画面操作制限も変更しない。

認証後にエージェントが行う固定処理:
1. API/Caddy/導入済みコードのハッシュ、既存サービス、空の削除履歴/queueを確認。
2. ②-B: CAP_SETUID/CAP_SETGIDに限定するdrop-inを追加、実workerを1回正常終了させる。
3. ②-C: timerを有効にし、削除workerの失敗/鮮度を既存health監視へ統合する。
4. 別の時点のverifyでtimerによる次回実行を確認する。
5. revokeで専用sudoersファイルをroot専用保管先へ移動し、この追加許可だけ解除する。

apply失敗時はtimer停止・今回のdrop-inを退避・旧healthコマンドを復元する。
既存DB、APIコード、Caddy、既存バックアップ設定は置換しない。
自動再実行・権限延長はしない。失敗/結果不明時は残した状態を確認する。
実アカウント削除・一般公開・②-D復旧訓練はこの許可の対象外。

固定処理/許可の新規テスト16件を含む全264件成功（スキップ0）。Linuxでvisudo構文検査とPython構文検査に成功。
貼付け不要の`サーバー作業の一時許可.command`をFinderで表示済み。
このファイルをユーザーがダブルクリックすると認証ラッパーを実行する。自動起動はしていない。
準備ファイルは`/home/ubuntu/nasus-deploy/20260925-deletion-maintenance-01/`へ転送済み。
この時点ではsudoersやhelperのroot配置は未実施、既存API/health/offsiteはactive。

## 19:05 実行後の状況（未完了）

19:27追記：保存結果`deploy/reports/deletion-security-context.ZnuVYs`では、
切替前からCapEff/CapPrm=`000001ffffffff7f`（CAP_SETUIDだけ欠落）、
CapBnd=`000001ffffffffff`、AppArmor=unconfined、NoNewPrivs=1。
CAP_SETUIDが許可上限にあることと、実際に有効であることは異なる。
capshのdecodeでも欠落を照合済み。権限が失われる上流の理由は未確定。

ユーザーは限定的な権限変更の一時テストを承認。
`run-deletion-capability-test-mac.sh`は永続unitを変更せず、一時serviceだけに
CapabilityBoundingSet/AmbientCapabilitiesをCAP_SETUIDとCAP_SETGIDに限定する。
NoNewPrivileges/ProtectSystem/ProtectHome等は維持。起動時に有効/許容/上限が
0xc0であることを確認し、不一致ならDB確認前に停止する。
UID切替後に実効・許容・ambient権限が残る場合もDB確認前に停止する。
4件の診断テストに成功。実サーバーでの限定権限テストは手動実行待ち。
結果は`deploy/reports/deletion-minimal-capabilities.*`へprivate保存。

19:22以降追記：ユーザー画面で`BoundaryPhase=setuid, PermissionError, Errno=1`を確認。
読み取り調査ではserviceのCapabilityBoundingSetにCAP_SETUID/CAP_SETGIDあり、
PrivateUsers=no、SecureBits=0、RestrictSUIDSGID=no、個別AppArmorProfile/追加drop-inなし。
kernelログの該当期間にDENIED記録なし。PID1のCapEff/CapPrmも対象bitを含む。
これだけではworker実行時の有効権限を証明しないので、NoNewPrivileges等を変更していない。
診断を補強し、権限切替前/setgroups後/setgid後の許可リスト済みproc情報のみを返す。
環境変数・コマンドライン・資格情報は読まない。別名`diagnose_deletion_worker_context.py`として転送。
Mac用コマンドは新規privateファイル`deploy/reports/deletion-security-context.*`へ出力を保存する。
今後はターミナル画面の読み取り権限を迂回せず、この診断が新しく生成した結果ファイルを確認する。
本番設定は変更せず、ユーザーの手動実行待ち。

19:10追記：読み取り診断は`database_schema`段階で、workerの
`as_database_user`94行（子から`OK: false`）を報告した。診断側のcallback例外捕捉より前の
alarm/setgroups/setgid/setuid/umask境界の失敗が残っているため、操作別の型・errnoを返す診断に補強。
元診断は保持し、別ファイル`diagnose_deletion_worker_boundary.py`として転送。
手動実行は未実施。権限制限が原因と断定せず、NoNewPrivileges等は変更しない。
境界エラーの機密本文を出さない2件のテストに成功。

ユーザーの実行は手順4の初回worker起動で停止。`CalledProcessError`。
SSHで`systemd-analyze verify`は成功、worker serviceはexit-code/1、timerはinactiveを確認。
APIはactive、既存health/offsite timerはactive、旧healthサービスの最終実行も成功。
追加health drop-inはなく、HTTPS `/healthz`は`{"Status":"ok"}`。
画面上はバックアップ・プローブ・追加スキーマまで完了し、スナップショット等は保管済み。
この段階で元の導入コマンドを再実行しない。原因はまだ未特定。

`run-deletion-diagnostic-mac.sh`を用意。元workerと同じsandbox制約の一時サービス内で、
claim/送信/ack/status書き込みを無効にしてスキーマ確認とqueue healthの読み取りを再現する。
エラーは型・errno・ファイル名/関数/行番号のみ出力し、例外本文/変数/行データは出力しない。
手動sudo認証待ち。永続unit/API/DBスキーマ/クラウドオブジェクトは変更しない。
root確認スクリプトはPrivateTmpに隠れない`/run`の専用一時ディレクトリへ置き、hash検証する。

---

2026-09-25。事前確認のユーザー画面では既存DBの整合性検査は成功、
削除記録用テーブルは未作成。今回は本番APIを置換せず専用の処理を追加する。
**この文書作成時点では未導入。サーバーへのファイル転送は実行許可や導入完了を意味しない。**

## 手動導入

Macのターミナルで次を実行する。要求されたVPSのsudoパスワードは
ターミナルだけに入力し、チャットには送らない。

```sh
bash /Users/andou/Desktop/game-creation/PlayerDataServer/deploy/run-deletion-worker-mac.sh
```

1. 既存API・Caddy・監視スクリプトの検査済みハッシュを照合。変更済みなら停止。
2. 既存の暗号化バックアップ処理を実行し、クラウド読み戻し成功の新しい記録を確認。
3. プレイヤー情報を含まない暗号化プローブを、非公開バケットの専用
   `deletion-probes/`へ1件PUT/GETしハッシュ一致を確認。実際の削除記録と区別する。
   この小さなテストオブジェクトは保管し、自動削除しない。
4. DB所有者witchplayerでSQLite backup APIによるスナップショットを作り整合性検証。
   1トランザクションで6個の空テーブルを追加し、既存7テーブルの内容が不変であることを確認。
   スナップショットは`/var/lib/witch-player/deletion-setup-backups/`に600で保管。
5. 独立した`/usr/local/lib/nasus-deletion/`にワーカーを配置。
   1分ごとのtimerと初回実行を確認し、既存healthサービスへ専用drop-inを追加する。
6. HTTPSのGET/HEAD `/healthz`が200、`/`が503、API/Caddyのハッシュ不変を確認。

成功表示は`DELETION_WORKER_SETUP_COMPLETE`。
API再起動、ゲーム公開、アカウント削除、課金処理の変更は行わない。

## 権限・再送・監視

- rootは既存のcurl設定を通常のクラウド認証に使う。資格情報を表示・別ファイルへ複製しない。
- DB操作は必ずfork後にgroups/gid/uidをwitchplayerへ降下。
  SQLiteのWAL/SHMをroot所有にせず、APIユーザーにクラウド資格情報を与えない。
- ageの公開宛先のみをDB側プロセスに渡す。秘密の復号鍵はVPSに配置しない。
- 先に暗号文と120秒leaseをDBへ保存し、root側でPUT/GET照合後、同じleaseだけを承認。
  再送では同じオブジェクト名・同じ暗号文を使用する。任意のエラー本文やpayloadはログに出さない。
- 1回1件、失敗時30秒〜最大1時間の再試行間隔。1分timerにより実際の最短再試行は約1分。
- root専用budgetに送信試行のバイト数を先に永続保存。試験導入上限64MiB（再試行も加算）。
  上限・不正・消失時は停止し、自動リセットしない。後日の本運用前に上限を見直す。
- statusは真偽と時刻だけ。送信失敗、15分超の未送信、停止timer、失敗service、
  5分超の古いstatusは既存`/healthz`の異常判定へ加わる。既存外形監視が通知を担当する。
- 通常のワーカー起動ではスキーマを作らない。復元隔離マーカー、instance不一致、欠落schemaは停止。
- 導入ファイルとmanifestは手動sudo後にroot専用一時領域で固定SHA-256を検証してから使用。
  APIソース、Caddy、旧healthスクリプトは上書きしない。

## 途中失敗と再実行

失敗時は新timerを停止し、追加したhealth drop-inを診断用一時領域へ移動して
旧healthコマンドに戻す。追加したDBテーブル・スナップショット・診断用ファイルは残す。
**DBを古いコピーへ自動的に戻さない。** 新しいユーザー書き込みを失う恐れがあるため。
既存導入先・部分導入先がある場合、再実行は停止する。完了画面またはエラー画面を確認して対応する。
今回の追加物以外は削除しない。既存API/Caddyハッシュを再検証する。

## 検証と残りの制約

ローカル自動テスト：既存データ不変・DDL rollback・バックアップ整合性・重複導入拒否、
分割lease処理・暗号文再利用・古いqueue・予算停止・監視・プローブ分離・改変bundle拒否を検証。
導入順序と途中失敗時の復旧は、模擬systemd/クラウドを使うテストで確認する。
実サーバーのsudo/systemd内の動作確認はユーザーによる手動実行後に別途行う。

今回の全Pythonテストは244件成功、失敗0・スキップ0（使い捨て鍵の実ageテストを含む）。
準備用ファイルを`/home/ubuntu/nasus-deploy/20260925-deletion-worker-01/`へ転送し、
9個の構成ファイルのmanifest照合とLinux Python構文検査に成功。
Macコマンドのbash構文、installer/manifestの固定SHA-256一致も確認済み。
この確認では導入スクリプトを実行していない。既存API/health/offsite timerは引き続きactive。

空queueが正常なだけでは、実アカウント削除・災害復旧・審査対応の完了を意味しない。
最新削除履歴の完全な外部一覧確認、保持ポリシー、Apple失効、通知メールの再試験、
実機の削除一連動作は未完了。削除APIの公開条件は満たしていない。
