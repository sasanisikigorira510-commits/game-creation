"""Isolated sidecar. Root owns cloud credentials; only witchplayer opens SQLite.

No API wiring, account deletion, bucket listing/deletion, or private age identity.
One leased event per invocation. Installer must explicitly provision the budget.
"""
import base64
from contextlib import closing
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import signal
import sqlite3
import subprocess
import tempfile
import time

from store import StoreConnection
from deletion_journal import DeletionJournal, MAX_CIPHERTEXT, validate_event
from deploy.preflight_deletion_worker import CORE, inspect_database, private_file
from deploy.sakura_deletion_journal import SakuraPublisher

DATA = Path('/var/lib/witch-player')
STATE = Path('/var/lib/witch-player-deletion')
CONFIG = Path('/etc/witch-player-offsite')
AGE = Path('/usr/local/lib/nasus-backup/age')
MAX_UPLOAD_BYTES = 64 * 1024 * 1024  # Explicit pilot limit; retries count too.
MAX_REPORT_AGE = 300


def atomic_json(path, value):
    fd, name = tempfile.mkstemp(prefix='.new-', dir=path.parent)
    try:
        with os.fdopen(fd, 'w') as out:
            json.dump(value, out, sort_keys=True, allow_nan=False)
            out.flush(); os.fsync(out.fileno()); os.fchmod(out.fileno(), 0o600)
        os.replace(name, path)
        directory = os.open(path.parent, os.O_RDONLY)
        try: os.fsync(directory)
        finally: os.close(directory)
    finally:
        Path(name).unlink(missing_ok=True)


def read_json(path, maximum=4096):
    if not private_file(path, os.geteuid()):
        raise ValueError('Unsafe state file')
    with path.open('rb') as source:
        data = source.read(maximum + 1)
    if len(data) > maximum:
        raise ValueError('Oversized state file')
    return json.loads(data)


def reserve_upload(state, size):
    """Called with worker lock held. Never silently reset a missing budget."""
    budget = read_json(state / 'budget.json')
    used = budget.get('ReservedBytes')
    if (type(used) is not int or not 0 <= used <= MAX_UPLOAD_BYTES
            or type(size) is not int or not 64 <= size <= MAX_CIPHERTEXT
            or used + size > MAX_UPLOAD_BYTES):
        raise ValueError('Upload pilot budget exhausted or invalid')
    atomic_json(state / 'budget.json', {'ReservedBytes': used + size})


def as_database_user(account, function):
    """Bounded anonymous IPC. Neither exceptions nor account data reach logs."""
    read_fd, write_fd = os.pipe()
    pid = os.fork()
    if pid == 0:
        os.close(read_fd)
        try:
            signal.alarm(45)
            os.setgroups([]); os.setgid(account.pw_gid); os.setuid(account.pw_uid)
            os.umask(0o077)
            value = {'OK': True, 'Value': function()}
        except Exception:
            value = {'OK': False}
        with os.fdopen(write_fd, 'wb') as output:
            output.write(json.dumps(value, allow_nan=False).encode())
        os._exit(0)
    os.close(write_fd)
    with os.fdopen(read_fd, 'rb') as source:
        raw = source.read(2 * 1024 * 1024 + 1)
    _, status = os.waitpid(pid, 0)
    if status or len(raw) > 2 * 1024 * 1024:
        raise RuntimeError('Database child failed')
    result = json.loads(raw)
    if result.get('OK') is not True:
        raise RuntimeError('Database operation failed')
    return result['Value']


class ExistingDatabase:
    """Unlike Store, never creates databases or implicitly adds tables."""
    def __init__(self, directory):
        self.path = str(directory / 'players.sqlite')

    def connect(self):
        db = sqlite3.connect(Path(self.path).as_uri() + '?mode=rw', uri=True,
                             timeout=15, factory=StoreConnection)
        db.row_factory = sqlite3.Row
        db.execute('PRAGMA trusted_schema=OFF')
        db.execute('PRAGMA foreign_keys=ON')
        db.execute('PRAGMA synchronous=FULL')
        return db


def open_journal(directory=DATA):
    result = inspect_database(directory)
    if (not all(result[name] for name in ('CoreTablesPresent', 'QuickCheckPassed',
                                         'ForeignKeysPassed', 'JournalInstanceMatches'))
            or not all(result['DeletionTables'].values())):
        raise ValueError('Existing journal schema not ready')
    return DeletionJournal(ExistingDatabase(directory), (directory / 'instance-id').read_text().strip(),
                           initialize=False)


def encrypt_public(payload, recipient):
    # Only the PUBLIC recipient crosses into the database-user process.
    if not re.fullmatch(r'age1[0-9a-z]{58}', recipient):
        raise ValueError('Invalid public encryption recipient')
    result = subprocess.run([str(AGE), '-r', recipient], input=payload,
                            capture_output=True, timeout=10)
    if result.returncode:
        raise RuntimeError('Encryption failed')
    return result.stdout  # DeletionJournal validates header and length.


def claim(directory, recipient):
    ticket = open_journal(directory).claim(lambda payload: encrypt_public(payload, recipient))
    if ticket is not None:
        ticket['ciphertext'] = base64.b64encode(ticket['ciphertext']).decode('ascii')
    return ticket


def queue_healthy(directory=DATA, now=None):
    now = time.time() if now is None else now
    journal = open_journal(directory)
    with journal.store.connect() as db:
        rows = db.execute("SELECT payload,last_error,state,verified_at FROM deletion_outbox WHERE state!='verified' LIMIT 1001").fetchall()
    if len(rows) > 1000:
        return False
    for row in rows:
        data = validate_event(row['payload'].encode(), journal.instance)
        age = now - dt.datetime.fromisoformat(data['DeletedUtc']).timestamp()
        if row['last_error'] is not None or not -60 <= age <= 900:
            return False
    return True


def fingerprint_core(db):
    """Streaming canonical row digest; never printed or exported."""
    digest = hashlib.sha256()
    for table in sorted(CORE):
        digest.update(table.encode())
        for row in db.execute('SELECT * FROM ' + table + ' ORDER BY rowid'):
            digest.update(json.dumps(tuple(row), ensure_ascii=False, separators=(',', ':')).encode())
            digest.update(b'\n')
    return digest.digest()


def migrate_empty_schema(directory=DATA):
    """Explicit one-time additive migration, with an offline verified snapshot.

    Reject unexpected/partially migrated schemas. No downgrade or auto-restore.
    Run as database owner so WAL/SHM ownership remains usable by the API.
    """
    inspection = inspect_database(directory)
    if not all(inspection[k] for k in ('CoreTablesPresent', 'QuickCheckPassed', 'ForeignKeysPassed')):
        raise ValueError('Database integrity check failed')
    database = ExistingDatabase(directory)
    backup_dir = directory / 'deletion-setup-backups'
    backup_dir.mkdir(mode=0o700, exist_ok=True)
    if not private_file(backup_dir, os.geteuid(), directory=True):
        raise ValueError('Unsafe backup directory')
    fd, name = tempfile.mkstemp(prefix='before-journal-', suffix='.sqlite', dir=backup_dir)
    os.close(fd)
    snapshot = Path(name)
    with database.connect() as source, closing(sqlite3.connect(snapshot)) as target:
        source.backup(target)
        if target.execute('PRAGMA integrity_check').fetchall() != [('ok',)] or target.execute('PRAGMA foreign_key_check').fetchone():
            raise ValueError('Pre-migration snapshot invalid')
    with snapshot.open('rb') as source: os.fsync(source.fileno())
    atomic_json(snapshot.with_suffix('.json'), {'Sha256': hashlib.sha256(snapshot.read_bytes()).hexdigest(),
                                              'Purpose': 'before-deletion-schema'})
    with database.connect() as db:
        db.execute('BEGIN IMMEDIATE')
        tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        if tables != CORE or db.execute("SELECT 1 FROM sqlite_master WHERE type IN ('trigger','view')").fetchone():
            raise ValueError('Unexpected or already migrated schema; review required')
        before = fingerprint_core(db)
        statements = [
            'CREATE TABLE deleted_players (player_hash TEXT PRIMARY KEY, deleted_at TEXT NOT NULL)',
            'CREATE TABLE retired_purchases (transaction_hash TEXT PRIMARY KEY)',
            'CREATE TABLE deletion_confirmations (confirmation_hash TEXT PRIMARY KEY, player_hash TEXT NOT NULL, credential_hash TEXT NOT NULL, fingerprint TEXT NOT NULL, expires REAL NOT NULL)',
            'CREATE TABLE deletion_receipts (proof_hash TEXT PRIMARY KEY, expires REAL NOT NULL, result TEXT NOT NULL)',
            'CREATE TABLE deletion_journal_meta (id INTEGER PRIMARY KEY CHECK(id=1), instance TEXT NOT NULL)',
            "CREATE TABLE deletion_outbox (event_id TEXT PRIMARY KEY, proof_hash TEXT NOT NULL UNIQUE, payload TEXT NOT NULL, ciphertext BLOB, state TEXT NOT NULL CHECK(state IN ('pending','sending','verified')), lease TEXT, lease_until REAL NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 0, next_attempt REAL NOT NULL DEFAULT 0, verified_at REAL, last_error TEXT)",
        ]
        for statement in statements: db.execute(statement)
        db.execute('INSERT INTO deletion_journal_meta VALUES(1,?)', ((directory / 'instance-id').read_text().strip(),))
        if before != fingerprint_core(db) or db.execute('PRAGMA foreign_key_check').fetchone():
            raise ValueError('Core data changed during schema migration')
    open_journal(directory)
    return {'SchemaAdded': True, 'CoreRowsUnchanged': True, 'SnapshotVerified': True}


def run_once(account, *, directory=DATA, state=STATE, inventory_commit=None):
    if not private_file(state, 0, directory=True):
        raise ValueError('Unsafe worker state directory')
    with (state / 'worker.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        healthy = False
        try:
            if not private_file(CONFIG / 'recipient.txt', 0):
                raise ValueError('Unsafe recipient metadata')
            with (CONFIG / 'recipient.txt').open() as source: recipient = source.read(128).strip()
            ticket = as_database_user(account, lambda: claim(directory, recipient))
            if ticket is not None:
                cipher = base64.b64decode(ticket['ciphertext'], validate=True)
                reserve_upload(state, len(cipher))
                verified = False
                try:
                    event_verified = SakuraPublisher(CONFIG / 'curl.conf')(ticket['key'], cipher) == hashlib.sha256(cipher).hexdigest()
                    if event_verified and inventory_commit is not None:
                        receipt = inventory_commit(ticket['event_id'])
                        if not isinstance(receipt, dict) or receipt.get('Status') != 'INVENTORY_HEAD_VERIFIED':
                            raise ValueError('Inventory publication not verified')
                    verified = event_verified
                except (OSError, RuntimeError, ValueError):
                    pass
                outcome = as_database_user(account, lambda: open_journal(directory).finish(ticket['event_id'], ticket['lease'], verified))
                if outcome != 'verified':
                    raise RuntimeError('Delivery not verified')
            elif inventory_commit is not None:
                receipt = inventory_commit(None)
                if not isinstance(receipt, dict) or receipt.get('Status') != 'INVENTORY_HEAD_VERIFIED':
                    raise ValueError('Inventory publication not verified')
            healthy = as_database_user(account, lambda: queue_healthy(directory)) is True
            if not healthy: raise RuntimeError('Queue requires attention')
        finally:
            atomic_json(state / 'status.json', {'Healthy': healthy, 'CheckedUnix': time.time()})


def main():
    if os.geteuid() != 0:
        raise SystemExit('Run through the dedicated root service')
    os.umask(0o077)
    try:
        run_once(pwd.getpwnam('witchplayer'))
    except Exception:
        raise SystemExit('DELETION_WORKER_FAILED: inspect schema, timer, encryption and offsite access; no secrets logged') from None


if __name__ == '__main__': main()
