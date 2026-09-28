"""Monitor adapter; caller must obtain fresh systemd properties, not cache them.

Not wired into production health yet. Missing/failed units cannot be hidden by
an otherwise fresh success marker. Read this private marker as its owner/root.
"""
from backup_health import healthy_marker
import json
import math
from pathlib import Path
import time


def healthy_runtime(marker, service, timer, now=None, monotonic_ns=None):
    if not isinstance(service, dict) or not isinstance(timer, dict):
        return False
    units_ok = (service.get('LoadState') == 'loaded'
            and service.get('Result') == 'success'
            and service.get('ExecMainStatus') == '0'
            and timer.get('LoadState') == 'loaded'
            and timer.get('ActiveState') == 'active'
            and timer.get('UnitFileState') == 'enabled')
    if not units_ok: return False
    if service.get('ActiveState') == 'inactive':
        return healthy_marker(marker, now)
    if service.get('ActiveState') != 'activating' or service.get('SubState') != 'start':
        return False
    # A bounded in-progress run is not a success. Use a recent *completed*
    # success, but only while systemd confirms the same current oneshot run.
    try:
        with Path(marker).open('rb') as stream: raw = stream.read(4097)
        if len(raw) > 4096: return False
        value = json.loads(raw)
        current = time.time() if now is None else now
        mono = time.monotonic_ns() if monotonic_ns is None else monotonic_ns
        started = value['CheckedUnix']; previous = value['PreviousSuccessUnix']
        mark_ns = value['StartedMonotonicNs']
        unit_ns = int(service['ExecMainStartTimestampMonotonic']) * 1000
        return (value.get('Status') == 'APPLE_WORKER_RUNNING' and value.get('Healthy') is False
                and all(type(n) in (int, float) and math.isfinite(n)
                        for n in (current, started, previous))
                and 0 <= current-previous <= 180 and previous <= started <= current
                and current-started < 90 and type(mark_ns) is int
                and 0 < unit_ns <= mark_ns <= mono and mono-unit_ns < 90_000_000_000)
    except (OSError, ValueError, TypeError, KeyError, AttributeError, OverflowError):
        return False
