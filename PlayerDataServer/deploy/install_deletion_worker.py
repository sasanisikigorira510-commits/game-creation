"""One-time, user-invoked sudo installation. Never opens API routes.

The bootstrap pins this installer AND manifest before executing either.
All imported code is first copied/hashed in a root-only directory.
Failure retains the additive schema/snapshot, stops the new worker and restores
the old health command. It never replaces a live database with an old snapshot.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import pwd
import shutil
import stat
import subprocess
import sys
import tempfile
import time
import uuid

FILES = {
    'store.py', 'deletion_journal.py', 'deploy/deletion_worker.py',
    'deploy/preflight_deletion_worker.py', 'deploy/sakura_deletion_journal.py',
    'deploy/publish_backup_health.py', 'run_worker.py',
    'deploy/witch-player-deletion.service', 'deploy/witch-player-deletion.timer',
}
TARGET = Path('/usr/local/lib/nasus-deletion')
UNITS = Path('/etc/systemd/system')
HOOK = UNITS / 'witch-player-health.service.d/30-deletion-worker.conf'
CADDY_HASH = '75ceb4d8ce8b107f11786354a7ca56442d5bb41a960ffa251e0f20bf724efa44'
HEALTH_HASH = '8b4b3e59b2586b2946ffaee39e501a835d799e1f87b21fe4f24a48e4b23412ac'


def digest(path):
    if path.is_symlink() or not path.is_file(): raise ValueError('Expected regular file')
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verified_copy(source, destination, expected):
    # Hash the ROOT-OWNED copy, not the mutable user's source, before importing.
    if source.is_symlink() or not source.is_file() or source.stat().st_size > 1024 * 1024:
        raise ValueError('Unexpected bundle file')
    destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    with source.open('rb') as src, destination.open('xb') as dst:
        shutil.copyfileobj(src, dst)
    destination.chmod(0o600)
    if digest(destination) != expected: raise ValueError('Bundle checksum mismatch')


def command(args, timeout=30):
    return subprocess.run(args, check=True, capture_output=True, text=True, timeout=timeout).stdout


def systemctl(*args, timeout=30):
    return command(['/usr/bin/systemctl', *args], timeout)


def http_code(path, *, head=False):
    args = ['/usr/bin/curl', '--disable', '--proto', '=https', '--silent', '--show-error',
            '--connect-timeout', '10', '--max-time', '20', '--output', '/dev/null', '--write-out', '%{http_code}']
    if head: args.append('--head')
    return command(args + ['https://api.nasus-games.com' + path], 25)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--manifest', required=True, type=Path)
    args = parser.parse_args()
    if os.geteuid() != 0: raise SystemExit('Run this installer through sudo in Mac Terminal')
    os.umask(0o077)
    root_stage = Path(tempfile.mkdtemp(prefix='nasus-deletion-install.', dir='/var/tmp'))
    manifest = json.loads(args.manifest.read_text())
    if not isinstance(manifest, dict) or set(manifest) != FILES: raise ValueError('Unexpected manifest')
    runtime = root_stage / 'runtime'
    for name, expected in manifest.items():
        verified_copy(args.source / name, runtime / name, expected)
    sys.path.insert(0, str(runtime))
    from deploy.preflight_deletion_worker import (DATA, SERVER, CONFIG, INSPECTED,
        private_file, database_as_service_user)
    from deploy.deletion_worker import (STATE, AGE, as_database_user, migrate_empty_schema,
        atomic_json, reserve_upload, read_json)
    from deploy.sakura_deletion_journal import AgeEncryptor, SakuraPublisher
    from deploy.publish_backup_health import collect, recent_success
    account = pwd.getpwnam('witchplayer')
    if account.pw_uid == 0: raise ValueError('Database account cannot be root')
    for path in (TARGET, STATE, HOOK, UNITS / 'witch-player-deletion.service', UNITS / 'witch-player-deletion.timer'):
        if path.exists() or path.is_symlink(): raise ValueError('Already or partially installed; review before retrying')
    for name, expected in INSPECTED.items():
        if digest(SERVER / name) != expected: raise ValueError('Live API changed since review')
    if digest(Path('/etc/caddy/Caddyfile')) != CADDY_HASH: raise ValueError('Public route configuration changed')
    if digest(Path('/usr/local/lib/nasus-backup/publish_backup_health.py')) != HEALTH_HASH:
        raise ValueError('Existing health publisher changed')
    for name in ('curl.conf', 'recipient.txt'):
        if not private_file(CONFIG / name, 0): raise ValueError('Unsafe offsite configuration metadata')
    age_info = AGE.lstat()
    if (not stat.S_ISREG(age_info.st_mode) or age_info.st_uid != 0 or age_info.st_mode & 0o022
            or age_info.st_mode & 0o005 != 0o005): raise ValueError('Unsafe age binary permissions')
    inspection = database_as_service_user(DATA, account)
    result = inspection.get('Database', {})
    if (inspection.get('Status') != 'INSPECTED'
            or not all(result.get(k) is True for k in ('CoreTablesPresent','QuickCheckPassed','ForeignKeysPassed'))
            or any(result.get('DeletionTables', {}).values())):
        raise ValueError('Preflight schema no longer matches reviewed state')
    if not collect()['Healthy'] or http_code('/') != '503': raise ValueError('Initial health or API closure check failed')
    print('1/5 Fresh verified encrypted database backup...', flush=True)
    started = time.time()
    systemctl('start', 'witch-player-offsite.service', timeout=1900)
    backup = read_json(Path('/var/lib/witch-player-offsite/last-success.json'))
    if not recent_success(backup, time.time()): raise ValueError('Fresh cloud backup not verified')
    import datetime as dt
    if dt.datetime.fromisoformat(backup['CompletedUtc']).timestamp() < started - 2:
        raise ValueError('Only an old backup report exists')
    installed_units = False
    hook_installed = False
    try:
        # Root-owned isolated modules; the API source tree is not replaced.
        shutil.copytree(runtime, TARGET, ignore=shutil.ignore_patterns('__pycache__'))
        for parent, dirs, files in os.walk(TARGET):
            Path(parent).chmod(0o755)
            for file in files: (Path(parent) / file).chmod(0o644)
        STATE.mkdir(mode=0o700)
        atomic_json(STATE / 'budget.json', {'ReservedBytes': 0})
        print('2/5 Encrypted synthetic PUT/readback probe (no player data)...', flush=True)
        cipher = AgeEncryptor(AGE, CONFIG / 'recipient.txt')(b'{"Purpose":"deletion-worker-installation-probe","Version":1}\n')
        reserve_upload(STATE, len(cipher))
        probe_key = 'deletion-probes/' + uuid.uuid4().hex + '.age'
        if SakuraPublisher(CONFIG / 'curl.conf').probe(probe_key, cipher) != hashlib.sha256(cipher).hexdigest():
            raise ValueError('Synthetic probe not verified')
        atomic_json(STATE / 'installation-probe.json', {'Verified': True, 'CheckedUnix': time.time(), 'Key': probe_key})
        print('3/5 Verified local snapshot and additive empty schema migration...', flush=True)
        migration = as_database_user(account, lambda: migrate_empty_schema(DATA))
        if not all(migration.values()): raise ValueError('Migration verification failed')
        atomic_json(STATE / 'migration.json', migration)
        print('4/5 Installing isolated worker and timer...', flush=True)
        for name in ('witch-player-deletion.service', 'witch-player-deletion.timer'):
            shutil.copyfile(TARGET / 'deploy' / name, UNITS / name)
            (UNITS / name).chmod(0o644)
        installed_units = True
        systemctl('daemon-reload')
        command(['/usr/bin/systemd-analyze', 'verify', str(UNITS / 'witch-player-deletion.service'), str(UNITS / 'witch-player-deletion.timer')])
        systemctl('start', 'witch-player-deletion.service', timeout=190)
        if read_json(STATE / 'status.json').get('Healthy') is not True: raise ValueError('Worker did not become healthy')
        systemctl('enable', '--now', 'witch-player-deletion.timer')
        print('5/5 Adding worker failures/staleness to existing health monitoring...', flush=True)
        HOOK.parent.mkdir(mode=0o755, exist_ok=True)
        with HOOK.open('x') as out:
            out.write('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I /usr/local/lib/nasus-deletion/deploy/publish_backup_health.py --require-deletion-worker\n')
        hook_installed = True
        HOOK.chmod(0o644)
        systemctl('daemon-reload')
        systemctl('start', 'witch-player-health.service', timeout=55)
        if not collect(require_deletion_worker=True)['Healthy']: raise ValueError('Integrated monitoring check failed')
        if http_code('/healthz') != '200' or http_code('/healthz', head=True) != '200' or http_code('/') != '503':
            raise ValueError('External health/closure check failed')
        for name, expected in INSPECTED.items():
            if digest(SERVER / name) != expected: raise ValueError('API source drift detected')
        if digest(Path('/etc/caddy/Caddyfile')) != CADDY_HASH: raise ValueError('Route configuration drift detected')
        print('DELETION_WORKER_SETUP_COMPLETE: empty schema, encrypted probe readback, idle worker and health verified.')
        print('API/Caddy unchanged. Game/admin/deletion routes remain CLOSED. No account was deleted.')
        print('Real deletion end-to-end, latest disaster-recovery inventory and alert delivery still need separate validation.')
    except Exception:
        # Never roll back a database from a snapshot: new user writes could exist.
        if installed_units:
            subprocess.run(['/usr/bin/systemctl', 'disable', '--now', 'witch-player-deletion.timer'], capture_output=True, timeout=30)
            subprocess.run(['/usr/bin/systemctl', 'stop', 'witch-player-deletion.service'], capture_output=True, timeout=190)
        if hook_installed:
            HOOK.replace(root_stage / 'disabled-health-hook.conf')
            subprocess.run(['/usr/bin/systemctl', 'daemon-reload'], capture_output=True, timeout=30)
            subprocess.run(['/usr/bin/systemctl', 'start', 'witch-player-health.service'], capture_output=True, timeout=55)
        print('INSTALLATION_INCOMPLETE: new timer stopped; existing health command restored when installed.')
        print('Additive schema, local snapshot and installed files are RETAINED for review. Do not rerun automatically.')
        raise


if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('INSTALLATION_STOPPED: ' + type(error).__name__ + '; no exception details or secrets printed.')
        raise SystemExit(1)
