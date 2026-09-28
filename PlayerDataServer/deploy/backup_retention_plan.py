"""Offline, non-destructive 30-day review of an authenticated S3 inventory.

No network, credential access, deletion or service configuration. An input flag
is NOT proof that inventory acquisition was complete; retain acquisition evidence
and independently review it before any future deletion implementation.
"""
import argparse
import datetime as dt
import json
from pathlib import Path
import re

BUCKET = 'nasus-player-backup-4433119'
KEY = re.compile(r'db/(\d{8}T\d{6}Z)-[a-f0-9]{32}\.tar\.gz\.age')
RETENTION_DAYS = 30
MAX_OBJECTS = 100000
MAX_INPUT_BYTES = 32 * 1024 * 1024
UTC = dt.timezone.utc


def require(value):
    if not value: raise ValueError('Invalid retention review input')


def timestamp(value):
    require(isinstance(value, str))
    result = dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(result.utcoffset() is not None)
    return result.astimezone(UTC)


def plan(snapshot, *, now):
    require(now.utcoffset() is not None)
    now = now.astimezone(UTC)
    require(isinstance(snapshot, dict) and snapshot.get('Bucket') == BUCKET
            and snapshot.get('Prefix') == 'db/' and snapshot.get('Complete') is True)
    objects = snapshot.get('Objects')
    require(isinstance(objects, list) and len(objects) <= MAX_OBJECTS)
    captured = timestamp(snapshot.get('CapturedUtc'))
    require(dt.timedelta(0) <= now - captured <= dt.timedelta(minutes=10))
    blockers = []
    # Suspended still has old versions. Never infer deletion semantics from a
    # latest-object list when versioning history has not been inspected.
    if snapshot.get('Versioning') != 'NeverEnabled':
        blockers.append('VERSION_HISTORY_REVIEW_REQUIRED')
    cutoff = now - dt.timedelta(days=RETENTION_DAYS)
    seen, candidates, retained, by_key = set(), [], [], {}
    for obj in objects:
        require(isinstance(obj, dict))
        key = obj.get('Key'); match = KEY.fullmatch(key) if isinstance(key, str) else None
        require(match is not None and key not in seen)
        seen.add(key)
        size = obj.get('Size')
        require(type(size) is int and 0 < size <= 64 * 1024 * 1024)
        modified = timestamp(obj.get('LastModified'))
        encoded = dt.datetime.strptime(match.group(1), '%Y%m%dT%H%M%SZ').replace(tzinfo=UTC)
        require(modified <= captured and encoded <= captured)
        by_key[key] = (obj, modified)
        # Both provider timestamp and generated filename must be older than
        # 30 x 24 hours. Boundary equality remains retained for review safety.
        target = candidates if max(modified, encoded) < cutoff else retained
        target.append({'Key': key, 'Bytes': size, 'LastModified': modified.isoformat()})
    receipt = snapshot.get('LastVerifiedBackup')
    recent_verified = False
    if isinstance(receipt, dict) and receipt.get('Status') == 'OFFSITE_CIPHERTEXT_VERIFIED':
        key = receipt.get('Key')
        if isinstance(key, str) and key in by_key:
            obj, modified = by_key[key]
            try:
                completed = timestamp(receipt.get('CompletedUtc'))
                recent_verified = (dt.timedelta(0) <= now-completed <= dt.timedelta(hours=2)
                    and modified <= completed and modified >= cutoff
                    and type(receipt.get('Bytes')) is int and receipt['Bytes'] == obj['Size']
                    and isinstance(receipt.get('Sha256'), str)
                    and re.fullmatch(r'[a-f0-9]{64}', receipt['Sha256']) is not None)
            except (ValueError, TypeError):
                pass
    if not recent_verified: blockers.append('RECENT_VERIFIED_BACKUP_REQUIRED')
    return dict(Status='RETENTION_REVIEW_BLOCKED' if blockers else 'RETENTION_REVIEW_READY',
        Bucket=BUCKET, Prefix='db/', RetentionDays=RETENTION_DAYS,
        CutoffUtc=cutoff.isoformat(), InventoryCapturedUtc=captured.isoformat(),
        CandidateCount=len(candidates), CandidateBytes=sum(x['Bytes'] for x in candidates),
        Candidates=sorted(candidates, key=lambda x: x['Key']), RetainedCount=len(retained),
        Blockers=blockers, DeleteAuthorized=False, ObjectsDeleted=0,
        InventoryCompletenessIndependentlyProven=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', required=True)
    args = parser.parse_args()
    with Path(args.inventory).open('rb') as stream: raw = stream.read(MAX_INPUT_BYTES + 1)
    require(len(raw) <= MAX_INPUT_BYTES)
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result); result[key] = value
        return result
    report = plan(json.loads(raw, object_pairs_hook=unique), now=dt.datetime.now(UTC))
    print(json.dumps(report, sort_keys=True))


if __name__ == '__main__':
    try: main()
    except Exception:
        print('RETENTION_REVIEW_STOPPED: no changes made.')
        raise SystemExit(1)
