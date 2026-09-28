#!/bin/bash
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/refund-notification-test.XXXXXX")
trap 'printf "\n結果: %s\n完了または停止したらCodexへ戻ってください。パスワードは保存していません。\n" "$report"' EXIT
printf '\nAppleのSandbox TEST通知を1回だけ送信し、配信結果を確認します。\n購入・返金・召喚は実行しません。既存のセーブや本番設定は変更しません。\n秘密鍵はVPS上の保護領域から移動せず、専用ユーザーで使用します。\nVPS管理者パスワードを入力してEnterを押してください。\n\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-refund-notification-test.XXXXXXXX)
install -m 644 /home/ubuntu/nasus-deploy/20260928-refund-notification-01/test_refund_notification_delivery.py "$stage/test.py"
printf "%s  %s\n" 57dedb9df207d9fe42a53c2fe61ff577bd1f0633c259a50f8e68ca33c540a58a "$stage/test.py" | sha256sum --check --status
chmod 755 "$stage"
exec /usr/sbin/runuser -u nasusrefund -- /opt/nasus-refund-sandbox-20260928/venv/bin/python -I -B "$stage/test.py"
'\'''
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
  -o BatchMode=yes ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n確認が完了しました。Codexへ「できた」と教えてください。再実行は不要です。\n'
