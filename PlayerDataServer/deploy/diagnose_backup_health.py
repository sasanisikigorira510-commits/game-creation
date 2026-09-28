"""One authenticated read-only check in normal and health-service contexts.

No DB, cloud requests, credential files, health publication or persistent units.
Only allowlisted booleans, times, process metadata and unit states reach stdout.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time

PUBLISHER = Path('/usr/local/lib/nasus-backup/publish_backup_health.py')
PUBLISHER_HASH = '8b4b3e59b2586b2946ffaee39e501a835d799e1f87b21fe4f24a48e4b23412ac'
REPORT = Path('/var/lib/witch-player-offsite/last-success.json')
PUBLIC = Path('/run/witch-player-health/status.json')
UNITS = ('witch-player-offsite.timer', 'witch-player-backup.timer',
         'witch-player-offsite.service', 'witch-player-backup.service')
UNIT_KEYS = {'LoadState', 'ActiveState', 'Result', 'ExecMainStatus'}
ENV = {'PATH': '/usr/sbin:/usr/bin:/sbin:/bin', 'LC_ALL': 'C'}


def error_info(error):
    return {'ErrorType': type(error).__name__,
            'Errno': error.errno if isinstance(error, OSError) else None}


def attempt(fn):
    try: return {'OK': True, 'Value': fn()}
    except Exception as error: return dict(OK=False, **error_info(error))


def backup_summary(raw, now):
    result = {'BoundedSize': len(raw) <= 4096}
    if len(raw) > 4096: return result
    value = json.loads(raw)
    result['RecognizedSuccessStatus'] = value.get('Status') == 'OFFSITE_CIPHERTEXT_VERIFIED'
    try:
        stamp = dt.datetime.fromisoformat(value['CompletedUtc'])
        result['TimestampHasZone'] = stamp.utcoffset() is not None
        age = now - stamp.timestamp()
        result['AgeSeconds'] = round(age, 3)
        result['AgeWithinTwoHours'] = result['TimestampHasZone'] and 0 <= age <= 7200
    except (KeyError, ValueError, TypeError, AttributeError, OverflowError):
        result['TimestampValid'] = False
    return result


def metadata(path):
    info = path.lstat()
    return {'Uid': info.st_uid, 'Gid': info.st_gid, 'Mode': oct(stat.S_IMODE(info.st_mode)),
            'RegularFile': stat.S_ISREG(info.st_mode), 'Directory': stat.S_ISDIR(info.st_mode),
            'Symlink': stat.S_ISLNK(info.st_mode), 'Bytes': info.st_size}


def read_bounded(path):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as source:
        if not stat.S_ISREG(os.fstat(source.fileno()).st_mode): raise ValueError('Not a regular file')
        return source.read(4097)


def unit_probe(unit):
    args = ['/usr/bin/systemctl', 'show', unit]
    for key in sorted(UNIT_KEYS): args.extend(['-p', key])
    result = subprocess.run(args, capture_output=True, text=True, timeout=10,
                            stdin=subprocess.DEVNULL, env=ENV)
    values = dict(line.split('=', 1) for line in result.stdout.splitlines() if '=' in line)
    values = {k: v for k, v in values.items() if k in UNIT_KEYS and len(v) < 80}
    # Never emit arbitrary stderr (or service environment/command lines).
    lower = result.stderr.lower()
    return {'ReturnCode': result.returncode, 'Properties': values,
            'BusError': 'bus' in lower, 'PermissionError': 'permission denied' in lower,
            'MemoryError': 'memory' in lower, 'StderrPresent': bool(result.stderr)}


def process_metadata():
    keys = {'Uid', 'Gid', 'CapEff', 'CapPrm', 'CapBnd', 'NoNewPrivs', 'Seccomp', 'Seccomp_filters'}
    result = {}
    for line in Path('/proc/self/status').read_text().splitlines():
        key, _, value = line.partition(':')
        if key in keys: result[key] = value.strip()
    return result


def publisher():
    info = PUBLISHER.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
        raise ValueError('Publisher metadata mismatch')
    raw = PUBLISHER.read_bytes()
    if hashlib.sha256(raw).hexdigest() != PUBLISHER_HASH: raise ValueError('Publisher changed')
    scope = {'__name__': 'diagnosed_health_publisher', '__file__': str(PUBLISHER)}
    exec(compile(raw, str(PUBLISHER), 'exec'), scope)
    return scope


def public_health():
    raw = read_bounded(PUBLIC)
    if len(raw) > 4096: raise ValueError('Oversized marker')
    value = json.loads(raw); stamp = value.get('CheckedUnix')
    return {'Healthy': value.get('Healthy') is True,
            'AgeSeconds': round(time.time() - stamp, 3) if type(stamp) in (int, float) else None}


def snapshot(label):
    scope = publisher()
    report = {'Context': label, 'CheckedUnix': time.time(),
              'Process': attempt(process_metadata),
              'ReportDirectory': attempt(lambda: metadata(REPORT.parent)),
              'ReportMetadata': attempt(lambda: metadata(REPORT)),
              'Backup': attempt(lambda: backup_summary(read_bounded(REPORT), time.time())),
              'Units': {name: attempt(lambda name=name: unit_probe(name)) for name in UNITS},
              'PublishedMarker': attempt(public_health)}
    report['OriginalUnitsHealthy'] = attempt(scope['units_healthy'])
    # collect() is read-only; deliberately never call publish().
    report['OriginalCollect'] = attempt(scope['collect'])
    print('NASUS_HEALTH_DIAGNOSTIC=' + json.dumps(report, sort_keys=True, allow_nan=False), flush=True)


def sandbox_command(script):
    return ['/usr/bin/systemd-run', '--quiet', '--wait', '--pipe', '--collect',
            '--property=User=root', '--property=UMask=0077',
            '--property=NoNewPrivileges=true', '--property=PrivateTmp=true',
            '--property=ProtectSystem=strict', '--property=ProtectHome=true',
            '--property=ReadWritePaths=/run/witch-player-health',
            '--property=RestrictAddressFamilies=AF_UNIX', '--property=MemoryMax=64M',
            '--property=RuntimeMaxSec=90', '--property=LimitCORE=0',
            '/usr/bin/python3', '-I', '-B', str(script), '--context=service-sandbox']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--context', choices=('normal-root', 'service-sandbox'), default='normal-root')
    args = parser.parse_args()
    if os.geteuid() != 0: raise PermissionError('User authentication required')
    sys.dont_write_bytecode = True
    snapshot(args.context)
    if args.context == 'normal-root':
        # Root-owned pinned script is already staged under /run, outside PrivateTmp.
        result = subprocess.run(sandbox_command(Path(__file__).resolve()),
                                stdin=subprocess.DEVNULL, env=ENV, timeout=100)
        print('SANDBOX_DIAGNOSTIC_EXIT=' + str(result.returncode), flush=True)
        if result.returncode: raise RuntimeError('Sandbox diagnostic incomplete')
        print('HEALTH_DIAGNOSTIC_COMPLETE: no service configuration or data changed.', flush=True)


if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('HEALTH_DIAGNOSTIC_STOPPED=' + json.dumps(error_info(error)), flush=True)
        raise SystemExit(1)
