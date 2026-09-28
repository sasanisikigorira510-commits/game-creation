"""Build an offline checkpoint from an independently pinned complete event set.

This proves equality to the supplied inventory, NOT that an operator has the
latest inventory. Never derive the trusted digest from downloaded files, an old
backup, or an unverified bucket listing. No cloud access or database mutation.
"""
import argparse
import datetime as dt
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import stat

from deletion_journal import validate_event
from deletion_restore import MAX_BYTES, MAX_RECORDS, _validate
from maintenance import write_private
from store import encode


def checkpoint_from_events(inventory_raw, expected_sha256, instance, events):
    if (not isinstance(inventory_raw, bytes) or len(inventory_raw) > MAX_BYTES
            or not isinstance(expected_sha256, str) or not re.fullmatch(r'[a-f0-9]{64}', expected_sha256)
            or not hmac.compare_digest(hashlib.sha256(inventory_raw).hexdigest(), expected_sha256)):
        raise ValueError('Independent inventory digest mismatch')
    inventory = json.loads(inventory_raw)
    if (not isinstance(inventory, dict) or set(inventory) != {'Version', 'InstanceId', 'CreatedUtc', 'Events'}
            or type(inventory['Version']) is not int or inventory['Version'] != 1
            or inventory['InstanceId'] != instance or not isinstance(instance, str)
            or not re.fullmatch(r'[A-Za-z0-9_-]{8,80}', instance)
            or (encode(inventory) + '\n').encode() != inventory_raw):
        raise ValueError('Invalid inventory schema or instance')
    created = dt.datetime.fromisoformat(inventory['CreatedUtc'])
    if created.utcoffset() is None: raise ValueError('Inventory date needs timezone')
    entries = inventory['Events']
    if not isinstance(entries, list) or len(entries) > MAX_RECORDS or not isinstance(events, dict):
        raise ValueError('Invalid event collection')
    ids = []
    for entry in entries:
        if (not isinstance(entry, dict) or set(entry) != {'EventId', 'Sha256', 'Bytes'}
                or not isinstance(entry['EventId'], str) or not re.fullmatch(r'[a-f0-9]{32}', entry['EventId'])
                or not isinstance(entry['Sha256'], str) or not re.fullmatch(r'[a-f0-9]{64}', entry['Sha256'])
                or type(entry['Bytes']) is not int or not 0 < entry['Bytes'] <= 1024 * 1024):
            raise ValueError('Invalid inventory entry')
        ids.append(entry['EventId'])
    if ids != sorted(set(ids)) or set(events) != set(ids):
        raise ValueError('Missing, extra or duplicate events')
    if sum(e['Bytes'] for e in entries) > MAX_BYTES:
        raise ValueError('Aggregate event limit exceeded')
    deleted, retired = {}, set()
    for entry in entries:
        payload = events[entry['EventId']]
        if (not isinstance(payload, bytes) or len(payload) != entry['Bytes']
                or not hmac.compare_digest(hashlib.sha256(payload).hexdigest(), entry['Sha256'])):
            raise ValueError('Event content mismatch')
        value = validate_event(payload, instance)
        if value['EventId'] != entry['EventId']:
            raise ValueError('Event identity mismatch')
        stamp = dt.datetime.fromisoformat(value['DeletedUtc'])
        if stamp > created: raise ValueError('Event newer than trusted inventory')
        previous = deleted.get(value['PlayerHash'])
        if previous is None or stamp < dt.datetime.fromisoformat(previous):
            deleted[value['PlayerHash']] = value['DeletedUtc']
        retired.update(value['RetiredPurchases'])
        if len(deleted) + len(retired) > MAX_RECORDS: raise ValueError('Checkpoint record limit exceeded')
    checkpoint = dict(Version=1, InstanceId=instance, CreatedUtc=inventory['CreatedUtc'],
                      DeletedPlayers=[{'Hash': key, 'DeletedUtc': deleted[key]} for key in sorted(deleted)],
                      RetiredPurchases=sorted(retired))
    _validate(checkpoint, instance)
    result = (encode(checkpoint) + '\n').encode()
    if len(result) > MAX_BYTES: raise ValueError('Checkpoint size limit exceeded')
    return result


def write_event_checkpoint(inventory_raw, expected_sha256, instance, events, output):
    raw = checkpoint_from_events(inventory_raw, expected_sha256, instance, events)
    write_private(output, raw.decode())
    return hashlib.sha256(raw).hexdigest()


def private_path(path, directory=False):
    path = Path(path).absolute()
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Symlink in recovery path')
    info = path.stat()
    if (info.st_uid != os.geteuid() or info.st_mode & 0o077
            or not (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode))):
        raise ValueError('Recovery inputs must be private and owned by the operator')
    return path


def bounded_read(path, limit):
    path = private_path(path)
    with path.open('rb') as stream: raw = stream.read(limit + 1)
    if len(raw) > limit: raise ValueError('Recovery input exceeds size limit')
    return raw


def checkpoint_from_directory(inventory, expected_sha256, instance, events_dir, output):
    """Offline only. Plaintext event files must be private <event-id>.json files."""
    raw = bounded_read(inventory, MAX_BYTES)
    folder = private_path(events_dir, directory=True)
    destination = Path(output).absolute()
    private_path(destination.parent, directory=True)
    if destination.exists() or destination.is_symlink():
        raise ValueError('Never overwrite an existing recovery checkpoint')
    events, total = {}, 0
    for path in folder.iterdir():
        if not re.fullmatch(r'[a-f0-9]{32}\.json', path.name) or len(events) >= MAX_RECORDS:
            raise ValueError('Unexpected recovery event file')
        payload = bounded_read(path, min(1024 * 1024, MAX_BYTES - total))
        events[path.stem] = payload; total += len(payload)
    digest = write_event_checkpoint(raw, expected_sha256, instance, events, destination)
    return {'Status': 'OFFLINE_EVENT_CHECKPOINT_VERIFIED', 'Sha256': digest,
            'Events': len(events), 'LatestCompletenessProven': False, 'ProductionDataChanged': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', required=True)
    parser.add_argument('--expected-sha256', required=True)
    parser.add_argument('--instance-id', required=True)
    parser.add_argument('--events-dir', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    os.umask(0o077)
    print(json.dumps(checkpoint_from_directory(args.inventory, args.expected_sha256,
          args.instance_id, args.events_dir, args.output), sort_keys=True))


if __name__ == '__main__':
    try: main()
    except Exception:
        print('OFFLINE_EVENT_CHECKPOINT_STOPPED: input retained; no private details printed.')
        raise SystemExit(1)
