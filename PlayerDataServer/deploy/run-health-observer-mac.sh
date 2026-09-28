#!/usr/bin/env bash
# User authenticates once; no saved password or standing sudo permission.
set -euo pipefail
umask 077
report_dir=/Users/andou/Desktop/game-creation/PlayerDataServer/deploy/reports
mkdir -p "$report_dir"
report_file=$(mktemp "$report_dir/health-reason-install.XXXXXX")
trap 'printf "\n結果の保存先: %s\n" "$report_file"' EXIT
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /run/nasus-health-reason-install.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260925-health-observer-01/install_health_observer.py "$stage/install.py"
printf "%s  %s\n" 29ec0def53d3630ecbd15e8050b26a8c16b0f73525f0bdb374c37792260d696c "$stage/install.py" | sha256sum --check --status
exec /usr/bin/python3 -I -B "$stage/install.py"
'\''' | tee "$report_file"
