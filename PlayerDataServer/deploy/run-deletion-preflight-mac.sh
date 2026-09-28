#!/usr/bin/env bash
# Manual Terminal command: sudo asks the user, never send the password to chat.
# This is an inspection, NOT deployment. No DB/schema/config/cloud writes.
set -euo pipefail
ssh -t -i /Users/andou/.ssh/id_ed25519_sakura_nasus_4433119 \
  -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=/Users/andou/.ssh/known_hosts_sakura_nasus_4433119 \
  -o ConnectTimeout=10 ubuntu@133.242.174.253 \
  'sudo /bin/bash -c '\''
set -euo pipefail
umask 077
stage=$(mktemp -d /var/tmp/nasus-deletion-preflight.XXXXXXXX)
install -m 600 /home/ubuntu/nasus-deploy/20260925-deletion-preflight-01/preflight_deletion_worker.py "$stage/preflight.py"
printf "%s  %s\n" f8a6d634b359eaf71c4ac1737345d576d6c742e027b86a8310030c13d9951b21 "$stage/preflight.py" | sha256sum --check --status
exec /usr/bin/python3 -I "$stage/preflight.py"
'\'''
