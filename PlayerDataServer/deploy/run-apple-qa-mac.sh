#!/usr/bin/env bash
# Single-use bounded deployment; sudo password stays on the user's terminal.
set -euo pipefail
printf 'この初回配置は停止済みです。再実行せず run-apple-qa-recovery-mac.sh を使ってください。\n' >&2
exit 1
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
client='/Users/andou/Library/Application Support/NasusApple/qa-20260926/client.json'
report=$(mktemp "$backend/deploy/reports/apple-qa-install.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
client_hash=$(shasum -a 256 "$client" | awk '{print $1}')
[[ $client_hash =~ ^[a-f0-9]{64}$ ]] || exit 1
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
remote=ubuntu@133.242.174.253
scp "${connection[@]}" -o BatchMode=yes "$client" "$remote:/home/ubuntu/nasus-deploy/20260926-apple-qa-01/client.json"
printf '24時間限定のApple実機検証APIを配置します。検証キー必須・管理画面は遮断、通常の公開APIは変更しません。\nVPSの管理者パスワードを入力してください。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-apple-qa.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260926-apple-qa-01
for name in install_apple_qa.py install_apple_staging.py; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" b9c45e26ec3593d15383f446e9ede39201a0b8a674ed07d2c7db596c7adca8f8 "$stage/install_apple_qa.py" | sha256sum --check --status
printf "%s  %s\n" bfde499d4a6ed3967d18403d3211adef8e4f46e79e993dd44db91c495ed42f34 "$stage/install_apple_staging.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install_apple_qa.py" 62d32f12a556b6375ea537429b95d1e7ab4e099925c3938d45784bfca60b7717 '
remote_command+="$client_hash"
remote_command+="'"
ssh -t "${connection[@]}" "$remote" "$remote_command" | tee "$report"
printf '\n検証用APIの接続確認が完了しました。アプリの入替えや実Apple認証はまだ行っていません。\n'
