"""Explicit replacement entry point. Never silently initializes the ledger."""
import os
import pwd

from deletion_inventory import inspect_inventory
from deletion_inventory_ledger import InventoryLedger, MAX_INVENTORY, MAX_STATE
from deploy.deletion_worker import (DATA, STATE, CONFIG, AGE, as_database_user, read_json, run_once)
from deploy.sakura_deletion_journal import AgeEncryptor
from deploy.sakura_inventory_store import SakuraInventoryStore


def make_commit(account):
    path = STATE/'inventory'/'ledger.json'
    saved = read_json(path, MAX_STATE)
    ledger = InventoryLedger(path, saved['InstanceId'], AgeEncryptor(AGE, CONFIG/'recipient.txt'),
                             SakuraInventoryStore(CONFIG/'curl.conf'))
    def snapshot():
        raw, states = inspect_inventory(DATA, ledger.instance)
        if len(raw) > MAX_INVENTORY: raise ValueError('Inventory pilot size limit')
        return raw.decode()
    def commit(event_id):
        raw = as_database_user(account, snapshot).encode()
        return ledger.publish(raw, event_id=event_id)
    return commit


def main():
    if os.geteuid() != 0: raise SystemExit('Dedicated root service required')
    os.umask(0o077)
    try:
        account = pwd.getpwnam('witchplayer')
        # Construct inside callback so missing ledger also produces a failed
        # worker marker under the existing worker lock/finally block.
        run_once(account, inventory_commit=lambda event_id: make_commit(account)(event_id))
    except Exception:
        raise SystemExit('INVENTORY_WORKER_FAILED: inspect retained state; no automatic bootstrap or secrets printed') from None
