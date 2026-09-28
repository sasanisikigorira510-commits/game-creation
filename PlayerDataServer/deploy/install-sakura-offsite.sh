#!/usr/bin/env bash
# Reviewed files are copied into root-owned staging before use.
set -euo pipefail
umask 077
[[ $EUID == 0 && $# == 1 ]] || { echo 'Usage: sudo bash install-sakura-offsite.sh STAGING'; exit 1; }
source_dir=$(realpath -- "$1")
cd /opt/witch-player/server
private_stage=$(mktemp -d /var/tmp/nasus-offsite-install.XXXXXXXX)
for name in sakura_offsite_backup.py setup_sakura_credentials.py witch-player-offsite.service witch-player-offsite.timer recipient.txt age-v1.3.2-linux-amd64.tar.gz SHA256SUMS; do
    install -m 600 "$source_dir/$name" "$private_stage/$name"
done
(cd "$private_stage" && sha256sum --check SHA256SUMS)
printf '%s  %s\n' cbe24006683f8eb669266162894b9a522a1af52f2665fbc63a4bb032ed26ac10 "$private_stage/age-v1.3.2-linux-amd64.tar.gz" 0b6761e495b246e3bf986eafec4344dad4f19dff54c0666f0cb3b9a20599691c "$private_stage/recipient.txt" | sha256sum --check --status
[[ ! -e /etc/systemd/system/witch-player-offsite.timer ]] || { echo 'Already installed; inspect existing service instead of reinstalling.'; exit 1; }
install -d -o root -g root -m 700 /etc/witch-player-offsite
install -d -o root -g root -m 711 /var/lib/witch-player-offsite /var/lib/witch-player-offsite/work
install -d -o root -g root -m 755 /usr/local/lib/nasus-backup
tar -xzf "$private_stage/age-v1.3.2-linux-amd64.tar.gz" -C "$private_stage" --no-same-owner age/age
install -o root -g root -m 755 "$private_stage/age/age" /usr/local/lib/nasus-backup/age
install -o root -g root -m 644 "$private_stage/sakura_offsite_backup.py" /usr/local/lib/nasus-backup/sakura_offsite_backup.py
install -o root -g root -m 600 "$private_stage/recipient.txt" /etc/witch-player-offsite/recipient.txt
if [[ ! -e /etc/witch-player-offsite/curl.conf ]]; then
    /usr/bin/python3 "$private_stage/setup_sakura_credentials.py"
fi
install -o root -g root -m 644 "$private_stage/witch-player-offsite.service" /etc/systemd/system/witch-player-offsite.service
# Only install/enable the timer after the initial transfer succeeds.
systemctl daemon-reload
if ! systemctl start witch-player-offsite.service; then
    echo 'FIRST_TRANSFER_FAILED: timer NOT enabled; credentials retained root-only for diagnosis.'
    exit 1
fi
install -o root -g root -m 644 "$private_stage/witch-player-offsite.timer" /etc/systemd/system/witch-player-offsite.timer
systemctl daemon-reload
systemctl enable --now witch-player-offsite.timer
echo 'OFFSITE_SETUP_COMPLETE: encrypted upload and read-back hash verified; hourly timer enabled.'
/usr/bin/python3 -c 'import json; print(json.dumps(json.load(open("/var/lib/witch-player-offsite/last-success.json")), sort_keys=True))'
echo 'Mac decryption/restore test and external failure notifications are still required.'
