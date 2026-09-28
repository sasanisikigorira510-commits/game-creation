#!/usr/bin/env bash
# Fixed-scope authenticated activation; does not save passwords or grant sudo.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
report=$(mktemp "$report_dir/inventory-install.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf '削除履歴一覧の暗号化外部保存と完了確認を反映します。実アカウントの削除は行いません。\nVPSの管理者パスワードを入力してください。通常2〜3分です。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-inventory-install.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260926-inventory-01
mkdir "$stage/deploy"
for name in install_inventory_worker.py manifest.json store.py maintenance.py deletion_journal.py deletion_restore.py deletion_event_restore.py deletion_inventory.py deletion_inventory_ledger.py run_inventory_worker.py deploy/preflight_deletion_worker.py deploy/deletion_worker.py deploy/sakura_deletion_journal.py deploy/sakura_inventory_store.py deploy/inventory_worker.py; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" af60cb0c68f2318b2986ecafdde581c806e0b5da1467fd4c7445579bbb839c63 "$stage/install_inventory_worker.py" b21ad0d7271365166b4e8109293292f5818c0b4e173f1cfc5b28b7b96f15b08e "$stage/manifest.json" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install_inventory_worker.py"
'\''' | tee "$report"
printf '\n外部保存・自動実行・監視の確認が完了しました。結果は自動保存されました。\n'
