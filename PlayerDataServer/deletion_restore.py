"""Offline deletion reconciliation. Not a durable deletion journal or promotion tool.

The expected checkpoint digest must come from an independently retained, latest
operator record, NOT the old backup or the checkpoint file being checked. This
tool cannot prove freshness when that trusted record is unavailable. Every output
remains quarantined; no live database, Apple endpoint or cloud storage is changed.
"""
import argparse
import datetime as dt
import hashlib
import hmac
import json
import re
import sqlite3
from contextlib import closing
from pathlib import Path

from maintenance import restore_copy, sync_directory, verify_database, write_private
from store import Store, encode, now
from refund_integrity import REFUND_COLUMNS, validate_refunds

MAX_BYTES = 16 * 1024 * 1024
MAX_RECORDS = 100000
HEX = re.compile(r'[a-f0-9]{64}')
INSTANCE = re.compile(r'[A-Za-z0-9_-]{8,80}')
BASE = {
    'players': 'id token_hash created free paid economy_revision epoch frozen migration_required tutorial_pulls recovery',
    'snapshots': 'id player revision epoch received data hash source',
    'operations': 'player request_id request_hash revision kind received response',
    'claims': 'player claim_key', 'purchases': 'store transaction_id player product received',
    'audit': 'id player actor action reason received detail', 'flags': 'id player code received detail',
}
OPTIONAL = {
    'deletion_journal_meta': 'id instance',
    'deletion_outbox': 'event_id proof_hash payload ciphertext state lease lease_until attempts next_attempt verified_at last_error',
    'deleted_players': 'player_hash deleted_at', 'retired_purchases': 'transaction_hash',
    'deletion_confirmations': 'confirmation_hash player_hash credential_hash fingerprint expires',
    'deletion_receipts': 'proof_hash expires result',
    'apple_links': 'subject_hash player created',
    'apple_challenges': 'id nonce mode player credential_hash expires result fingerprint',
    'apple_exchanges': 'challenge subject_hash generation state grant_id created',
    'apple_unlinks': 'id player credential_hash subject_hash expires result',
    'apple_grants': 'id subject_hash client_id encrypted_token created state attempts due lease last_error',
    'apple_grant_generations': 'subject_hash client_id generation',
}
APPLE = {name for name in OPTIONAL if name.startswith('apple_')}
OPTIONAL.update(REFUND_COLUMNS)


def _regular(path):
    path = Path(path).absolute()
    if any(p.is_symlink() for p in (path, *path.parents)) or not path.is_file():
        raise ValueError('A regular file without symlink components is required')
    return path


def _schema(db):
    db.execute('PRAGMA trusted_schema=OFF')
    objects = db.execute("SELECT type,name FROM sqlite_master WHERE name NOT GLOB 'sqlite_*'").fetchall()
    if any(row[0] not in ('table', 'index') for row in objects):
        raise ValueError('Unreviewed triggers or views in recovery database')
    tables = {row[1] for row in objects if row[0] == 'table'}
    if not set(BASE) <= tables or tables - (set(BASE) | set(OPTIONAL)):
        raise ValueError('Unreviewed recovery schema; do not silently leave account data behind')
    if tables & APPLE and not APPLE <= tables:
        raise ValueError('Incomplete Apple recovery schema requires manual review')
    journal_tables = {'deletion_journal_meta', 'deletion_outbox'}
    if tables & journal_tables and not journal_tables <= tables:
        raise ValueError('Incomplete deletion journal schema')
    for table in tables:
        columns = {r[1] for r in db.execute('PRAGMA table_info(' + table + ')')}
        if columns != set((BASE | OPTIONAL)[table].split()):
            raise ValueError('Unreviewed recovery columns')
    validate_refunds(db)
    return tables


def _validate(data, instance):
    if not isinstance(instance, str) or not INSTANCE.fullmatch(instance):
        raise ValueError('An explicit instance identity is required')
    if (not isinstance(data, dict) or set(data) !=
            {'Version', 'InstanceId', 'CreatedUtc', 'DeletedPlayers', 'RetiredPurchases'}
            or type(data['Version']) is not int or data['Version'] != 1 or data['InstanceId'] != instance):
        raise ValueError('Deletion checkpoint version or instance mismatch')
    try:
        created = dt.datetime.fromisoformat(data['CreatedUtc'])
        if created.tzinfo is None:
            raise ValueError()
    except (TypeError, ValueError):
        raise ValueError('Invalid checkpoint date') from None
    deleted, retired = data['DeletedPlayers'], data['RetiredPurchases']
    if not isinstance(deleted, list) or not isinstance(retired, list) or len(deleted) + len(retired) > MAX_RECORDS:
        raise ValueError('Invalid checkpoint record count')
    hashes = []
    for entry in deleted:
        if (not isinstance(entry, dict) or set(entry) != {'Hash', 'DeletedUtc'}
                or not isinstance(entry['Hash'], str) or not HEX.fullmatch(entry['Hash'])
                or not isinstance(entry['DeletedUtc'], str) or len(entry['DeletedUtc']) > 80):
            raise ValueError('Invalid deletion record')
        try:
            if dt.datetime.fromisoformat(entry['DeletedUtc']).tzinfo is None:
                raise ValueError()
        except ValueError:
            raise ValueError('Invalid deletion date') from None
        hashes.append(entry['Hash'])
    if any(not isinstance(h, str) or not HEX.fullmatch(h) for h in retired):
        raise ValueError('Invalid retired purchase hash')
    if hashes != sorted(set(hashes)) or retired != sorted(set(retired)):
        raise ValueError('Checkpoint records must be unique and sorted')
    return data


def export_checkpoint(data_directory, output, instance):
    """Read one committed snapshot; never initialize or alter the source DB."""
    directory = Path(data_directory).absolute()
    marker = _regular(directory / 'instance-id')
    if marker.read_text().strip() != instance:
        raise ValueError('Source instance identity mismatch')
    if (directory / 'RECOVERY-PENDING.txt').exists() or (directory / 'RECOVERY-PENDING.txt').is_symlink():
        raise ValueError('A recovery candidate cannot supply the authoritative deletion checkpoint')
    source = _regular(directory / 'players.sqlite')
    output = Path(output).absolute()
    if any(p.is_symlink() for p in (output, *output.parents)):
        raise ValueError('Checkpoint destination must not traverse symlinks')
    with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as db:
        db.execute('BEGIN')
        tables = _schema(db)
        if not {'deleted_players', 'retired_purchases'} <= tables:
            raise ValueError('Source lacks deletion history; absence is not an empty deletion history')
        if db.execute('PRAGMA integrity_check').fetchall() != [('ok',)] or db.execute('PRAGMA foreign_key_check').fetchone():
            raise ValueError('Invalid checkpoint source database')
        deleted = [{'Hash': r[0], 'DeletedUtc': r[1]} for r in db.execute(
            'SELECT player_hash,deleted_at FROM deleted_players ORDER BY player_hash LIMIT ?', (MAX_RECORDS + 1,))]
        retired = [r[0] for r in db.execute(
            'SELECT transaction_hash FROM retired_purchases ORDER BY transaction_hash LIMIT ?', (MAX_RECORDS + 1,))]
        data = _validate(dict(Version=1, InstanceId=instance, CreatedUtc=now(),
                              DeletedPlayers=deleted, RetiredPurchases=retired), instance)
    payload = encode(data) + '\n'
    if len(payload.encode()) > MAX_BYTES:
        raise ValueError('Checkpoint exceeds the reviewed size limit')
    write_private(output, payload)
    return hashlib.sha256(payload.encode()).hexdigest()


def load_checkpoint(path, expected_sha256, instance):
    if not isinstance(expected_sha256, str) or not HEX.fullmatch(expected_sha256):
        raise ValueError('An independently retained latest checkpoint digest is required')
    path = _regular(path)
    with path.open('rb') as stream:
        payload = stream.read(MAX_BYTES + 1)
    if len(payload) > MAX_BYTES or not hmac.compare_digest(hashlib.sha256(payload).hexdigest(), expected_sha256):
        raise ValueError('Deletion checkpoint does not match the trusted digest')
    data = json.loads(payload)
    # Reject duplicate keys, alternative encodings and ambiguous parsing.
    if (encode(data) + '\n').encode() != payload:
        raise ValueError('Deletion checkpoint must use canonical encoding')
    return _validate(data, instance)


def _scrub_apple(db, players):
    subjects = set()
    for player in players:
        subjects.update(r[0] for r in db.execute(
            'SELECT subject_hash FROM apple_links WHERE player=? '
            'UNION SELECT subject_hash FROM apple_unlinks WHERE player=? '
            'UNION SELECT e.subject_hash FROM apple_exchanges e JOIN apple_challenges c '
            'ON c.id=e.challenge WHERE c.player=?', (player, player, player)))
    queued = 0
    for subject in subjects:
        owner = db.execute('SELECT player FROM apple_links WHERE subject_hash=?', (subject,)).fetchone()
        if owner and owner[0] not in players:
            continue  # An old unlink request cannot revoke another account.
        clients = {r[0] for r in db.execute(
            'SELECT client_id FROM apple_grants WHERE subject_hash=? UNION '
            'SELECT client_id FROM apple_grant_generations WHERE subject_hash=?', (subject, subject))}
        for client in clients:
            db.execute('INSERT INTO apple_grant_generations VALUES(?,?,1) '
                       'ON CONFLICT(subject_hash,client_id) DO UPDATE SET generation=generation+1', (subject, client))
        queued += db.execute("UPDATE apple_grants SET state='pending',due=0,lease=NULL "
                             'WHERE subject_hash=?', (subject,)).rowcount
    for player in players:
        db.execute('DELETE FROM apple_links WHERE player=?', (player,))
    # Cached recovery previews can refer to a deleted player even when player is
    # NULL (new device). Invalidate ALL short-lived previews, not other saves.
    db.execute('DELETE FROM apple_challenges')
    db.execute('DELETE FROM apple_unlinks')
    # Retain encrypted outbox / ambiguous exchange evidence for offline review.
    # Never call Apple or claim that a historical token is now revoked.
    return queued


def _reconcile(database, checkpoint):
    with closing(sqlite3.connect(str(database))) as db:
        db.execute('PRAGMA foreign_keys=ON')
        db.execute('PRAGMA secure_delete=ON')
        db.execute('PRAGMA synchronous=FULL')
        db.execute('BEGIN IMMEDIATE')
        tables = _schema(db)
        db.execute('CREATE TABLE IF NOT EXISTS deleted_players (player_hash TEXT PRIMARY KEY, deleted_at TEXT NOT NULL)')
        db.execute('CREATE TABLE IF NOT EXISTS retired_purchases (transaction_hash TEXT PRIMARY KEY)')
        db.executemany('INSERT OR IGNORE INTO deleted_players VALUES(?,?)',
                       [(e['Hash'], e['DeletedUtc']) for e in checkpoint['DeletedPlayers']])
        db.executemany('INSERT OR IGNORE INTO retired_purchases VALUES(?)',
                       [(h,) for h in checkpoint['RetiredPurchases']])
        deleted = {r[0] for r in db.execute('SELECT player_hash FROM deleted_players')}
        retired = {r[0] for r in db.execute('SELECT transaction_hash FROM retired_purchases')}
        players = {r[0] for r in db.execute('SELECT id FROM players') if Store.player_hash(r[0]) in deleted}
        removed = len(players)
        # These two tables intentionally have no FK: scrub orphaned historical
        # records too, not only identities still present in players.
        players.update(r[0] for r in db.execute('SELECT player FROM audit UNION SELECT player FROM flags')
                       if isinstance(r[0], str) and Store.player_hash(r[0]) in deleted)
        for store, transaction, owner in db.execute('SELECT store,transaction_id,player FROM purchases').fetchall():
            hashed = Store.transaction_hash(store, transaction)
            if owner in players:
                db.execute('INSERT OR IGNORE INTO retired_purchases VALUES(?)', (hashed,))
            elif hashed in retired:
                raise ValueError('Retired purchase belongs to a surviving account; manual reconciliation required')
        queued = _scrub_apple(db, players) if tables & APPLE else 0
        for player in players:
            # Explicitly scrub all refund records, as well as relying on their
            # validated CASCADE constraints. Never retain replay/waiver identity.
            for table in tables & REFUND_COLUMNS.keys():
                db.execute('DELETE FROM ' + table + ' WHERE player=?', (player,))
            for table in ('snapshots', 'operations', 'claims', 'purchases', 'audit', 'flags'):
                db.execute('DELETE FROM ' + table + ' WHERE player=?', (player,))
            db.execute('DELETE FROM players WHERE id=?', (player,))
        # Never replay historical confirmations/receipts after rolling back data.
        for table in ('deletion_confirmations', 'deletion_receipts'):
            if table in tables:
                db.execute('DELETE FROM ' + table)
        refunds = bool(tables & REFUND_COLUMNS.keys())
        if refunds:
            # A backup's lease belongs to a dead worker, and its old success is
            # not proof of current Apple state. Recheck every delivered purchase.
            db.execute("INSERT OR IGNORE INTO refund_checks(transaction_id,player,due) "
                       "SELECT transaction_id,player,0 FROM purchases WHERE store='apple'")
            db.execute("UPDATE refund_checks SET due=0,attempts=0,last_success=NULL,"
                       "last_error='recovery_recheck_required',lease=NULL,lease_until=0")
            validate_refunds(db)
        if db.execute('PRAGMA foreign_key_check').fetchone():
            raise ValueError('Recovery foreign key check failed')
        db.commit()
        # Compact this candidate only. This is NOT secure physical erasure of
        # the backup, SSD, filesystem snapshots, journal or failed candidates.
        db.execute('VACUUM')
    verify_database(database)
    return dict(RemovedPlayers=removed, QueuedHistoricalGrants=queued,
                AppleReconciliationRequired=bool(tables & APPLE),
                RefundReconciliationRequired=refunds,
                PostBackupEconomyReconciliationRequired=True)


def restore_filtered(source, checkpoint_path, expected_sha256, instance, destination):
    checkpoint = load_checkpoint(checkpoint_path, expected_sha256, instance)
    source = _regular(source)
    manifest_path = _regular(str(source) + '.json')
    with manifest_path.open('rb') as stream:
        manifest = json.loads(stream.read(16385))
    if not isinstance(manifest, dict) or manifest.get('InstanceId') != instance:
        raise ValueError('Backup instance missing or mismatched; legacy backups require offline identity review')
    destination = Path(destination).absolute()
    if any(p.is_symlink() for p in (destination, *destination.parents)):
        raise ValueError('Recovery destination must not traverse symlinks')
    restore_copy(source, destination)
    result = _reconcile(destination / 'players.sqlite', checkpoint)
    result.update(Status='DELETIONS_APPLIED_OFFLINE_NOT_READY', InstanceId=instance,
                  CheckpointSha256=expected_sha256, CheckpointCreatedUtc=checkpoint['CreatedUtc'])
    write_private(destination / 'DELETION-RECONCILIATION.json', encode(result) + '\n')
    sync_directory(destination)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    export = commands.add_parser('export-checkpoint')
    export.add_argument('--data-dir', required=True); export.add_argument('--output', required=True)
    restore = commands.add_parser('restore-filtered')
    restore.add_argument('--backup', required=True); restore.add_argument('--checkpoint', required=True)
    restore.add_argument('--expected-sha256', required=True); restore.add_argument('--new-directory', required=True)
    for command in (export, restore):
        command.add_argument('--instance-id', required=True)
    args = parser.parse_args()
    if args.command == 'export-checkpoint':
        print(encode(dict(CheckpointSha256=export_checkpoint(args.data_dir, args.output, args.instance_id))))
    else:
        print(encode(restore_filtered(args.backup, args.checkpoint, args.expected_sha256,
                                     args.instance_id, args.new_directory)))


if __name__ == '__main__':
    main()
