"""Fail-closed WSGI factory; production never creates an empty replacement DB."""
import json
import logging
import os
import sqlite3
from contextlib import closing
from pathlib import Path

from application import Application
from backup_health import healthy_marker
from security import AdminAuth
from store import Store


def create_app(environ=None):
    env = os.environ if environ is None else environ
    required = ('WITCH_DATA_DIR', 'WITCH_INSTANCE_ID', 'WITCH_PUBLIC_ORIGIN', 'WITCH_OPERATORS_FILE')
    if any(not env.get(key) for key in required):
        raise ValueError('Missing production configuration: ' + ', '.join(required))
    directory = Path(env['WITCH_DATA_DIR'])
    if not directory.is_absolute() or not directory.is_dir() or directory.stat().st_mode & 0o077:
        raise ValueError('Production data directory must exist, be absolute and have mode 700')
    recovery = directory / 'RECOVERY-PENDING.txt'
    if recovery.exists() or recovery.is_symlink():
        raise ValueError('Recovery candidate is quarantined; refusing startup')
    marker = directory / 'instance-id'
    if not marker.is_file() or marker.read_text().strip() != env['WITCH_INSTANCE_ID']:
        raise ValueError('Persistent instance identity missing or mismatched; refusing startup')
    database = directory / 'players.sqlite'
    # Read-only open cannot silently create a database when the volume is missing.
    with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as db:
        tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        if not {'players', 'snapshots', 'operations', 'claims', 'purchases', 'audit', 'flags'} <= tables:
            raise ValueError('Database schema incomplete; refusing startup')
    verifier = None
    if env.get('WITCH_APPLE_BUNDLE_ID'):
        from apple_verifier import build_verifier
        verifier = build_verifier(env)
    store = Store(database, json.loads(Path(__file__).with_name('catalog.json').read_text()), verifier, must_exist=True)
    auth = AdminAuth(path=env['WITCH_OPERATORS_FILE'])
    health_path = env.get('WITCH_BACKUP_HEALTH_FILE')
    if health_path and not Path(health_path).is_absolute():
        raise ValueError('Backup health marker must be an absolute path')
    return Application(store, auth, public_origin=env['WITCH_PUBLIC_ORIGIN'],
                       backup_health=(lambda: healthy_marker(health_path)) if health_path else None)


def application_factory():
    os.umask(0o077)
    logging.basicConfig(level=logging.INFO, format='%(message)s')
    return create_app()
