"""One authenticated Apple worker/health activation; no API or schema switch.

Run only from a root-private, hash-pinned package. On failure stop the newly
installed worker and restore the previous health command; retain all evidence.
Never rerun this installer, grant sudo, rotate keys, or roll back the database.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import signal
import stat
import subprocess
import sys
import time

OLD = Path('/opt/nasus-apple-20260926')
WORKER = Path('/opt/nasus-apple-worker-20260926')
HEALTH = Path('/usr/local/lib/nasus-apple-health-20260926')
RETENTION = Path('/usr/local/lib/nasus-retention-20260926')
HOOK = Path('/etc/systemd/system/witch-player-health.service.d/80-apple-worker.conf')
UNITS = Path('/etc/systemd/system')
SERVICE = 'witch-player-apple-revoke.service'
TIMER = 'witch-player-apple-revoke.timer'
MARKER = Path('/var/lib/witch-player-apple/status.json')
PYTHON = str(OLD/'venv/bin/python')
CONFIG = '/etc/witch-player-apple/worker.json'
PREVIOUS_DROPS = ('/etc/systemd/system/witch-player-health.service.d/40-reason-logging.conf '
    '/etc/systemd/system/witch-player-health.service.d/60-integrated-health.conf '
    '/etc/systemd/system/witch-player-health.service.d/70-backup-retention.conf')
HOOK_TEXT = '[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '+str(HEALTH/'apple_integrated_health.py')+'\n'
UNCHANGED = {
    'apple_grants.py': '449a33835089ee7cdb63526c207cbf1c221ff624262f6bfbf03fc0001527bfa0',
    'apple_identity.py': '0351f796f7c39506afb1b06c500e7e27a77fd943828c392be0b7e259a5f7e1ce',
    'apple_revocation_worker.py': 'de83701ad55953adea44efd1e4ecb95cc36cb6b3dc24100691b4a2a8276b60e3',
    'apple_tokens.py': '1f2a919ad9ec9f6aa38b9e482f73abfba12184f8bdc6cb892b5e20d52da2205b',
    'maintenance.py': '07d64bddb74b2a17b846666ed32c605d7f64f0ad2c6c33b99f03d7ff2dc3cbbd',
    'run_apple_revocation.py': 'ba55e962e209e4d30d71cb84f43c13ef0025da0964b03cd0772682b1156e0e21',
    'store.py': '2d2664694f5951a00f65880a15ace2cc238e2edbf94f3ac45c9cb36fa0e95bb9'}
COLLECTORS = {
    'retention_health.py': '84e395abd88f2dc8565369d182b59bbe2f95921f851c53481e5e54bec9fdbf67',
    'base_health.py': 'a42a6d7cb7eb49064b95babc7360d0927c18a2d192a320cc5dd180dc54decafb'}
PACKAGE = {'apple_revocation_runtime.py', 'apple_revocation_health.py', 'backup_health.py',
           'apple_integrated_health.py', SERVICE, TIMER}
APPLE_TABLES = ('apple_links','apple_challenges','apple_exchanges','apple_unlinks',
                'apple_grants','apple_grant_generations')


def require(ok):
    if not ok: raise ValueError('Apple activation validation failed')


def trusted(path, owner=0, maximum=1024*1024):
    require(not any(p.is_symlink() for p in (path, *path.parents)))
    for parent in path.parents:
        info = parent.stat()
        require(info.st_uid in (0, owner) and not info.st_mode & 0o022)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(fd, 'rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == owner
                and not info.st_mode & 0o022 and 0 < info.st_size <= maximum)
        value = stream.read(maximum+1)
    require(len(value) <= maximum)
    return value


def checked(path, digest):
    raw = trusted(path)
    require(hashlib.sha256(raw).hexdigest() == digest)
    return raw


def write_new(path, raw, mode=0o600):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, mode)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush(); os.fchmod(stream.fileno(), mode); os.fsync(stream.fileno())


def run(args, timeout=60):
    result = subprocess.run(args, capture_output=True, timeout=timeout, cwd='/',
        stdin=subprocess.DEVNULL, env={'PATH':'/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL':'C'})
    require(result.returncode == 0)
    return result.stdout.decode().strip()


def ctl(*args, timeout=60): return run(['/usr/bin/systemctl', *args], timeout)
def prop(unit, name): return ctl('show', unit, '-p', name, '--value')


def external():
    for path, expected in (('/healthz', '200'), ('/', '503')):
        code = run(['/usr/bin/curl', '--disable', '--proto', '=https', '--silent', '--show-error',
                    '--max-time', '15', '-o', '/dev/null', '-w', '%{http_code}',
                    'https://api.nasus-games.com'+path], 20)
        require(code == expected)


def marker(path, owner, since):
    value = json.loads(trusted(path, owner, 4096))
    stamp = value.get('CheckedUnix')
    require(value.get('Healthy') is True and type(stamp) in (int, float)
            and since <= stamp <= time.time() and time.time()-stamp < 180)
    return value


def empty_runtime_probe():
    # Construct only the read-only configured runtime, then count all Apple
    # tables. No raw token, account or configuration values leave the child.
    script = ('import sys;sys.path.insert(0,'+repr(str(OLD))+');'
        'from apple_revocation_runtime import load_components;'
        'v,c,s=load_components('+repr(CONFIG)+',readonly=True);'
        'db=v.store.connect();'
        'tables=[r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type=\'table\' AND name LIKE \'apple_%\'")];'
        'assert set(tables)==set('+repr(APPLE_TABLES)+');'
        'assert all(db.execute(\'SELECT count(*) FROM "\'+t+\'"\').fetchone()[0]==0 for t in '+repr(APPLE_TABLES)+');'
        'db.close();print("APPLE_EMPTY_RUNTIME_VERIFIED")')
    require(run(['/usr/sbin/runuser', '-u', 'witchplayer', '--', PYTHON, '-I', '-B', '-c', script])
            == 'APPLE_EMPTY_RUNTIME_VERIFIED')


def rollback():
    # Continue independent recovery steps if any individual stop fails.
    errors = []
    for args in (('disable','--now',TIMER), ('stop',SERVICE)):
        try: ctl(*args, timeout=100)
        except Exception: errors.append('worker_stop')
    try:
        if os.path.lexists(HOOK):
            require(trusted(HOOK) == HOOK_TEXT.encode())
            require(not os.path.lexists(HEALTH/'disabled-hook.conf'))
            HOOK.rename(HEALTH/'disabled-hook.conf')
        ctl('daemon-reload')
        require(prop('witch-player-health.service','DropInPaths') == PREVIOUS_DROPS)
        ctl('start','witch-player-health.service')
        external()
    except Exception: errors.append('health_restore')
    print('APPLE_ACTIVATION_ROLLBACK_'+('INCOMPLETE' if errors else 'VERIFIED')+
          ': files retained; no database/key rollback. Do not rerun.', flush=True)


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 2 and re.fullmatch('[a-f0-9]{64}',sys.argv[1]))
    os.umask(0o077)
    source = Path(__file__).resolve().parent
    manifest = json.loads(checked(source/'manifest.json', sys.argv[1]))
    require(set(manifest) == PACKAGE)
    content = {name: checked(source/name, digest) for name,digest in manifest.items()}
    for name,raw in content.items():
        if name.endswith('.py'): compile(raw, name, 'exec')
    account = pwd.getpwnam('witchplayer'); require(account.pw_uid != 0)
    require(json.loads(trusted(OLD/'staging-result.json'))['Status'] == 'APPLE_STAGING_COMPLETE')
    old_code = {name: checked(OLD/name,digest) for name,digest in UNCHANGED.items()}
    checked(OLD/'apple_revocation_runtime.py', 'eff153a9c4f6a7f08fa69e12cea6f335a18a09b53a0676d9dab3354045500f9a')
    collectors = {name: checked(RETENTION/name,digest) for name,digest in COLLECTORS.items()}
    require(prop('witch-player-health.service','DropInPaths') == PREVIOUS_DROPS)
    require(prop('witch-player-health.timer','ActiveState') == 'active')
    for unit in (SERVICE,TIMER): require(prop(unit,'LoadState') == 'not-found')
    for path in (WORKER,HEALTH,HOOK,UNITS/SERVICE,UNITS/TIMER):
        require(not os.path.lexists(path))
        info=path.parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
    protected = [Path('/etc/caddy/Caddyfile'), Path('/etc/systemd/system/witch-player.service'),
                 Path('/etc/systemd/system/witch-player-health.service')]
    before = {path:hashlib.sha256(trusted(path)).hexdigest() for path in protected}
    external(); empty_runtime_probe()
    print('1/4 Empty Apple runtime verified; preserve live API, keys and database schema.', flush=True)
    WORKER.mkdir(mode=0o755); WORKER.chmod(0o755); HEALTH.mkdir(mode=0o700)
    for name,raw in old_code.items(): write_new(WORKER/name,raw,0o644)
    write_new(WORKER/'apple_revocation_runtime.py',content['apple_revocation_runtime.py'],0o644)
    for name in ('apple_integrated_health.py','apple_revocation_health.py','backup_health.py'):
        write_new(HEALTH/name,content[name])
    for name,raw in collectors.items(): write_new(HEALTH/name,raw)
    changed = False
    try:
        print('2/4 Install isolated worker; verify first empty run.', flush=True)
        # Mark before the first unit write so partial unit installation is handled.
        changed = True
        for name in (SERVICE,TIMER): write_new(UNITS/name,content[name])
        ctl('daemon-reload')
        run(['/usr/bin/systemd-analyze','verify',SERVICE,TIMER])
        started = time.time(); ctl('start',SERVICE,timeout=100)
        first = marker(MARKER,account.pw_uid,started)
        require(first.get('Status') == 'APPLE_REVOCATION_BATCH_OBSERVED')
        require(first.get('Processed') == dict(revoked=0,retry=0,stale=0,idle=1))
        print('3/4 Enable timer and compose existing backup/deletion/retention health.', flush=True)
        ctl('enable','--now',TIMER)
        require(ctl('is-enabled',TIMER) == 'enabled' and prop(TIMER,'ActiveState') == 'active')
        # Test collector before replacing the active health command.
        result=run(['/usr/bin/python3','-I','-B',str(HEALTH/'apple_integrated_health.py')])
        require(json.loads(result.removeprefix('NASUS_HEALTH_REASON='))['Reason'] == 'HEALTHY')
        write_new(HOOK,HOOK_TEXT.encode()); ctl('daemon-reload')
        require(prop('witch-player-health.service','DropInPaths') == PREVIOUS_DROPS+' '+str(HOOK))
        health_started = time.time(); ctl('start','witch-player-health.service')
        marker(Path('/run/witch-player-health/status.json'),0,health_started); external()
        print('4/4 Wait for automatic worker and health timer observations (up to 3 minutes).', flush=True)
        deadline = time.monotonic()+180
        automatic_stamp = None
        while time.monotonic() < deadline:
            time.sleep(5)
            # A normal run may still be in progress. Final success requires a
            # newly completed run followed by a newly published healthy result.
            observation=json.loads(trusted(MARKER,account.pw_uid,4096))
            if observation.get('Healthy') is not True: continue
            if observation.get('CheckedUnix',0) <= first['CheckedUnix']: continue
            if prop(SERVICE,'ActiveState') != 'inactive': continue
            later=marker(MARKER,account.pw_uid,first['CheckedUnix'])
            if automatic_stamp is None: automatic_stamp = later['CheckedUnix']
            health=json.loads(trusted(Path('/run/witch-player-health/status.json'),0,4096))
            if health.get('Healthy') is True and health.get('CheckedUnix',0) > automatic_stamp:
                marker(Path('/run/witch-player-health/status.json'),0,automatic_stamp)
                break
        else: raise ValueError('Automatic observations not complete')
        external(); empty_runtime_probe()
        for path,digest in before.items(): require(hashlib.sha256(trusted(path)).hexdigest() == digest)
        require(prop(SERVICE,'Result') == 'success' and prop(TIMER,'ActiveState') == 'active')
        receipt=dict(Status='APPLE_WORKER_ACTIVATION_VERIFIED',AutomaticWorkerRun=True,
            AutomaticCombinedHealth=True,AppleTablesStillEmpty=True,GameRootClosed=True,
            LiveApiConfigurationChanged=False,SchemaChanged=False,KeysChanged=False,
            VerifiedUnix=time.time())
        write_new(HEALTH/'activation-result.json',json.dumps(receipt,sort_keys=True).encode())
        print(json.dumps(receipt,sort_keys=True),flush=True)
    except BaseException:
        if changed: rollback()
        raise


if __name__ == '__main__':
    def interrupt(*_): raise KeyboardInterrupt()
    for sig in (signal.SIGTERM,signal.SIGHUP): signal.signal(sig,interrupt)
    try: main()
    except BaseException:
        print('APPLE_ACTIVATION_STOPPED: inspect retained files; no secrets printed; do not rerun.',flush=True)
        raise SystemExit(1)
