"""One authenticated, fixed-scope installation of baseline reason logging.

No sudo policy, worker enabling, database or credential operations.
Failure restores only our own exact health drop-in; artifacts are retained.
"""
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time

SOURCE = Path('/home/ubuntu/nasus-deploy/20260925-health-observer-01/observe_backup_health.py')
OBS_HASH = 'fb715fe7d0e80e91543c4d91ddf5f6ac0f06184716951fbe7b39943bbab32218'
TARGET = Path('/usr/local/lib/nasus-health-observer-20260925')
MARKER = Path('/run/witch-player-health/status.json')
HOOK = Path('/etc/systemd/system/witch-player-health.service.d/40-reason-logging.conf')
TEXT = ('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '
        '/usr/local/lib/nasus-health-observer-20260925/observe.py\n')
PINS = {
    '/etc/systemd/system/witch-player-health.service': 'fdc6732cc6c28081980df8e72d2751cd5a1c3b445f44e17e07f5e26962a2d4e5',
    '/usr/local/lib/nasus-backup/publish_backup_health.py': '8b4b3e59b2586b2946ffaee39e501a835d799e1f87b21fe4f24a48e4b23412ac',
    '/etc/caddy/Caddyfile': '75ceb4d8ce8b107f11786354a7ca56442d5bb41a960ffa251e0f20bf724efa44',
    '/opt/witch-player/server/application.py': '4fdbfafde3a91b4b633f510338296373dbbc2aac06c8722721467547db19f2dd',
}
ENV = {'PATH': '/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL': 'C'}


def require(ok):
    if not ok: raise ValueError('Precondition failed')


def trusted(path, directory=False):
    info = path.lstat()
    require(info.st_uid == 0 and not info.st_mode & 0o022
            and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
    for parent in path.parents:
        info = parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)


def run(args, check=True):
    result = subprocess.run(args, capture_output=True, text=True, timeout=60,
                            stdin=subprocess.DEVNULL, env=ENV)
    require(not check or result.returncode == 0)
    return result.stdout.strip()


def ctl(*args, check=True):
    return run(['/usr/bin/systemctl', *args], check)


def pins():
    for name, expected in PINS.items():
        path = Path(name); trusted(path)
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected)


def state():
    require(ctl('is-enabled', 'witch-player-deletion.timer', check=False) == 'disabled')
    require(ctl('is-active', 'witch-player-deletion.timer', check=False) == 'inactive')
    for unit in ('witch-player.service', 'witch-player-health.timer',
                 'witch-player-backup.timer', 'witch-player-offsite.timer'):
        require(ctl('is-active', unit) == 'active')


def https():
    for suffix, expected in (('/healthz', '200'), ('/', '503')):
        code = run(['/usr/bin/curl', '--disable', '--silent', '--show-error', '--proto', '=https',
                    '--connect-timeout', '5', '--max-time', '15', '--output', '/dev/null',
                    '--write-out', '%{http_code}', 'https://api.nasus-games.com' + suffix])
        require(code == expected)


def create(path, raw, mode):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, mode)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno()); os.fchmod(stream.fileno(), mode)


def rollback():
    trusted(HOOK)
    require(HOOK.read_text() == TEXT)
    require(not (TARGET / 'disabled-reason-logging.conf').exists())
    HOOK.rename(TARGET / 'disabled-reason-logging.conf')
    ctl('daemon-reload'); ctl('start', 'witch-player-health.service')
    require(not ctl('show', 'witch-player-health.service', '-p', 'DropInPaths', '--value'))


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1)
    os.umask(0o077)
    pins(); state(); https()
    require(not ctl('show', 'witch-player-health.service', '-p', 'DropInPaths', '--value'))
    require(not TARGET.exists() and not TARGET.is_symlink())
    require(not HOOK.exists() and not HOOK.is_symlink())
    # Read unprivileged staging data, then verify those exact bytes before use.
    with SOURCE.open('rb') as source: raw = source.read(65537)
    require(len(raw) <= 65536 and hashlib.sha256(raw).hexdigest() == OBS_HASH)
    compile(raw, 'observe.py', 'exec')
    trusted(TARGET.parent, directory=True)
    if not HOOK.parent.exists(): HOOK.parent.mkdir(mode=0o755)
    trusted(HOOK.parent, directory=True)
    TARGET.mkdir(mode=0o755)
    create(TARGET / 'observe.py', raw, 0o644)
    started = time.time()
    added = False
    try:
        create(HOOK, TEXT.encode(), 0o644); added = True
        ctl('daemon-reload')
        require(ctl('show', 'witch-player-health.service', '-p', 'DropInPaths', '--value') == str(HOOK))
        run(['/usr/bin/systemd-analyze', 'verify', 'witch-player-health.service'])
        ctl('start', 'witch-player-health.service')
        marker = MARKER
        trusted(marker)
        require(marker.stat().st_size <= 4096)
        value = json.loads(marker.read_text())
        require(value.get('Healthy') is True and type(value.get('CheckedUnix')) in (float, int)
                and started <= value['CheckedUnix'] <= time.time())
        require(ctl('show', 'witch-player-health.service', '-p', 'Result', '--value') == 'success')
        pins(); state(); https()
        create(TARGET / 'installed.json', json.dumps({'InstalledUnix': time.time(),
               'PublicMarkerUnchangedFormat': True, 'WorkerStillDisabled': True}).encode(), 0o600)
        print('HEALTH_REASON_LOGGING_INSTALLED: baseline decisions unchanged; worker remains disabled.', flush=True)
    except BaseException:
        if added:
            try:
                rollback()
                print('HEALTH_REASON_CONFIG_RESTORED: artifacts retained; recheck public health.', flush=True)
            except Exception:
                print('HEALTH_REASON_ROLLBACK_INCOMPLETE: inspect retained artifacts.', flush=True)
        raise


if __name__ == '__main__':
    try: main()
    except Exception:
        print('HEALTH_REASON_INSTALL_STOPPED: no automatic retry; no private details printed.', flush=True)
        raise SystemExit(1)
