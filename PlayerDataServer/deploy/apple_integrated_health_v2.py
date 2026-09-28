"""Compose Apple health with bounded, consistent systemd observations (v2).

Deployment must supply hash-verified root-owned copies of retention_health and
its base_health dependency. This file does not install or activate anything.
"""
import json
import os
from pathlib import Path
import pwd
import stat
import subprocess
import sys
import time

if __name__ == '__main__':
    # Installation supplies a root-owned, hash-verified directory. Support -I.
    sys.path.insert(0, str(Path(__file__).resolve().parent))
from apple_revocation_health import healthy_runtime

REPORT = Path('/var/lib/witch-player-apple/status.json')


def properties(unit):
    result = subprocess.run(['/usr/bin/systemctl', 'show', unit, '--no-pager',
        '--property=LoadState,ActiveState,SubState,Result,ExecMainStatus,UnitFileState,ExecMainStartTimestampMonotonic'],
        capture_output=True, timeout=8, stdin=subprocess.DEVNULL,
        env={'PATH': '/usr/bin:/bin', 'LC_ALL': 'C'})
    if result.returncode: raise ValueError('Unit query failed')
    return dict(line.split('=', 1) for line in result.stdout.decode().splitlines() if '=' in line)


def private_marker(path, owner):
    try:
        if any(p.is_symlink() for p in (path, *path.parents)): return False
        for item, kind in ((path.parent, stat.S_ISDIR), (path, stat.S_ISREG)):
            info = item.lstat()
            if not kind(info.st_mode) or info.st_uid != owner or info.st_mode & 0o077:
                return False
        for parent in path.parent.parents:
            info = parent.stat()
            if info.st_uid not in (0, os.geteuid()) or info.st_mode & 0o022: return False
        return path.stat().st_size <= 4096
    except OSError:
        return False


def sampled_runtime(report, query, now=None, monotonic_ns=None, pause=time.sleep):
    """Reject mixed observations across a oneshot start/finish boundary.

Only retry bounded read-only sampling, never start a unit or manufacture a
healthy marker. Stable failure/disabled timer still fails immediately.
"""
    for attempt in range(3):
        service = query('witch-player-apple-revoke.service')
        timer = query('witch-player-apple-revoke.timer')
        healthy = healthy_runtime(report, service, timer, now, monotonic_ns)
        after = query('witch-player-apple-revoke.service')
        after_timer = query('witch-player-apple-revoke.timer')
        changed = service != after or timer != after_timer
        starting = (after.get('ActiveState') == 'activating' and after.get('SubState') == 'start'
                    and after.get('Result') == 'success' and after.get('ExecMainStatus') == '0'
                    and after_timer.get('ActiveState') == 'active'
                    and after_timer.get('UnitFileState') == 'enabled')
        if not changed and healthy: return True
        if not changed and not starting: return False
        if attempt < 2: pause(0.25)
    return False


def collect(existing, *, report=REPORT, owner=None, query=properties, now=None,
            monotonic_ns=None, on_reason=None):
    reasons = []
    value = dict(existing(on_reason=reasons.append))
    if value.get('Healthy') is True:
        try:
            uid = pwd.getpwnam('witchplayer').pw_uid if owner is None else owner
            if not private_marker(report, uid):
                value['Healthy'] = False
                reasons.append({'Reason': 'APPLE_MARKER_REJECTED'})
            else:
                value['Healthy'] = sampled_runtime(report, query, now, monotonic_ns)
                if not value['Healthy']: reasons.append({'Reason': 'APPLE_RUNTIME_REJECTED'})
        except Exception:
            value['Healthy'] = False
            reasons.append({'Reason': 'APPLE_QUERY_FAILED'})
    else:
        value['Healthy'] = False
    value['CheckedUnix'] = time.time() if now is None else now
    if on_reason: on_reason(reasons[-1] if reasons else {'Reason': 'HEALTHY'})
    return value


def main():
    if os.geteuid() != 0 or len(sys.argv) != 1: raise SystemExit(1)
    import base_health
    import retention_health
    reasons = []
    result = collect(lambda **kw: retention_health.collect(base_health, **kw), on_reason=reasons.append)
    base_health.publish(result)
    print('NASUS_HEALTH_REASON='+json.dumps(reasons[-1], sort_keys=True))


if __name__ == '__main__': main()
