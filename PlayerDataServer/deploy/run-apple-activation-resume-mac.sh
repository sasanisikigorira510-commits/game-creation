#!/usr/bin/env bash
# Resume the verified stopped installation only; never rerun initial staging.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
receipt='/Users/andou/Library/Application Support/NasusBackups/apple-keys-4ap0esb1/usb-verified.json'
[[ "$(shasum -a 256 "$receipt" | awk '{print $1}')" == 0e8fb5e9a11665459b46ba15c53650ff6e5ad696093e258bfade7c99189339ea ]] || exit 1
report=$(mktemp "$backend/deploy/reports/apple-activation-resume.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf '停止済みのApple定期処理と監視接続を再開します。公開・鍵・DB構成は変更しません。\nVPSの管理者パスワードを入力してください。自動実行確認まで通常2〜4分です。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-apple-resume.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260926-apple-resume-01
for name in resume_apple_activation.py activate_apple_worker.py apple-activation-manifest.json apple_integrated_health_v2.py; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" c8956bad04f54f96779510ae80048675cf74ec5b3308b49c061494c7c6588ca6 "$stage/resume_apple_activation.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/resume_apple_activation.py"
'\''' | tee "$report"
printf '\nApple定期処理と監視の自動実行を確認しました。結果は自動保存されています。\n'
