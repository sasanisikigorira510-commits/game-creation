"""Build a bounded expected event inventory from one committed DB snapshot.

No cloud I/O, schema initialization, delivery acknowledgement, or promotion.
All outbox states are included: pending delivery must not silently disappear
from recovery expectations. Monotonic checks require an independently retained
previous inventory digest. Neither a current snapshot nor this digest proves
that the caller has the latest externally committed inventory after DB loss.
"""
import argparse
from contextlib import closing
import datetime as dt
import hashlib
import hmac
import json
import math
import os
from pathlib import Path
import re
import sqlite3

from deletion_event_restore import bounded_read, private_path
from deletion_journal import validate_event, MAX_PAYLOAD, MAX_CIPHERTEXT
from deletion_restore import MAX_BYTES, MAX_RECORDS, _schema, _validate
from maintenance import write_private
from store import encode

HEX = re.compile(r'[a-f0-9]{64}')
INSTANCE = re.compile(r'[A-Za-z0-9_-]{8,80}')


def require(ok):
    if not ok: raise ValueError('Deletion inventory validation failed')


def stamp(value):
    require(isinstance(value, str))
    parsed = dt.datetime.fromisoformat(value)
    require(parsed.utcoffset() is not None)
    return parsed


def parse_inventory(raw, instance):
    require(isinstance(raw, bytes) and 0 < len(raw) <= MAX_BYTES)
    value = json.loads(raw)
    require(isinstance(value, dict) and set(value) == {'Version', 'InstanceId', 'CreatedUtc', 'Events'})
    require(type(value['Version']) is int and value['Version'] == 1
            and isinstance(instance, str) and INSTANCE.fullmatch(instance)
            and value['InstanceId'] == instance and (encode(value)+'\n').encode() == raw)
    stamp(value['CreatedUtc'])
    rows = value['Events']
    require(isinstance(rows, list) and len(rows) <= MAX_RECORDS)
    ids, total = [], 0
    for row in rows:
        require(isinstance(row, dict) and set(row) == {'EventId', 'Sha256', 'Bytes'})
        require(isinstance(row['EventId'], str) and re.fullmatch(r'[a-f0-9]{32}', row['EventId'])
                and isinstance(row['Sha256'], str) and HEX.fullmatch(row['Sha256'])
                and type(row['Bytes']) is int and 0 < row['Bytes'] <= MAX_PAYLOAD)
        ids.append(row['EventId']); total += row['Bytes']
    require(ids == sorted(set(ids)) and total <= MAX_BYTES)
    return value


def monotonic_inventory(current_raw, previous_raw, expected_previous_sha256, instance):
    """Reject lost/changed events and clock rollback against a trusted anchor."""
    require(isinstance(expected_previous_sha256, str) and HEX.fullmatch(expected_previous_sha256))
    require(isinstance(previous_raw, bytes) and len(previous_raw) <= MAX_BYTES
            and hmac.compare_digest(hashlib.sha256(previous_raw).hexdigest(), expected_previous_sha256))
    previous = parse_inventory(previous_raw, instance)
    current = parse_inventory(current_raw, instance)
    require(stamp(current['CreatedUtc']) >= stamp(previous['CreatedUtc']))
    by_id = {row['EventId']: row for row in current['Events']}
    require(all(by_id.get(row['EventId']) == row for row in previous['Events']))
    return {'PreviousEvents': len(previous['Events']), 'CurrentEvents': len(current['Events']),
            'AddedEvents': len(current['Events'])-len(previous['Events'])}


def inventory_from_connection(db, instance, captured):
    """Caller must own a query-only read transaction. Never expose row payloads."""
    require(db.in_transaction and db.execute('PRAGMA query_only').fetchone()[0] == 1)
    require(isinstance(instance, str) and INSTANCE.fullmatch(instance))
    created = stamp(captured)
    tables = _schema(db)
    require({'deleted_players', 'retired_purchases', 'deletion_outbox', 'deletion_journal_meta'} <= tables)
    require(db.execute('SELECT id,instance FROM deletion_journal_meta').fetchall() == [(1, instance)])
    require(db.execute('PRAGMA quick_check').fetchall() == [('ok',)]
            and db.execute('PRAGMA foreign_key_check').fetchone() is None)
    count, total, largest = db.execute(
        'SELECT count(*),coalesce(sum(length(CAST(payload AS BLOB))),0),'
        'coalesce(max(length(CAST(payload AS BLOB))),0) FROM deletion_outbox').fetchone()
    require(count <= MAX_RECORDS and total <= MAX_BYTES and largest <= MAX_PAYLOAD)
    entries, deleted, retired = [], {}, set()
    states = dict(Pending=0, Sending=0, Verified=0)
    for event_id, payload, state, verified_at, cipher_size, cipher_header in db.execute(
            'SELECT event_id,payload,state,verified_at,length(ciphertext),substr(ciphertext,1,22) '
            'FROM deletion_outbox ORDER BY event_id'):
        require(isinstance(payload, str) and state in ('pending', 'sending', 'verified'))
        raw = payload.encode(); event = validate_event(raw, instance)
        require(event['EventId'] == event_id and stamp(event['DeletedUtc']) <= created)
        if state == 'verified':
            require(type(verified_at) in (int, float) and math.isfinite(verified_at)
                    and stamp(event['DeletedUtc']).timestamp() <= verified_at <= created.timestamp())
        else: require(verified_at is None)
        if state in ('sending', 'verified') or cipher_size is not None:
            require(type(cipher_size) is int and 64 <= cipher_size <= MAX_CIPHERTEXT
                    and cipher_header == b'age-encryption.org/v1\n'[:22])
        require(event['PlayerHash'] not in deleted)
        deleted[event['PlayerHash']] = event['DeletedUtc']
        retired.update(event['RetiredPurchases'])
        require(len(deleted)+len(retired) <= MAX_RECORDS)
        entries.append(dict(EventId=event_id, Bytes=len(raw), Sha256=hashlib.sha256(raw).hexdigest()))
        states[state.capitalize()] += 1
    # A valid outbox subset is insufficient: account and purchase tombstones
    # must match the union of ALL events in the same committed snapshot.
    tombstones = db.execute('SELECT player_hash,deleted_at FROM deleted_players ORDER BY player_hash LIMIT ?',
                            (MAX_RECORDS+1,)).fetchall()
    purchases = db.execute('SELECT transaction_hash FROM retired_purchases ORDER BY transaction_hash LIMIT ?',
                           (MAX_RECORDS+1,)).fetchall()
    require(tombstones == sorted(deleted.items()) and purchases == [(h,) for h in sorted(retired)])
    _validate(dict(Version=1, InstanceId=instance, CreatedUtc=captured,
        DeletedPlayers=[dict(Hash=h, DeletedUtc=deleted[h]) for h in sorted(deleted)],
        RetiredPurchases=sorted(retired)), instance)
    result = (encode(dict(Version=1, InstanceId=instance, CreatedUtc=captured, Events=entries))+'\n').encode()
    parse_inventory(result, instance)
    return result, states


def inspect_inventory(data_directory, instance, *, clock=lambda: dt.datetime.now(dt.timezone.utc)):
    directory = private_path(data_directory, directory=True)
    require(isinstance(instance, str) and INSTANCE.fullmatch(instance))
    marker = bounded_read(directory/'instance-id', 128).decode().strip()
    require(marker == instance)
    quarantine = directory/'RECOVERY-PENDING.txt'
    require(not quarantine.exists() and not quarantine.is_symlink())
    source = private_path(directory/'players.sqlite')
    with closing(sqlite3.connect(source.as_uri()+'?mode=ro', uri=True, timeout=5)) as db:
        db.execute('PRAGMA query_only=ON'); db.execute('PRAGMA trusted_schema=OFF')
        remaining = [50000]
        def progress():
            remaining[0] -= 1
            return int(remaining[0] <= 0)
        db.set_progress_handler(progress, 1000)
        db.execute('BEGIN')
        # Establish snapshot before assigning its capture time. Writes after
        # this read are deliberately not claimed to be part of this snapshot.
        db.execute('SELECT count(*) FROM deletion_journal_meta').fetchone()
        captured = clock().isoformat()
        return inventory_from_connection(db, instance, captured)


def export_inventory(data_directory, instance, output, *, previous=None,
                     expected_previous_sha256=None, bootstrap_empty=False, clock=None):
    destination = Path(output).absolute()
    private_path(destination.parent, directory=True)
    require(not destination.exists() and not destination.is_symlink())
    # Never establish a nonempty baseline without a separately reviewed anchor.
    require(type(bootstrap_empty) is bool)
    if bootstrap_empty:
        require(previous is None and expected_previous_sha256 is None)
    else:
        require(previous is not None and expected_previous_sha256 is not None)
    raw, states = inspect_inventory(data_directory, instance, **({'clock': clock} if clock else {}))
    current = parse_inventory(raw, instance)
    if bootstrap_empty:
        require(not current['Events'])
        changes = dict(PreviousEvents=0, CurrentEvents=0, AddedEvents=0)
    else:
        previous_raw = bounded_read(previous, MAX_BYTES)
        changes = monotonic_inventory(raw, previous_raw, expected_previous_sha256, instance)
    write_private(destination, raw.decode())
    return dict(Status='DB_DELETION_INVENTORY_EXPORTED', Sha256=hashlib.sha256(raw).hexdigest(),
                **changes, **states, DatabaseChanged=False, ExternallyStored=False,
                LatestCompletenessProven=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--data-dir', required=True)
    parser.add_argument('--instance-id', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--previous')
    parser.add_argument('--expected-previous-sha256')
    parser.add_argument('--bootstrap-empty', action='store_true')
    args = parser.parse_args(); os.umask(0o077)
    result = export_inventory(args.data_dir, args.instance_id, args.output,
        previous=args.previous, expected_previous_sha256=args.expected_previous_sha256,
        bootstrap_empty=args.bootstrap_empty)
    print(json.dumps(result, sort_keys=True))


if __name__ == '__main__':
    try: main()
    except Exception:
        print('DELETION_INVENTORY_STOPPED: source unchanged; no event or credential details printed.')
        raise SystemExit(1)
