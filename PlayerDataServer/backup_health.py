"""Read a sanitized root-published heartbeat, never backup credentials or DB data."""
import json
import math
import time
from pathlib import Path


def healthy_marker(path, now=None):
    try:
        with Path(path).open('rb') as stream:
            raw = stream.read(1025)
        if len(raw) > 1024:
            return False
        value = json.loads(raw)
        stamp = value['CheckedUnix']
        if type(stamp) not in (int, float) or not math.isfinite(stamp):
            return False
        age = (time.time() if now is None else now) - stamp
        return value['Healthy'] is True and 0 <= age <= 180
    except (OSError, ValueError, TypeError, KeyError):
        return False
