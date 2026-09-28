#!/usr/bin/env bash
# Fixed read-only S3 inspection. Authentication stays in the user's Terminal.
set -euo pipefail
umask 077
repo=/Users/andou/Desktop/game-creation/PlayerDataServer
report=$(mktemp "$repo/deploy/reports/retention-inspection.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report"' EXIT
printf 'VPSの管理者パスワードを入力してください。バックアップの削除や設定変更は行いません。\n'
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-retention-inspect.XXXXXXXX)
source=/home/ubuntu/nasus-deploy/20260925-retention-inspect-01
install -m 600 "$source/inspect_backup_retention.py" "$stage/inspect.py"
install -m 600 "$source/backup_retention_plan.py" "$stage/backup_retention_plan.py"
printf "%s  %s\n" \
 94682c824e9a7d4bd05c1ce33f4f12a807a2784cc8da86c19801ad6bab6df893 "$stage/inspect.py" \
 7eb5304dce431776e0d7b61dd274adc132f8f461075e4759ad0f98a51d20e9d6 "$stage/backup_retention_plan.py" | sha256sum --check --status
exec /usr/bin/python3 -I "$stage/inspect.py"
'\''' | tee "$report"
printf '\n読み取り確認が完了しました。結果は自動保存されました。\n'
