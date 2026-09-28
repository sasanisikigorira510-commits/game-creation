#!/usr/bin/env bash
# One authenticated read-only diagnostic; no standing sudo grant.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
mkdir -p "$report_dir"
report_file=$(mktemp "$report_dir/backup-health-diagnostic.XXXXXX")
trap 'printf "\n診断結果の保存先: %s\n" "$report_file"' EXIT
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-health-diagnostic.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260925-health-diagnostic-01/diagnose_backup_health.py "$stage/diagnose.py"
printf "%s  %s\n" 6252c0ed08d1f6e965b38d373dfc1211707a4f05a6fb133e16a3a272a02029ca "$stage/diagnose.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/diagnose.py"
'\''' | tee "$report_file"
