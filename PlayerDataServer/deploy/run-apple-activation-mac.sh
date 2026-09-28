#!/usr/bin/env bash
# One authenticated activation. No password storage or standing sudo grant.
set -euo pipefail
umask 077
backend=/Users/andou/Desktop/game-creation/PlayerDataServer
receipt='/Users/andou/Library/Application Support/NasusBackups/apple-keys-4ap0esb1/usb-verified.json'
[[ "$(shasum -a 256 "$receipt" | awk '{print $1}')" == 0e8fb5e9a11665459b46ba15c53650ff6e5ad696093e258bfade7c99189339ea ]] || { printf 'USB復号検証の記録が一致しません。停止しました。\n' >&2; exit 1; }
[[ "$(shasum -a 256 "$backend/deploy/activate_apple_worker.py" | awk '{print $1}')" == 3730d1aeba6d2ac032441a400f879d0870ea1ee74f347931e21a1eccc6139beb ]] || exit 1
[[ "$(shasum -a 256 "$backend/deploy/apple-activation-manifest.json" | awk '{print $1}')" == 27dbabd260c60a1b41794da73c36d1644d3ec6d9dfaf403edcf7965eacf092bc ]] || exit 1
report=$(mktemp "$backend/deploy/reports/apple-activation.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf 'Apple定期処理と監視を反映します。ゲームの公開・API切替・鍵変更はしません。\nVPSの管理者パスワードを入力してください。自動実行の確認まで通常2〜4分です。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-apple-activation.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260926-apple-activation-01
for name in activate_apple_worker.py apple_revocation_runtime.py apple_revocation_health.py backup_health.py apple_integrated_health.py witch-player-apple-revoke.service witch-player-apple-revoke.timer; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
install -m 600 "$source_dir/apple-activation-manifest.json" "$stage/manifest.json"
printf "%s  %s\n" 3730d1aeba6d2ac032441a400f879d0870ea1ee74f347931e21a1eccc6139beb "$stage/activate_apple_worker.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/activate_apple_worker.py" 27dbabd260c60a1b41794da73c36d1644d3ec6d9dfaf403edcf7965eacf092bc
'\''' | tee "$report"
printf '\nApple定期処理と統合監視の確認が完了しました。ゲーム公開は閉じたままです。\n'
