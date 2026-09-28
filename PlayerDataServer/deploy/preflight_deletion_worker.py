"""Read-only privileged preflight. Never install, migrate, send or reveal secrets.

Run manually through sudo. SQLite is inspected by a child dropped to witchplayer,
using mode=ro, query_only and trusted_schema=OFF. No application module is loaded.
"""
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import sqlite3
import stat
from contextlib import closing

DATA = Path('/var/lib/witch-player')
SERVER = Path('/opt/witch-player/server')
CONFIG = Path('/etc/witch-player-offsite')
INSPECTED = {
    'store.py': '840f038c7109241153a24234914bde1ebf375dfb8a7bf3373ed98dd166c8a891',
    'production.py': 'd312a016bf24e9b9e3afd892d89eb617c9fc95bdffad300d1534462a858533fa',
    'application.py': '4fdbfafde3a91b4b633f510338296373dbbc2aac06c8722721467547db19f2dd',
}
CORE = {'players', 'snapshots', 'operations', 'claims', 'purchases', 'audit', 'flags'}
JOURNAL = {
    'deleted_players': {'player_hash', 'deleted_at'},
    'retired_purchases': {'transaction_hash'},
    'deletion_receipts': {'proof_hash', 'expires', 'result'},
    'deletion_journal_meta': {'id', 'instance'},
    'deletion_outbox': {'event_id', 'proof_hash', 'payload', 'ciphertext', 'state', 'lease',
                        'lease_until', 'attempts', 'next_attempt', 'verified_at', 'last_error'},
}


def private_file(path, uid, *, directory=False):
    try:
        path = Path(path)
        info = path.lstat()
        return (not path.is_symlink() and info.st_uid == uid and info.st_mode & 0o077 == 0
                and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
    except OSError:
        return False


def inspect_database(directory):
    """Return only schema booleans and queue counts, no IDs/tokens/data."""
    directory = Path(directory)
    marker, database = directory / 'instance-id', directory / 'players.sqlite'
    if (not private_file(directory, os.geteuid(), directory=True)
            or not private_file(database, os.geteuid()) or not private_file(marker, os.geteuid())):
        raise ValueError('Unsafe database metadata')
    quarantine = directory / 'RECOVERY-PENDING.txt'
    if quarantine.exists() or quarantine.is_symlink():
        raise ValueError('Recovery candidate cannot be configured')
    with marker.open() as stream:
        instance = stream.read(128).strip()
    if not re.fullmatch(r'[A-Za-z0-9_-]{8,80}', instance):
        raise ValueError('Invalid instance marker')
    with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True, timeout=5)) as db:
        db.execute('PRAGMA query_only=ON')
        db.execute('PRAGMA trusted_schema=OFF')
        # Bound checks even if an unexpected schema or huge table is present.
        remaining = [10000]
        def progress():
            remaining[0] -= 1
            return int(remaining[0] <= 0)
        db.set_progress_handler(progress, 1000)
        db.execute('BEGIN')
        tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        integrity = db.execute('PRAGMA quick_check').fetchall() == [('ok',)]
        foreign_keys = db.execute('PRAGMA foreign_key_check').fetchone() is None
        matches = {table: table in tables and {r[1] for r in db.execute('PRAGMA table_info(' + table + ')')} == columns
                   for table, columns in JOURNAL.items()}
        journal_ready = all(matches.values())
        instance_matches = False
        pending = verified = None
        history = bool(db.execute('SELECT 1 FROM deleted_players LIMIT 1').fetchone()) if matches['deleted_players'] else None
        if journal_ready:
            rows = db.execute('SELECT id,instance FROM deletion_journal_meta').fetchall()
            instance_matches = rows == [(1, instance)]
            groups = dict(db.execute('SELECT state,count(*) FROM deletion_outbox GROUP BY state'))
            if set(groups) - {'pending', 'sending', 'verified'}:
                raise ValueError('Invalid delivery states')
            pending = groups.get('pending', 0) + groups.get('sending', 0)
            verified = groups.get('verified', 0)
    return dict(CoreTablesPresent=CORE <= tables, QuickCheckPassed=integrity,
                ForeignKeysPassed=foreign_keys, DeletionTables=matches,
                JournalInstanceMatches=instance_matches, HistoricalDeletionsPresent=history,
                PendingEvents=pending, VerifiedEvents=verified)


def database_as_service_user(directory, account):
    read_fd, write_fd = os.pipe()
    pid = os.fork()
    if pid == 0:
        os.close(read_fd)
        try:
            os.setgroups([])
            os.setgid(account.pw_gid)
            os.setuid(account.pw_uid)
            result = {'Status': 'INSPECTED', 'Database': inspect_database(directory)}
        except Exception as error:
            result = {'Status': 'INSPECTION_FAILED', 'ErrorType': type(error).__name__}
        payload = json.dumps(result, sort_keys=True).encode()
        with os.fdopen(write_fd, 'wb') as output:
            output.write(payload)
        os._exit(0)
    os.close(write_fd)
    with os.fdopen(read_fd, 'rb') as source:
        payload = source.read(16385)
    _, status = os.waitpid(pid, 0)
    if status != 0 or len(payload) > 16384:
        raise RuntimeError('Service-user inspection failed')
    return json.loads(payload)


def summarize(database_result, metadata):
    issues = []
    data = database_result.get('Database', {})
    if database_result.get('Status') != 'INSPECTED':
        issues.append('DATABASE_INSPECTION_FAILED')
    elif not all(data.get(key) is True for key in ('CoreTablesPresent', 'QuickCheckPassed', 'ForeignKeysPassed')):
        issues.append('DATABASE_REVIEW_REQUIRED')
    if not all(data.get('DeletionTables', {}).get(name) is True for name in JOURNAL):
        issues.append('DELETION_SCHEMA_NOT_DEPLOYED')
    elif data.get('JournalInstanceMatches') is not True:
        issues.append('JOURNAL_INSTANCE_MISMATCH')
    if not metadata.get('ExistingCodeMatchesInspected'):
        issues.append('LIVE_CODE_CHANGED_REVIEW_REQUIRED')
    if not metadata.get('CredentialsMetadataSafe'):
        issues.append('CREDENTIAL_CONFIGURATION_REVIEW_REQUIRED')
    if not metadata.get('RecipientMetadataSafe'):
        issues.append('ENCRYPTION_CONFIGURATION_REVIEW_REQUIRED')
    if not metadata.get('AgeInstalled'):
        issues.append('ENCRYPTION_BINARY_MISSING')
    return dict(Status='PREFLIGHT_COMPLETE_NO_CHANGES',
                WorkerSchemaReady=not issues, Blockers=issues,
                Database=database_result, Configuration=metadata,
                Installed=False, DatabaseChanged=False, CloudRequests=0, NotificationsChanged=False)


def main():
    if os.geteuid() != 0:
        raise SystemExit('Run this read-only inspection through sudo in your terminal')
    os.umask(0o077)
    account = pwd.getpwnam('witchplayer')
    metadata = dict(
        ExistingCodeMatchesInspected=all(not (SERVER / name).is_symlink()
            and hashlib.sha256((SERVER / name).read_bytes()).hexdigest() == digest for name, digest in INSPECTED.items()),
        CredentialsMetadataSafe=private_file(CONFIG / 'curl.conf', 0),
        RecipientMetadataSafe=private_file(CONFIG / 'recipient.txt', 0),
        AgeInstalled=Path('/usr/local/lib/nasus-backup/age').is_file())
    report = summarize(database_as_service_user(DATA, account), metadata)
    print(json.dumps(report, sort_keys=True, indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        # Do not print paths, SQL rows or arbitrary exception messages.
        print(json.dumps({'Status': 'PREFLIGHT_FAILED_NO_CHANGES', 'ErrorType': type(error).__name__}))
        raise SystemExit(1)
