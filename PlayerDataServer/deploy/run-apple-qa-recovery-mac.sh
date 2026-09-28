#!/usr/bin/env bash
# Fixed-scope recovery; no persistent sudo grant and no password storage.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/apple-qa-recovery.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
printf 'Caddyの元設定と権限を復旧し、正常確認後に限定QA接続だけ再開します。DB・鍵・公開範囲・有効期限は変更しません。\nVPSの管理者パスワードを入力してください。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-qa-recovery.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260927-qa-recovery-01
for name in recover_apple_qa.py install_apple_qa.py install_apple_staging.py; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" d42020317e816262d0236e0fe6f9666839efe77400ed5f3b22ea720578a1be50 "$stage/recover_apple_qa.py" | sha256sum --check --status
printf "%s  %s\n" b9c45e26ec3593d15383f446e9ede39201a0b8a674ed07d2c7db596c7adca8f8 "$stage/install_apple_qa.py" | sha256sum --check --status
printf "%s  %s\n" bfde499d4a6ed3967d18403d3211adef8e4f46e79e993dd44db91c495ed42f34 "$stage/install_apple_staging.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/recover_apple_qa.py"
'\'''
ssh -t "${connection[@]}" ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n限定QA接続の復旧と境界検証が完了しました。実機導入・実Apple認証はまだ行っていません。\n'
