#!/usr/bin/env bash
# Reviewed single-use Sandbox copy and cutover. No stored sudo password.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$backend/deploy/reports/qa-isolation.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3)
printf '現在の進行・無料石1200・テスト購入分9600を専用DBへコピーします。本番DB・鍵・公開範囲は変更しません。\nVPSの管理者パスワードを入力してください（保存・表示しません）。通常1〜2分です。\n'
remote_command='sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-qa-isolation.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260927-qa-isolation-01
for name in install_qa_isolation.py isolate_apple_qa_data.py qa_isolated_runtime.py install_apple_staging.py install_apple_qa.py; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" 073aae48fcfa34ca027f83774a53c399d6577b655ab7fd0906413e8c38fc3056 "$stage/install_qa_isolation.py" | sha256sum --check --status
printf "%s  %s\n" ebfa60570a385be98906b85ce5d9c3182942902645e7b2095d95017d010f18ba "$stage/isolate_apple_qa_data.py" | sha256sum --check --status
printf "%s  %s\n" 49a4f56ea39b0e755e7b2648ae7da3ebbc0827e75e6d13c352077255d5949d91 "$stage/qa_isolated_runtime.py" | sha256sum --check --status
printf "%s  %s\n" bfde499d4a6ed3967d18403d3211adef8e4f46e79e993dd44db91c495ed42f34 "$stage/install_apple_staging.py" | sha256sum --check --status
printf "%s  %s\n" f2c3388ef1d44bd7b927cf2b09a4712b6499bc0a6a56a2e72fe572fa5df0f1d7 "$stage/install_apple_qa.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install_qa_isolation.py"
'\'''
ssh -t "${connection[@]}" ubuntu@133.242.174.253 "$remote_command" | tee "$report"
printf '\n検証DBの分離が完了しました。Apple実認証はまだです。アプリの削除・再インストールは不要です。\n'
