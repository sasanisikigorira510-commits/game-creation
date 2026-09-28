"""Single-use operator-run Sandbox IAP cutover. Never enables production.

Run only from a root-private, hash-pinned copy. All package bytes are verified
before use. Preserve gate expiry, DB/schema, current tokens and QA maintenance.
Failure restores the previous non-purchasing QA executable, never a database.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import stat
import subprocess
import sys
import time
import urllib.error
import urllib.request

SOURCE = Path('/home/ubuntu/nasus-deploy/20260928-iap-sandbox-01')
OLD = Path('/opt/nasus-qa-sandbox-20260927')
TARGET = Path('/opt/nasus-iap-sandbox-20260928')
ROOT = Path('/var/lib/nasus-qa-sandbox-20260927')
CREDENTIALS = Path('/etc/nasus-iap-sandbox-20260928')
DROP = Path('/etc/systemd/system/witch-player-qa.service.d/95-iap-sandbox.conf')
KEY = Path('/home/ubuntu/.nasus-iap-sandbox-20260928/SubscriptionKey_2LX8L872Z8.p8')
GATE = Path('/etc/witch-player-qa/gate.json')
CLIENT = Path('/home/ubuntu/nasus-deploy/20260926-apple-qa-01/client.json')
FILES = {'apple_verifier.py', 'qa_purchase_runtime.py', 'requirements-iap-sandbox.lock',
         'iap-sandbox.json', 'roots/AppleIncRootCertificate.cer',
         'roots/AppleRootCA-G2.cer', 'roots/AppleRootCA-G3.cer'}


def require(value):
    if not value: raise ValueError('Sandbox IAP precondition failed')


def read(path, uid, private=False):
    require(not any(p.is_symlink() for p in (path, *path.parents)))
    with path.open('rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == uid
                and not info.st_mode & (0o077 if private else 0o022)
                and 0 < info.st_size < 16*1024*1024)
        return stream.read(16*1024*1024)


def write_new(path, raw, mode=0o600, user=None):
    fd = os.open(path, os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush()
        os.fchmod(stream.fileno(), mode)
        if user: os.fchown(stream.fileno(), user.pw_uid, user.pw_gid)
        os.fsync(stream.fileno())


def mkdir(path, mode, user=None):
    path.mkdir(mode=mode); path.chmod(mode)
    if user: os.chown(path, user.pw_uid, user.pw_gid)


def run(args, *, env=None, timeout=120):
    result = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True, timeout=timeout,
                            cwd='/', env=env or {'PATH':'/usr/sbin:/usr/bin:/sbin:/bin','LC_ALL':'C'})
    require(result.returncode == 0)
    return result.stdout.decode().strip()


def ctl(*args): return run(['/usr/bin/systemctl', *args])


def package(manifest, uid):
    require(set(manifest) == {'Files','SigningKeySha256'})
    entries = manifest['Files']
    require(isinstance(entries, dict) and FILES <= set(entries))
    result = {}
    for name, digest in entries.items():
        require(name in FILES or re.fullmatch(r'wheels/[A-Za-z0-9_.-]+\.whl', name))
        require(isinstance(digest, str) and re.fullmatch('[a-f0-9]{64}', digest))
        raw = read(SOURCE/name, uid)
        require(hashlib.sha256(raw).hexdigest() == digest)
        result[name] = raw
    require(any(name.startswith('wheels/') for name in result))
    config = json.loads(result['iap-sandbox.json'])
    require(config == dict(Version=1, Environment='Sandbox',
        BundleId='com.nasus.dungeonmonsterroguelike', KeyId='2LX8L872Z8',
        IssuerId='827aa07e-acf5-4e10-a1ea-46758f5eae57'))
    return result


def override():
    return ('[Service]\nWorkingDirectory='+str(TARGET)+'\nExecStart=\nExecStart='
            +str(TARGET/'venv/bin/gunicorn')+' --config gunicorn.conf.py --bind 127.0.0.1:8790 '
            'qa_purchase_runtime:application_factory()\n')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs): return None


def status(path, token=None):
    request = urllib.request.Request('https://api.nasus-games.com'+path,
                    headers={'Authorization':'NasusQA '+token} if token else {})
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    try:
        with opener.open(request, timeout=10) as response: return response.status
    except urllib.error.HTTPError as error: return error.code


def boundaries(token):
    for path, key, expected in (('/healthz',None,200), ('/',None,503), ('/admin',None,503),
                               ('/qa/healthz',None,404), ('/qa/admin',token,404)):
        require(status(path, key) == expected)


def preflight(manifest_digest):
    """Read-only checks; return reviewed bytes without writing or restarting."""
    require(os.geteuid() == 0 and re.fullmatch('[a-f0-9]{64}',manifest_digest))
    user, uploader = pwd.getpwnam('witchplayer'), pwd.getpwnam('ubuntu')
    for path in (TARGET, CREDENTIALS, DROP, ROOT/'iap-sandbox.json', ROOT/'iap-before-20260928.sqlite'):
        require(not os.path.lexists(path))
        for parent in (path.parent, *path.parent.parents):
            info = parent.lstat()
            require(stat.S_ISDIR(info.st_mode) and not parent.is_symlink()
                    and info.st_uid in (0,user.pw_uid) and not info.st_mode & 0o022)
    require(ctl('is-active','witch-player-qa.service') == 'active')
    require(ctl('show','witch-player-qa.service','-p','WorkingDirectory','--value') == str(OLD))
    require(ctl('show','witch-player-qa.service','-p','DropInPaths','--value') == str(DROP.parent/'90-sandbox-isolation.conf'))
    raw = read(SOURCE/'manifest.json', uploader.pw_uid)
    require(hashlib.sha256(raw).hexdigest() == manifest_digest)
    manifest = json.loads(raw); contents = package(manifest, uploader.pw_uid)
    signing = read(KEY, uploader.pw_uid, True)
    require(hashlib.sha256(signing).hexdigest() == manifest['SigningKeySha256'])
    gate_raw = read(GATE, user.pw_uid, True); gate = json.loads(gate_raw)
    require(gate['IssuedUnix'] <= time.time() < gate['ExpiresUnix']-600
            and 0 < gate['ExpiresUnix']-gate['IssuedUnix'] <= 86400)
    client = json.loads(read(CLIENT, uploader.pw_uid, True)); token = client['QaAccessKey']
    require(hashlib.sha256(token.encode()).hexdigest() == gate['TokenSha256'])
    boundaries(token); require(status('/qa/healthz',token) == 200)
    # Preserve exact live/public and QA baseline configurations. Never print them.
    unchanged_paths = (Path('/etc/caddy/Caddyfile'), Path('/etc/witch-player/server.env'),
        Path('/etc/systemd/system/witch-player.service'), ROOT/'runtime.env',
        DROP.parent/'90-sandbox-isolation.conf')
    originals = {p:read(p,0) for p in unchanged_paths}
    env = dict(PATH='/usr/sbin:/usr/bin:/sbin:/bin', LC_ALL='C')
    for line in originals[ROOT/'runtime.env'].decode().splitlines():
        if line and not line.startswith('#'):
            k,v = line.split('=',1); require(re.fullmatch('WITCH_[A-Z_]+',k)); env[k]=v
    return user, contents, signing, token, gate_raw, originals, env


def main():
    require(len(sys.argv) in (2,3) and (len(sys.argv) == 2 or sys.argv[2] == '--check'))
    user, contents, signing, token, gate_raw, originals, env = preflight(sys.argv[1])
    if len(sys.argv) == 3:
        print('SANDBOX_IAP_PREFLIGHT_PASSED: read-only; no files, services or DB changed.',flush=True)
        return
    os.umask(0o077)
    print('1/4 Build separate pinned Sandbox verification runtime; live API unchanged.',flush=True)
    mkdir(TARGET,0o755); mkdir(TARGET/'wheels',0o755)
    baseline = {}
    for path in OLD.iterdir():
        if path.suffix == '.py' or path.name == 'catalog.json':
            require(re.fullmatch('[a-z_.]+',path.name)); baseline[path.name]=read(path,0)
    require({'qa_isolated_runtime.py','apple_application.py','store.py','gunicorn.conf.py'} <= set(baseline))
    baseline.update({name:contents[name] for name in ('apple_verifier.py','qa_purchase_runtime.py')})
    for name,data in baseline.items(): write_new(TARGET/name,data,0o644)
    for name,data in contents.items():
        if name.startswith('wheels/') or name == 'requirements-iap-sandbox.lock':
            write_new(TARGET/name,data,0o644)
    run(['/usr/bin/python3','-m','venv',str(TARGET/'venv')])
    run([str(TARGET/'venv/bin/pip'),'install','--no-index','--only-binary=:all:',
         '--find-links',str(TARGET/'wheels'),'-r',str(TARGET/'requirements-iap-sandbox.lock')],timeout=180)
    run([str(TARGET/'venv/bin/pip'),'check'])
    # venv files created under umask 077 must be readable/executable by the service.
    for directory, dirs, files in os.walk(TARGET/'venv'):
        Path(directory).chmod(0o755)
        for name in files:
            path=Path(directory)/name
            if not path.is_symlink(): path.chmod(0o755 if path.stat().st_mode & 0o111 else 0o644)
    print('2/4 Install owner-private IAP key, Apple roots and Sandbox-only configuration.',flush=True)
    mkdir(CREDENTIALS,0o700,user); mkdir(CREDENTIALS/'roots',0o700,user)
    write_new(CREDENTIALS/'signing.p8',signing,user=user)
    for name in FILES:
        if name.startswith('roots/'): write_new(CREDENTIALS/name,contents[name],user=user)
    write_new(ROOT/'iap-sandbox.json',contents['iap-sandbox.json'],user=user)
    def as_user(code):
        return run(['/usr/sbin/runuser','-u','witchplayer','--',str(TARGET/'venv/bin/python'),'-I','-B','-c',
            'import sys;sys.path.insert(0,'+repr(str(TARGET))+');'+code],env=env)
    # Factory startup is offline; does not make a purchase or initialize schema.
    as_user('from qa_purchase_runtime import application_factory;app=application_factory();'
            'assert callable(app.app.app.store.purchase_verifier);print("SANDBOX_FACTORY_OK")')
    changed = False
    try:
        print('3/4 Preserve QA DB snapshot and switch only gated QA; expiry unchanged.',flush=True)
        ctl('stop','witch-player-qa.service')
        changed = True
        as_user('import sqlite3;from pathlib import Path;'
            'src=sqlite3.connect(Path('+repr(str(ROOT/'data/players.sqlite'))+').as_uri()+"?mode=ro",uri=True);'
            'dst=sqlite3.connect('+repr(str(ROOT/'iap-before-20260928.sqlite'))+');'
            'src.backup(dst);assert dst.execute("PRAGMA quick_check").fetchone()[0]=="ok";dst.close();src.close()')
        write_new(DROP,override().encode(),0o644)
        ctl('daemon-reload'); ctl('start','witch-player-qa.service')
        for _ in range(15):
            if status('/qa/healthz',token) == 200: break
            time.sleep(1)
        else: raise ValueError('QA health failed')
        boundaries(token)
        require(read(GATE,user.pw_uid,True) == gate_raw)
        require(all(read(path,0) == data for path,data in originals.items()))
        require(ctl('is-active','witch-player.service') == 'active')
        print('4/4 QA healthy; production, gate, maintenance configuration preserved.',flush=True)
        print(json.dumps(dict(Status='SANDBOX_IAP_CONNECTED', Environment='Sandbox',
            ProductionChanged=False, GateExpiryChanged=False, RealPurchaseTested=False,
            RefundNotificationsConfigured=False)),flush=True)
    except BaseException:
        if changed:
            ctl('stop','witch-player-qa.service')
            if DROP.exists(): DROP.rename(TARGET/'failed-95-iap-sandbox.conf')
            ctl('daemon-reload'); ctl('start','witch-player-qa.service')
        print('SANDBOX_IAP_STOPPED: files retained, no DB restore, do not rerun.',flush=True)
        raise


if __name__ == '__main__':
    try: main()
    except Exception as error:
        # Only source locations/types, never exception messages or local values.
        import traceback
        frames = [dict(File=Path(frame.filename).name, Function=frame.name, Line=frame.lineno)
                  for frame in traceback.extract_tb(error.__traceback__)]
        print(json.dumps(dict(Status='SANDBOX_IAP_FAILED', Type=type(error).__name__,
                              Errno=getattr(error,'errno',None), Frames=frames)),flush=True)
        if sys.argv[-1:] == ['--check']:
            # Fixed target metadata only; never file contents, token, or key data.
            metadata = {}
            for name, path in dict(QaRoot=ROOT, QaEnvironment=ROOT/'runtime.env', Gate=GATE,
                    Client=CLIENT, SigningKey=KEY, Caddy=Path('/etc/caddy/Caddyfile'),
                    LiveEnvironment=Path('/etc/witch-player/server.env'),
                    LiveUnit=Path('/etc/systemd/system/witch-player.service'),
                    ExistingOverride=DROP.parent/'90-sandbox-isolation.conf').items():
                try:
                    info=path.lstat()
                    metadata[name]=dict(Uid=info.st_uid, Mode=oct(stat.S_IMODE(info.st_mode)),
                                        Symlink=stat.S_ISLNK(info.st_mode), Bytes=info.st_size)
                except OSError as failure: metadata[name]=dict(Errno=failure.errno)
            print(json.dumps(dict(ReadOnlyMetadata=metadata)),flush=True)
        raise SystemExit(1)
