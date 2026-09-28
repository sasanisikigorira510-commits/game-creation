"""One explicit empty-baseline activation. No schema changes or public API enable.

Bootstrap verifies this installer and the exact manifest before execution.
All runtime code is copied/hashed root-only before importing. Retain old runtime
and new state on failure; never restore DB snapshots or delete cloud objects.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import signal
import stat
import subprocess
import sys
import time

TARGET = Path('/usr/local/lib/nasus-inventory-20260926')
STATE = Path('/var/lib/witch-player-deletion/inventory')
HOOK = Path('/etc/systemd/system/witch-player-deletion.service.d/60-inventory.conf')
SERVICE, TIMER = 'witch-player-deletion.service', 'witch-player-deletion.timer'
FILES = {'store.py','maintenance.py','deletion_journal.py','deletion_restore.py','deletion_event_restore.py',
         'deletion_inventory.py','deletion_inventory_ledger.py','run_inventory_worker.py',
         'deploy/preflight_deletion_worker.py','deploy/deletion_worker.py',
         'deploy/sakura_deletion_journal.py','deploy/sakura_inventory_store.py','deploy/inventory_worker.py'}
HOOK_TEXT = ('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '
             '/usr/local/lib/nasus-inventory-20260926/run_inventory_worker.py\n'
             'TimeoutStartSec=360\nMemoryMax=256M\n')
OLD_DROP = '/etc/systemd/system/witch-player-deletion.service.d/40-minimal-capabilities.conf'


def require(ok):
    if not ok: raise ValueError('Inventory activation check failed')


def trusted(path, directory=False):
    info = path.lstat()
    require(info.st_uid == 0 and not info.st_mode & 0o022
            and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
    for parent in path.parents:
        info = parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022)


def write_new(path, raw):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as out: out.write(raw); out.flush(); os.fsync(out.fileno())


def run(args, timeout=60):
    result = subprocess.run(args, capture_output=True, timeout=timeout, stdin=subprocess.DEVNULL,
                            env={'PATH':'/usr/sbin:/usr/bin:/sbin:/bin','LC_ALL':'C'})
    require(result.returncode == 0); return result.stdout.decode().strip()


def ctl(*args, timeout=60): return run(['/usr/bin/systemctl', *args], timeout)
def prop(unit, name): return ctl('show', unit, '-p', name, '--value')


def external():
    for path, expected in (('/healthz','200'),('/','503')):
        require(run(['/usr/bin/curl','--disable','--proto','=https','--silent','--show-error',
            '--max-time','15','--output','/dev/null','--write-out','%{http_code}',
            'https://api.nasus-games.com'+path],20) == expected)


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 1); os.umask(0o077)
    source=Path(__file__).resolve().parent
    manifest_path=source/'manifest.json'; trusted(manifest_path)
    manifest=json.loads(manifest_path.read_bytes()); require(set(manifest) == FILES)
    contents={}
    for name, expected in manifest.items():
        path=source/name; trusted(path); require(path.stat().st_size <= 1024*1024)
        raw=path.read_bytes(); require(hashlib.sha256(raw).hexdigest() == expected)
        compile(raw, name, 'exec'); contents[name]=raw
    for path in (TARGET, STATE, HOOK):
        trusted(path.parent, True); require(not path.exists() and not path.is_symlink())
    for path, digest in {
        '/usr/local/lib/nasus-deletion/deploy/deletion_worker.py':'aa8d1e4c2ac217ccfab43cdb25bc043fdf3e95a6cf4f5c041b8352b4ef26b7c4',
        '/etc/caddy/Caddyfile':'75ceb4d8ce8b107f11786354a7ca56442d5bb41a960ffa251e0f20bf724efa44',
    }.items():
        path=Path(path); trusted(path); require(hashlib.sha256(path.read_bytes()).hexdigest() == digest)
    require(prop(SERVICE,'DropInPaths') == OLD_DROP and prop(TIMER,'ActiveState') == 'active'
            and ctl('is-enabled',TIMER) == 'enabled')
    external()
    TARGET.mkdir(mode=0o700); (TARGET/'deploy').mkdir(mode=0o700)
    for name, raw in contents.items(): write_new(TARGET/name, raw)
    sys.dont_write_bytecode=True; sys.path.insert(0,str(TARGET))
    from deletion_inventory import inspect_inventory, parse_inventory
    from deletion_inventory_ledger import initialize_ledger, MAX_STATE
    from deploy.deletion_worker import DATA, CONFIG, as_database_user, read_json
    from deploy.sakura_inventory_store import SakuraInventoryStore
    account=pwd.getpwnam('witchplayer'); require(account.pw_uid != 0)
    def snapshot():
        instance=(DATA/'instance-id').read_text().strip()
        raw, states=inspect_inventory(DATA,instance)
        require(len(raw) < 4096 and sum(states.values()) == 0)
        return {'Instance':instance,'Raw':raw.decode()}
    stopped=False
    try:
        print('1/3 Verify empty baseline and preserve the existing runtime.',flush=True)
        ctl('stop',TIMER); stopped=True; ctl('stop',SERVICE,timeout=370)
        baseline=as_database_user(account,snapshot); instance=baseline['Instance']; raw=baseline['Raw'].encode()
        require(not parse_inventory(raw,instance)['Events'])
        remote=SakuraInventoryStore(CONFIG/'curl.conf')
        require(remote.get('deletion-inventories/'+instance+'/head.json') is None)
        STATE.mkdir(mode=0o700); initialize_ledger(STATE/'ledger.json',instance,raw)
        write_new(HOOK,HOOK_TEXT.encode()); ctl('daemon-reload')
        require(prop(SERVICE,'DropInPaths') == OLD_DROP+' '+str(HOOK))
        run(['/usr/bin/systemd-analyze','verify',SERVICE])
        print('2/3 Publish encrypted inventory and verify external readback.',flush=True)
        started=time.time(); ctl('start',SERVICE,timeout=380)
        def worker_marker():
            value=read_json(STATE.parent/'status.json')
            stamp=value.get('CheckedUnix')
            require(value.get('Healthy') is True and type(stamp) in (int,float)
                    and started <= stamp <= time.time() and time.time()-stamp < 300)
            require(prop(SERVICE,'Result') == 'success' and prop(SERVICE,'ExecMainStatus') == '0')
            return stamp
        first=worker_marker(); ctl('start',TIMER)
        ctl('start','witch-player-health.service')
        external()
        print('3/3 Observe the following automatic worker run and check combined health.',flush=True)
        deadline=time.monotonic()+140
        while time.monotonic() < deadline:
            time.sleep(5)
            if worker_marker() > first: break
        else: raise ValueError('Automatic inventory worker not observed')
        require(prop(TIMER,'ActiveState') == 'active')
        ctl('start','witch-player-health.service'); external()
        # Compare the separately stored cloud head to its durable local receipt.
        saved=read_json(STATE/'ledger.json',MAX_STATE)
        require(saved['Pending'] is None and saved['Committed'] is not None)
        head=saved['Committed']['Head']
        from deletion_inventory_ledger import canonical
        require(remote.get('deletion-inventories/'+instance+'/head.json') == canonical(head))
        final=as_database_user(account,snapshot); require(final['Instance'] == instance)
        result=dict(Status='INVENTORY_WORKER_INSTALLED',InventoryEvents=head['Events'],Sequence=head['Sequence'],
                    HeadSha256=hashlib.sha256(canonical(head)).hexdigest(),
                    AutomaticWorkerRun=True,HealthHealthy=True,GameRoutesClosed=True,
                    RealAccountDeleted=False,LatestDisasterCompletenessProven=False,VerifiedUnix=time.time())
        write_new(STATE/'installed.json',json.dumps(result,sort_keys=True).encode())
        print('INVENTORY_INSTALL_JSON='+json.dumps(result,sort_keys=True),flush=True)
    except BaseException:
        try:
            if stopped:
                ctl('stop',TIMER); ctl('stop',SERVICE,timeout=370)
                if HOOK.exists():
                    trusted(HOOK); require(HOOK.read_text() == HOOK_TEXT)
                    HOOK.rename(TARGET/'disabled-inventory.conf')
                ctl('daemon-reload'); ctl('start',SERVICE,timeout=190); ctl('start',TIMER)
                ctl('start','witch-player-health.service')
            print('INVENTORY_ACTIVATION_STOPPED: old worker restored; new local/cloud evidence retained. Do not rerun.',flush=True)
        except BaseException:
            print('INVENTORY_ROLLBACK_INCOMPLETE: inspect retained state; do not rerun.',flush=True)
        raise


if __name__ == '__main__':
    def interrupted(*_): raise KeyboardInterrupt()
    for sig in (signal.SIGTERM,signal.SIGHUP): signal.signal(sig,interrupted)
    try: main()
    except BaseException:
        print('INVENTORY_INSTALL_FAILED: no secrets printed; retain the report.',flush=True)
        raise SystemExit(1)
