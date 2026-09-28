"""Add retention health to the unchanged, pinned existing health collector."""
import json
import os
from pathlib import Path
import sys
import time

REPORT = Path('/var/lib/witch-player-retention/status.json')


def marker_healthy(path=REPORT, now=None):
    try:
        info = path.lstat()
        if path.is_symlink() or not path.is_file() or info.st_uid != os.geteuid() or info.st_mode & 0o077:
            return False
        with path.open('rb') as stream: raw = stream.read(4097)
        value = json.loads(raw)
        stamp = value.get('CheckedUnix')
        current = time.time() if now is None else now
        return (len(raw) <= 4096 and value.get('Healthy') is True
                and value.get('Status') == 'RETENTION_COMPLETE' and value.get('Execute') is True
                and value.get('RetentionDays') == 30 and type(stamp) in (int, float)
                and 0 <= current-stamp <= 7200)
    except (OSError, ValueError, TypeError, AttributeError): return False


def collect(base, *, report=REPORT, now=None, on_reason=None):
    reasons = []
    value = base.collect(require_deletion_worker=True, on_reason=reasons.append)
    if value['Healthy']:
        try:
            timer = base.unit_properties('witch-player-retention.timer')
            service = base.unit_properties('witch-player-retention.service')
            healthy = (timer.get('LoadState') == 'loaded' and timer.get('ActiveState') == 'active'
                and service.get('LoadState') == 'loaded' and service.get('ActiveState') != 'failed'
                and service.get('Result') == 'success' and service.get('ExecMainStatus') == '0')
            if not healthy: reasons.append({'Reason': 'RETENTION_UNIT_REJECTED'})
            elif not marker_healthy(report, now):
                healthy = False; reasons.append({'Reason': 'RETENTION_MARKER_REJECTED'})
            value['Healthy'] = bool(healthy)
        except Exception:
            value['Healthy'] = False; reasons.append({'Reason': 'RETENTION_QUERY_FAILED'})
    if on_reason: on_reason(reasons[-1] if reasons else {'Reason': 'HEALTHY'})
    return value


if __name__ == '__main__':
    if os.geteuid() != 0 or len(sys.argv) != 1: raise SystemExit(1)
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import base_health
    reasons = []
    base_health.publish(collect(base_health, on_reason=reasons.append))
    print('NASUS_HEALTH_REASON='+json.dumps(reasons[-1], sort_keys=True), flush=True)
