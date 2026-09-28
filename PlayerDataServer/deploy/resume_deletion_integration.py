"""Single authenticated 2-B/2-C repair with bounded timer verification.

No standing sudo grant. Requires a reviewed empty deletion queue and unchanged
closed API. Retains old code and baseline observer. Restores own drop-ins on
failure; never restores or modifies a database snapshot.
"""
import hashlib
import json
import os
from pathlib import Path
import signal
import sys
import time

HELPER_HASH = '51698a01a881d73b4b2b4a646038a4f48d003f2e7e6ed4b8bf4c52b4b3c32c83'
PUBLISHER_HASH = 'a42a6d7cb7eb49064b95babc7360d0927c18a2d192a320cc5dd180dc54decafb'
STATE = Path('/var/lib/nasus-deletion-integration-20260925')
TARGET = Path('/usr/local/lib/nasus-health-integrated-20260925')
CAP = Path('/etc/systemd/system/witch-player-deletion.service.d/40-minimal-capabilities.conf')
HOOK = Path('/etc/systemd/system/witch-player-health.service.d/60-integrated-health.conf')
BASE_HOOK = Path('/etc/systemd/system/witch-player-health.service.d/40-reason-logging.conf')
BASE_TEXT = ('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '
             '/usr/local/lib/nasus-health-observer-20260925/observe.py\n')
HOOK_TEXT = ('[Service]\nExecStart=\nExecStart=/usr/bin/python3 -I -B '
             '/usr/local/lib/nasus-health-integrated-20260925/publish.py --require-deletion-worker\n')
MARKER = Path('/run/witch-player-health/status.json')
EXTRA_PINS = {
    '/etc/systemd/system/witch-player-health.service': 'fdc6732cc6c28081980df8e72d2751cd5a1c3b445f44e17e07f5e26962a2d4e5',
    '/usr/local/lib/nasus-health-observer-20260925/observe.py': 'fb715fe7d0e80e91543c4d91ddf5f6ac0f06184716951fbe7b39943bbab32218',
}


def helper():
    path = Path(__file__).resolve().parent / 'deletion_maintenance.py'
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != HELPER_HASH: raise ValueError('Helper mismatch')
    scope = {'__name__': 'bounded_integration_helper', '__file__': str(path)}
    exec(compile(raw, str(path), 'exec'), scope)
    scope.update(ROOT=STATE, CAP=CAP, HOOK=HOOK, HOOK_TEXT=HOOK_TEXT)
    scope['trusted'](path)
    return scope


def verify_pins(h):
    h['unchanged']()
    for name, expected in EXTRA_PINS.items():
        path = Path(name); h['trusted'](path)
        h['require'](hashlib.sha256(path.read_bytes()).hexdigest() == expected, 'EXTRA_PIN_CHANGED')
    h['trusted'](BASE_HOOK)
    h['require'](BASE_HOOK.read_text() == BASE_TEXT, 'BASELINE_HOOK_CHANGED')


def preflight(h):
    verify_pins(h); h['base_health'](); h['queue_empty']()
    prop, require, ctl = h['prop'], h['require'], h['ctl']
    require(not prop(h['SERVICE'], 'DropInPaths'), 'WORKER_DROPIN_CHANGED')
    require(prop('witch-player-health.service', 'DropInPaths') == str(BASE_HOOK), 'HEALTH_DROPIN_CHANGED')
    require(prop(h['SERVICE'], 'ActiveState') in ('inactive', 'failed')
            and prop(h['TIMER'], 'ActiveState') == 'inactive'
            and ctl('is-enabled', h['TIMER'], check=False) == 'disabled', 'WORKER_STATE_CHANGED')
    for path in (STATE, TARGET, CAP, HOOK):
        require(not path.exists() and not path.is_symlink(), 'DO_NOT_REPEAT_OR_OVERWRITE')


def integrated_health(h, started):
    h['trusted'](MARKER)
    h['require'](MARKER.stat().st_size <= 4096, 'PUBLIC_MARKER_SIZE')
    value = json.loads(MARKER.read_text())
    stamp = value.get('CheckedUnix')
    h['require'](value.get('Healthy') is True and type(stamp) in (int, float)
                 and started <= stamp <= time.time() and time.time() - stamp <= 180,
                 'INTEGRATED_MARKER_UNHEALTHY')
    h['base_health']()
    return stamp


def verify_cycles(h, initial, started, *, clock=time.monotonic, pause=time.sleep):
    """At most 210 seconds; two later worker completions and a later monitor run."""
    begin = clock(); previous = initial; runs = 0; health_first = None; health_later = False
    while clock() - begin < 210:
        h['require'](h['prop'](h['TIMER'], 'ActiveState') == 'active', 'TIMER_STOPPED')
        worker = h['check_worker']()
        if worker > previous:
            previous = worker; runs += 1
            print('TIMER_COMPLETION_VERIFIED=' + str(runs), flush=True)
        marker = integrated_health(h, started)
        if health_first is None: health_first = marker
        elif marker > health_first: health_later = True
        if runs >= 2 and health_later and clock() - begin >= 90:
            return {'LaterWorkerCompletions': runs, 'LaterHealthRunVerified': True}
        pause(5)
    h['require'](False, 'TIMER_VERIFICATION_TIMED_OUT')


def rollback(h):
    h['ctl']('disable', '--now', h['TIMER'])
    h['ctl']('stop', h['SERVICE'], timeout=190)
    h['archive_dropin'](HOOK, HOOK_TEXT, 'disabled-integrated-health.conf')
    h['archive_dropin'](CAP, h['CAP_TEXT'], 'disabled-capabilities.conf')
    h['ctl']('daemon-reload')
    h['ctl']('start', 'witch-player-health.service', timeout=55)
    h['require'](h['prop']('witch-player-health.service', 'DropInPaths') == str(BASE_HOOK),
                 'BASELINE_NOT_RESTORED')
    h['base_health']()
    print('INTEGRATION_ROLLED_BACK: baseline reason logging retained; deletion timer disabled.', flush=True)


def apply(h):
    preflight(h)
    source = Path(__file__).resolve().parent / 'publish_backup_health.py'
    h['trusted'](source)
    raw = source.read_bytes()
    h['require'](hashlib.sha256(raw).hexdigest() == PUBLISHER_HASH, 'NEW_PUBLISHER_CHANGED')
    compile(raw, str(source), 'exec')
    h['trusted'](STATE.parent, directory=True); h['trusted'](TARGET.parent, directory=True)
    STATE.mkdir(mode=0o700); TARGET.mkdir(mode=0o700)
    h['new_file'](TARGET / 'publish.py', raw.decode(), 0o600)
    h['new_file'](STATE / 'started.json', json.dumps({'StartedUnix': time.time()}))
    try:
        print('1/3: restoring the tested minimal worker capabilities.', flush=True)
        h['owned_dropin'](CAP, h['CAP_TEXT']); h['ctl']('daemon-reload'); h['check_settings']()
        h['ctl']('start', h['SERVICE'], timeout=190)
        initial = h['check_worker']()
        h['ctl']('enable', '--now', h['TIMER'])
        print('2/3: enabling integrated health with sanitized reason logging.', flush=True)
        started = time.time()
        h['owned_dropin'](HOOK, HOOK_TEXT); h['ctl']('daemon-reload')
        h['run'](['/usr/bin/systemd-analyze', 'verify', h['SERVICE'], 'witch-player-health.service'])
        h['ctl']('start', 'witch-player-health.service', timeout=55)
        expected = str(BASE_HOOK) + ' ' + str(HOOK)
        h['require'](h['prop']('witch-player-health.service', 'DropInPaths') == expected, 'UNEXPECTED_HEALTH_DROPINS')
        print('3/3: checking automatic worker and health runs for up to 210 seconds.', flush=True)
        result = verify_cycles(h, initial, started)
        verify_pins(h); h['check_settings'](); h['queue_empty'](); integrated_health(h, started)
        h['trusted'](HOOK)
        h['require'](HOOK.read_text() == HOOK_TEXT
                     and h['prop']('witch-player-health.service', 'DropInPaths') == expected,
                     'INTEGRATED_HOOK_CHANGED')
        h['trusted'](TARGET / 'publish.py')
        h['require'](hashlib.sha256((TARGET / 'publish.py').read_bytes()).hexdigest() == PUBLISHER_HASH,
                     'INSTALLED_PUBLISHER_CHANGED')
        result.update(Status='DELETION_INTEGRATION_VERIFIED', GameRoutesClosed=True,
                      RealDeletionTestComplete=False, VerifiedUnix=time.time())
        h['new_file'](STATE / 'verified.json', json.dumps(result, sort_keys=True))
        print(json.dumps(result, sort_keys=True), flush=True)
    except BaseException:
        try: rollback(h)
        except BaseException:
            print('INTEGRATION_ROLLBACK_INCOMPLETE: inspect retained state; do not rerun.', flush=True)
        raise


def main():
    if os.geteuid() != 0 or len(sys.argv) != 1: raise ValueError('Authenticated invocation only')
    os.umask(0o077); sys.dont_write_bytecode = True
    def interrupted(*_): raise KeyboardInterrupt()
    for name in (signal.SIGTERM, signal.SIGHUP): signal.signal(name, interrupted)
    apply(helper())


if __name__ == '__main__':
    try: main()
    except BaseException:
        print('INTEGRATION_STOPPED: retain logs; do not rerun automatically.', flush=True)
        raise SystemExit(1)
