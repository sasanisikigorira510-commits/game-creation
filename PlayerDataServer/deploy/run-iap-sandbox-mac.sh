#!/usr/bin/env bash
# One reviewed Sandbox-only cutover. Administrator password is not saved.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/iap-sandbox.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
printf 'Sandbox課金検証だけを既存QAへ接続します。本番・公開範囲・QA期限は変更しません。\nQAデータは退避します。購入は自動実行しません。通常1〜3分です。\nVPSの管理者パスワードを入力してください（表示・保存しません）。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-iap-sandbox.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260928-iap-sandbox-01
install -m 600 "$source_dir/install_iap_sandbox.py" "$stage/install.py"
printf "%s  %s\n" f7bd0010d16fea6f931aa64a577355859c55512130e776b751de9179e9d39e07 "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py" 75fd8ec0cdf42088cdb83682a4f817d3cb21881ac684b0642497b6d790f08035
'\'''
ssh -t "${connection[@]}" ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\nSandbox接続と公開遮断の確認が完了しました。実購入の検証はこれからです。再実行は不要です。\n'
