"""Prepare Apple runtime/schema/keys without enabling any routes or timers.

Invoked from a root-private hash-checked copy. Never overwrite existing staging,
restore a database, modify the live API environment, or print credential material.
"""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import shutil
import stat
import subprocess
import sys
import time

SOURCE = Path('/home/ubuntu/nasus-deploy/20260926-apple-stage-01')
TARGET = Path('/opt/nasus-apple-20260926')
CONFIG = Path('/etc/witch-player-apple')
STATE = Path('/var/lib/witch-player-apple')
BACKUP = Path('/var/backups/witch-player-apple-20260926')
CODE = {'store.py','maintenance.py','apple_identity.py','apple_tokens.py','apple_grants.py',
        'apple_revocation_worker.py','apple_revocation_runtime.py','run_apple_revocation.py',
        'prepare_apple_schema.py'}
WHEELS = {'pyjwt-2.15.0-py3-none-any.whl',
          'cryptography-50.0.1-cp311-abi3-manylinux_2_28_x86_64.whl',
          'cffi-2.1.1-cp312-cp312-manylinux2014_x86_64.manylinux_2_17_x86_64.whl',
          'pycparser-3.0-py3-none-any.whl'}


def require(ok):
    if not ok: raise ValueError('Apple preparation precondition failed')


def safe_file(path, uid, maximum=8*1024*1024, private=False):
    require(not any(p.is_symlink() for p in (path, *path.parents)))
    with path.open('rb') as source:
        info = os.fstat(source.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == uid
                and not info.st_mode & (0o077 if private else 0o022)
                and 0 < info.st_size <= maximum)
        raw = source.read(maximum+1)
    require(len(raw) <= maximum)
    return raw


def write_new(path, raw, mode=0o600, owner=None):
    fd = os.open(path, os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW, mode)
    with os.fdopen(fd, 'wb') as out:
        out.write(raw); out.flush(); os.fsync(out.fileno())
        if owner: os.fchown(out.fileno(), owner.pw_uid, owner.pw_gid)


def run(args, timeout=60, input_bytes=None):
    input_options = {'stdin':subprocess.DEVNULL} if input_bytes is None else {'input':input_bytes}
    result = subprocess.run(args, capture_output=True, timeout=timeout, cwd='/',
                            **input_options,
                            env={'PATH':'/usr/sbin:/usr/bin:/sbin:/bin','LC_ALL':'C'})
    require(result.returncode == 0)
    return result.stdout.decode().strip()


def external():
    for suffix, expected in (('/healthz','200'),('/','503')):
        require(run(['/usr/bin/curl','--disable','--proto','=https','--silent','--show-error',
                     '--max-time','15','-o','/dev/null','-w','%{http_code}',
                     'https://api.nasus-games.com'+suffix],20) == expected)


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 4)
    manifest_hash, signing_hash, token_hash = sys.argv[1:]
    require(all(re.fullmatch('[a-f0-9]{64}', x) for x in sys.argv[1:]))
    os.umask(0o077)
    account = pwd.getpwnam('witchplayer'); uploader = pwd.getpwnam('ubuntu')
    require(account.pw_uid != 0)
    for unit in ('witch-player-apple-revoke.service','witch-player-apple-revoke.timer'):
        require(run(['/usr/bin/systemctl','show',unit,'-p','LoadState','--value']) == 'not-found')
    for path in (TARGET, CONFIG, STATE, BACKUP):
        require(not os.path.lexists(path))
        info = path.parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
    manifest_raw = safe_file(SOURCE/'manifest.json', uploader.pw_uid, 16384)
    require(hashlib.sha256(manifest_raw).hexdigest() == manifest_hash)
    manifest = json.loads(manifest_raw)
    require(set(manifest) == CODE | {'wheels/'+name for name in WHEELS})
    contents = {}
    for name, digest in manifest.items():
        raw = safe_file(SOURCE/name, uploader.pw_uid)
        require(hashlib.sha256(raw).hexdigest() == digest)
        if name in CODE: compile(raw, name, 'exec')
        contents[name] = raw
    signing = safe_file(SOURCE/'signing.p8', uploader.pw_uid, 16384, True)
    token = safe_file(SOURCE/'token-encryption.key', uploader.pw_uid, 32, True)
    require(hashlib.sha256(signing).hexdigest() == signing_hash
            and hashlib.sha256(token).hexdigest() == token_hash and len(token) == 32)
    external()
    print('1/4 Build isolated, hash-verified Apple runtime (live API unchanged).', flush=True)
    TARGET.mkdir(mode=0o755); (TARGET/'wheels').mkdir(mode=0o755)
    for name, raw in contents.items(): write_new(TARGET/name, raw, 0o644)
    # mkdir/open modes are narrowed by umask; these non-secret files must be
    # readable by the existing service user. Secrets below remain 600/700.
    TARGET.chmod(0o755); (TARGET/'wheels').chmod(0o755)
    for name in contents: (TARGET/name).chmod(0o644)
    run(['/usr/bin/python3','-m','venv',str(TARGET/'venv')],120)
    python = str(TARGET/'venv/bin/python')
    run([python,'-I','-m','pip','install','--no-index','--no-deps',
         *[str(TARGET/'wheels'/name) for name in sorted(WHEELS)]],180)
    run([python,'-I','-m','pip','check'])
    # Venv contains code only, not secrets. Keep root ownership, allow service-user execution.
    for base, dirs, files in os.walk(TARGET/'venv'):
        Path(base).chmod(0o755)
        for name in files:
            path=Path(base)/name
            if not path.is_symlink(): path.chmod(0o755 if os.access(path,os.X_OK) else 0o644)
    validator = ('import sys; from cryptography.hazmat.primitives import serialization; '
                 'from cryptography.hazmat.primitives.asymmetric import ec; '
                 'k=serialization.load_pem_private_key(sys.stdin.buffer.read(),None); '
                 'assert isinstance(k,ec.EllipticCurvePrivateKey) and isinstance(k.curve,ec.SECP256R1)')
    run([python,'-I','-c',validator],input_bytes=signing)
    # Validate imports/constructor compatibility as the service user before DB mutation.
    probe = ('import sys;sys.path.insert(0,"'+str(TARGET)+'"); '
             'from apple_identity import AppleIdentityVerifier; '
             'AppleIdentityVerifier("com.nasus.dungeonmonsterroguelike")')
    run(['/usr/sbin/runuser','-u','witchplayer','--',python,'-I','-B','-c',probe])
    print('2/4 Verify a fresh encrypted offsite backup.', flush=True)
    started = time.time()
    run(['/usr/bin/systemctl','start','witch-player-offsite.service'],1900)
    report=json.loads(safe_file(Path('/var/lib/witch-player-offsite/last-success.json'),0,4096,True))
    stamp=dt.datetime.fromisoformat(report['CompletedUtc'])
    require(stamp.utcoffset() is not None and started <= stamp.timestamp() <= time.time()
            and report['Status'] == 'OFFSITE_CIPHERTEXT_VERIFIED')
    print('3/4 Preserve local snapshot and add empty Apple tables atomically.', flush=True)
    BACKUP.mkdir(mode=0o700); os.chown(BACKUP,account.pw_uid,account.pw_gid)
    result=json.loads(run(['/usr/sbin/runuser','-u','witchplayer','--',python,'-I','-B',
                           str(TARGET/'prepare_apple_schema.py')],180))
    require(result['Status'] == 'APPLE_EMPTY_SCHEMA_PREPARED')
    instance=result['InstanceId']
    require(re.fullmatch(r'[A-Za-z0-9_-]{8,80}',instance) is not None)
    for path in (CONFIG,STATE):
        path.mkdir(mode=0o700); os.chown(path,account.pw_uid,account.pw_gid)
    write_new(CONFIG/'signing.p8',signing,owner=account)
    write_new(CONFIG/'token-encryption.key',token,owner=account)
    config=dict(Version=1,InstanceId=instance,ClientId='com.nasus.dungeonmonsterroguelike',
                TeamId='687D767B8W',KeyId='38GM3RYVD9',DataDirectory='/var/lib/witch-player',
                SigningKeyFile=str(CONFIG/'signing.p8'),TokenEncryptionKeyFile=str(CONFIG/'token-encryption.key'),
                StateDirectory=str(STATE))
    write_new(CONFIG/'worker.json',json.dumps(config,sort_keys=True).encode(),owner=account)
    print('4/4 Check configured runtime read-only; keep routes and timers disabled.', flush=True)
    checked=json.loads(run(['/usr/sbin/runuser','-u','witchplayer','--',python,'-I','-B',
                            str(TARGET/'run_apple_revocation.py'),'--config',str(CONFIG/'worker.json')]))
    require(checked['Status'] == 'APPLE_WORKER_PREFLIGHT_PASSED' and checked['Activated'] is False)
    external()
    receipt=dict(Status='APPLE_STAGING_COMPLETE',AddedEmptyTables=6,ExistingRowsChanged=False,
                 ConfiguredRuntimeChecked=True,AppleRequests=0,WorkerEnabled=False,
                 GameRootClosed=True,PublicRouteConfigurationChanged=False,
                 LiveApiEnvironmentChanged=False,BackupVerified=True)
    write_new(TARGET/'staging-result.json',json.dumps(receipt,sort_keys=True).encode())
    # Source copies are no longer needed; verified originals remain on Mac and
    # service-owned copies remain under /etc. Never delete originals on failure.
    for name, digest in (('signing.p8',signing_hash),('token-encryption.key',token_hash)):
        path=SOURCE/name
        if hashlib.sha256(safe_file(path,uploader.pw_uid,16384,True)).hexdigest() == digest:
            path.unlink()
    print(json.dumps(receipt,sort_keys=True))


if __name__ == '__main__':
    try: main()
    except BaseException:
        print('APPLE_STAGING_STOPPED: retain runtime/keys/backup; do not rerun or restore DB automatically. No secret details printed.',flush=True)
        raise SystemExit(1)
