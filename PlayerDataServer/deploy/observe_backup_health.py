"""Observe the pinned baseline publisher without changing its health decisions.

No credentials, database access or cloud requests. Only fixed reason codes enter
the journal. The public marker is still written by the original publisher.
"""
import hashlib
import json
import os
from pathlib import Path
import stat
import sys

SOURCE = Path('/usr/local/lib/nasus-backup/publish_backup_health.py')
SOURCE_HASH = '8b4b3e59b2586b2946ffaee39e501a835d799e1f87b21fe4f24a48e4b23412ac'
UNITS = {f'witch-player-{name}.{kind}' for name in ('backup', 'offsite')
         for kind in ('timer', 'service')}


def load_source():
    for parent in SOURCE.parents:
        info = parent.lstat()
        if not stat.S_ISDIR(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
            raise ValueError('Untrusted parent')
    info = SOURCE.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
        raise ValueError('Untrusted publisher')
    raw = SOURCE.read_bytes()
    if hashlib.sha256(raw).hexdigest() != SOURCE_HASH:
        raise ValueError('Publisher changed')
    scope = {'__name__': 'observed_publisher', '__file__': str(SOURCE)}
    exec(compile(raw, str(SOURCE), 'exec'), scope)
    return scope


def observe(scope):
    """One collection, same reads and decision time; no second diagnostic probe."""
    events = []
    original_recent = scope['recent_success']
    original_read = scope['unit_properties']

    def recent(value, now):
        accepted = original_recent(value, now)
        if not accepted:
            events.append({'Reason': 'BACKUP_REPORT_REJECTED'})
        return accepted

    def read(unit):
        try:
            props = original_read(unit)
        except Exception:
            events.append({'Reason': 'UNIT_QUERY_FAILED',
                           'Unit': unit if unit in UNITS else 'other'})
            raise
        # Boolean observations only; no arbitrary systemctl text is logged.
        ok = props.get('LoadState') == 'loaded'
        if unit.endswith('.timer'):
            ok = ok and props.get('ActiveState') == 'active'
        else:
            ok = (ok and props.get('ActiveState') != 'failed'
                  and props.get('Result') == 'success' and props.get('ExecMainStatus') == '0')
        if not ok:
            events.append({'Reason': 'UNIT_STATE_REJECTED',
                           'Unit': unit if unit in UNITS else 'other'})
        return props

    scope['recent_success'] = recent
    try:
        value = scope['collect'](read=read)
    finally:
        scope['recent_success'] = original_recent
    if value['Healthy']:
        reason = {'Reason': 'HEALTHY'}
    else:
        # collect() catches file, JSON and subprocess errors. If it never
        # reached either observed gate, its report read/size/parse gate failed.
        reason = events[0] if events else {'Reason': 'REPORT_READ_SIZE_OR_PARSE_FAILED'}
    return value, reason


def main():
    if os.geteuid() != 0 or len(sys.argv) != 1:
        raise ValueError('Service invocation only')
    sys.dont_write_bytecode = True
    scope = load_source()
    value, reason = observe(scope)
    scope['publish'](value)
    print('NASUS_HEALTH_REASON=' + json.dumps(reason, sort_keys=True), flush=True)


if __name__ == '__main__':
    try:
        main()
    except Exception:
        # Fail closed: do not renew a healthy marker on observer failure.
        print('NASUS_HEALTH_REASON={"Reason":"OBSERVER_OR_PUBLISH_FAILED"}', flush=True)
        raise SystemExit(1)
