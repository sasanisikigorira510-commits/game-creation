#!/usr/bin/env bash
# One user-authenticated operation; no password storage or standing sudo grant.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
mkdir -p "$report_dir"
report_file=$(mktemp "$report_dir/deletion-integration-resume.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report_file"' EXIT
printf 'VPSのパスワード入力後、自動実行を2回確認します（通常2〜4分）。完了までこの画面を開いておいてください。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-integration-resume.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260925-integration-resume-01
install -m 600 "$source_dir/resume_deletion_integration.py" "$stage/resume_deletion_integration.py"
install -m 600 "$source_dir/publish_backup_health.py" "$stage/publish_backup_health.py"
install -m 600 "$source_dir/deletion_maintenance.py" "$stage/deletion_maintenance.py"
printf "%s  %s\n" a518fddbc71b525a7ba1016522767071508c954b094a9e2eb5ee4ce00bcf19de "$stage/resume_deletion_integration.py" | sha256sum --check --status
printf "%s  %s\n" a42a6d7cb7eb49064b95babc7360d0927c18a2d192a320cc5dd180dc54decafb "$stage/publish_backup_health.py" | sha256sum --check --status
printf "%s  %s\n" 51698a01a881d73b4b2b4a646038a4f48d003f2e7e6ed4b8bf4c52b4b3c32c83 "$stage/deletion_maintenance.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/resume_deletion_integration.py"
'\''' | tee "$report_file"
