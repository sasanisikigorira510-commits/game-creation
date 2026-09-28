"""Recover the pinned 20260926 QA proxy failure; never rerun installation.

Uses the immutable failed installer only for constants/read-only helpers. Restore
the baseline first, then resume the already verified runtime if all checks pass.
No migrations, package installation, key changes, or gate lifetime extension.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import stat
import sys
import tempfile
import time

sys.path.insert(0, str(Path(__file__).resolve().parent))
import install_apple_qa as qa

MANIFEST_HASH = '62d32f12a556b6375ea537429b95d1e7ab4e099925c3938d45784bfca60b7717'


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def check_parent(path):
    for parent in (path.parent, *path.parent.parents):
        info = parent.lstat()
        qa.require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0
                   and not info.st_mode & 0o022)


def write_exact(path, raw, mode=0o600):
    # Create private, finish contents, then set the requested final mode by fd.
    # os.open(mode=0644) alone is narrowed to 0600 under the installer umask.
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as out:
        out.write(raw)
        out.flush()
        os.fchmod(out.fileno(), mode)
        os.fsync(out.fileno())


def validate_as_caddy(path):
    script = ('set -e; set -a; . /etc/witch-player/caddy.env; set +a; '
              'exec /usr/sbin/runuser -u caddy -- /usr/bin/caddy validate '
              '--config "$1" --adapter caddyfile')
    qa.run(['/bin/bash', '-c', script, 'qa-recovery-validate', str(path)])


def replace_proxy(raw, allowed):
    check_parent(qa.CADDY)
    qa.require(qa.safe_file(qa.CADDY, 0) in allowed)
    # No fixed temporary filename and no overwrite of retained failure evidence.
    fd, name = tempfile.mkstemp(prefix='Caddyfile.qa-recovery-', dir=qa.CADDY.parent)
    with os.fdopen(fd, 'wb') as out:
        out.write(raw)
        out.flush()
        os.fchmod(out.fileno(), 0o644)
        os.fsync(out.fileno())
    staged = Path(name)
    validate_as_caddy(staged)
    qa.require(qa.safe_file(qa.CADDY, 0) in allowed)
    os.replace(staged, qa.CADDY)
    qa.ctl('reload', 'caddy.service')
    qa.require(qa.ctl('show', 'caddy.service', '-p', 'ReloadResult', '--value') == 'success')
    qa.require(qa.safe_file(qa.CADDY, 0) == raw)
    qa.require(stat.S_IMODE(qa.CADDY.stat().st_mode) == 0o644)


def closed_baseline():
    for path, expected in (('/healthz', 200), ('/', 503), ('/admin', 503), ('/qa/healthz', 503)):
        qa.require(qa.status('https://' + qa.DOMAIN + path) == expected)


def validate_existing_runtime(candidate):
    ubuntu = pwd.getpwnam('ubuntu')
    account = pwd.getpwnam('witchplayer')
    raw = qa.safe_file(qa.SOURCE / 'manifest.json', ubuntu.pw_uid)
    qa.require(digest(raw) == MANIFEST_HASH)
    for name, expected in json.loads(raw).items():
        qa.require(re.fullmatch(r'(?:[a-z_]+\.py|gunicorn\.conf\.py|catalog\.json|wheels/[A-Za-z0-9_.-]+\.whl)', name))
        check_parent(qa.TARGET / name)
        qa.require(digest(qa.safe_file(qa.TARGET / name, 0)) == expected)
    qa.require(qa.safe_file(qa.TARGET / 'candidate-Caddyfile', 0) == candidate)
    qa.require(qa.safe_file(qa.UNIT, 0) == qa.SERVICE.encode())
    qa.require(qa.ctl('show', qa.UNIT.name, '-p', 'UnitFileState', '--value') == 'static')
    gate = json.loads(qa.safe_file(qa.CONFIG / 'gate.json', account.pw_uid, 4096, True))
    qa.require(set(gate) == {'Version', 'IssuedUnix', 'ExpiresUnix', 'TokenSha256'})
    qa.require(gate['Version'] == 1 and gate['IssuedUnix'] <= time.time()
               and 0 < gate['ExpiresUnix'] - gate['IssuedUnix'] <= 86400
               and gate['ExpiresUnix'] > time.time() + 600)
    client = json.loads(qa.safe_file(qa.SOURCE / 'client.json', ubuntu.pw_uid, 4096, True))
    qa.require(client['BaseUrl'] == 'https://' + qa.DOMAIN + '/qa'
               and client['ExperimentalAppleLinking'] is True)
    token = client['QaAccessKey']
    qa.require(isinstance(token, str) and re.fullmatch('[a-f0-9]{64}', token)
               and digest(token.encode()) == gate['TokenSha256'])
    return token, gate['ExpiresUnix']


def main():
    qa.require(os.geteuid() == 0 and len(sys.argv) == 1)
    os.umask(0o077)
    check_parent(qa.CADDY)
    check_parent(qa.TARGET / 'previous-Caddyfile')
    previous = qa.safe_file(qa.TARGET / 'previous-Caddyfile', 0)
    qa.require(digest(previous) == qa.BASE_HASH)
    candidate = previous.replace(b'    @health {', qa.PROXY.encode() + b'    @health {', 1)
    current = qa.safe_file(qa.CADDY, 0)
    qa.require(current in (previous, candidate))
    qa.require(qa.ctl('show', qa.UNIT.name, '-p', 'ActiveState', '--value') == 'inactive')
    evidence = Path(tempfile.mkdtemp(prefix='nasus-qa-recovery-', dir='/var/tmp'))
    write_exact(evidence / 'observed-Caddyfile', current)
    write_exact(evidence / 'metadata.json', json.dumps({
        'ObservedMode': oct(stat.S_IMODE(qa.CADDY.stat().st_mode)),
        'ObservedSha256': digest(current), 'BaselineSha256': qa.BASE_HASH,
    }).encode())
    stage = 'restore-baseline'
    try:
        print('1/3 Restore pinned baseline with explicit readable permissions; QA stays stopped.', flush=True)
        replace_proxy(previous, (previous, candidate))
        closed_baseline()
        print('BASELINE_RESTORED: health=200; root/admin/QA closed; Caddy reload successful.', flush=True)
        stage = 'check-existing-runtime'
        token, expires = validate_existing_runtime(candidate)
        print('2/3 Resume hash-verified existing QA runtime; keep original expiration.', flush=True)
        qa.ctl('start', qa.UNIT.name)
        stage = 'qa-loopback'
        for _ in range(20):
            request = qa.urllib.request.Request('http://127.0.0.1:8790/healthz', headers={
                'Host': qa.DOMAIN, 'X-Real-IP': '127.0.0.1', 'X-Forwarded-Proto': 'https',
                'Authorization': 'NasusQA ' + token})
            try:
                with qa.open_request(request, timeout=3) as response:
                    if response.status == 200:
                        break
            except (OSError, qa.urllib.error.HTTPError):
                pass
            time.sleep(1)
        else:
            raise ValueError('loopback check failed')
        stage = 'qa-proxy'
        replace_proxy(candidate, (previous,))
        stage = 'https-boundary'
        print('3/3 Verify HTTPS boundary: valid key, rejected keys, health and closed admin.', flush=True)
        for path, key, expected in (
            ('/healthz', None, 200), ('/', None, 503), ('/admin', None, 503),
            ('/qa/healthz', None, 404), ('/qa/healthz', '0' * 64, 404),
            ('/qa/healthz', token, 200), ('/qa/admin', token, 404), ('/qa/', token, 404)):
            qa.require(qa.status('https://' + qa.DOMAIN + path, key) == expected)
        qa.require(qa.ctl('is-active', 'witch-player.service') == 'active')
        print(json.dumps({'Status': 'APPLE_QA_RECOVERY_VERIFIED', 'ExpiresUnix': expires,
                          'SchemaChanged': False, 'KeysChanged': False, 'PublicGameClosed': True,
                          'RealAppleAuthenticationTested': False}), flush=True)
    except BaseException:
        restored = False
        try:
            replace_proxy(previous, (previous, candidate))
            qa.ctl('stop', qa.UNIT.name)
            closed_baseline()
            restored = qa.ctl('show', qa.UNIT.name, '-p', 'ActiveState', '--value') == 'inactive'
        except BaseException:
            # Even if proxy validation/reload fails, stop only the QA service.
            try:
                qa.ctl('stop', qa.UNIT.name)
            except BaseException:
                pass
        print('QA_RECOVERY_STOPPED: stage=' + stage + '; baseline_verified=' + str(restored), flush=True)
        raise SystemExit(1)
    finally:
        print('Recovery evidence: ' + str(evidence), flush=True)


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print('QA_RECOVERY_PRECHECK_STOPPED: unexpected state; no secret details printed.', flush=True)
        raise SystemExit(1)
