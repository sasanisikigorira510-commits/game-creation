#!/usr/bin/env bash
# Fresh-server staging only. Does not create player data or start any service.
set -euo pipefail
umask 077

[[ $EUID == 0 ]] || { echo 'Run this script with sudo.' >&2; exit 1; }
[[ $# == 2 && $2 =~ ^[0-9a-f]{64}$ ]] || { echo 'Usage: install-code-only.sh ARCHIVE SHA256' >&2; exit 1; }
[[ -f $1 && ! -L $1 ]] || { echo 'Expected a regular release archive.' >&2; exit 1; }
for target in /opt/witch-player /etc/witch-player /var/lib/witch-player /var/backups/witch-player; do
    [[ ! -e $target && ! -L $target ]] || { echo "Already exists; refusing to overwrite: $target" >&2; exit 1; }
done
if getent passwd witchplayer >/dev/null || getent group witchplayer >/dev/null; then
    echo 'Existing witchplayer account/group requires review.' >&2
    exit 1
fi
. /etc/os-release
[[ $ID == ubuntu && $VERSION_ID == 24.04 ]] || { echo 'Expected Ubuntu 24.04.' >&2; exit 1; }

# Snapshot before validating, so subsequent extraction uses exactly these bytes.
staging_dir=$(mktemp -d /var/tmp/witch-code-install.XXXXXXXX)
install -m 600 -- "$1" "$staging_dir/release.tar.gz"
printf '%s  %s\n' "$2" "$staging_dir/release.tar.gz" | sha256sum --check --status
python3 - "$staging_dir/release.tar.gz" <<'PY'
import hashlib, json, sys, tarfile
from pathlib import PurePosixPath
with tarfile.open(sys.argv[1], 'r:gz') as archive:
    members = archive.getmembers()
    names = [m.name for m in members]
    if len(names) != len(set(names)):
        raise SystemExit('Duplicate archive entries')
    for member in members:
        path = PurePosixPath(member.name)
        if not member.isfile() or path.is_absolute() or '..' in path.parts:
            raise SystemExit('Unsafe archive entry')
    manifest = json.load(archive.extractfile('SHA256.json'))
    if set(names) != set(manifest) | {'SHA256.json'}:
        raise SystemExit('Manifest mismatch')
    for name, expected in manifest.items():
        if hashlib.sha256(archive.extractfile(name).read()).hexdigest() != expected:
            raise SystemExit('File checksum mismatch: ' + name)
print('Release manifest verified.')
PY

apt-get update
apt-get install -y python3-venv
install -d -m 755 /opt/witch-player /opt/witch-player/server
tar --extract --gzip --file "$staging_dir/release.tar.gz" --directory /opt/witch-player/server --no-same-owner --no-same-permissions
chmod -R u=rwX,go=rX /opt/witch-player/server
python3 -m venv /opt/witch-player/venv
/opt/witch-player/venv/bin/python -m pip install -r /opt/witch-player/server/requirements-production.txt
/opt/witch-player/venv/bin/python -m pip check
/opt/witch-player/venv/bin/python -m compileall -q /opt/witch-player/server
# The service runs as witchplayer, not root. This directory contains only
# installed runtime/dependency files, never credentials or player data.
chmod -R u=rwX,go=rX /opt/witch-player/venv
useradd --system --user-group --home-dir /var/lib/witch-player --shell /usr/sbin/nologin witchplayer
install -d -o witchplayer -g witchplayer -m 700 /var/lib/witch-player /var/backups/witch-player
install -d -o root -g witchplayer -m 750 /etc/witch-player
cd /opt/witch-player/server
runuser -u witchplayer -- /opt/witch-player/venv/bin/python -m pip check
runuser -u witchplayer -- /opt/witch-player/venv/bin/gunicorn --version
echo 'CODE_STAGING_COMPLETE: no database created; no API/HTTPS service started.'
echo "Verified archive retained at: $staging_dir/release.tar.gz"
