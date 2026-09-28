#!/bin/bash
# Explicitly authorized separate lab installation; password stays in SSH/sudo.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/refund-lab-install.XXXXXX")
trap 'printf "\n結果: %s\nパスワードは保存していません。完了または停止したらCodexへ戻ってください。\n" "$report"' EXIT
printf '\n返金試験専用SandboxをVPS内部に追加します。通常1〜3分です。\n空の専用DB・専用ユーザー・専用サービスを作ります。既存のデータはコピーしません。\n公開URL・既存サービス・iPhoneは変更しません。購入も実行しません。\n新環境のアクセス期限は作成から24時間です。\nVPSの管理者パスワードを入力してEnterを押してください（文字は表示されません）。\n\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-refund-lab-install.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260928-refund-lab-01/install_refund_lab.py "$stage/install.py"
printf "%s  %s\n" d21917f5ed0d46e1b3e9deedb184338dfeeaa149267ff2c9207961b758f7354d "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py" 5a094dda90e920a9324a8e74cf7ff22f14e8ae32b7f9178bedc39cd70e5786ea
'\'''
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
  -o BatchMode=yes ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n内部Sandboxの追加が完了しました。Apple実通知・実機試験はまだ実施していません。\nCodexへ「できた」と教えてください。再実行は不要です。\n'
