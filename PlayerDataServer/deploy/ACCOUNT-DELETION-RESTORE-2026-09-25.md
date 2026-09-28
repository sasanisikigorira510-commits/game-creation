# アカウント削除：オフライン復元候補への再適用

2026-09-25。開発用・ローカル検証のみ。本番、実アカウント、実バックアップ、クラウド、
実Apple認証情報には触れていない。前段は[端末削除](ACCOUNT-DELETION-CLIENT-2026-09-25.md)。

## 今回できること

`deletion_restore.py`を追加した。現行DBから確定済み削除のチェックポイントを読み取り専用で
書き出し、独立して保管した期待SHA-256と照合したうえで、古いバックアップの**新しい調査用コピー**に適用する。
既存ディレクトリ、稼働DB、バックアップは上書きしない。

- チェックポイントはinstance ID、生成日時、削除PlayerIdのハッシュ/削除日時、
  再付与禁止の購入取引ハッシュだけ。元ID、資格情報、確認token、セーブ、Apple tokenは含めない。
  ハッシュは匿名化の保証ではない。ファイルは600で作成し、外部保管時も暗号化する。
- コピー内の対象アカウント、セーブ、操作、購入、権利、監査、フラグを除去。
  player行を既に失った孤立監査/フラグも除去する。別アカウントの保存データは保持。
- バックアップ後に購入してから削除された取引のハッシュも取り込み、他アカウントへの再付与を拒否。
  生存アカウントの購入が削除済み取引と衝突した場合は、自動修正せず停止する。
- 復元候補に既存の削除記録があれば消さず、合併する。
- 全ての短命な削除確認/結果記録、Apple認証/解除プレビューは失効させる。
  新端末のApple復旧プレビューはplayer列がNULLでもセーブを含み得るため、対象だけでなく全て消す。
- 対象の古いAppleリンクを外す。対象subjectの暗号化grantは捨てず失効待ちに戻し、
  世代番号を進める。別アカウントへ既に紐付いたsubjectは古い解除記録だけで失効させない。
  Appleへの通信は行わず、失効済みとも判定しない。Apple情報のある候補は必ず別途照合が必要。
- 未対応テーブル/列/ビュー/トリガー、不完全なAppleスキーマ、異なるinstance、破損や不正形式は停止。
- コピーより先に`RECOVERY-PENDING.txt`を作成する。失敗して残った候補も公開しない。
  production factoryはこの印があれば、正しいinstance-idを手動コピーしても起動を拒否する。
- 成功しても`DELETIONS_APPLIED_OFFLINE_NOT_READY`。削除適用レポートのみ出力し、
  起動用instance-idの生成や公開許可、隔離印の解除はしない。

新しい`maintenance.py backup`は元DBの隣のinstance-idがあればmanifestに記録する。
この情報を持たない旧バックアップは、filtered復元では拒否する。通常の`restore-copy`で
隔離して出所を人が調査できるが、推測でmanifestを埋めて公開してはいけない。

## 開発用操作例（本番で実行していない）

各パスとinstanceは説明用。運用では先にゲームAPI、Appleワーカーなどの書込みを止め、
最後に確定した削除まで含む出所と記録の新しさを確認する。

```sh
python deletion_restore.py export-checkpoint \
  --data-dir /secure/current-data --instance-id reviewed-instance \
  --output /secure/deletion-checkpoints/new-checkpoint.json

python deletion_restore.py restore-filtered \
  --backup /secure/backups/players-TIMESTAMP.sqlite \
  --checkpoint /secure/deletion-checkpoints/new-checkpoint.json \
  --expected-sha256 INDEPENDENTLY_RETAINED_LATEST_SHA256 \
  --instance-id reviewed-instance --new-directory /secure/recovery/new-incident
```

期待ハッシュは、復元対象バックアップや渡されたチェックポイント自身からその場で計算して渡さない。
最新の独立保管記録から取得する。ハッシュは破損/取り違え検出であり、電子署名ではない。
レポートの日付も「現在まで全部の削除を含む証明」ではない。

## 重要な未完了事項：これだけで本番有効化しない

後続で[外部保存用outboxと完了確認](ACCOUNT-DELETION-JOURNAL-2026-09-25.md)を開発用に追加。
以下はこの復元ツール単独での制約。後続処理も実クラウド/ワーカー/最新一覧には未接続。

**削除APIと外部永続記録の原子的な連動はまだない。** 現行の削除APIは同じゲームDBへ
削除記録を書いている。手動チェックポイントの後に削除が確定し、DBを失った場合の不足分は、
このツールだけでは回収できない。古いチェックポイントと古い期待ハッシュの両方を渡した場合も
最新性を自動で判定できない。最新記録を確認できないなら復元候補を公開しない。

公開前に必要:

- ゲームDBの巻き戻しとは独立した、永続的な削除ジャーナル/外部保存。
- 削除応答、取消、クラッシュ、外部保存障害の整合性。削除を成功と返した後に記録が失われない保証。
- 最新記録の独立したチェックポイント管理、欠落/停止監視、復旧と公開の承認手順。
- バックアップ後の購入・操作の照合。Apple grantの現行世代/所有者/失効状態の照合。
  古い暗号化grantをこの候補から自動失効させるワーカーを起動してはいけない。
- バックアップの保持/期限切れ消去の方針とユーザー向け説明。

VACUUM/secure_deleteは調査用コピーの論理的な残存を減らすだけで、元バックアップ、
失敗した候補、SSD、WAL、OSスナップショット、クラウド世代の物理消去を保証しない。
調査候補自体を機密データとして管理する。解除手順を自動化するコマンドは追加していない。

## ファイル・検証

- 追加: `deletion_restore.py`、`tests/test_deletion_restore.py`、この文書。
- 変更: `maintenance.py`、`production.py`、README/DEPLOYMENT、配布許可リスト。
- 一時DBの復元テスト20件。元DB/バックアップの保持、他アカウント保全、
  購入再付与拒否、Appleキャッシュ消去、旧解除と新所有者、途中失敗/トランザクション巻き戻し、
  古い/破損記録・未知schema・symlink拒否、隔離候補の起動拒否を検証。
- 最終のPython全体テスト196件成功・失敗0・スキップ0。既存の削除/取消・Apple連携・
  課金・バックアップ・配布許可リストのテストを含む。`pip check`も成功。
- Unity/iPhone、実Apple、実クラウド復旧は今回の検証対象外。
- BaseUrl空、実験フラグOFF相当、productionの削除/Apple依存なし、公開ルートの制限は維持。
