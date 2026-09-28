"""One explicitly approved 24-hour renewal of the existing isolated QA gate.

No DB, key, proxy, unit or production changes. Never retries a consumed renewal.
Failures after quiescing leave QA stopped and restore the previous expired gate.
"""
import datetime
import hashlib
import json
import math
import os
from pathlib import Path
import pwd
import re
import sys
import tempfile
import time
import urllib.error
import urllib.request

sys.path.insert(0, str(Path(__file__).resolve().parent))
from install_apple_staging import safe_file, require, run, write_new

GATE = Path('/etc/witch-player-qa/gate.json')
# Keep the single-use receipt outside the service-owned gate directory.
RECEIPT = Path('/etc/nasus-qa-renewal-20260928.original.json')
CODE = '/opt/nasus-qa-sandbox-20260927'
ROOT = '/var/lib/nasus-qa-sandbox-20260927'
UNIT = 'witch-player-qa.service'
DROP = '/etc/systemd/system/witch-player-qa.service.d/90-sandbox-isolation.conf'
CLIENT = Path('/home/ubuntu/nasus-deploy/20260926-apple-qa-01/client.json')


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result)
        result[key] = value
    return result


def renew_config(raw, token, now):
    gate = json.loads(raw, object_pairs_hook=unique)
    require(set(gate) == {'Version', 'IssuedUnix', 'ExpiresUnix', 'TokenSha256'})
    require(type(gate['Version']) is int and gate['Version'] == 1)
    require(all(type(v) in (int, float) and math.isfinite(v)
                for v in (now, gate['IssuedUnix'], gate['ExpiresUnix'])))
    require(0 < gate['ExpiresUnix'] - gate['IssuedUnix'] <= 86400)
    require(gate['ExpiresUnix'] <= now)
    require(isinstance(token, str) and re.fullmatch('[a-f0-9]{64}', token))
    require(hashlib.sha256(token.encode()).hexdigest() == gate['TokenSha256'])
    return dict(gate, IssuedUnix=int(now), ExpiresUnix=int(now) + 86400)


def ctl(*args):
    return run(['/usr/bin/systemctl', *args], 90)


def prop(name):
    return ctl('show', UNIT, '-p', name, '--value')


def validate_gate_directory(user):
    directory = GATE.parent
    info = directory.lstat()
    # The original installer deliberately made this private service-owned dir.
    require(directory.is_dir() and not directory.is_symlink()
            and info.st_uid == user.pw_uid and info.st_gid == user.pw_gid
            and info.st_mode & 0o777 == 0o700)
    for parent in directory.parents:
        info = parent.lstat()
        require(parent.is_dir() and not parent.is_symlink()
                and info.st_uid == 0 and not info.st_mode & 0o022)


def replace_gate(expected, raw, user):
    require(safe_file(GATE, user.pw_uid, 4096, True) == expected)
    fd, name = tempfile.mkstemp(prefix='.renewal-', dir=GATE.parent)
    try:
        with os.fdopen(fd, 'wb') as out:
            out.write(raw); out.flush(); os.fsync(out.fileno())
            os.fchmod(out.fileno(), 0o600)
            os.fchown(out.fileno(), user.pw_uid, user.pw_gid)
        require(safe_file(GATE, user.pw_uid, 4096, True) == expected)
        os.replace(name, GATE)
        directory = os.open(GATE.parent, os.O_RDONLY | os.O_DIRECTORY)
        try: os.fsync(directory)
        finally: os.close(directory)
    finally:
        if os.path.exists(name): os.unlink(name)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs): return None


def status(path, token=None):
    headers = {'Authorization': 'NasusQA ' + token} if token else {}
    request = urllib.request.Request('https://api.nasus-games.com' + path, headers=headers)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    try:
        with opener.open(request, timeout=10) as response: return response.status
    except urllib.error.HTTPError as error: return error.code


def boundaries(token):
    for path, key, expected in (('/healthz', None, 200), ('/', None, 503),
            ('/admin', None, 503), ('/qa/healthz', None, 404),
            ('/qa/healthz', '0'*64, 404), ('/qa/admin', token, 404), ('/qa/', token, 404)):
        require(status(path, key) == expected)


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1)
    os.umask(0o077)
    user = pwd.getpwnam('witchplayer')
    require(user.pw_uid != 0)
    validate_gate_directory(user)
    require(not os.path.lexists(RECEIPT))
    require(prop('ActiveState') == 'active' and prop('User') == 'witchplayer')
    require(prop('WorkingDirectory') == CODE and prop('DropInPaths') == DROP)
    require(prop('EnvironmentFiles') == ROOT + '/runtime.env (ignore_errors=no)')
    require(prop('ReadWritePaths') == ROOT)
    require(set(prop('InaccessiblePaths').split()) ==
            {'/var/lib/witch-player', '/var/lib/witch-player-apple', '/etc/witch-player'})
    command = prop('ExecStart')
    require(' --bind 127.0.0.1:8790 qa_isolated_runtime:application_factory()' in command)
    # Record bytes only in memory; do not print private configs or touch databases.
    paths = ((Path('/etc/caddy/Caddyfile'), 0, False),
             (Path('/etc/systemd/system') / UNIT, 0, False),
             (Path(DROP), 0, False), (Path(ROOT) / 'runtime.env', 0, True),
             (Path(ROOT) / 'worker.json', user.pw_uid, True))
    originals = [(path, uid, private, safe_file(path, uid, private=private))
                 for path, uid, private in paths]
    raw = safe_file(GATE, user.pw_uid, 4096, True)
    client = json.loads(safe_file(CLIENT, pwd.getpwnam('ubuntu').pw_uid, 4096, True),
                        object_pairs_hook=unique)
    require(client['BaseUrl'] == 'https://api.nasus-games.com/qa')
    token = client['QaAccessKey']
    renewed = renew_config(raw, token, time.time())
    boundaries(token)
    require(status('/qa/healthz', token) == 404)
    # O_EXCL is the single-use lock as well as the retained rollback copy.
    write_new(RECEIPT, raw)
    activate(raw, renewed, token, user, originals)


def activate(raw, renewed, token, user, originals):
    new_raw = json.dumps(renewed, sort_keys=True).encode()
    try:
        print('1/3 Existing isolated QA and closed production boundaries verified.', flush=True)
        ctl('stop', UNIT)
        require(prop('ActiveState') == 'inactive')
        replace_gate(raw, new_raw, user)
        print('2/3 Renew only the same QA gate for 24 hours; key unchanged.', flush=True)
        ctl('start', UNIT)
        for _ in range(15):
            try:
                if status('/qa/healthz', token) == 200: break
            except (OSError, urllib.error.URLError): pass
            time.sleep(1)
        else: raise ValueError('QA health check failed')
        boundaries(token)
        for path, uid, private, original in originals:
            require(safe_file(path, uid, private=private) == original)
        require(safe_file(GATE, user.pw_uid, 4096, True) == new_raw)
        print('3/3 Authenticated QA healthy; public game/admin remain closed.', flush=True)
        print(json.dumps(dict(Status='QA_RENEWAL_VERIFIED',
            ExpiresUtc=datetime.datetime.fromtimestamp(renewed['ExpiresUnix'],
                datetime.timezone.utc).isoformat(), LifetimeHours=24,
            KeysChanged=False, DatabaseMigrationPerformed=False,
            ProxyChanged=False, AutomaticRenewal=False)), flush=True)
    except BaseException:
        stopped = restored = False
        try:
            ctl('stop', UNIT); stopped = prop('ActiveState') == 'inactive'
        except Exception: pass
        try:
            current = safe_file(GATE, user.pw_uid, 4096, True)
            if current == new_raw: replace_gate(new_raw, raw, user)
            restored = safe_file(GATE, user.pw_uid, 4096, True) == raw
        except Exception: pass
        print(json.dumps(dict(Status='QA_RENEWAL_STOPPED', QaStopped=stopped,
            ExpiredGateRestored=restored, AutomaticRetry=False)), flush=True)
        raise SystemExit(1)


if __name__ == '__main__':
    try: main()
    except Exception:
        print('QA_RENEWAL_PRECHECK_FAILED: no private details printed; do not rerun.', flush=True)
        raise SystemExit(1)
