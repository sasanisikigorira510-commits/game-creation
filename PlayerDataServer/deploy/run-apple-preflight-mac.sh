#!/usr/bin/env bash
# One interactive authentication, service-user inspection only. No sudo grant.
set -euo pipefail
umask 077
deploy_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy
inspection="$deploy_dir/preflight_apple_configuration.py"
expected=1f0ebaa66df611f3be5e9d3187670c186ffe6de31be7733aa0e16282aa0e8466
actual=$(shasum -a 256 "$inspection" | awk '{print $1}')
if [[ "$actual" != "$expected" ]]; then
    printf '検査スクリプトが変更されています。実行せず停止しました。\n' >&2
    exit 1
fi
mkdir -p "$deploy_dir/reports"
report=$(mktemp "$deploy_dir/reports/apple-configuration.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
payload=$(base64 < "$inspection" | tr -d '\n')
# The encoded payload is this reviewed code, NOT credentials. Run as witchplayer,
# not root. -I excludes remote cwd and environment Python import injection.
remote="sudo -u witchplayer /usr/bin/python3 -I -c \"import base64; exec(compile(base64.b64decode('$payload'), 'apple-preflight', 'exec'))\""
printf 'VPSの管理者パスワードを入力してください。入力文字は表示されません。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 "$remote" | tee "$report"
printf '\n読み取り確認が完了しました。設定・DB・公開状態は変更していません。\n'
