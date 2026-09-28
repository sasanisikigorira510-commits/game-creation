"""Private Sandbox runtime. Never load with the production factory.

Health covers a local QA snapshot and the separate QA revoke worker; it does
NOT attest to production/offsite backups. Deletion tests remain blocked until
their own isolated offsite journal has been provisioned.
"""
import json
import os
from pathlib import Path
import sqlite3
import time
from contextlib import closing

from apple_revocation_runtime import private, read_private, require, execute, publish
from qa_gateway import QaGateway

ROOT = Path('/var/lib/nasus-qa-sandbox-20260927')
DATA = ROOT / 'data'
STATE = ROOT / 'state'
CONFIG = ROOT / 'worker.json'
INSTANCE = 'nasus-qa-sandbox-20260927'


def guard(environ):
    require(environ.get('WITCH_DATA_DIR') == str(DATA))
    require(environ.get('WITCH_INSTANCE_ID') == INSTANCE)
    require(environ.get('WITCH_APPLE_CONFIG') == str(CONFIG))
    marker = json.loads(read_private(DATA / 'SANDBOX-ONLY.json', 4096))
    require(marker['Purpose'] == 'SANDBOX_ONLY_NOT_PRODUCTION'
            and marker['InstanceId'] == INSTANCE
            and marker['ProductionMigrationApproved'] is False)
    config = json.loads(read_private(CONFIG, 4096))
    require(config['DataDirectory'] == str(DATA) and config['InstanceId'] == INSTANCE
            and config['StateDirectory'] == str(STATE))
    require(environ.get('WITCH_BACKUP_HEALTH_FILE') == str(ROOT / 'backup-status.json'))
    require(not environ.get('WITCH_APPLE_BUNDLE_ID'))


class NoDeletion:
    def __init__(self, app): self.app = app

    def __call__(self, env, start):
        if env.get('PATH_INFO', '').startswith('/v1/account-deletion/'):
            body = b'{"Error":"Account deletion is not enabled in this isolated QA environment"}'
            start('503 Service Unavailable', [('Content-Type', 'application/json'),
                ('Cache-Control', 'no-store'), ('Content-Length', str(len(body)))])
            return [body]
        return self.app(env, start)


def application_factory():
    from apple_application import create_apple_app
    os.umask(0o077)
    guard(os.environ)
    gate = QaGateway(None, json.loads(read_private(os.environ['WITCH_QA_GATE_CONFIG'], 4096)))
    gate.app = NoDeletion(create_apple_app(os.environ))
    return gate


def maintain():
    """Only the explicitly isolated DB/config; never receive arbitrary paths."""
    os.umask(0o077)
    guard(os.environ)
    # A crashed/failed run leaves health failed, never the old green marker.
    publish(ROOT, dict(Healthy=False, CheckedUnix=time.time(), Status='QA_LOCAL_BACKUP_RUNNING'))
    # publish() targets status.json; backup-status has a stable separate path.
    os.replace(ROOT / 'status.json', ROOT / 'backup-status.json')
    temporary = ROOT / 'local-backup.pending.sqlite'
    require(not temporary.exists() and not temporary.is_symlink())
    with closing(sqlite3.connect(private(DATA / 'players.sqlite').as_uri() + '?mode=ro', uri=True)) as source:
        with closing(sqlite3.connect(temporary)) as backup:
            source.backup(backup)
            require(backup.execute('PRAGMA quick_check').fetchone()[0] == 'ok')
            require(backup.execute('PRAGMA foreign_key_check').fetchone() is None)
    os.replace(temporary, ROOT / 'local-backup.sqlite')
    result = execute(str(CONFIG), run=True)
    require(result['Healthy'] is True)
    publish(ROOT, dict(Healthy=True, CheckedUnix=time.time(), Status='QA_LOCAL_BACKUP_VERIFIED'))
    os.replace(ROOT / 'status.json', ROOT / 'backup-status.json')
    print('QA_LOCAL_BACKUP_AND_REVOKE_HEALTHY', flush=True)


if __name__ == '__main__':
    try: maintain()
    except Exception:
        print('QA_MAINTENANCE_FAILED: private details omitted.', flush=True)
        raise SystemExit(1)
