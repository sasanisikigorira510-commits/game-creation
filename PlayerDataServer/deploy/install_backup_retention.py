"""One authenticated install of the authorized 30-day db/ retention policy.

No standing sudo grant, credential edits, DB access or public route change.
On failure stop the new timer and restore the prior health command. Deleted
objects cannot be rolled back; the durable audit and all install files remain.
"""
import hashlib
import json
import os
from pathlib import Path
import signal
import stat
import subprocess
import sys
import time

TARGET = Path('/usr/local/lib/nasus-retention-20260926')
STATE = Path('/var/lib/witch-player-retention')
HOOK = Path('/etc/systemd/system/witch-player-health.service.d/70-backup-retention.conf')
UNITS = Path('/etc/systemd/system')
SERVICE = 'witch-player-retention.service'
TIMER = 'witch-player-retention.timer'
OLD = Path('/usr/local/lib/nasus-health-integrated-20260925/publish.py')
OLD_HASH = 'a42a6d7cb7eb49064b95babc7360d0927c18a2d192a320cc5dd180dc54decafb'
OLD_DROPS = ('/etc/systemd/system/witch-player-health.service.d/40-reason-logging.conf '
             '/etc/systemd/system/witch-player-health.service.d/60-integrated-health.conf')
HOOK_TEXT = ('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '
             '/usr/local/lib/nasus-retention-20260926/retention_health.py\n')
FILES = {
 'expire_backups.py': '23d582403e49f38faa9b5eb1e3fb1786839b46e52df3c0a6336e7a05632a2635',
 'retention_health.py': '84e395abd88f2dc8565369d182b59bbe2f95921f851c53481e5e54bec9fdbf67',
 'inspect_backup_retention.py': '94682c824e9a7d4bd05c1ce33f4f12a807a2784cc8da86c19801ad6bab6df893',
 'backup_retention_plan.py': '7eb5304dce431776e0d7b61dd274adc132f8f461075e4759ad0f98a51d20e9d6',
 SERVICE: '1a9c33e0dd210177c6e4474980fc3acd01760ae1ed65c91beb0bafe291776e4f',
 TIMER: '62d882a8748d06999b4e5cde8d6c77c546fe1b1289a0c32423cf030f4c3a1b75',
}


def require(ok):
    if not ok: raise ValueError('Retention install validation failed')


def trusted(path, directory=False):
    info = path.lstat()
    require(info.st_uid == 0 and not info.st_mode & 0o022
            and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
    for parent in path.parents:
        info = parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)


def new_file(path, raw):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno())


def run(args, timeout=60):
    result = subprocess.run(args, capture_output=True, timeout=timeout, stdin=subprocess.DEVNULL,
                            env={'PATH': '/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL': 'C'})
    require(result.returncode == 0)
    return result.stdout.decode().strip()


def ctl(*args, timeout=60): return run(['/usr/bin/systemctl', *args], timeout)
def prop(unit, name): return ctl('show', unit, '-p', name, '--value')


def public_status():
    for path, expected in (('/healthz', '200'), ('/', '503')):
        code = run(['/usr/bin/curl', '--disable', '--proto', '=https', '--silent', '--show-error',
                    '--output', '/dev/null', '--write-out', '%{http_code}', '--max-time', '15',
                    'https://api.nasus-games.com'+path], 20)
        require(code == expected)


def marker(path, since):
    trusted(path); require(path.stat().st_size <= 4096)
    value = json.loads(path.read_bytes()); stamp = value.get('CheckedUnix')
    require(value.get('Healthy') is True and type(stamp) in (int, float)
            and since <= stamp <= time.time() and time.time()-stamp < 180)
    return value


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1)
    os.umask(0o077)
    source = Path(__file__).resolve().parent
    contents = {}
    for name, digest in FILES.items():
        path = source/name; trusted(path); raw = path.read_bytes()
        require(hashlib.sha256(raw).hexdigest() == digest)
        if name.endswith('.py'): compile(raw, name, 'exec')
        contents[name] = raw
    trusted(OLD)
    previous = OLD.read_bytes(); require(hashlib.sha256(previous).hexdigest() == OLD_HASH)
    require(prop('witch-player-health.service', 'DropInPaths') == OLD_DROPS)
    require(prop('witch-player-health.timer', 'ActiveState') == 'active')
    require(prop('witch-player-deletion.timer', 'ActiveState') == 'active')
    # Match the existing uploader that uses the same lock and immutable UUID keys.
    uploader = Path('/usr/local/lib/nasus-backup/sakura_offsite_backup.py'); trusted(uploader)
    require(hashlib.sha256(uploader.read_bytes()).hexdigest() ==
            '840858d9dbe54b1bba6ac81e40f15c8552b6b176bb2167c65c26ab4a59670e90')
    protected = [Path('/etc/caddy/Caddyfile'), Path('/etc/systemd/system/witch-player-health.service')]
    before = {}
    for path in protected:
        trusted(path); before[path] = hashlib.sha256(path.read_bytes()).hexdigest()
    public_status()
    for path in (TARGET, STATE, HOOK, UNITS/SERVICE, UNITS/TIMER):
        trusted(path.parent, directory=True)
        require(not path.exists() and not path.is_symlink())
    TARGET.mkdir(mode=0o700); STATE.mkdir(mode=0o700)
    for name, raw in contents.items(): new_file(TARGET/name, raw)
    new_file(TARGET/'base_health.py', previous)
    new_file(STATE/'authorization.json', json.dumps(dict(
        AuthorizedDate='2026-09-26', Bucket='nasus-player-backup-4433119', Prefix='db/',
        RetentionDays=30, Excluded=['deletions/', 'deletion-probes/', 'keys', 'Mac', 'USB'])).encode())
    installed = False
    try:
        print('1/3 GET/HEAD preflight; no deletions in this step.', flush=True)
        dry = json.loads(run(['/usr/bin/python3', '-I', '-B', str(TARGET/'expire_backups.py'), '--dry-run'], 850))
        require(dry['Status'] == 'RETENTION_DRY_RUN_COMPLETE' and dry['ObjectsDeleted'] == 0)
        new_file(STATE/'initial-dry-run.json', json.dumps(dry, sort_keys=True).encode())
        for name in (SERVICE, TIMER): new_file(UNITS/name, contents[name])
        installed = True
        ctl('daemon-reload')
        run(['/usr/bin/systemd-analyze', 'verify', SERVICE, TIMER])
        print('2/3 Apply authorized expiration and enable hourly timer.', flush=True)
        started = time.time()
        ctl('start', SERVICE, timeout=920)
        result = marker(STATE/'status.json', started)
        require(result['Status'] == 'RETENTION_COMPLETE' and result['Execute'] is True)
        ctl('enable', '--now', TIMER)
        require(prop(TIMER, 'ActiveState') == 'active' and ctl('is-enabled', TIMER) == 'enabled')
        new_file(HOOK, HOOK_TEXT.encode()); ctl('daemon-reload')
        require(prop('witch-player-health.service', 'DropInPaths') == OLD_DROPS+' '+str(HOOK))
        print('3/3 Verify combined health, then observe one automatic health update.', flush=True)
        started = time.time(); ctl('start', 'witch-player-health.service')
        first = marker(Path('/run/witch-player-health/status.json'), started)
        public_status()
        deadline = time.monotonic()+100
        while time.monotonic() < deadline:
            time.sleep(5)
            later = marker(Path('/run/witch-player-health/status.json'), started)
            if later['CheckedUnix'] > first['CheckedUnix']: break
        else: raise ValueError('Automatic health update was not observed')
        public_status()
        for path, digest in before.items(): require(hashlib.sha256(path.read_bytes()).hexdigest() == digest)
        require(prop(SERVICE, 'Result') == 'success' and prop(TIMER, 'ActiveState') == 'active')
        result.update(Status='RETENTION_INSTALL_VERIFIED', AutomaticHealthUpdate=True,
                      GameRoutesClosed=True, VerifiedUnix=time.time())
        new_file(STATE/'installed.json', json.dumps(result, sort_keys=True).encode())
        print('RETENTION_INSTALL_JSON='+json.dumps(result, sort_keys=True), flush=True)
    except BaseException:
        try:
            if installed:
                ctl('disable', '--now', TIMER); ctl('stop', SERVICE, timeout=930)
            if HOOK.exists():
                trusted(HOOK); require(HOOK.read_text() == HOOK_TEXT)
                require(not (STATE/'disabled-health.conf').exists())
                HOOK.rename(STATE/'disabled-health.conf')
            ctl('daemon-reload'); ctl('start', 'witch-player-health.service')
            print('RETENTION_INSTALL_STOPPED: new timer stopped, prior health restored. Audit/files retained; deleted objects are not restored.', flush=True)
        except BaseException:
            print('RETENTION_ROLLBACK_INCOMPLETE: inspect retained files and audit. Do not rerun.', flush=True)
        raise


if __name__ == '__main__':
    def interrupt(*_): raise KeyboardInterrupt()
    for sig in (signal.SIGTERM, signal.SIGHUP): signal.signal(sig, interrupt)
    try: main()
    except BaseException:
        print('RETENTION_INSTALL_FAILED: no secrets printed; do not rerun automatically.', flush=True)
        raise SystemExit(1)
