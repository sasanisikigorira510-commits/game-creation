"""One-time isolated refund lab install; root-run from a hash-pinned script.

No public proxy edits, no existing DB copies, no Apple requests or purchases.
Fresh private credentials/data, loopback listeners only. Existing services stay up.
"""
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import pwd
import re
import secrets
import stat
import subprocess
import sys
import tarfile
import time
import urllib.request
import urllib.error

SOURCE = Path('/home/ubuntu/nasus-deploy/20260928-refund-lab-01')
CODE = Path('/opt/nasus-refund-sandbox-20260928')
ROOT = Path('/var/lib/nasus-refund-sandbox-20260928')
CREDENTIALS = Path('/etc/nasus-refund-sandbox-20260928')
OLD = Path('/opt/nasus-iap-sandbox-20260928')
INSTANCE = 'nasus-refund-sandbox-20260928'
BASELINE = {
    '/etc/caddy/Caddyfile':'ed400215f4741f7c40a69cd5cc2bab61da26e507afba9f86df635d41fca8025d',
    '/etc/systemd/system/witch-player-qa.service.d/95-iap-sandbox.conf':'94e167c57eb0fda4ce5bbeb0193bd90c9ee15648254fea1661a15a77d5691a14',
    '/etc/systemd/system/witch-player.service':'8897433a811e9a85edf7b6fd3d1955793f5f6c9cece1a3bf0a4a744e40cbab0d',
    '/etc/witch-player/server.env':'444719db015122f6fe337e310b7872fb9e49fd5338b664f62f9f52982dbfb8b5',
    '/var/lib/nasus-qa-sandbox-20260927/runtime.env':'59e0897a8d5c5006b4213ee623e90df86f72d483d841c1325d261d56b198cf23'}
APIS = {'witch-player-refund-sandbox.service':(8791,'device'),
        'witch-player-refund-sandbox-review.service':(8792,'review'),
        'witch-player-refund-sandbox-notifications.service':(8793,'notifications')}
JOBS = {'witch-player-refund-lab-maintain':'maintain', 'witch-player-refunds':'reconcile'}
UNITS = set(APIS) | {base+suffix for base in JOBS for suffix in ('.service','.timer')}


def require(value):
    if not value: raise ValueError('Refund lab precondition failed')


def read(path, uid=0, private=False, limit=32*1024*1024):
    path = Path(path)
    require(not any(p.is_symlink() for p in (path,*path.parents)))
    with path.open('rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == uid
                and not info.st_mode & (0o077 if private else 0o022) and info.st_size <= limit)
        return stream.read(limit+1)


def run(args, timeout=120):
    result = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True, text=True,
                            timeout=timeout, cwd='/', env={'PATH':'/usr/sbin:/usr/bin:/sbin:/bin','LC_ALL':'C'})
    require(result.returncode == 0)
    return result.stdout.strip()


def ctl(*args): return run(['/usr/bin/systemctl', *args])


def baseline():
    for path, digest in BASELINE.items(): require(hashlib.sha256(read(path)).hexdigest() == digest)
    for unit in ('witch-player.service','witch-player-qa.service','caddy.service'):
        require(ctl('is-active',unit) == 'active')


def package(raw, digest):
    require(re.fullmatch('[a-f0-9]{64}',digest) and hashlib.sha256(raw).hexdigest() == digest)
    result = {}
    with tarfile.open(fileobj=io.BytesIO(raw), mode='r:gz') as archive:
        for entry in archive:
            path = PurePosixPath(entry.name)
            require(entry.isfile() and not path.is_absolute() and '..' not in path.parts
                    and str(path) == entry.name and entry.name not in result
                    and entry.size < 4*1024*1024 and len(result) < 150)
            result[entry.name] = archive.extractfile(entry).read()
    manifest = json.loads(result.pop('SHA256.json'))
    require(set(manifest) == set(result))
    require({'refund_lab_runtime.py','refund_sandbox.py','refund_runtime.py','store.py',
             'deploy/requirements-iap-sandbox.lock'} <= set(result))
    for name, data in result.items(): require(hashlib.sha256(data).hexdigest() == manifest[name])
    return result


def write(path, data, mode=0o600, owner=None):
    with os.fdopen(os.open(path, os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW, mode), 'wb') as output:
        output.write(data); output.flush(); os.fchmod(output.fileno(), mode)
        if owner: os.fchown(output.fileno(),owner.pw_uid,owner.pw_gid)
        os.fsync(output.fileno())


def directory(path, mode=0o755, owner=None):
    path.mkdir(mode=mode); path.chmod(mode)
    if owner: os.chown(path,owner.pw_uid,owner.pw_gid)


def unit(command, *, oneshot=False):
    return ('[Unit]\nDescription=Isolated refund Sandbox only\nAfter=network-online.target\n'
        '[Service]\nUser=nasusrefund\nGroup=nasusrefund\nWorkingDirectory='+str(CODE)+'\n'
        'Type='+('oneshot' if oneshot else 'simple')+'\nExecStart='+command+'\n'
        'UMask=0077\nNoNewPrivileges=true\nPrivateTmp=true\nProtectSystem=strict\nProtectHome=true\n'
        'ReadWritePaths='+str(ROOT)+'\nRestrictSUIDSGID=true\nCapabilityBoundingSet=\n'
        'RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6\nLimitCORE=0\nMemoryMax=192M\n'
        +('TimeoutStartSec=180\n' if oneshot else 'Restart=on-failure\nRestartSec=10\n')+
        'TimeoutStopSec=10\n[Install]\nWantedBy=multi-user.target\n').encode()


def timer(base):
    return ('[Unit]\nDescription=Isolated refund lab periodic maintenance\n[Timer]\n'
            'OnBootSec=60\nOnUnitInactiveSec=60\nAccuracySec=5\nUnit='+base+'.service\n'
            '[Install]\nWantedBy=timers.target\n').encode()


def probe(port, path, token=None, body=None):
    headers = {'Host':'api.nasus-games.com','X-Forwarded-Proto':'https','X-Real-IP':'127.0.0.1'}
    if token: headers['Authorization'] = token
    raw = None if body is None else json.dumps(body).encode()
    if raw is not None: headers['Content-Type'] = 'application/json'
    request = urllib.request.Request('http://127.0.0.1:'+str(port)+path, data=raw, headers=headers)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    try:
        with opener.open(request, timeout=10) as response: return response.status
    except urllib.error.HTTPError as error: return error.code


def install(digest):
    require(os.geteuid() == 0); os.umask(0o077)
    baseline()
    old_user = pwd.getpwnam('witchplayer'); uploader = pwd.getpwnam('ubuntu')
    try: pwd.getpwnam('nasusrefund')
    except KeyError: pass
    else: raise ValueError('Lab user already exists; inspect rather than overwrite')
    for path in (CODE,ROOT,CREDENTIALS):
        require(not os.path.lexists(path))
        for parent in path.parents:
            info = parent.lstat(); require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)
    for name in UNITS:
        require(not os.path.lexists('/etc/systemd/system/'+name)
                and ctl('show',name,'--property=LoadState','--value') == 'not-found')
    listeners = run(['/usr/bin/ss','-H','-ltn'])
    require(not any(line.split()[3].rsplit(':',1)[-1] in ('8791','8792','8793') for line in listeners.splitlines()))
    memory = {line.split(':')[0]:int(line.split()[1]) for line in Path('/proc/meminfo').read_text().splitlines()}
    require(memory['MemAvailable'] >= 384*1024)
    contents = package(read(SOURCE/'code.tar.gz', uploader.pw_uid, True), digest)
    require(read(OLD/'requirements-iap-sandbox.lock') == contents['deploy/requirements-iap-sandbox.lock'])
    identity = json.loads(read('/var/lib/nasus-qa-sandbox-20260927/worker.json',old_user.pw_uid,True))
    iap = json.loads(read('/var/lib/nasus-qa-sandbox-20260927/iap-sandbox.json',old_user.pw_uid,True))
    require(iap['Environment'] == 'Sandbox' and identity['ClientId'] == iap['BundleId'] == 'com.nasus.dungeonmonsterroguelike')
    identity_key = read(identity['SigningKeyFile'],old_user.pw_uid,True)
    purchase_key = read('/etc/nasus-iap-sandbox-20260928/signing.p8',old_user.pw_uid,True)
    roots = {p.name:read(p,old_user.pw_uid,True) for p in Path('/etc/nasus-iap-sandbox-20260928/roots').glob('*.cer')}
    require(bool(roots))
    print('1/4 Create separate code, user and empty data. Existing environments unchanged.',flush=True)
    directory(CODE)
    for name, data in contents.items():
        target = CODE/name
        if not target.parent.exists(): directory(target.parent)
        write(target,data,0o644)
    run(['/usr/bin/python3','-m','venv','--copies',str(CODE/'venv')])
    run([str(CODE/'venv/bin/pip'),'install','--no-index','--only-binary=:all:',
         '--find-links',str(OLD/'wheels'),'-r',str(CODE/'deploy/requirements-iap-sandbox.lock')],timeout=180)
    run([str(CODE/'venv/bin/pip'),'check'])
    for folder,dirs,files in os.walk(CODE/'venv'):
        Path(folder).chmod(0o755)
        for name in files:
            path = Path(folder)/name
            if not path.is_symlink(): path.chmod(0o755 if path.stat().st_mode & 0o111 else 0o644)
    run(['/usr/sbin/useradd','--system','--user-group','--no-create-home','--home-dir','/nonexistent',
         '--shell','/usr/sbin/nologin','nasusrefund'])
    owner = pwd.getpwnam('nasusrefund')
    for path in (ROOT,CREDENTIALS,ROOT/'identity-state',ROOT/'refund-state',CREDENTIALS/'roots'):
        directory(path,0o700,owner)
    write(CREDENTIALS/'identity-signing.p8',identity_key,owner=owner)
    write(CREDENTIALS/'iap-signing.p8',purchase_key,owner=owner)
    write(CREDENTIALS/'encryption.key',secrets.token_bytes(32),owner=owner)
    for name,data in roots.items(): write(CREDENTIALS/'roots'/name,data,owner=owner)
    identity.update(InstanceId=INSTANCE,DataDirectory=str(ROOT/'data'),StateDirectory=str(ROOT/'identity-state'),
                    SigningKeyFile=str(CREDENTIALS/'identity-signing.p8'),TokenEncryptionKeyFile=str(CREDENTIALS/'encryption.key'))
    apple = dict(WITCH_APPLE_ENVIRONMENT='Sandbox',WITCH_APPLE_BUNDLE_ID=iap['BundleId'],
        WITCH_APPLE_KEY_ID=iap['KeyId'],WITCH_APPLE_ISSUER_ID=iap['IssuerId'],
        WITCH_APPLE_KEY_FILE=str(CREDENTIALS/'iap-signing.p8'),WITCH_APPLE_ROOTS_DIR=str(CREDENTIALS/'roots'))
    worker = dict(Version=1,InstanceId=INSTANCE,DataDirectory=str(ROOT/'data'),StateDirectory=str(ROOT/'refund-state'),
                  AppleConfigFile=str(CREDENTIALS/'iap.json'),Environment='Sandbox',BatchLimit=2)
    now = time.time(); token = secrets.token_hex(32)
    gate = dict(Version=1,TokenSha256=hashlib.sha256(token.encode()).hexdigest(),IssuedUnix=now,ExpiresUnix=now+86400)
    for path,value in ((ROOT/'identity.json',identity),(ROOT/'refund.json',worker),
                       (CREDENTIALS/'iap.json',apple),(CREDENTIALS/'gate.json',gate),
                       (CREDENTIALS/'device-access.json',dict(QaAccessKey=token,ExpiresUnix=gate['ExpiresUnix']))):
        write(path,json.dumps(value).encode(),owner=owner)
    def as_user(code):
        return run(['/usr/sbin/runuser','-u','nasusrefund','--',str(CODE/'venv/bin/python'),'-I','-B','-c',
                    'import sys;sys.path.insert(0,'+repr(str(CODE))+');'+code])
    as_user('from refund_lab_runtime import initialize_empty;initialize_empty()')
    as_user('from refund_lab_runtime import device;device();print("FACTORY_OFFLINE_OK")')
    print('2/4 Offline factory passed; install loopback-only services and private maintenance.',flush=True)
    new_units = []
    try:
        for name,(port,surface) in APIS.items():
            command = str(CODE/'venv/bin/gunicorn')+' --config gunicorn.conf.py --bind 127.0.0.1:'+str(port)+' refund_lab_runtime:'+surface+'()'
            write(Path('/etc/systemd/system')/name,unit(command),0o644); new_units.append(name)
        for base,action in JOBS.items():
            command = str(CODE/'venv/bin/python')+' -B '+str(CODE/'refund_lab_runtime.py')+' '+action
            write(Path('/etc/systemd/system')/(base+'.service'),unit(command,oneshot=True),0o644)
            new_units.append(base+'.service')
            write(Path('/etc/systemd/system')/(base+'.timer'),timer(base),0o644); new_units.append(base+'.timer')
        ctl('daemon-reload')
        ctl('start',*(base+'.service' for base in JOBS))
        ctl('enable','--now',*(base+'.timer' for base in JOBS))
        ctl('start',*APIS)
        print('3/4 Verify internal health and rejected cross-surface requests; no purchases.',flush=True)
        for attempt in range(20):
            try:
                if probe(8791,'/healthz','NasusQA '+token) == 200: break
            except OSError: pass
            time.sleep(1)
        else: raise ValueError('Lab health not ready')
        require(probe(8791,'/healthz') == 404)
        require(probe(8791,'/admin/refunds/preview','NasusQA '+token,{}) == 404)
        require(probe(8792,'/admin/refunds/preview',body={}) == 401)
        require(probe(8792,'/v1/accounts',body={}) == 404)
        require(probe(8793,'/v1/store/apple/notifications',body={'signedPayload':'unsigned-invalid-fixture'}) == 400)
        require(probe(8793,'/admin/refunds/preview',body={}) == 404)
        baseline()
        as_user('from refund_lab_runtime import ROOT;import sqlite3;'
                'db=sqlite3.connect(ROOT/"data/players.sqlite");'
                'assert db.execute("SELECT count(*) FROM players").fetchone()[0]==0;'
                'assert db.execute("SELECT count(*) FROM purchases").fetchone()[0]==0')
        ctl('enable',*APIS)
        report = dict(Status='REFUND_LAB_INTERNAL_READY_NOT_PUBLIC',InstanceId=INSTANCE,
                      GateExpiresUnix=gate['ExpiresUnix'],Players=0,Purchases=0,
                      PublicProxyChanged=False,AppleNotificationURLRegistered=False,
                      ExistingDataCopied=False,BaselineConfigSHA256=BASELINE)
        write(ROOT/'installation.json',json.dumps(report,indent=2).encode(),owner=owner)
        print('4/4 REFUND_LAB_INTERNAL_READY_NOT_PUBLIC: empty DB; existing configuration unchanged.',flush=True)
    except BaseException:
        # Only stop the units created in this run. Never restore/overwrite a database.
        for name in new_units:
            subprocess.run(['/usr/bin/systemctl','disable','--now',name],capture_output=True,timeout=30)
        print('REFUND_LAB_STOPPED: new files retained for inspection; existing services untouched.',flush=True)
        raise


if __name__ == '__main__':
    try:
        require(len(sys.argv) == 2); install(sys.argv[1])
    except Exception:
        print('REFUND_LAB_INSTALL_INCOMPLETE: private details omitted; inspect before retrying.',flush=True)
        raise SystemExit(1)
