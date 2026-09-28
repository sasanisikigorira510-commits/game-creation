#!/usr/bin/env bash
# User-approved temporary capability test; no persistent unit/config changes.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
mkdir -p "$report_dir"
report_file=$(mktemp "$report_dir/deletion-minimal-capabilities.XXXXXX")
trap 'printf "\nテスト結果の保存先: %s\n" "$report_file"' EXIT
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-deletion-capability-test.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260925-deletion-worker-01/diagnose_deletion_minimal.py "$stage/diagnose.py"
printf "%s  %s\n" 04b85c55406c9fc05bf03987cb52beda953815656bbfa183bc25b05907f44081 "$stage/diagnose.py" | sha256sum --check --status
exec /usr/bin/systemd-run --quiet --wait --pipe --collect \
  --property=User=root --property=Group=root --property=UMask=0077 \
  --property="CapabilityBoundingSet=CAP_SETUID CAP_SETGID" \
  --property="AmbientCapabilities=CAP_SETUID CAP_SETGID" \
  --property=NoNewPrivileges=true --property=PrivateTmp=true \
  --property=ProtectSystem=strict --property=ProtectHome=true \
  --property="ReadWritePaths=/var/lib/witch-player /var/lib/witch-player-deletion" \
  --property="RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6" \
  --property=RuntimeMaxSec=180 --property=MemoryMax=128M --property=LimitCORE=0 \
  /usr/bin/python3 -I "$stage/diagnose.py" --minimal-capabilities
'\''' | tee "$report_file"
