#!/usr/bin/env bash
# Reviewed cutover after read-only preflight passed. No automatic purchases.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/iap-sandbox-connect.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
printf '事前確認済みのSandbox専用接続を反映します。本番・公開範囲・QA期限は変更しません。\nQAデータは退避します。購入は自動実行しません。通常1〜3分です。\nVPSの管理者パスワードを入力してください（表示・保存しません）。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-iap-connect.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260928-iap-sandbox-check-01/install.py "$stage/install.py"
printf "%s  %s\n" 2ce15b94dd658163f12dcdfe858da3d466828d94bce82337dc8ff01eb69198c4 "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py" 75fd8ec0cdf42088cdb83682a4f817d3cb21881ac684b0642497b6d790f08035
'\'''
ssh -t "${connection[@]}" ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\nSandbox接続と公開遮断の確認が完了しました。実購入テストは未実施です。再実行は不要です。\n'
