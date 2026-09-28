"""Explicit pre-release assembly, NOT the deployed production factory.

No schema creation, key generation, migration, timers or public proxy changes.
The caller must provision/review keys, schema, worker and backup monitoring first.
Real Apple/device and purchase tests remain required before public deployment.
"""
import json
from pathlib import Path

from account_deletion import AccountDeletion
from account_linking import AccountLinking
from application import Application
from apple_revocation_runtime import load_components, private, require
from backup_health import healthy_marker
from deletion_journal import DeletionJournal
from security import AdminAuth


def create_apple_app(environ, *, components=load_components, purchase_verifier=None):
    required=('WITCH_APPLE_CONFIG','WITCH_DATA_DIR','WITCH_INSTANCE_ID',
              'WITCH_PUBLIC_ORIGIN','WITCH_OPERATORS_FILE','WITCH_BACKUP_HEALTH_FILE')
    require(all(isinstance(environ.get(k),str) and environ[k] for k in required))
    vault,client,state=components(environ['WITCH_APPLE_CONFIG'],readonly=False)
    store=vault.store
    require(Path(environ['WITCH_DATA_DIR']) == store.directory
            and environ['WITCH_INSTANCE_ID'] == store.instance)
    # Never silently advertise verified purchases without the configured verifier.
    if environ.get('WITCH_APPLE_BUNDLE_ID'):
        require(environ['WITCH_APPLE_BUNDLE_ID'] == client.client_id
                and callable(purchase_verifier))
    store.catalog=json.loads(Path(__file__).with_name('catalog.json').read_text())
    store.purchase_verifier=purchase_verifier
    with store.connect() as db:
        db.execute('PRAGMA query_only=ON')
        for query in (
            'SELECT id,token_hash,created,free,paid,economy_revision,epoch,frozen,migration_required,tutorial_pulls,recovery FROM players LIMIT 0',
            'SELECT id,player,revision,epoch,received,data,hash,source FROM snapshots LIMIT 0',
            'SELECT player,request_id,request_hash,revision,kind,received,response FROM operations LIMIT 0',
            'SELECT player,claim_key FROM claims LIMIT 0',
            'SELECT store,transaction_id,player,product,received FROM purchases LIMIT 0',
            'SELECT id,player,actor,action,reason,received,detail FROM audit LIMIT 0',
            'SELECT id,player,code,received,detail FROM flags LIMIT 0',
            'SELECT player_hash,deleted_at FROM deleted_players LIMIT 0',
            'SELECT transaction_hash FROM retired_purchases LIMIT 0'):
            db.execute(query)
    linking=AccountLinking(store,client.verifier,tokens=client,vault=vault,initialize=False)
    journal=DeletionJournal(store,store.instance,initialize=False)
    deletion=AccountDeletion(store,account_linking=linking,journal=journal,initialize=False)
    # Operator credentials are existing owner-private files, never generated here.
    auth=AdminAuth(path=private(environ['WITCH_OPERATORS_FILE']))
    backup=Path(environ['WITCH_BACKUP_HEALTH_FILE'])
    require(backup.is_absolute() and not backup.is_symlink())
    apple=state/'status.json'
    def health():
        try: private(apple)
        except (OSError,ValueError): return False
        return healthy_marker(backup) and healthy_marker(apple)
    return Application(store,auth,public_origin=environ['WITCH_PUBLIC_ORIGIN'],
                       backup_health=health,account_linking=linking,account_deletion=deletion)
