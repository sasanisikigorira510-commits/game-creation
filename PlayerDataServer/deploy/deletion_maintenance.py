#!/usr/bin/python3 -I
"""Fixed, two-hour maintenance authorization for steps 2-B/2-C only.

No arbitrary commands, file arguments, stdin, schema changes or account deletion.
Installed root-owned; only apply/verify/rollback/revoke are sudo-authorized.
"""
import fcntl
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time

ROOT = Path('/var/lib/nasus-maintenance-20260925')
EXECUTABLE = Path('/usr/local/sbin/nasus-deletion-maintenance-20260925')
POLICY = Path('/etc/sudoers.d/nasus-deletion-maintenance-20260925')
SERVICE = 'witch-player-deletion.service'
TIMER = 'witch-player-deletion.timer'
CAP = Path('/etc/systemd/system/witch-player-deletion.service.d/40-minimal-capabilities.conf')
HOOK = Path('/etc/systemd/system/witch-player-health.service.d/30-deletion-worker.conf')
CAP_TEXT = ('[Service]\nCapabilityBoundingSet=\n'
            'CapabilityBoundingSet=CAP_SETUID CAP_SETGID\nAmbientCapabilities=\n'
            'AmbientCapabilities=CAP_SETUID CAP_SETGID\n')
HOOK_TEXT = ('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I '
             '/usr/local/lib/nasus-deletion/deploy/publish_backup_health.py --require-deletion-worker\n')
ACTIONS = ('apply', 'verify', 'rollback', 'revoke')
PINS = {
 '/etc/caddy/Caddyfile': '75ceb4d8ce8b107f11786354a7ca56442d5bb41a960ffa251e0f20bf724efa44',
 '/etc/systemd/system/witch-player-deletion.service': '1697450a863d6e53e081012b89ff058b581edb6518038a8f01be22eec4173851',
 '/etc/systemd/system/witch-player-deletion.timer': 'f9c617eb4ff1cdb8dda9b38b37b08c52bec43ff667e9e44501128625572d3a6b',
 '/usr/local/lib/nasus-backup/publish_backup_health.py': '8b4b3e59b2586b2946ffaee39e501a835d799e1f87b21fe4f24a48e4b23412ac',
 '/opt/witch-player/server/store.py': '840f038c7109241153a24234914bde1ebf375dfb8a7bf3373ed98dd166c8a891',
 '/opt/witch-player/server/production.py': 'd312a016bf24e9b9e3afd892d89eb617c9fc95bdffad300d1534462a858533fa',
 '/opt/witch-player/server/application.py': '4fdbfafde3a91b4b633f510338296373dbbc2aac06c8722721467547db19f2dd',
 '/usr/local/lib/nasus-deletion/run_worker.py': '496e9fa769aea3fea10d83534f2c5ed50457cc3996b795d24f39c3c0a4351808',
 '/usr/local/lib/nasus-deletion/store.py': '2d2664694f5951a00f65880a15ace2cc238e2edbf94f3ac45c9cb36fa0e95bb9',
 '/usr/local/lib/nasus-deletion/deletion_journal.py': '0bddbb1c9a43ff13143f3bb72c6418b0e5b1d1ef9a693d89b0ae2f3a709e2427',
 '/usr/local/lib/nasus-deletion/deploy/deletion_worker.py': 'aa8d1e4c2ac217ccfab43cdb25bc043fdf3e95a6cf4f5c041b8352b4ef26b7c4',
 '/usr/local/lib/nasus-deletion/deploy/preflight_deletion_worker.py': 'f8a6d634b359eaf71c4ac1737345d576d6c742e027b86a8310030c13d9951b21',
 '/usr/local/lib/nasus-deletion/deploy/sakura_deletion_journal.py': '04407d59083a36295d00e514d7468c7f162d36955ba854a513b5969760874bdc',
 '/usr/local/lib/nasus-deletion/deploy/publish_backup_health.py': '5609ab7b15d3ba9ec7bce4cefc59f0c14e3d5923de26b8e8c8446e538b7fdaab',
}


class MaintenanceError(RuntimeError):
    pass


def require(condition, code):
    if not condition: raise MaintenanceError(code)


def trusted(path, *, directory=False, private=False):
    info = path.lstat()
    require(info.st_uid == 0 and not info.st_mode & (0o077 if private else 0o022)
            and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)), 'UNSAFE_METADATA')
    for parent in path.parents:
        info = parent.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022,
                'UNSAFE_PARENT')


def read_json(path):
    trusted(path, private=True)
    require(path.stat().st_size <= 8192, 'OVERSIZED_STATE')
    return json.loads(path.read_text())


def new_file(path, text, mode=0o600):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, mode)
    with os.fdopen(fd, 'w') as stream:
        stream.write(text); stream.flush(); os.fsync(stream.fileno())


def run(args, *, timeout=30, check=True):
    result = subprocess.run(args, capture_output=True, text=True, timeout=timeout,
                            stdin=subprocess.DEVNULL,
                            env={'PATH': '/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL': 'C'})
    require(not check or result.returncode == 0, 'COMMAND_FAILED')
    return result.stdout.strip()


def ctl(*args, timeout=30, check=True):
    return run(['/usr/bin/systemctl', *args], timeout=timeout, check=check)


def prop(unit, name):
    return ctl('show', unit, '--property=' + name, '--value')


def unchanged():
    for name, expected in PINS.items():
        path = Path(name); trusted(path)
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected, 'PIN_MISMATCH')


def external_health():
    for suffix, head, expected in (('/healthz', False, '200'), ('/healthz', True, '200'), ('/', False, '503')):
        args = ['/usr/bin/curl', '--disable', '--proto', '=https', '--silent', '--show-error',
                '--connect-timeout', '5', '--max-time', '15', '--output', '/dev/null', '--write-out', '%{http_code}']
        if head: args.append('--head')
        require(run(args + ['https://api.nasus-games.com' + suffix], timeout=20) == expected, 'HTTP_CHECK_FAILED')


def queue_empty():
    # Import only hash-pinned root-owned modules; data never returns to stdout.
    import pwd
    sys.path.insert(0, '/usr/local/lib/nasus-deletion')
    from deploy.preflight_deletion_worker import database_as_service_user, DATA
    account = pwd.getpwnam('witchplayer')
    require(account.pw_uid == 999 and account.pw_gid == 988, 'ACCOUNT_CHANGED')
    result = database_as_service_user(DATA, account)
    data = result.get('Database', {})
    require(result.get('Status') == 'INSPECTED'
            and all(data.get(k) is True for k in ('CoreTablesPresent', 'QuickCheckPassed', 'ForeignKeysPassed', 'JournalInstanceMatches'))
            and data.get('DeletionTables') and all(data['DeletionTables'].values())
            and data.get('PendingEvents') == 0 and data.get('VerifiedEvents') == 0
            and data.get('HistoricalDeletionsPresent') is False, 'NOT_REVIEWED_EMPTY_QUEUE')


def base_health():
    for unit in ('witch-player.service', 'witch-player-health.timer', 'witch-player-offsite.timer', 'witch-player-backup.timer'):
        require(prop(unit, 'ActiveState') == 'active', 'EXISTING_SERVICE_UNHEALTHY')
    external_health()


def check_settings():
    require(set(prop(SERVICE, 'CapabilityBoundingSet').split()) == {'cap_setuid', 'cap_setgid'}, 'CAP_SCOPE')
    require(set(prop(SERVICE, 'AmbientCapabilities').split()) == {'cap_setuid', 'cap_setgid'}, 'CAP_SCOPE')
    for name, expected in (('NoNewPrivileges', 'yes'), ('PrivateTmp', 'yes'), ('ProtectSystem', 'strict'), ('ProtectHome', 'yes')):
        require(prop(SERVICE, name) == expected, 'SANDBOX_CHANGED')
    require(prop(SERVICE, 'DropInPaths') == str(CAP), 'UNEXPECTED_DROPIN')


def check_worker():
    require(prop(SERVICE, 'Result') == 'success' and prop(SERVICE, 'ExecMainStatus') == '0', 'WORKER_FAILED')
    status = read_json(Path('/var/lib/witch-player-deletion/status.json'))
    require(status.get('Healthy') is True and type(status.get('CheckedUnix')) in (int, float)
            and 0 <= time.time() - status['CheckedUnix'] <= 180, 'STALE_OR_FAILED_WORKER')
    return status['CheckedUnix']


def authorize(action):
    require(action in ACTIONS, 'UNSUPPORTED_ACTION')
    grant = read_json(ROOT / 'grant.json')
    require(grant.get('Version') == 1, 'BAD_GRANT')
    if action in ('apply', 'verify'):
        require(type(grant.get('IssuedUnix')) in (int, float)
                and 0 <= time.time() - grant['IssuedUnix'] < 7200, 'AUTHORIZATION_EXPIRED')
    return grant


def owned_dropin(path, text):
    if not path.parent.exists(): path.parent.mkdir(mode=0o755)
    trusted(path.parent, directory=True)
    new_file(path, text, 0o644)


def archive_dropin(path, text, name):
    if not path.exists() and not path.is_symlink(): return
    trusted(path)
    require(path.read_text() == text, 'ROLLBACK_REFUSES_CHANGED_FILE')
    require(not (ROOT / name).exists(), 'ROLLBACK_ARCHIVE_EXISTS')
    path.rename(ROOT / name)


def rollback():
    require((ROOT / 'apply-started.json').exists(), 'NOTHING_APPLIED')
    # The preflight accepted only an inactive, disabled timer. Restore that state.
    ctl('disable', '--now', TIMER)
    ctl('stop', SERVICE, timeout=190)
    archive_dropin(HOOK, HOOK_TEXT, 'disabled-health-hook.conf')
    archive_dropin(CAP, CAP_TEXT, 'disabled-capability-hook.conf')
    ctl('daemon-reload')
    ctl('start', 'witch-player-health.service', timeout=55)
    base_health()
    print('ROLLBACK_COMPLETE: prior health command restored; deletion timer disabled.', flush=True)


def verify():
    unchanged(); check_settings(); queue_empty()
    trusted(HOOK); require(HOOK.read_text() == HOOK_TEXT, 'HEALTH_HOOK_CHANGED')
    require(prop('witch-player-health.service', 'DropInPaths') == str(HOOK), 'HEALTH_DROPIN_CHANGED')
    require(prop(TIMER, 'ActiveState') == 'active' and ctl('is-enabled', TIMER) == 'enabled', 'TIMER_NOT_ACTIVE')
    checked = check_worker(); base_health()
    from deploy.publish_backup_health import collect
    require(collect(require_deletion_worker=True)['Healthy'], 'INTEGRATED_HEALTH_FAILED')
    previous = read_json(ROOT / 'applied.json') if (ROOT / 'applied.json').exists() else {}
    return {'Status': 'DELETION_MAINTENANCE_VERIFIED', 'WorkerCheckedUnix': checked,
            'LaterTimerRunVerified': checked > previous.get('WorkerCheckedUnix', checked),
            'TimerActive': True, 'MonitoringIntegrated': True, 'GameRoutesClosed': True}


def apply():
    require(not (ROOT / 'apply-started.json').exists(), 'DO_NOT_RERUN_APPLY')
    unchanged(); base_health(); queue_empty()
    require(not prop(SERVICE, 'DropInPaths') and not prop('witch-player-health.service', 'DropInPaths'), 'EXISTING_DROPIN')
    require(prop(SERVICE, 'ActiveState') in ('inactive', 'failed')
            and prop(TIMER, 'ActiveState') == 'inactive' and ctl('is-enabled', TIMER, check=False) == 'disabled', 'UNEXPECTED_WORKER_STATE')
    for path in (CAP, HOOK):
        require(not path.exists() and not path.is_symlink(), 'EXISTING_REPAIR_FILE')
    new_file(ROOT / 'apply-started.json', json.dumps({'StartedUnix': time.time()}))
    try:
        print('2-B/1: installing only the bounded capability drop-in.', flush=True)
        owned_dropin(CAP, CAP_TEXT); ctl('daemon-reload'); check_settings()
        run(['/usr/bin/systemd-analyze', 'verify', SERVICE])
        started = time.time()
        ctl('start', SERVICE, timeout=190)
        require(check_worker() >= started - 1, 'NO_FRESH_WORKER_RUN')
        print('2-B COMPLETE: real worker completed; no deletion events present.', flush=True)
        print('2-C/1: enabling timer and integrating existing health monitor.', flush=True)
        ctl('enable', '--now', TIMER)
        owned_dropin(HOOK, HOOK_TEXT); ctl('daemon-reload')
        ctl('start', 'witch-player-health.service', timeout=55)
        report = verify()
        new_file(ROOT / 'applied.json', json.dumps(report, sort_keys=True))
        print(json.dumps(report, sort_keys=True), flush=True)
        print('NEXT: verify a later timer run, then revoke this temporary sudo authorization.', flush=True)
    except BaseException:
        try: rollback()
        except Exception:
            print('ROLLBACK_INCOMPLETE: manual review required; no automatic retry.', flush=True)
        raise


def revoke(grant):
    if not POLICY.exists():
        require((ROOT / 'authorization.revoked').exists(), 'POLICY_MISSING'); return
    trusted(POLICY)
    require(hashlib.sha256(POLICY.read_bytes()).hexdigest() == grant['PolicySha256'], 'POLICY_CHANGED')
    require(not (ROOT / 'authorization.revoked').exists(), 'REVOCATION_ARCHIVE_EXISTS')
    POLICY.rename(ROOT / 'authorization.revoked')
    print('AUTHORIZATION_REVOKED: only the dedicated maintenance permission was removed.', flush=True)


def main():
    require(os.geteuid() == 0 and len(sys.argv) == 2, 'USE_DEDICATED_SUDO_COMMAND')
    trusted(ROOT, directory=True, private=True)
    os.umask(0o077)
    fd = os.open(ROOT / 'maintenance.lock', os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        action = sys.argv[1]; grant = authorize(action)
        if action == 'apply': apply()
        elif action == 'verify': print(json.dumps(verify(), sort_keys=True))
        elif action == 'rollback': rollback()
        else: revoke(grant)


if __name__ == '__main__':
    try: main()
    except Exception as error:
        # Never emit subprocess stderr, arbitrary messages, credentials or DB rows.
        code = str(error) if isinstance(error, MaintenanceError) else type(error).__name__
        print('MAINTENANCE_STOPPED: ' + code + '; no secrets printed.')
        raise SystemExit(1)
