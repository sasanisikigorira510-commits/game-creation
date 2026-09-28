#!/usr/bin/env bash
# Add sanitized backup monitoring; preserve DB, credentials, and closed game routes.
set -euo pipefail
umask 077
[[ $EUID == 0 && $# == 1 ]] || { echo 'Usage: sudo bash install-backup-health.sh STAGING'; exit 1; }
source_dir=$(realpath -- "$1")
cd /opt/witch-player/server
for target in /opt/witch-player/server/backup_health.py /etc/systemd/system/witch-player.service.d/backup-health.conf /etc/systemd/system/witch-player-health.service /etc/systemd/system/witch-player-health.timer; do
    [[ ! -e "$target" ]] || { echo 'Health setup already present; inspect instead of reinstalling.'; exit 1; }
done
printf '%s  %s\n' \
  bd51735e23c5ed61dd224cb5e5449f1e3666db5c44e668b2eb7b0676f752596a /opt/witch-player/server/application.py \
  d358a729c5158f1c304a319e4fc9559ec73c101d0584b3e3710226bb2419d0f3 /opt/witch-player/server/production.py \
  90611973d230a908187abeb31e12b8734cf595d74e6d007101b27e4356bdbf6f /etc/caddy/Caddyfile | sha256sum --check --status
stage=$(mktemp -d /var/tmp/nasus-health-install.XXXXXXXX)
for name in application.py production.py backup_health.py publish_backup_health.py witch-player-health.service witch-player-health.timer backup-health.conf Caddyfile-health-only SHA256SUMS; do
    install -m 600 "$source_dir/$name" "$stage/$name"
done
(cd "$stage" && sha256sum --check SHA256SUMS)
/usr/bin/python3 -m py_compile "$stage/application.py" "$stage/production.py" "$stage/backup_health.py" "$stage/publish_backup_health.py"
WITCH_DOMAIN=api.nasus-games.com ACME_EMAIL=sasanisikigorira@yahoo.co.jp /usr/bin/caddy validate --adapter caddyfile --config "$stage/Caddyfile-health-only"
systemd-analyze verify "$stage/witch-player-health.service" "$stage/witch-player-health.timer"
install -m 600 /opt/witch-player/server/application.py "$stage/application.previous.py"
install -m 600 /opt/witch-player/server/production.py "$stage/production.previous.py"
install -m 600 /etc/caddy/Caddyfile "$stage/Caddyfile.previous"
changed=0
rollback() {
    result=$?
    trap - EXIT
    if [[ $result != 0 && $changed == 1 ]]; then
        set +e
        systemctl disable --now witch-player-health.timer
        install -m 644 "$stage/application.previous.py" /opt/witch-player/server/application.py
        install -m 644 "$stage/production.previous.py" /opt/witch-player/server/production.py
        install -m 644 "$stage/Caddyfile.previous" /etc/caddy/Caddyfile
        if [[ -e /etc/systemd/system/witch-player.service.d/backup-health.conf ]]; then
            mv /etc/systemd/system/witch-player.service.d/backup-health.conf "$stage/backup-health.disabled.conf"
        fi
        systemctl daemon-reload
        systemctl restart witch-player
        systemctl reload caddy
        echo "INSTALL_FAILED: previous API/proxy restored; diagnostics retained at $stage"
    fi
    exit "$result"
}
trap rollback EXIT
changed=1
install -m 644 "$stage/publish_backup_health.py" /usr/local/lib/nasus-backup/publish_backup_health.py
install -m 644 "$stage/witch-player-health.service" /etc/systemd/system/witch-player-health.service
install -m 644 "$stage/witch-player-health.timer" /etc/systemd/system/witch-player-health.timer
systemctl daemon-reload
systemctl start witch-player-health.service
/usr/bin/python3 -c 'import json; x=json.load(open("/run/witch-player-health/status.json")); assert x["Healthy"] is True, "Backups not healthy; API change cancelled"'
for name in application.py production.py backup_health.py; do
    install -m 644 "$stage/$name" "/opt/witch-player/server/$name"
done
install -d -m 755 /etc/systemd/system/witch-player.service.d
install -m 644 "$stage/backup-health.conf" /etc/systemd/system/witch-player.service.d/backup-health.conf
install -m 644 "$stage/Caddyfile-health-only" /etc/caddy/Caddyfile
systemctl daemon-reload
systemctl enable --now witch-player-health.timer
systemctl restart witch-player
systemctl reload caddy
healthy=0
for attempt in {1..15}; do
    get_code=$(curl --silent --show-error --max-time 5 --resolve api.nasus-games.com:443:127.0.0.1 -o /dev/null -w '%{http_code}' https://api.nasus-games.com/healthz) || get_code=000
    head_code=$(curl --head --silent --show-error --max-time 5 --resolve api.nasus-games.com:443:127.0.0.1 -o /dev/null -w '%{http_code}' https://api.nasus-games.com/healthz) || head_code=000
    if [[ $get_code == 200 && $head_code == 200 ]]; then healthy=1; break; fi
    sleep 1
done
[[ $healthy == 1 ]] || { echo 'Health verification failed'; exit 1; }
closed_code=$(curl --silent --show-error --max-time 5 --resolve api.nasus-games.com:443:127.0.0.1 -o /dev/null -w '%{http_code}' https://api.nasus-games.com/)
[[ $closed_code == 503 ]] || { echo 'Expected maintenance gate missing'; exit 1; }
systemctl is-active --quiet witch-player-health.timer
echo 'BACKUP_HEALTH_ENABLED: GET/HEAD healthy; game/admin routes remain blocked.'
echo 'Backup failure/stopped timers or cloud success older than 2 hours now cause health failure.'
echo 'A publisher heartbeat older than 3 minutes also fails closed. Email delivery must be checked separately.'
echo "Previous configuration preserved at $stage"
