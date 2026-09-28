"""One reviewed Sandbox account, copied to a NEW database. Never edit the source.

This is an operator migration of test balances, not purchase verification.
Refuse any existing Apple identity, purchase ledger, deletion, recovery or online
economy activity: those require a separately reviewed migration.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
from contextlib import closing


def require(ok):
    if not ok:
        raise ValueError('Sandbox isolation precondition failed')


def write_new(path, raw):
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno())


def inspect(db, player_digest, free, paid):
    require(db.execute('PRAGMA quick_check').fetchone()[0] == 'ok')
    require(db.execute('PRAGMA foreign_key_check').fetchone() is None)
    require(db.execute("SELECT 1 FROM sqlite_master WHERE type IN ('trigger','view') LIMIT 1").fetchone() is None)
    players = db.execute('SELECT * FROM players').fetchall()
    require(len(players) == 1)
    p = players[0]
    require(hashlib.sha256(p['id'].encode()).hexdigest() == player_digest)
    require(p['migration_required'] == 1 and p['economy_revision'] == 0
            and p['epoch'] == 0 and p['frozen'] == 0 and p['recovery'] is None)
    tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    empty = {'operations', 'claims', 'purchases', 'deleted_players', 'retired_purchases'}
    empty |= {t for t in tables if t.startswith('apple_') or t.startswith('deletion_')}
    empty.discard('deletion_journal_meta')
    for name in sorted(empty):
        require(re.fullmatch('[a-z_]+', name) is not None)
        require(db.execute('SELECT count(*) FROM "' + name + '"').fetchone()[0] == 0)
    require(db.execute('SELECT count(*) FROM deletion_journal_meta').fetchone()[0] == 1)
    require(db.execute('SELECT 1 FROM snapshots WHERE player!=? OR epoch!=0 LIMIT 1', (p['id'],)).fetchone() is None)
    row = db.execute('SELECT data,hash FROM snapshots WHERE player=? ORDER BY revision DESC LIMIT 1', (p['id'],)).fetchone()
    require(row is not None and hashlib.sha256(row['data'].encode()).hexdigest() == row['hash'])
    data = json.loads(row['data'])
    require(data['PlayerId'] == p['id'] and data['EconomyRevision'] == 0)
    require(type(data['FreeGachaStones']) is int and data['FreeGachaStones'] == free)
    require(type(data['PaidGachaStones']) is int and data['PaidGachaStones'] == paid)
    return p['id'], row['hash']


def isolate(source, destination, player_digest, free, paid, instance):
    source, destination = Path(source), Path(destination)
    require(source.is_absolute() and destination.is_absolute())
    require(re.fullmatch(r'nasus-qa-sandbox-[a-z0-9-]+', instance) is not None)
    require(re.fullmatch('[a-f0-9]{64}', player_digest) is not None)
    require(type(free) is int and type(paid) is int and free >= 0 and paid >= 0)
    require(not any(p.is_symlink() for p in (source, *source.parents, destination, *destination.parents)))
    require(source.is_file() and not destination.exists())
    require(source.stat().st_size < 256 * 1024 * 1024)
    # Consistent WAL-aware read-only source snapshot. No credentials are read
    # from the Mac or used as bearer tokens by this migration.
    with closing(sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)) as original:
        original.row_factory = sqlite3.Row
        original.execute('PRAGMA query_only=ON'); original.execute('BEGIN')
        player, snapshot_hash = inspect(original, player_digest, free, paid)
        destination.mkdir(mode=0o700)
        target = destination / 'players.sqlite'
        write_new(target, b'')
        with closing(sqlite3.connect(target)) as clone:
            original.backup(clone)
    write_new(destination / 'instance-id', (instance + '\n').encode())
    with closing(sqlite3.connect(target)) as clone:
        clone.row_factory = sqlite3.Row
        inspect(clone, player_digest, free, paid)
        clone.execute('UPDATE deletion_journal_meta SET instance=? WHERE id=1', (instance,))
        clone.commit()
    # Use the existing audited migration implementation, only in the new DB.
    from apple_revocation_runtime import ExistingStore
    store = ExistingStore(destination, instance)
    result = store.adjust(player, free, paid, 0, 'sandbox-isolation-20260927',
        'User reports Sandbox purchases, no real charge. QA-only balances; not verified production purchases.',
        migration=True)
    require(result['Revision'] == 1 and result['Free'] == free and result['Paid'] == paid)
    marker = dict(Version=1, Purpose='SANDBOX_ONLY_NOT_PRODUCTION', InstanceId=instance,
                  PlayerSha256=player_digest, SourceSnapshotSha256=snapshot_hash,
                  PurchaseHistoryVerified=False, ProductionMigrationApproved=False)
    write_new(destination / 'SANDBOX-ONLY.json', json.dumps(marker, sort_keys=True).encode())
    with store.connect() as db:
        require(db.execute('PRAGMA quick_check').fetchone()[0] == 'ok')
        require(db.execute('PRAGMA foreign_key_check').fetchone() is None)
    return dict(Status='SANDBOX_COPY_MIGRATED', Free=free, Paid=paid,
                SourceDatabaseChanged=False, PurchaseHistoryVerified=False)
