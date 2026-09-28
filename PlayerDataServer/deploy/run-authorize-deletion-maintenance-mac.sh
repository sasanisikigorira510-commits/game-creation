#!/usr/bin/env bash
# One manual authentication; never reads or stores the password.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
mkdir -p "$report_dir"
report_file=$(mktemp "$report_dir/deletion-maintenance-authorization.XXXXXX")
trap 'printf "\n許可設定の結果: %s\n" "$report_file"' EXIT
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-maintenance-authorize.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260925-deletion-maintenance-01/authorize_deletion_maintenance.py "$stage/authorize.py"
printf "%s  %s\n" a937dd64f2779e3d70806522415563569b322972eae29a8e91d5b0d1cfb1a123 "$stage/authorize.py" | sha256sum --check --status
exec /usr/bin/python3 -I "$stage/authorize.py"
'\''' | tee "$report_file"
