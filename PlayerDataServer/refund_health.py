"""Read-only monitor: a marker alone cannot prove the timer is still enabled."""
import json
import math
import subprocess
import time

from apple_revocation_runtime import read_private


def healthy_runtime(marker, instance, environment, service, timer, *, now=None):
    try:
        if (not isinstance(service, dict) or not isinstance(timer, dict)
                or service.get('LoadState') != 'loaded' or service.get('ActiveState') != 'inactive'
                or service.get('Result') != 'success' or service.get('ExecMainStatus') != '0'
                or timer.get('LoadState') != 'loaded' or timer.get('ActiveState') != 'active'
                or timer.get('UnitFileState') != 'enabled'):
            return False
        value = json.loads(read_private(marker, 8192))
        stamp = value['CheckedUnix']; current = time.time() if now is None else now
        counts = value['Counts']
        return (value.get('Status') == 'REFUND_BATCH_COMPLETED' and value.get('Healthy') is True
                and value.get('InstanceId') == instance and value.get('Environment') == environment
                and environment in ('Sandbox', 'Production')
                and all(type(n) in (int, float) and math.isfinite(n) for n in (stamp, current))
                and 0 <= current-stamp <= 240 and isinstance(counts, dict)
                and set(counts) == {'Checked','Changed','Failed','PendingFailures','Due','Unscheduled'}
                and all(type(n) is int and n >= 0 for n in counts.values())
                and all(counts[k] == 0 for k in ('Failed','PendingFailures','Due','Unscheduled'))
                and counts['Changed'] <= counts['Checked'])
    except (OSError, ValueError, TypeError, KeyError, AttributeError):
        return False


def check_systemd(marker, instance, environment):
    """Fetch fresh local unit properties. Never start, enable, or restart units."""
    def properties(unit):
        result = subprocess.run(['/usr/bin/systemctl', 'show', unit,
            '--property=LoadState,ActiveState,Result,ExecMainStatus,UnitFileState'],
            capture_output=True, text=True, timeout=5, check=True)
        return dict(line.split('=', 1) for line in result.stdout.splitlines() if '=' in line)
    try:
        return healthy_runtime(marker, instance, environment,
            properties('witch-player-refunds.service'), properties('witch-player-refunds.timer'))
    except (OSError, ValueError, subprocess.SubprocessError):
        return False
