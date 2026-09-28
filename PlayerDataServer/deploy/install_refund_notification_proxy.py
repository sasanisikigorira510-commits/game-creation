"""Add one approved Sandbox notification route. No Apple settings or DB writes."""
import hashlib
import json
import os
from pathlib import Path
import pwd
import stat
import subprocess
import tempfile
import time

CADDY = Path('/etc/caddy/Caddyfile')
EVIDENCE = Path('/var/lib/nasus-refund-proxy-20260928')
PREVIOUS_SHA = 'ed400215f4741f7c40a69cd5cc2bab61da26e507afba9f86df635d41fca8025d'
PUBLIC_PATH = '/sandbox-refund-20260928/apple/notifications'
ROUTE = b'''    # Dedicated Sandbox notification only; no device or review routes.
    @refund_sandbox_notice {
        path /sandbox-refund-20260928/apple/notifications
        method POST
    }
    handle @refund_sandbox_notice {
        rewrite * /v1/store/apple/notifications
        reverse_proxy 127.0.0.1:8793 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-Proto https
            transport http {
                dial_timeout 3s
                response_header_timeout 45s
            }
        }
    }
'''
SERVICES = ('witch-player.service', 'witch-player-qa.service', 'caddy.service',
            'witch-player-refund-sandbox-notifications.service')


def require(ok):
    if not ok:
        raise ValueError('Notification proxy precondition or verification failed')


def read(path, uid=0, private=False):
    path = Path(path)
    require(not any(p.is_symlink() for p in (path, *path.parents)))
    with path.open('rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == uid
                and not info.st_mode & (0o077 if private else 0o022)
                and info.st_size < 1024 * 1024)
        return stream.read()


def candidate(previous):
    require(hashlib.sha256(previous).hexdigest() == PREVIOUS_SHA)
    anchor = b'    handle_path /qa/* {\n'
    require(previous.count(anchor) == 1 and b'refund_sandbox_notice' not in previous)
    return previous.replace(anchor, ROUTE + anchor, 1)


def run(args):
    result = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True,
                            text=True, timeout=75, cwd='/',
                            env={'PATH':'/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL':'C'})
    require(result.returncode == 0)
    return result.stdout.strip()


def ctl(*args):
    return run(['/usr/bin/systemctl', *args])


def write_new(path, raw, mode=0o600):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, mode)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush(); os.fchmod(stream.fileno(), mode); os.fsync(stream.fileno())


def stage(raw):
    fd, name = tempfile.mkstemp(prefix='Caddyfile.refund-notification-', dir=CADDY.parent)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush(); os.fchmod(stream.fileno(), 0o644); os.fsync(stream.fileno())
    path = Path(name)
    # Use the same configured environment and user as the live proxy.
    run(['/bin/bash', '-c',
         'set -e; set -a; . /etc/witch-player/caddy.env; set +a; '
         'exec /usr/sbin/runuser -u caddy -- /usr/bin/caddy validate '
         '--config "$1" --adapter caddyfile', 'refund-notification-validate', str(path)])
    return path


def reload_proxy():
    ctl('reload', 'caddy.service')
    require(ctl('show', 'caddy.service', '-p', 'ReloadResult', '--value') == 'success')


def status(path, post=False):
    args = ['/usr/bin/curl', '--silent', '--show-error', '--noproxy', '*',
            '--connect-timeout', '5', '--max-time', '55', '--output', '/dev/null',
            '--write-out', '%{http_code}', '--resolve', 'api.nasus-games.com:443:127.0.0.1']
    if post:
        args += ['--header', 'Content-Type: application/json', '--data-binary',
                 '{"signedPayload":"unsigned-invalid-fixture"}']
    return int(run(args + ['https://api.nasus-games.com' + path]))


def install():
    require(os.geteuid() == 0); os.umask(0o077)
    previous = read(CADDY)
    proposed = candidate(previous)
    require(not EVIDENCE.exists())
    for path in (CADDY.parent, EVIDENCE.parent):
        require(not path.is_symlink() and path.stat().st_uid == 0
                and not path.stat().st_mode & 0o022)
    for service in SERVICES:
        require(ctl('is-active', service) == 'active')
    owner = pwd.getpwnam('nasusrefund')
    gate = json.loads(read('/etc/nasus-refund-sandbox-20260928/gate.json', owner.pw_uid, True))
    require(gate['IssuedUnix'] <= time.time() < gate['ExpiresUnix'] - 600)
    # Record read-only baseline responses. An expiring QA gate is not renewed.
    require(status('/healthz') == 200 and status('/') == 503)
    require(status('/qa/healthz') == 404 and status(PUBLIC_PATH, True) == 503)
    EVIDENCE.mkdir(mode=0o700)
    write_new(EVIDENCE/'previous-Caddyfile', previous)
    write_new(EVIDENCE/'candidate-Caddyfile', proposed)
    staged = stage(proposed)  # Validate before touching the active file.
    require(read(CADDY) == previous)
    changed = False
    try:
        print('1/2 Validated: add only the exact Sandbox POST notification route.', flush=True)
        os.replace(staged, CADDY); changed = True
        reload_proxy()
        require(status(PUBLIC_PATH, True) == 400)  # Unsigned data must be rejected.
        for path, method_post, expected in (
            (PUBLIC_PATH, False, 503), (PUBLIC_PATH+'/', True, 503),
            ('/sandbox-refund-20260928/admin/refunds/preview', True, 503),
            ('/sandbox-refund-20260928/v1/accounts', True, 503),
            ('/v1/store/apple/notifications', True, 503),
            ('/healthz', False, 200), ('/qa/healthz', False, 404), ('/', False, 503)):
            require(status(path, method_post) == expected)
        for service in SERVICES:
            require(ctl('is-active', service) == 'active')
        require(read(CADDY) == proposed)
        write_new(EVIDENCE/'result.json', json.dumps({
            'Status':'SANDBOX_NOTIFICATION_PROXY_READY', 'VerifiedUnix':time.time(),
            'PublicURL':'https://api.nasus-games.com'+PUBLIC_PATH,
            'PreviousSHA256':PREVIOUS_SHA, 'CurrentSHA256':hashlib.sha256(proposed).hexdigest(),
            'AppleURLConfigured':False, 'RealNotificationTested':False,
            'ExistingDatabasesChanged':False, 'GateExpiresUnix':gate['ExpiresUnix']
        }, indent=2).encode())
        print('2/2 SANDBOX_NOTIFICATION_PROXY_READY: unsigned rejected; existing routes unchanged.', flush=True)
    except Exception:
        if changed:
            require(read(CADDY) == proposed)  # Never overwrite concurrent admin edits.
            rollback = stage(previous)
            require(read(CADDY) == proposed)
            os.replace(rollback, CADDY)
            reload_proxy()
            require(read(CADDY) == previous and status('/healthz') == 200)
            print('Previous proxy configuration restored; evidence retained.', flush=True)
        raise


if __name__ == '__main__':
    try:
        install()
    except Exception:
        print('NOTIFICATION_PROXY_STOPPED: inspect stage and retained evidence; do not rerun blindly.', flush=True)
        raise SystemExit(1)
