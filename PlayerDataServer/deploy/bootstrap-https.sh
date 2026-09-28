#!/usr/bin/env bash
# One-time Ubuntu bootstrap. Public access is restricted to /healthz.
# Stop and inspect any failure; never rerun by deleting the database.
set -euo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
[[ $# == 1 ]] || { echo 'Expected the staged configuration directory.' >&2; exit 1; }
source_dir=$(realpath -- "$1")
# runuser preserves cwd. Gunicorn validates cwd even for --version, so never
# invoke it inside the private upload directory or the administrator's home.
cd /opt/witch-player/server
. /etc/os-release
[[ $ID == ubuntu && $VERSION_ID == 24.04 ]] || exit 1
for path in /etc/witch-player/server.env /etc/witch-player/caddy.env /etc/witch-player/operators.json /etc/witch-player/initial-admin-token /etc/caddy /etc/systemd/system/caddy.service.d /etc/apt/sources.list.d/caddy-stable.list /usr/share/keyrings/caddy-stable-archive-keyring.gpg; do
    [[ ! -e $path && ! -L $path ]] || { echo "Existing configuration requires review: $path" >&2; exit 1; }
done
for unit in caddy.service witch-player.service witch-player-backup.service witch-player-backup.timer; do
    [[ $(systemctl show "$unit" -p LoadState --value) == not-found ]] || { echo "Existing unit requires review: $unit" >&2; exit 1; }
done
for path in /var/lib/witch-player /var/backups/witch-player; do
    [[ -d $path && ! -L $path && -z $(find "$path" -mindepth 1 -maxdepth 1 -print -quit) ]] || { echo "Directory must already exist and be empty: $path" >&2; exit 1; }
done
runuser -u witchplayer -- /opt/witch-player/venv/bin/python -m pip check
runuser -u witchplayer -- /opt/witch-player/venv/bin/gunicorn --version

staging_dir=$(mktemp -d /var/tmp/witch-https-install.XXXXXXXX)
for file in Caddyfile server.env caddy.env; do
    [[ -f $source_dir/$file && ! -L $source_dir/$file ]] || exit 1
    install -m 600 "$source_dir/$file" "$staging_dir/$file"
done
set -a
. "$staging_dir/server.env"
. "$staging_dir/caddy.env"
set +a
[[ $WITCH_DOMAIN == api.nasus-games.com && $WITCH_PUBLIC_ORIGIN == https://api.nasus-games.com && $WITCH_DATA_DIR == /var/lib/witch-player && $WITCH_OPERATORS_FILE == /etc/witch-player/operators.json ]] || { echo 'Unexpected deployment configuration.' >&2; exit 1; }
[[ -z ${WITCH_APPLE_BUNDLE_ID:-} ]] || { echo 'Apple billing must remain disabled.' >&2; exit 1; }

# Verify the already-installed allowlisted code before executing it as root.
/opt/witch-player/venv/bin/python - <<'PY'
import hashlib, json
from pathlib import Path
root = Path('/opt/witch-player/server')
for name, digest in json.loads((root / 'SHA256.json').read_text()).items():
    file = root / name
    if file.is_symlink() or not file.is_file() or hashlib.sha256(file.read_bytes()).hexdigest() != digest:
        raise SystemExit('Installed release checksum mismatch: ' + name)
print('Installed release checksums verified.')
PY

caddy_unmasked=0
api_started=0
failed() {
    echo 'Bootstrap stopped. Keep all files and data for inspection; do not rerun or delete them.' >&2
    if [[ $caddy_unmasked == 1 ]]; then systemctl disable --now caddy.service || true; fi
    if [[ $api_started == 1 ]]; then systemctl disable --now witch-player.service || true; fi
}
trap failed ERR
# Prevent the package's default website from starting during installation.
systemctl mask caddy.service
(
    umask 022
    apt-get update
    apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl gnupg
)
curl --proto '=https' --tlsv1.2 -fsSL https://dl.cloudsmith.io/public/caddy/stable/gpg.key -o "$staging_dir/caddy.key"
gpg --batch --dearmor --output "$staging_dir/caddy.gpg" "$staging_dir/caddy.key"
curl --proto '=https' --tlsv1.2 -fsSL https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt -o "$staging_dir/caddy.list"
install -m 644 "$staging_dir/caddy.gpg" /usr/share/keyrings/caddy-stable-archive-keyring.gpg
install -m 644 "$staging_dir/caddy.list" /etc/apt/sources.list.d/caddy-stable.list
(
    umask 022
    apt-get update
    apt-get install -y caddy
)
if [[ -f /etc/caddy/Caddyfile ]]; then
    install -m 600 /etc/caddy/Caddyfile "$staging_dir/Caddyfile.package-default"
fi
install -m 644 "$staging_dir/Caddyfile" /etc/caddy/Caddyfile
install -m 600 "$staging_dir/server.env" /etc/witch-player/server.env
install -m 600 "$staging_dir/caddy.env" /etc/witch-player/caddy.env
install -d -m 755 /etc/systemd/system/caddy.service.d
install -m 644 /opt/witch-player/server/deploy/caddy-env.conf /etc/systemd/system/caddy.service.d/witch-player.conf
# Validation provisions configuration but does not start a listening server.
caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile

runuser -u witchplayer -- /opt/witch-player/venv/bin/python /opt/witch-player/server/maintenance.py init --data-dir /var/lib/witch-player --instance-id "$WITCH_INSTANCE_ID"
/opt/witch-player/venv/bin/python /opt/witch-player/server/maintenance.py admin-add --file /etc/witch-player/operators.json --name support-owner --role operator --token-out /etc/witch-player/initial-admin-token
chown witchplayer:witchplayer /etc/witch-player/operators.json
chmod 600 /etc/witch-player/operators.json /etc/witch-player/initial-admin-token
# The raw administrator token stays root-only; it is never printed or sent to the app.
for unit in witch-player.service witch-player-backup.service witch-player-backup.timer; do
    install -m 644 "/opt/witch-player/server/deploy/$unit" "/etc/systemd/system/$unit"
done
systemctl daemon-reload
api_started=1
systemctl start witch-player.service
curl --fail --silent --show-error --retry 10 --retry-connrefused --retry-delay 1 --max-time 5 -H "Host: $WITCH_DOMAIN" -H 'X-Forwarded-Proto: https' -H 'X-Real-IP: 127.0.0.1' http://127.0.0.1:8788/healthz
systemctl start witch-player-backup.service
systemctl unmask caddy.service
caddy_unmasked=1
systemctl start caddy.service
curl --fail --silent --show-error --retry 12 --retry-all-errors --retry-delay 5 --connect-timeout 5 --max-time 10 --resolve "$WITCH_DOMAIN:443:127.0.0.1" "https://$WITCH_DOMAIN/healthz"
[[ $(curl --silent --show-error --output /dev/null --write-out '%{http_code}' --max-time 10 --resolve "$WITCH_DOMAIN:443:127.0.0.1" "https://$WITCH_DOMAIN/") == 503 ]]
[[ $(curl --silent --show-error --request POST --output /dev/null --write-out '%{http_code}' --max-time 10 --resolve "$WITCH_DOMAIN:443:127.0.0.1" "https://$WITCH_DOMAIN/v1/accounts") == 503 ]]
systemctl enable witch-player.service caddy.service
systemctl enable --now witch-player-backup.timer
trap - ERR
echo
echo 'HTTPS_BOOTSTRAP_COMPLETE: health endpoint active; all game/admin routes remain blocked.'
echo 'Local verified backup enabled. Off-server backup and recovery/monitoring are still required.'
