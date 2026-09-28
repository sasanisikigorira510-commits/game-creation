"""Fixed-path, separate refund lab. Never points at the current QA or public DB."""
import json
import os
from pathlib import Path
import time

from apple_revocation_runtime import read_private, publish, execute as revoke
from refund_runtime import execute as reconcile_batch
from refund_sandbox import create_sandbox_apps
from qa_gateway import QaGateway
from maintenance import backup_generation, prune_generations

ROOT = Path('/var/lib/nasus-refund-sandbox-20260928')
CREDENTIALS = Path('/etc/nasus-refund-sandbox-20260928')
INSTANCE = 'nasus-refund-sandbox-20260928'


def environment():
    return dict(WITCH_REFUND_SANDBOX_ENABLED='1', WITCH_DATA_DIR=str(ROOT/'data'),
        WITCH_INSTANCE_ID=INSTANCE, WITCH_PUBLIC_ORIGIN='https://api.nasus-games.com',
        WITCH_APPLE_CONFIG=str(ROOT/'identity.json'), WITCH_REFUND_WORKER_CONFIG=str(ROOT/'refund.json'),
        WITCH_QA_GATE_CONFIG=str(CREDENTIALS/'gate.json'), WITCH_OPERATORS_FILE=str(CREDENTIALS/'operators.json'),
        WITCH_BACKUP_HEALTH_FILE=str(ROOT/'backup-status.json'))


def gate():
    return QaGateway(None, json.loads(read_private(CREDENTIALS/'gate.json', 4096)))


def device(): return create_sandbox_apps(environment()).device
def review(): return create_sandbox_apps(environment()).review
def notifications(): return create_sandbox_apps(environment()).notifications


def initialize_empty():
    """Explicit one-time provisioning only, never called by WSGI factories."""
    from maintenance import initialize, add_operator
    from store import Store
    from apple_grants import AppleGrantVault
    from account_linking import AccountLinking
    from account_deletion import AccountDeletion
    from deletion_journal import DeletionJournal
    from apple_revocation_runtime import load_components
    data = ROOT/'data'
    if data.exists(): raise ValueError('Refuse to initialize an existing data directory')
    initialize(data, INSTANCE)
    store = Store(data/'players.sqlite', refunds_enabled=True)
    identity = json.loads(read_private(ROOT/'identity.json', 8192))
    vault = AppleGrantVault(store, read_private(CREDENTIALS/'encryption.key', 32), identity['ClientId'])
    # Schema-only callback: never used for authentication or exposed to callers.
    def unavailable(*args): raise RuntimeError('Provisioning callback must never authenticate')
    AccountLinking(store, unavailable)
    vault, client, _ = load_components(ROOT/'identity.json', readonly=False)
    store = vault.store
    store.refunds_enabled = True
    linking = AccountLinking(store, client.verifier, tokens=client, vault=vault, initialize=False)
    journal = DeletionJournal(store, INSTANCE)
    AccountDeletion(store, account_linking=linking, journal=journal)
    from maintenance import write_private
    write_private(data/'SANDBOX-ONLY.json', json.dumps(dict(Purpose='SANDBOX_ONLY_NOT_PRODUCTION',
                  InstanceId=INSTANCE, ProductionMigrationApproved=False)))
    add_operator(CREDENTIALS/'operators.json', 'refund-lab-operator', 'operator', CREDENTIALS/'operator-token')
    with store.connect() as db:
        if db.execute('SELECT count(*) FROM players').fetchone()[0] != 0:
            raise ValueError('Lab must start empty')


def maintain():
    gate()
    publish(ROOT, dict(Healthy=False, CheckedUnix=time.time(), Status='REFUND_LAB_BACKUP_RUNNING'))
    os.replace(ROOT/'status.json', ROOT/'backup-status.json')
    backup_generation(ROOT/'data/players.sqlite', ROOT/'backups')
    prune_generations(ROOT/'backups')
    result = revoke(ROOT/'identity.json', run=True)
    if result['Healthy'] is not True: raise ValueError('Identity maintenance incomplete')
    publish(ROOT, dict(Healthy=True, CheckedUnix=time.time(), Status='REFUND_LAB_LOCAL_BACKUP_VERIFIED'))
    os.replace(ROOT/'status.json', ROOT/'backup-status.json')


def reconcile():
    gate()
    result = reconcile_batch(ROOT/'refund.json', run=True)
    if result['Healthy'] is not True: raise ValueError('Refund reconciliation incomplete')


if __name__ == '__main__':
    import sys
    os.umask(0o077)
    try:
        if len(sys.argv) != 2 or sys.argv[1] not in ('initialize-empty', 'maintain', 'reconcile'):
            raise ValueError('Explicit lab command required')
        {'initialize-empty':initialize_empty, 'maintain':maintain, 'reconcile':reconcile}[sys.argv[1]]()
        print('REFUND_LAB_OPERATION_OK')
    except Exception:
        print('REFUND_LAB_OPERATION_STOPPED: details kept private; existing environments unchanged.')
        raise SystemExit(1)
