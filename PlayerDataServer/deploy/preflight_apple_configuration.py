"""Service-user read-only inspection; no imports from the live application.

Reports booleans/counts only. Never loads keys, tokens, player rows or env files.
Does not initialize schemas, contact Apple, or change service configuration.
"""
import json
import os
from pathlib import Path
import sqlite3
import stat
import subprocess
from contextlib import closing

TABLES = {
    'apple_links': 'subject_hash player created',
    'apple_challenges': 'id nonce mode player credential_hash expires result fingerprint',
    'apple_exchanges': 'challenge subject_hash generation state grant_id created',
    'apple_unlinks': 'id player credential_hash subject_hash expires result',
    'apple_grants': 'id subject_hash client_id encrypted_token created state attempts due lease last_error',
    'apple_grant_generations': 'subject_hash client_id generation',
    'deletion_journal_meta': 'id instance',
    'deletion_outbox': 'event_id state',
}


def metadata(path, directory=False):
    try:
        path = Path(path)
        info = path.lstat()
        safe = (not any(p.is_symlink() for p in (path, *path.parents))
                and info.st_uid == os.geteuid() and info.st_mode & 0o077 == 0
                and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
        return {'Present': True, 'OwnerPrivate': safe}
    except FileNotFoundError:
        return {'Present': False, 'OwnerPrivate': False}
    except OSError:
        return {'Present': None, 'OwnerPrivate': False}


def inspect_database(directory):
    directory = Path(directory)
    database = directory / 'players.sqlite'
    if not metadata(directory, True)['OwnerPrivate'] or not metadata(database)['OwnerPrivate']:
        raise ValueError('unsafe database')
    if os.path.lexists(directory / 'RECOVERY-PENDING.txt'):
        raise ValueError('recovery quarantine')
    for suffix in ('-wal', '-shm', '-journal'):
        side = Path(str(database) + suffix)
        if os.path.lexists(side) and not metadata(side)['OwnerPrivate']:
            raise ValueError('unsafe sidecar')
    with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True, timeout=5)) as db:
        db.execute('PRAGMA query_only=ON')
        db.execute('PRAGMA trusted_schema=OFF')
        budget = [10000]
        def progress():
            budget[0] -= 1
            return int(budget[0] <= 0)
        db.set_progress_handler(progress, 1000)
        db.execute('BEGIN')
        tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        schema = {name: name in tables and set(columns.split()) <= {
            r[1] for r in db.execute('PRAGMA table_info(' + name + ')')}
            for name, columns in TABLES.items()}
        # Aggregate only; never return identifiers or credential material.
        rows = {name: db.execute('SELECT count(*) FROM ' + name).fetchone()[0]
                if schema[name] else None for name in TABLES}
        return {'QuickCheckPassed': db.execute('PRAGMA quick_check').fetchall() == [('ok',)],
                'ForeignKeysPassed': db.execute('PRAGMA foreign_key_check').fetchone() is None,
                'NoViewsOrTriggers': db.execute("SELECT 1 FROM sqlite_master WHERE type IN ('view','trigger') LIMIT 1").fetchone() is None,
                'RequiredColumnsPresent': schema, 'RowCounts': rows}


def main():
    import pwd
    if os.geteuid() != pwd.getpwnam('witchplayer').pw_uid:
        raise ValueError('run as service user')
    report = {'Status': 'APPLE_CONFIGURATION_INSPECTED', 'DatabaseChanged': False,
              'ConfigurationChanged': False, 'AppleRequests': 0,
              'Database': inspect_database('/var/lib/witch-player')}
    report['PrivateFiles'] = {name: metadata('/etc/witch-player-apple/' + name)
                              for name in ('worker.json', 'signing.p8', 'token-encryption.key')}
    report['StateDirectory'] = metadata('/var/lib/witch-player-apple', True)
    report['Units'] = {}
    for unit in ('witch-player.service', 'witch-player-apple-revoke.service',
                 'witch-player-apple-revoke.timer'):
        result = subprocess.run(['/usr/bin/systemctl', 'show', unit, '--no-pager',
                                 '-p', 'LoadState', '-p', 'ActiveState', '-p', 'Result'],
                                capture_output=True, text=True, timeout=10)
        report['Units'][unit] = dict(line.split('=', 1) for line in result.stdout.splitlines()
                                     if line.startswith(('LoadState=', 'ActiveState=', 'Result=')))
    print(json.dumps(report, sort_keys=True, indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(json.dumps({'Status': 'APPLE_INSPECTION_STOPPED', 'ErrorType': type(error).__name__,
                          'DatabaseChanged': False, 'ConfigurationChanged': False}))
        raise SystemExit(1)
