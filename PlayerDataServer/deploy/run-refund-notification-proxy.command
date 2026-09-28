#!/bin/bash
# Approved single Sandbox callback route; password stays in the sudo terminal.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/refund-notification-proxy.XXXXXX")
trap 'printf "\n結果: %s\n完了または停止したらCodexへ戻ってください。パスワードは保存していません。\n" "$report"' EXIT
printf '\n返金Sandboxの通知受信URLを1本だけ追加します。\n管理画面・ゲーム操作APIは公開しません。既存データやiPhoneも変更しません。\n設定を退避・検証してから反映し、確認失敗時は元の公開設定へ戻します。\nApple側の設定・購入・返金は、この操作では実行しません。\nVPS管理者パスワードを入力してEnterを押してください（文字は表示されません）。\n\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-refund-notification-proxy.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260928-refund-notification-01/install_refund_notification_proxy.py "$stage/install.py"
printf "%s  %s\n" 01cd6429d09fbda3c76c8cae68f95f332b02e08942f77415621e7afa27984c71 "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py"
'\'''
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
  -o BatchMode=yes ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n受信経路の追加が完了しました。Codexへ「できた」と教えてください。\n再実行は不要です。Apple側の通知設定と実通知試験はこの後に行います。\n'
