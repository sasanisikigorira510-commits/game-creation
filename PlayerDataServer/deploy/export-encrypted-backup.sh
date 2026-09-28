#!/usr/bin/env bash
# One-shot DB-only export. Neither raw credentials nor decryption keys leave
# their original hosts. This does not modify the live DB or install a timer.
set -euo pipefail
umask 077
[[ $EUID == 0 && $# == 2 ]] || { echo 'Usage: sudo bash export-encrypted-backup.sh STAGING RECIPIENT_SHA256' >&2; exit 1; }
source_dir=$(realpath -- "$1")
recipient_digest=$2
[[ $recipient_digest =~ ^[0-9a-f]{64}$ ]] || exit 1
cd /opt/witch-player/server
private_work=$(mktemp -d /var/tmp/nasus-encrypted-export.XXXXXXXX)
install -m 600 "$source_dir/recipient.txt" "$private_work/recipient.txt"
install -m 600 "$source_dir/age-v1.3.2-linux-amd64.tar.gz" "$private_work/age.tar.gz"
printf '%s  %s\n' "$recipient_digest" "$private_work/recipient.txt" cbe24006683f8eb669266162894b9a522a1af52f2665fbc63a4bb032ed26ac10 "$private_work/age.tar.gz" | sha256sum --check --status
tar -xzf "$private_work/age.tar.gz" -C "$private_work" --no-same-owner age/age
"$private_work/age/age" --version
backup_path=$(runuser -u witchplayer -- /opt/witch-player/venv/bin/python /opt/witch-player/server/maintenance.py backup --database /var/lib/witch-player/players.sqlite --output-dir /var/backups/witch-player)
[[ $backup_path =~ ^/var/backups/witch-player/players-[0-9]{8}T[0-9]{12}Z-[a-f0-9]{8}\.sqlite$ ]] || { echo 'Unexpected snapshot path.' >&2; exit 1; }
runuser -u witchplayer -- /opt/witch-player/venv/bin/python /opt/witch-player/server/maintenance.py verify --backup "$backup_path"
base_name=$(basename "$backup_path")
tar -czf - -C /var/backups/witch-player "$base_name" "$base_name.json" | "$private_work/age/age" --encrypt --recipients-file "$private_work/recipient.txt" --output "$private_work/players-backup.tar.gz.age"
(
    cd "$private_work"
    sha256sum players-backup.tar.gz.age > SHA256SUMS
)
# Only ciphertext and its checksum become readable to the SSH account.
export_parent=/home/ubuntu/nasus-backup-export
[[ ! -L $export_parent ]] || { echo 'Refusing symlink export directory.' >&2; exit 1; }
if [[ -e $export_parent ]]; then
    [[ -d $export_parent && $(stat -c %U "$export_parent") == ubuntu && $(stat -c %a "$export_parent") == 700 ]] || exit 1
else
    install -d -o ubuntu -g ubuntu -m 700 "$export_parent"
fi
export_dir=$(mktemp -d "$export_parent/export.XXXXXXXX")
install -o ubuntu -g ubuntu -m 600 "$private_work/players-backup.tar.gz.age" "$export_dir/players-backup.tar.gz.age"
install -o ubuntu -g ubuntu -m 600 "$private_work/SHA256SUMS" "$export_dir/SHA256SUMS"
chown ubuntu:ubuntu "$export_dir"
echo "ENCRYPTED_EXPORT_READY: $export_dir"
echo 'Only the encrypted database snapshot and its manifest were exported. No administrator token was copied.'
