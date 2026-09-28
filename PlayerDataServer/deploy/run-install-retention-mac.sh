#!/usr/bin/env bash
# One authenticated, hash-pinned install. Never stores a password or grants sudo.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
report=$(mktemp "$report_dir/retention-install.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf '30日超の外部DBバックアップだけを自動削除する設定を反映します。\nVPSの管理者パスワードを入力してください。通常2〜3分です。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-retention-install.XXXXXXXX)
source_dir=/home/ubuntu/nasus-deploy/20260926-retention-01
for name in install_backup_retention.py expire_backups.py retention_health.py inspect_backup_retention.py backup_retention_plan.py witch-player-retention.service witch-player-retention.timer; do
  install -m 600 "$source_dir/$name" "$stage/$name"
done
printf "%s  %s\n" 12ab34f6cc69721e6f20479d982fb254eb3c8e8efe986d19285eb59722279d56 "$stage/install_backup_retention.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install_backup_retention.py"
'\''' | tee "$report"
printf '\n保持設定と監視の確認が完了しました。結果は自動保存されました。\n'
