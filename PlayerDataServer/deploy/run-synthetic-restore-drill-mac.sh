#!/usr/bin/env bash
# One credential prompt; encrypted cloud probe, download and local recovery.
set -euo pipefail
umask 077
repo=/Users/andou/Desktop/game-creation/PlayerDataServer
drill='/Users/andou/Library/Application Support/NasusBackups/deletion-drill-20260925-01'
age='/Users/andou/Library/Application Support/NasusBackups/tools/age-v1.3.2-darwin/age/age'
ssh_options=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10)
mkdir "$drill/attempt-started" # Never rerun or overwrite a partial drill.
report=$(mktemp "$repo/deploy/reports/synthetic-cloud-restore.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf 'VPSの管理者パスワードを求められたら入力してください。入力文字は表示されません。\n'
ssh -t "${ssh_options[@]}" ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-synthetic-drill.XXXXXXXX)
source=/home/ubuntu/nasus-deploy/20260925-synthetic-drill-01
install -m 600 "$source/synthetic_drill_cloud.py" "$stage/probe.py"
install -m 600 "$source/outgoing.age" "$stage/outgoing.age"
install -m 600 "$source/transfer.json" "$stage/transfer.json"
printf "%s  %s\n" \
 e193d0cc16563e6ff12c49fce2506ec21420514b64de62fe3f3dc3d7b5f628e9 "$stage/probe.py" \
 397133246435fe56171c8f43de6734d96e56d6e0bb2f0ed3d49b8688e0e6730d "$stage/outgoing.age" \
 c84b4ea3958358e3071e6d2c2779714d5a09cdc5f7b7b7e8a0baa6502cb92033 "$stage/transfer.json" | sha256sum --check --status
exec /usr/bin/python3 -I "$stage/probe.py"
'\''' | tee "$report"
export_dir=$(LC_ALL=C tr -d '\r' < "$report" | sed -nE 's|^DRILL_EXPORT_DIRECTORY=(/var/tmp/nasus-drill-return\.[a-z0-9_]{8})$|\1|p')
[[ "$export_dir" =~ ^/var/tmp/nasus-drill-return\.[a-z0-9_]{8}$ ]]
mkdir -m 700 "$drill/cloud-return"
scp "${ssh_options[@]}" -o BatchMode=yes \
  "ubuntu@133.242.174.253:$export_dir/readback.age" \
  "ubuntu@133.242.174.253:$export_dir/receipt.json" "$drill/cloud-return/"
chmod 600 "$drill/cloud-return/readback.age" "$drill/cloud-return/receipt.json"
cd "$repo"
"$repo/.venv-production/bin/python" -m deploy.deletion_restore_drill complete \
  --root "$drill" --age "$age" | tee -a "$report"
printf '\n削除・暗号化保存・復元試験が完了しました。本番データは変更していません。\n' | tee -a "$report"
