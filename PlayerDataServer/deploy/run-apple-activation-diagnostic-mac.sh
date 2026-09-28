#!/usr/bin/env bash
# Read-only: no service starts, health publication, or configuration changes.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/apple-activation-diagnostic.XXXXXX")
trap 'printf "\n診断結果の保存先: %s\n" "$report"' EXIT
printf 'Apple監視の読取り診断のみ行います。定期処理の再起動やDB変更はしません。\nVPSの管理者パスワードを入力してください。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-apple-diagnostic.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260926-apple-diagnostic-01/diagnose_apple_activation.py "$stage/diagnose.py"
printf "%s  %s\n" f47b04f7307b9a6d06a7a89d95a85f882bd830bac7b20b3bc6205a3415e9169c "$stage/diagnose.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/diagnose.py"
'\''' | tee "$report"
