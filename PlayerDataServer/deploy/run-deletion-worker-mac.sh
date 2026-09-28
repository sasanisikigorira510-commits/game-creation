#!/usr/bin/env bash
# Explicit one-time installation; enter VPS sudo password only in Terminal.
set -euo pipefail
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /var/tmp/nasus-deletion-bootstrap.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260925-deletion-worker-01/install_deletion_worker.py "$stage/installer.py"
install -m 600 /home/ubuntu/nasus-deploy/20260925-deletion-worker-01/manifest.json "$stage/manifest.json"
printf "%s  %s\n" 2296777af5fb5c7787a192f0b6105b4d6b929944dbde04327ff7ea03ef9d309e "$stage/installer.py" | sha256sum --check --status
printf "%s  %s\n" 7377ff307fb072b93fd98bc5f19aeb242aaf83bb1f574c78c7c8e32d4f8a6194 "$stage/manifest.json" | sha256sum --check --status
exec /usr/bin/python3 -I "$stage/installer.py" --source /home/ubuntu/nasus-deploy/20260925-deletion-worker-01 --manifest "$stage/manifest.json"
'\'''
