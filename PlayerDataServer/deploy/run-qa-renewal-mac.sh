#!/usr/bin/env bash
# Single approved 24-hour renewal; password stays in the user's terminal.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/qa-renewal.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
printf '同じ検証用接続のみ24時間再開します。本番・鍵・DB構造・公開範囲は変更しません。\nVPSの管理者パスワードを入力してください（表示・保存しません）。通常1分程度です。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-qa-renewal.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260928-qa-renewal-02
for name in renew_apple_qa.py install_apple_staging.py; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" ee9a2d250972bb3414a0cdc28cb8076576b6c0e641eb51da3e7f54a969f88db2 "$stage/renew_apple_qa.py" | sha256sum --check --status
printf "%s  %s\n" bfde499d4a6ed3967d18403d3211adef8e4f46e79e993dd44db91c495ed42f34 "$stage/install_apple_staging.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/renew_apple_qa.py"
'\'''
ssh -t "${connection[@]}" ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n検証用接続の再開と遮断境界の確認が完了しました。再実行は不要です。\n'
