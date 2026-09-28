#!/usr/bin/env bash
# Run manually in Terminal. The VPS sudo password is requested without echo.
set -euo pipefail
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'cd /home/ubuntu/nasus-deploy/20260925-health-01 && printf "%s  %s\n" 34c9298d5cfb82e992b0fec8ea3a0445f251a105a3ad907ef9864f41736b72eb install-backup-health.sh | sha256sum --check --status && sudo bash /home/ubuntu/nasus-deploy/20260925-health-01/install-backup-health.sh /home/ubuntu/nasus-deploy/20260925-health-01'
