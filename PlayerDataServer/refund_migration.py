"""Offline refund schema rehearsal on a NEW quarantined backup copy only.

Default is read-only preflight. No in-place/live migration or promotion exists.
The independently retained backup digest is required, not inferred from a file.
"""
import argparse
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3

from apple_revocation_runtime import private, read_private, unique
from deletion_restore import _schema
from maintenance import digest_file, restore_copy, sync_directory, verify_generation, write_private
from purchase_refunds import initialize
from refund_integrity import REFUND_COLUMNS, validate_refunds
from store import encode


def inspect_backup(source, expected_sha256, instance):
    if (not isinstance(expected_sha256, str) or not re.fullmatch('[a-f0-9]{64}', expected_sha256)
            or not isinstance(instance, str) or not re.fullmatch('[A-Za-z0-9_-]{8,80}', instance)):
        raise ValueError('Explicit backup digest and instance required')
    source = private(source)
    # Require a frozen backup generation, not a live DB/WAL pair.
    if (source.parent/'instance-id').exists() or (source.parent/'instance-id').is_symlink():
        raise ValueError('Do not pass a live data directory')
    for suffix in ('-wal', '-shm', '-journal'):
        side = Path(str(source) + suffix)
        if side.exists() or side.is_symlink():
            raise ValueError('Use a completed standalone backup, not a live SQLite database')
    manifest = json.loads(read_private(str(source) + '.json', 8192), object_pairs_hook=unique)
    if not isinstance(manifest, dict) or manifest.get('InstanceId') != instance:
        raise ValueError('Backup instance does not match')
    if digest_file(source) != expected_sha256:
        raise ValueError('Backup does not match the independently retained digest')
    verify_generation(source)
    with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as db:
        db.execute('PRAGMA query_only=ON')
        tables = _schema(db)
        present = validate_refunds(db)
    return source, present, tables


def fingerprint(db, tables):
    """Bounded-memory logical comparison, including auth/BLOB values, never logged."""
    digest = hashlib.sha256()
    for table in sorted(tables):
        # Names come exclusively from the reviewed _schema allowlist.
        columns = list(db.execute('PRAGMA table_info(' + table + ')'))
        for row in db.execute('SELECT * FROM ' + table + ' ORDER BY ' +
                              ','.join(str(n) for n in range(1, len(columns)+1))):
            raw = repr((table, tuple(row))).encode('utf-8')
            digest.update(len(raw).to_bytes(8, 'big')); digest.update(raw)
    return digest.hexdigest()


def prepare(source, expected_sha256, instance, destination, *, create=False):
    source, present, tables = inspect_backup(source, expected_sha256, instance)
    destination = Path(destination)
    if not destination.is_absolute() or destination.name in ('', '.', '..'):
        raise ValueError('Explicit absolute new destination required')
    private(destination.parent, True)
    if destination.exists() or destination.is_symlink():
        raise ValueError('Destination must not exist; never overwrite a candidate')
    result = dict(Status='REFUND_MIGRATION_PREFLIGHT_PASSED', SourceSha256=expected_sha256,
                  SourceInstance=instance, ExistingRefundLedger=present,
                  SourceChanged=False, Activated=False, NetworkRequests=0,
                  ReadyForDeployment=False)
    if not create:
        return result
    # restore_copy creates the quarantine marker before any database file.
    # Failed artifacts deliberately remain quarantined for investigation.
    restore_copy(source, destination)
    target = destination/'players.sqlite'
    # Revalidate copied logical contents; backup() can change SQLite file bytes.
    with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as original, \
         closing(sqlite3.connect(target)) as db:
        original.execute('PRAGMA query_only=ON')
        _schema(original); _schema(db)
        before = fingerprint(original, tables)
        if fingerprint(db, tables) != before or digest_file(source) != expected_sha256:
            raise ValueError('Source changed during rehearsal')
        db.execute('PRAGMA foreign_keys=ON'); db.execute('PRAGMA synchronous=FULL')
        if not present:
            initialize(db)
        preserved = tables - {'refund_checks'}
        preserved_before = fingerprint(original, preserved)
        # Never retain a success/lease from the old runtime as migration evidence.
        with db:
            db.execute('UPDATE refund_checks SET due=0,attempts=0,last_success=NULL,'
                       "last_error='migration_recheck_required',lease=NULL,lease_until=0")
            db.execute('INSERT OR IGNORE INTO refund_checks(transaction_id,player,due,last_error) '
                       "SELECT transaction_id,player,0,'migration_recheck_required' FROM purchases WHERE store='apple'")
        _schema(db)
        if (not validate_refunds(db) or fingerprint(db, preserved) != preserved_before
                or db.execute('PRAGMA integrity_check').fetchall() != [('ok',)]
                or db.execute('PRAGMA foreign_key_check').fetchone()):
            raise ValueError('Candidate validation failed; leave quarantined')
        result['PurchasesRequiringRecheck'] = db.execute('SELECT count(*) FROM refund_checks').fetchone()[0]
    if digest_file(source) != expected_sha256:
        raise ValueError('Source changed; candidate must not be used')
    if not (destination/'RECOVERY-PENDING.txt').is_file() or (destination/'instance-id').exists():
        raise ValueError('Candidate quarantine missing')
    with target.open('rb') as stream: os.fsync(stream.fileno())
    sync_directory(destination)
    result.update(Status='REFUND_CANDIDATE_QUARANTINED', CandidateSha256=digest_file(target),
                  PreservedHistory=True, PostBackupReconciliationRequired=True,
                  DeletionReconciliationRequired=True)
    write_private(destination/'refund-migration.json', encode(result) + '\n')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--backup', required=True)
    parser.add_argument('--expected-sha256', required=True)
    parser.add_argument('--instance', required=True)
    parser.add_argument('--new-directory', required=True)
    parser.add_argument('--prepare', action='store_true', help='Create a new quarantined rehearsal copy only')
    args = parser.parse_args(); os.umask(0o077)
    try:
        result = prepare(args.backup, args.expected_sha256, args.instance, args.new_directory, create=args.prepare)
        print(encode(result)); return 0
    except Exception:
        print('REFUND_MIGRATION_STOPPED: source unchanged by this tool; keep any candidate quarantined.')
        return 1


if __name__ == '__main__': raise SystemExit(main())
