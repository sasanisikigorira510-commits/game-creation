"""Fixed-scope QA-only cutover. Requires an interactive operator sudo approval.

Never changes the live DB, Caddy, original Apple worker, keys or gate expiration.
If cutover fails, leave QA stopped (do not reconnect a migrated phone to live DB).
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import secrets
import subprocess
import sys
import time
import urllib.error
import urllib.request

sys.path.insert(0, str(Path(__file__).resolve().parent))
from install_apple_staging import safe_file, require, run
from install_apple_qa import write_new

OLD = Path('/opt/nasus-apple-qa-20260926')
CODE = Path('/opt/nasus-qa-sandbox-20260927')
ROOT = Path('/var/lib/nasus-qa-sandbox-20260927')
DROP = Path('/etc/systemd/system/witch-player-qa.service.d')
UNIT = Path('/etc/systemd/system')
SOURCE = Path('/home/ubuntu/nasus-deploy/20260926-apple-qa-01')
MANIFEST_HASH = '62d32f12a556b6375ea537429b95d1e7ab4e099925c3938d45784bfca60b7717'
PLAYER_HASH = 'd5231546a171dcc92f01e1629a7a2590049e3dbf3b13be7363db2befebefcdb9'
PYTHON = str(OLD / 'venv/bin/python')
INSTANCE = 'nasus-qa-sandbox-20260927'
DOMAIN = 'https://api.nasus-games.com'


def ctl(*args): return run(['/usr/bin/systemctl', *args], 120)


def as_user(code, timeout=120):
    return run(['/usr/sbin/runuser', '-u', 'witchplayer', '--', PYTHON, '-I', '-B', '-c',
                'import sys;sys.path.insert(0,' + repr(str(CODE)) + ');' + code], timeout)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs): return None


def status(path, token=None):
    headers = {'Authorization': 'NasusQA ' + token} if token else {}
    request = urllib.request.Request(DOMAIN + path, headers=headers)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())
    try:
        with opener.open(request, timeout=10) as response: return response.status
    except urllib.error.HTTPError as error: return error.code


def mkdir(path, user=None):
    path.mkdir(mode=0o700 if user else 0o755)
    if user: os.chown(path, user.pw_uid, user.pw_gid)
    else: path.chmod(0o755)


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1)
    os.umask(0o077)
    user = pwd.getpwnam('witchplayer'); ubuntu = pwd.getpwnam('ubuntu')
    maintenance = UNIT / 'nasus-qa-sandbox-maintenance.service'
    timer = UNIT / 'nasus-qa-sandbox-maintenance.timer'
    for path in (CODE, ROOT, DROP, maintenance, timer):
        require(not os.path.lexists(path))
        for parent in (path.parent, *path.parent.parents):
            info = parent.lstat()
            require(parent.is_dir() and not parent.is_symlink() and info.st_uid == 0 and not info.st_mode & 0o022)
    require(ctl('is-active', 'witch-player-qa.service') == 'active')
    require(ctl('show', 'witch-player-qa.service', '-p', 'DropInPaths', '--value') == '')
    from install_apple_qa import SERVICE
    require(safe_file(UNIT / 'witch-player-qa.service', 0) == SERVICE.encode())
    manifest_raw = safe_file(SOURCE / 'manifest.json', ubuntu.pw_uid)
    require(hashlib.sha256(manifest_raw).hexdigest() == MANIFEST_HASH)
    modules = {}
    for name, digest in json.loads(manifest_raw).items():
        require(re.fullmatch(r'(?:[a-z_]+\.py|gunicorn\.conf\.py|catalog\.json|wheels/[A-Za-z0-9_.-]+\.whl)', name))
        raw = safe_file(OLD / name, 0)
        require(hashlib.sha256(raw).hexdigest() == digest)
        if not name.startswith('wheels/'): modules[name] = raw
    config = json.loads(safe_file(Path('/etc/witch-player-apple/worker.json'), user.pw_uid, 4096, True))
    require(config['DataDirectory'] == '/var/lib/witch-player'
            and config['StateDirectory'] == '/var/lib/witch-player-apple')
    require(config['ClientId'] == 'com.nasus.dungeonmonsterroguelike')
    gate_path = Path('/etc/witch-player-qa/gate.json')
    gate_raw = safe_file(gate_path, user.pw_uid, 4096, True)
    gate = json.loads(gate_raw)
    require(gate['Version'] == 1 and gate['IssuedUnix'] <= time.time()
            and 0 < gate['ExpiresUnix'] - gate['IssuedUnix'] <= 86400
            and gate['ExpiresUnix'] > time.time() + 600)
    client = json.loads(safe_file(SOURCE / 'client.json', ubuntu.pw_uid, 4096, True))
    token = client['QaAccessKey']
    require(isinstance(token, str) and re.fullmatch('[a-f0-9]{64}', token)
            and hashlib.sha256(token.encode()).hexdigest() == gate['TokenSha256'])
    require(status('/healthz') == 200 and status('/') == 503 and status('/qa/healthz', token) == 200)
    mkdir(CODE); mkdir(ROOT, user)
    for name, raw in modules.items(): write_new(CODE / name, raw, 0o644)
    for name in ('qa_isolated_runtime.py', 'isolate_apple_qa_data.py'):
        write_new(CODE / name, safe_file(Path(__file__).parent / name, 0), 0o644)
    write_new(CODE / 'original-qa.service', SERVICE.encode())
    stopped = False
    stage = 'quiesce'
    try:
        ctl('stop', 'witch-player-qa.service'); stopped = True
        require(ctl('show', 'witch-player-qa.service', '-p', 'ActiveState', '--value') == 'inactive')
        print('1/4 Copy one reviewed legacy account to a separate Sandbox DB; live DB read-only.', flush=True)
        stage = 'copy-migrate'
        result = json.loads(as_user('import json;from isolate_apple_qa_data import isolate;'
            'print(json.dumps(isolate("/var/lib/witch-player/players.sqlite",'
            + repr(str(ROOT / 'data')) + ',' + repr(PLAYER_HASH) + ',1200,9600,' + repr(INSTANCE) + ')))'))
        require(result['Status'] == 'SANDBOX_COPY_MIGRATED')
        mkdir(ROOT / 'state', user)
        config.update(DataDirectory=str(ROOT / 'data'), StateDirectory=str(ROOT / 'state'), InstanceId=INSTANCE)
        write_new(ROOT / 'worker.json', json.dumps(config).encode(), owner=user)
        # Admin is blocked by QaGateway. No live operator credential is reused.
        operator_hash = hashlib.sha256(secrets.token_bytes(32)).hexdigest()
        write_new(ROOT / 'operators.json', json.dumps([dict(Name='qa-disabled-admin', Role='operator',
            TokenHash=operator_hash)]).encode(), owner=user)
        env = dict(WITCH_DATA_DIR=str(ROOT / 'data'), WITCH_INSTANCE_ID=INSTANCE,
            WITCH_APPLE_CONFIG=str(ROOT / 'worker.json'), WITCH_PUBLIC_ORIGIN=DOMAIN,
            WITCH_OPERATORS_FILE=str(ROOT / 'operators.json'),
            WITCH_BACKUP_HEALTH_FILE=str(ROOT / 'backup-status.json'),
            WITCH_QA_GATE_CONFIG=str(gate_path))
        write_new(ROOT / 'runtime.env', ''.join(k + '=' + v + '\n' for k, v in env.items()).encode())
        stage = 'maintenance'
        print('2/4 Install separate QA local backup/revocation worker; production timers unchanged.', flush=True)
        common = ('User=witchplayer\nGroup=witchplayer\nWorkingDirectory=' + str(CODE) + '\n'
            'EnvironmentFile=' + str(ROOT / 'runtime.env') + '\nUMask=0077\n'
            'NoNewPrivileges=true\nPrivateTmp=true\nProtectSystem=strict\nProtectHome=true\n'
            'ReadWritePaths=' + str(ROOT) + '\n'
            'InaccessiblePaths=/var/lib/witch-player /var/lib/witch-player-apple /etc/witch-player\n'
            'RestrictSUIDSGID=true\nRestrictAddressFamilies=AF_UNIX AF_INET AF_INET6\n'
            'CapabilityBoundingSet=\nMemoryMax=400M\nLimitCORE=0\n')
        # A root-owned env file is read by systemd, not the service user.
        worker = ('[Unit]\nDescription=Isolated Sandbox local backup and Apple revocation\n'
            '[Service]\nType=oneshot\nTimeoutStartSec=90\n' + common
            + 'ExecStart=' + PYTHON + ' -B ' + str(CODE / 'qa_isolated_runtime.py') + '\n')
        write_new(maintenance, worker.encode(), 0o644)
        write_new(timer, ('[Unit]\nDescription=Isolated Sandbox maintenance (not enabled on boot)\n'
            '[Timer]\nOnActiveSec=30s\nOnUnitInactiveSec=60s\nAccuracySec=5s\n'
            'Unit=nasus-qa-sandbox-maintenance.service\n').encode(), 0o644)
        ctl('daemon-reload'); ctl('start', maintenance.name)
        require(ctl('show', maintenance.name, '-p', 'Result', '--value') == 'success')
        ctl('start', timer.name)
        stage = 'cutover'
        print('3/4 Switch only gated QA to the isolated DB; no app reinstall or gate extension.', flush=True)
        mkdir(DROP)
        override = ('[Service]\nEnvironment=\nEnvironmentFile=\nEnvironmentFile=' + str(ROOT / 'runtime.env')
            + '\nWorkingDirectory=' + str(CODE) + '\nReadWritePaths=\nReadWritePaths=' + str(ROOT)
            + '\nInaccessiblePaths=/var/lib/witch-player /var/lib/witch-player-apple /etc/witch-player\n'
            'ExecStart=\nExecStart=' + str(OLD / 'venv/bin/gunicorn')
            + ' --config gunicorn.conf.py --bind 127.0.0.1:8790 qa_isolated_runtime:application_factory()\n')
        write_new(DROP / '90-sandbox-isolation.conf', override.encode(), 0o644)
        ctl('daemon-reload'); ctl('start', 'witch-player-qa.service')
        stage = 'verify'
        for attempt in range(15):
            if status('/qa/healthz', token) == 200: break
            time.sleep(1)
        else: raise ValueError('QA startup failed')
        for path, key, expected in (('/healthz', None, 200), ('/', None, 503),
                ('/admin', None, 503), ('/qa/healthz', None, 404), ('/qa/admin', token, 404)):
            require(status(path, key) == expected)
        require(safe_file(gate_path, user.pw_uid, 4096, True) == gate_raw)
        # Source remains pending: no production migration was performed.
        check = ('import sqlite3;from pathlib import Path;from isolate_apple_qa_data import inspect;'
            'db=sqlite3.connect(Path("/var/lib/witch-player/players.sqlite").as_uri()+"?mode=ro",uri=True);'
            'db.row_factory=sqlite3.Row;db.execute("PRAGMA query_only=ON");'
            'inspect(db,' + repr(PLAYER_HASH) + ',1200,9600);db.close()')
        as_user(check)
        print('4/4 QA health and production boundaries verified.', flush=True)
        print(json.dumps(dict(Status='QA_SANDBOX_ISOLATION_VERIFIED', Free=1200, Paid=9600,
            ProductionMigrationApproved=False, GateExpiryUnchanged=True,
            QaBackup='local-only', DeletionTestsEnabled=False, RealAppleAuthenticationTested=False)), flush=True)
    except BaseException:
        if stopped:
            try: ctl('stop', 'witch-player-qa.service')
            except Exception: pass
        # Retain isolated tokens and the maintenance worker if already started.
        # Never fall back to the live DB after a phone may have read QA revision 1.
        print('QA_ISOLATION_STOPPED: stage=' + stage + '; QA stopped; files retained; do not rerun.', flush=True)
        raise SystemExit(1)


if __name__ == '__main__':
    try: main()
    except Exception:
        print('QA_ISOLATION_PRECHECK_FAILED: private details omitted; no automatic retry.', flush=True)
        raise SystemExit(1)
