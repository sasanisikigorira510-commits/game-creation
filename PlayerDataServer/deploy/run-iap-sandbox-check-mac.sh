#!/usr/bin/env bash
# Read-only diagnostic: no service restart, DB writes or configuration changes.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/iap-sandbox-check.XXXXXX")
trap 'printf "\n診断結果の保存先: %s\n" "$report"' EXIT
digest=2ce15b94dd658163f12dcdfe858da3d466828d94bce82337dc8ff01eb69198c4
actual=$(shasum -a 256 "$backend/deploy/install_iap_sandbox.py" | cut -d ' ' -f 1)
test "$actual" = "$digest"
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
remote=ubuntu@133.242.174.253
source_dir=/home/ubuntu/nasus-deploy/20260928-iap-sandbox-check-01
ssh "${connection[@]}" -o BatchMode=yes "$remote" "umask 077; mkdir -m 700 $source_dir"
scp "${connection[@]}" -o BatchMode=yes "$backend/deploy/install_iap_sandbox.py" "$remote:$source_dir/install.py"
printf '課金接続の停止理由だけを読み取り確認します。設定・DB・サービスは変更しません。\nVPSの管理者パスワードを入力してください（表示・保存しません）。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-iap-check.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260928-iap-sandbox-check-01/install.py "$stage/install.py"
printf "%s  %s\n" 2ce15b94dd658163f12dcdfe858da3d466828d94bce82337dc8ff01eb69198c4 "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py" 75fd8ec0cdf42088cdb83682a4f817d3cb21881ac684b0642497b6d790f08035 --check
'\'''
ssh -t "${connection[@]}" "$remote" "$remote_command" | tee "$report"
