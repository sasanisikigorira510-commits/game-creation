#!/usr/bin/env bash
# Fixed-scope staging, not public activation. No password storage or sudo grant.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
helper="$backend/deploy/prepare_apple_staging_mac.py"
expected_helper=7789c037a623d94ea560de9c40b9c402e235fa02acf3425caf9c43653bd133d8
[[ "$(shasum -a 256 "$helper" | awk '{print $1}')" == "$expected_helper" ]] || { printf '準備コードが変更されています。停止しました。\n' >&2; exit 1; }
connection=(-i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 -o ConnectTimeout=10)
remote=ubuntu@133.242.174.253
source_dir=/home/ubuntu/nasus-deploy/20260926-apple-stage-01
private_dir='/Users/andou/Library/Application Support/NasusApple/keys'
report=$(mktemp "$backend/deploy/reports/apple-staging.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf 'Apple用の空DB構成・専用環境・鍵をVPSへ配置します。ゲーム公開と自動処理は有効化しません。\n'
hashes=$("$backend/.venv-production/bin/python" "$helper" --keys)
read -r signing_hash token_hash <<< "$hashes"
[[ "$signing_hash" =~ ^[a-f0-9]{64}$ && "$token_hash" =~ ^[a-f0-9]{64}$ ]] || exit 1
ssh "${connection[@]}" -o BatchMode=yes "$remote" "test -d '$source_dir' && test ! -e /opt/nasus-apple-20260926 && test ! -e '$source_dir/signing.p8' && test ! -L '$source_dir/signing.p8' && test ! -e '$source_dir/token-encryption.key' && test ! -L '$source_dir/token-encryption.key'"
scp "${connection[@]}" -o BatchMode=yes "$private_dir/AuthKey_38GM3RYVD9.p8" "$remote:$source_dir/signing.p8"
scp "${connection[@]}" -o BatchMode=yes "$private_dir/token-encryption-20260926.key" "$remote:$source_dir/token-encryption.key"
printf 'VPSの管理者パスワードを入力してください（表示・保存されません）。通常1〜3分です。\n'
bootstrap='set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-apple-staging.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260926-apple-stage-01/install_apple_staging.py "$stage/install.py"
printf "%s  %s\n" bfde499d4a6ed3967d18403d3211adef8e4f46e79e993dd44db91c495ed42f34 "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py" 2ef4889eaa736f72bbb56229acc9d9e0eb716e6263c86a0117cbd57fec2a05ac '
bootstrap+="$signing_hash $token_hash"
ssh "${connection[@]}" -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -t "$remote" \
  "sudo /bin/bash -c '$bootstrap'" | tee "$report"
printf '\nApple構成の下準備が完了しました。鍵はMacにも保管されています。公開・定期起動はまだ無効です。\n'
