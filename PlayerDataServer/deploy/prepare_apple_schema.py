"""One-shot additive migration. No routes, tokens, existing rows or keys changed."""
import json
import os
from pathlib import Path
import re
import sqlite3
import stat
from contextlib import closing

SCHEMA = (
    'CREATE TABLE apple_links (subject_hash TEXT PRIMARY KEY, player TEXT NOT NULL UNIQUE REFERENCES players(id), created TEXT NOT NULL)',
    'CREATE TABLE apple_challenges (id TEXT PRIMARY KEY, nonce TEXT NOT NULL, mode TEXT NOT NULL, player TEXT REFERENCES players(id), credential_hash TEXT NOT NULL, expires REAL NOT NULL, result TEXT, fingerprint TEXT)',
    "CREATE TABLE apple_exchanges (challenge TEXT PRIMARY KEY, subject_hash TEXT NOT NULL, generation INTEGER NOT NULL, state TEXT NOT NULL CHECK(state IN ('requested','uncertain','failed','stored','attached')), grant_id TEXT, created REAL NOT NULL)",
    'CREATE INDEX apple_exchanges_subject ON apple_exchanges(subject_hash,state)',
    'CREATE TABLE apple_unlinks (id TEXT PRIMARY KEY, player TEXT NOT NULL REFERENCES players(id), credential_hash TEXT NOT NULL, subject_hash TEXT NOT NULL, expires REAL NOT NULL, result TEXT)',
    "CREATE TABLE apple_grants (id TEXT PRIMARY KEY, subject_hash TEXT NOT NULL, client_id TEXT NOT NULL, encrypted_token BLOB NOT NULL, created REAL NOT NULL, state TEXT NOT NULL CHECK(state IN ('active','pending','leased')), attempts INTEGER NOT NULL DEFAULT 0, due REAL NOT NULL DEFAULT 0, lease TEXT, last_error TEXT)",
    'CREATE INDEX apple_grants_due ON apple_grants(client_id,state,due)',
    'CREATE INDEX apple_grants_subject ON apple_grants(subject_hash,client_id,state)',
    'CREATE TABLE apple_grant_generations (subject_hash TEXT NOT NULL, client_id TEXT NOT NULL, generation INTEGER NOT NULL, PRIMARY KEY(subject_hash,client_id))',
)
TABLES = {'apple_links','apple_challenges','apple_exchanges','apple_unlinks','apple_grants','apple_grant_generations'}
CORE = {'players','snapshots','operations','claims','purchases','audit','flags',
        'deletion_journal_meta','deletion_outbox','deleted_players','retired_purchases'}


def require(ok):
    if not ok: raise ValueError('Apple schema preparation rejected')


def private(path, directory=False):
    path = Path(path)
    info = path.lstat()
    require(not any(p.is_symlink() for p in (path, *path.parents))
            and info.st_uid == os.geteuid() and not info.st_mode & 0o077
            and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
    return path


def check(db, instance):
    require(db.execute('PRAGMA quick_check').fetchall() == [('ok',)])
    require(db.execute('PRAGMA foreign_key_check').fetchone() is None)
    require(db.execute("SELECT 1 FROM sqlite_master WHERE type IN ('view','trigger') LIMIT 1").fetchone() is None)
    names = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    require(CORE <= names and not names & TABLES)
    require(db.execute('SELECT id,instance FROM deletion_journal_meta').fetchall() == [(1, instance)])


def migrate(directory, backup_directory, *, statements=SCHEMA):
    # Modules are from the separately hash-verified root-owned runtime.
    from maintenance import backup_generation, verify_generation
    directory = private(directory, True)
    database = private(directory / 'players.sqlite')
    require(not os.path.lexists(directory / 'RECOVERY-PENDING.txt'))
    instance = private(directory / 'instance-id').read_text().strip()
    require(re.fullmatch(r'[A-Za-z0-9_-]{8,80}', instance) is not None)
    private(backup_directory, True)
    for suffix in ('-wal','-shm','-journal'):
        if os.path.lexists(str(database) + suffix): private(str(database) + suffix)
    with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True, timeout=10)) as db:
        db.execute('PRAGMA query_only=ON'); db.execute('PRAGMA trusted_schema=OFF')
        check(db, instance)
    snapshot = backup_generation(database, backup_directory)
    verify_generation(snapshot)
    with closing(sqlite3.connect(database.as_uri() + '?mode=rw', uri=True, timeout=10)) as db:
        db.execute('PRAGMA foreign_keys=ON'); db.execute('PRAGMA trusted_schema=OFF')
        db.execute('PRAGMA synchronous=FULL')
        db.execute('BEGIN IMMEDIATE')
        try:
            check(db, instance)
            for statement in statements: db.execute(statement)
            require(all(db.execute('SELECT count(*) FROM ' + table).fetchone()[0] == 0 for table in TABLES))
            require(db.execute('PRAGMA foreign_key_check').fetchone() is None)
            db.commit()
        except BaseException:
            db.rollback()
            raise
    return {'Status':'APPLE_EMPTY_SCHEMA_PREPARED','AddedTables':6,'ExistingRowsChanged':False,
            'Backup':str(snapshot),'InstanceId':instance}


if __name__ == '__main__':
    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    try:
        os.umask(0o077)
        print(json.dumps(migrate(Path('/var/lib/witch-player'), Path('/var/backups/witch-player-apple-20260926'))))
    except Exception:
        print('APPLE_SCHEMA_STOPPED: no error details or secrets printed; inspect retained backup before retry.')
        raise SystemExit(1)
