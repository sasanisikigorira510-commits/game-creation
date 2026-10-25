"""One bounded recovery: preserve only verified failed synthetic canary inputs."""
import copy
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import time

BROKER_HASH = '8eba42bb06b9dd4beee549d2e623eb15b71eac5fe21799db5fe1f2ef42b43e29'
BASE = Path('/opt/witch-player-arena/production')
ARTIFACT = 'e4c6970aeff35954a154fa52a255d87d6626c56817b83d091b7f527e10990b24'
SPOOL = BASE/'artifacts'/ARTIFACT/'jobs'
TIMERS = ['witch-player-arena-production-native-probe.timer',
          'witch-player-arena-production-operations.timer']
PROBE = 'witch-player-arena-production-native-probe.service'
OPERATIONS = 'witch-player-arena-production-operations.service'
report = dict(Purpose='ARENA_RETAINED_CANARY_RECOVERY_20261025', StartedUnix=time.time(),
              Actions=[], SourceChanged=False, ConfigurationChanged=False,
              DatabaseDirectlyOpened=False, DeletedFiles=0, ArchiveVerified=False)
stopped = []
lock = None


def require(value, reason):
    if not value:
        raise RuntimeError(reason)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()


def protected(path, directory=False):
    for ancestor in path.parents:
        info = ancestor.lstat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and not info.st_mode & 0o022,
                'UNTRUSTED_PARENT')
    info = path.lstat()
    require(info.st_uid == 0 and not info.st_mode & 0o022 and
            (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)),
            'UNTRUSTED_NODE')
    if not directory:
        require(info.st_nlink == 1 and info.st_size <= 262144, 'INPUT_NODE_REJECTED')
    return info


def public_json(path):
    protected(path)
    return json.loads(path.read_text())


def state(unit):
    result = subprocess.run(['systemctl', 'show', unit,
        '--property=LoadState,ActiveState,SubState,Result,ExecMainStatus'],
        capture_output=True, text=True, timeout=10, check=True)
    return dict(line.split('=', 1) for line in result.stdout.splitlines() if '=' in line)


def action(verb, unit):
    require(unit in TIMERS + [PROBE, OPERATIONS], 'UNIT_REJECTED')
    result = subprocess.run(['systemctl', verb, unit], stdout=subprocess.DEVNULL,
                            stderr=subprocess.DEVNULL, timeout=65)
    report['Actions'].append(dict(Action=verb, Unit=unit, ExitCode=result.returncode))
    return result.returncode == 0


def health():
    result = {}
    for name, path in [('Native', Path('/run/witch-player-arena/production-health.json')),
                       ('Operations', Path('/run/witch-player-arena-production-operations/status.json'))]:
        value = public_json(path)
        result[name] = {k: value.get(k) for k in
                       ['Healthy', 'NativeReady', 'SettlementCaughtUp', 'CheckedUnix']}
        result[name]['AgeSeconds'] = round(time.time()-value['CheckedUnix'], 3)
    return result


def normalize_canary(value, template):
    require(type(value) is dict and set(value) == {'Version', 'Profile'} and value['Version'] == 1,
            'NON_CANARY_RETAINED_INPUT')
    selected = value.get('Profile', {}).get('PartyMonsterInstanceIds')
    original = template['Profile']['PartyMonsterInstanceIds']
    require(type(selected) is list and len(selected) == len(original) == 2,
            'NON_CANARY_RETAINED_INPUT')
    matches = [re.fullmatch(r'arena-probe-([a-f0-9]{32})-([01])', x) if isinstance(x, str)
               else None for x in selected]
    require(all(matches) and matches[0][1] == matches[1][1]
            and [int(x[2]) for x in matches] == [0, 1], 'NON_CANARY_RETAINED_INPUT')
    mapping = dict(zip(selected, original))
    def convert(node):
        if isinstance(node, dict):
            return {key: convert(item) for key, item in node.items()}
        if isinstance(node, list):
            return [convert(item) for item in node]
        return mapping.get(node, node) if isinstance(node, str) else node
    require(convert(value) == template, 'NON_CANARY_RETAINED_INPUT')


def retained(template):
    protected(SPOOL, directory=True)
    entries = list(SPOOL.iterdir())
    require(len(entries) <= 16, 'UNEXPECTED_RETENTION_COUNT')
    result = []
    for directory in entries:
        require(re.fullmatch(r'witch-weekly-job-[a-f0-9]{32}', directory.name),
                'RETAINED_DIRECTORY_REJECTED')
        info = protected(directory, directory=True)
        require(stat.S_IMODE(info.st_mode) == 0o700 and
                {x.name for x in directory.iterdir()} == {'input.json'},
                'RETAINED_CONTENT_REJECTED')
        source = directory/'input.json'
        source_info = protected(source)
        require(stat.S_IMODE(source_info.st_mode) == 0o444, 'INPUT_MODE_REJECTED')
        raw = source.read_bytes()
        normalize_canary(json.loads(raw), template)
        result.append((directory, hashlib.sha256(raw).hexdigest(), info.st_mtime))
    return result


try:
    require(os.geteuid() == 0, 'ADMIN_AUTHENTICATION_REQUIRED')
    config = public_json(Path('/etc/witch-player-arena/production-broker.json'))
    require(hashlib.sha256(canonical(config)).hexdigest() == BROKER_HASH
            and config['Realm'] == 'Production' and config['Spool'] == str(SPOOL), 'CONFIG_PIN_REJECTED')
    template = public_json(Path('/etc/witch-player-arena/production-probe.json'))['InputTemplate']
    report['Before'] = health()
    report['UnitsBefore'] = {u: state(u) for u in TIMERS + [PROBE, OPERATIONS]}
    memory = int(next(x.split()[1] for x in Path('/proc/meminfo').read_text().splitlines()
                      if x.startswith('MemAvailable:')))
    report['MemAvailableKiB'] = memory
    require(memory >= 393216, 'CURRENT_MEMORY_RESERVE_UNAVAILABLE')
    # Stop only these two scheduled diagnostic checks; restore them even on failure.
    # Do not touch API, broker, settlement, Review, market or other game services.
    for timer in TIMERS:
        require(state(timer)['ActiveState'] == 'active', 'TIMER_NOT_ORIGINALLY_ACTIVE')
        require(action('stop', timer), 'TIMER_STOP_FAILED')
        stopped.append(timer)
    for unit in (PROBE, OPERATIONS):
        if state(unit)['ActiveState'] == 'activating':
            require(action('stop', unit), 'RUNNING_CHECK_STOP_FAILED')
    lock = os.open('/opt/witch-player-arena/native-admission.lock', os.O_RDONLY | os.O_NOFOLLOW)
    require(stat.S_ISREG(os.fstat(lock).st_mode) and os.fstat(lock).st_uid == 0, 'LOCK_REJECTED')
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    jobs = retained(template)
    report['RetainedCount'] = len(jobs)
    report['AllRetainedInputsExactlyMatchSyntheticCanary'] = True
    report['RetainedInputs'] = [dict(Name=x.name, Sha256=digest, ModifiedUnix=mtime)
                                for x, digest, mtime in jobs]
    require(len(jobs) >= 8, 'RETENTION_EXHAUSTION_NOT_CONFIRMED')
    archive = BASE/('recovery-20261025-' + str(time.time_ns()))
    archive.mkdir(mode=0o700, exist_ok=False)
    report['ArchivePath'] = str(archive)
    report['MovedDirectories'] = 0
    for directory, digest, _ in jobs:
        require(hashlib.sha256((directory/'input.json').read_bytes()).hexdigest() == digest,
                'INPUT_CHANGED_BEFORE_MOVE')
        destination = archive/directory.name
        require(not destination.exists(), 'ARCHIVE_DESTINATION_EXISTS')
        os.rename(directory, destination)
        require(hashlib.sha256((destination/'input.json').read_bytes()).hexdigest() == digest
                and not directory.exists(), 'ARCHIVE_VERIFICATION_FAILED')
        report['MovedDirectories'] += 1
    report['ArchiveVerified'] = True
    require(not list(SPOOL.iterdir()), 'SPOOL_NOT_EMPTY_AFTER_ARCHIVE')
    os.close(lock)
    lock = None
    # The operations guard requires the original timers to be active.
    for timer in list(stopped):
        require(action('start', timer), 'TIMER_RESTORE_FAILED')
        stopped.remove(timer)
    checks = []
    for index in range(3):
        require(action('start', PROBE), 'NATIVE_PROBE_FAILED_AFTER_ARCHIVE')
        observed = health()['Native']
        require(observed['Healthy'] and 0 <= observed['AgeSeconds'] < 120,
                'NATIVE_WITNESS_NOT_FRESH')
        checks.append(observed)
        if index < 2:
            time.sleep(2)
    report['NativeChecks'] = checks
    require(action('start', OPERATIONS), 'OPERATIONS_CHECK_FAILED_AFTER_ARCHIVE')
    report['After'] = health()
    require(report['After']['Operations']['Healthy'] is True
            and report['After']['Operations']['NativeReady'] is True
            and report['After']['Operations']['SettlementCaughtUp'] is True,
            'OPERATIONS_NOT_HEALTHY')
    report['Status'] = 'RECOVERED'
except Exception as error:
    report['Status'] = 'STOPPED'
    report['ErrorType'] = type(error).__name__
    # Every RuntimeError message here is a fixed diagnostic code, not user data.
    if type(error) is RuntimeError:
        report['Reason'] = str(error)
finally:
    if lock is not None:
        os.close(lock)
    for timer in stopped:
        try:
            if not action('start', timer):
                report.setdefault('TimerRestoreErrors', []).append(timer)
        except Exception:
            report.setdefault('TimerRestoreErrors', []).append(timer)
    report['FinishedUnix'] = time.time()
    print('ARENA_RECOVERY_RESULT=' + json.dumps(report, sort_keys=True), flush=True)
