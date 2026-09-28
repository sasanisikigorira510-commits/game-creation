"""Publish only a boolean and check timestamp. Never change backups or credentials."""
import datetime as dt
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

REPORT = Path('/var/lib/witch-player-offsite/last-success.json')
OUTPUT = Path('/run/witch-player-health/status.json')
MAX_BACKUP_AGE = 7200


def recent_success(report, now):
    try:
        stamp = dt.datetime.fromisoformat(report['CompletedUtc'])
        if stamp.utcoffset() is None:
            return False
        age = now - stamp.timestamp()
        return report['Status'] == 'OFFSITE_CIPHERTEXT_VERIFIED' and 0 <= age <= MAX_BACKUP_AGE
    except (KeyError, ValueError, TypeError, AttributeError, OverflowError):
        return False


def unit_properties(unit):
    result = subprocess.run(['/usr/bin/systemctl', 'show', unit,
                             '-p', 'LoadState', '-p', 'ActiveState', '-p', 'Result',
                             '-p', 'ExecMainStatus'], check=True, capture_output=True, text=True, timeout=10)
    return dict(line.split('=', 1) for line in result.stdout.splitlines() if '=' in line)


def units_healthy(read=unit_properties, require_deletion_worker=False, on_reason=None):
    names = ('witch-player-offsite', 'witch-player-backup')
    if require_deletion_worker:
        names += ('witch-player-deletion',)
    for name in names:
        timer, service = read(name + '.timer'), read(name + '.service')
        if timer.get('LoadState') != 'loaded' or timer.get('ActiveState') != 'active':
            if on_reason: on_reason({'Reason': 'TIMER_STATE_REJECTED', 'Unit': name + '.timer'})
            return False
        if (service.get('LoadState') != 'loaded' or service.get('ActiveState') == 'failed'
                or service.get('Result') != 'success' or service.get('ExecMainStatus') != '0'):
            if on_reason: on_reason({'Reason': 'SERVICE_STATE_REJECTED', 'Unit': name + '.service'})
            return False
    return True


def deletion_worker_healthy(path, now=None):
    try:
        if path.is_symlink() or not path.is_file(): return False
        info = path.stat()
        if info.st_uid != os.geteuid() or info.st_mode & 0o077: return False
        with path.open('rb') as stream: raw = stream.read(4097)
        value = json.loads(raw)
        stamp = value.get('CheckedUnix')
        # Compare against wall time after the atomic marker was read, not the
        # collection start: the worker can finish while systemctl is queried.
        now = time.time() if now is None else now
        return (len(raw) <= 4096 and value.get('Healthy') is True
                and type(stamp) in (int, float) and 0 <= now - stamp <= 300)
    except (OSError, ValueError, TypeError, AttributeError):
        return False


def collect(report=REPORT, now=None, read=unit_properties, *, require_deletion_worker=False,
            deletion_report=Path('/var/lib/witch-player-deletion/status.json'), on_reason=None):
    checked = time.time() if now is None else now
    reason = {'Reason': 'REPORT_READ_SIZE_OR_PARSE_FAILED'}
    def rejected(value):
        nonlocal reason
        reason = value
    try:
        with report.open('rb') as stream:
            raw = stream.read(4097)
        healthy = False
        if len(raw) <= 4096:
            value = json.loads(raw)
            reason = {'Reason': 'BACKUP_REPORT_REJECTED'}
            healthy = recent_success(value, time.time() if now is None else now)
        if healthy:
            reason = {'Reason': 'UNIT_QUERY_FAILED'}
            healthy = units_healthy(read, require_deletion_worker, rejected)
        if healthy and require_deletion_worker:
            reason = {'Reason': 'DELETION_MARKER_REJECTED'}
            healthy = deletion_worker_healthy(deletion_report, now)
    except (OSError, ValueError, TypeError, subprocess.SubprocessError):
        healthy = False
    if on_reason: on_reason({'Reason': 'HEALTHY'} if healthy else reason)
    return {'Healthy': bool(healthy), 'CheckedUnix': checked}


def publish(value, destination=OUTPUT):
    fd, temporary = tempfile.mkstemp(prefix='.health-', dir=destination.parent)
    try:
        with os.fdopen(fd, 'w') as stream:
            json.dump(value, stream)
            stream.flush()
            os.fsync(stream.fileno())
            os.fchmod(stream.fileno(), 0o644)
        os.replace(temporary, destination)
    finally:
        Path(temporary).unlink(missing_ok=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--require-deletion-worker', action='store_true')
    args = parser.parse_args()
    if os.geteuid() != 0:
        raise SystemExit('Must run through the root health publisher service')
    reasons = []
    publish(collect(require_deletion_worker=args.require_deletion_worker, on_reason=reasons.append))
    print('NASUS_HEALTH_REASON=' + json.dumps(reasons[-1], sort_keys=True), flush=True)
