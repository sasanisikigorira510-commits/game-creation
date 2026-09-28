#!/bin/bash
# User types the VPS sudo password directly into SSH's terminal. Never captured.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/refund-host-check.XXXXXX")
trap 'printf "\n確認結果: %s\nパスワードは保存していません。この画面は閉じて構いません。\n" "$report"' EXIT
python=/tmp/nasus-refund-tests.WimCvv/venv/bin/python
remote_command=$("$python" -c 'import pathlib,shlex,sys;print("sudo /usr/bin/python3 -I -B -c "+shlex.quote(pathlib.Path(sys.argv[1]).read_text()))' "$backend/deploy/inspect_refund_sandbox_host.py")
printf '\n返金試験用Sandboxの事前確認だけを行います。\nサービス・データ・鍵は変更しません。\nVPSの管理者パスワードを入力してEnterを押してください。入力文字は表示されません。\n\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
  -o BatchMode=yes ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n事前確認が終わりました。Codexのチャットに「できた」と教えてください。\n'
