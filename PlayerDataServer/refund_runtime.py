"""Explicit worker entry point. Default preflight is read-only and offline.

No migrations, secret discovery, scheduling, deployment or live enablement.
--run performs one bounded batch against an already provisioned refund DB.
"""
import argparse
import fcntl
import json
import os
from pathlib import Path
import re
import stat
import time

from apple_revocation_runtime import ExistingStore, private, read_private, unique, publish
from deletion_restore import _schema
from refund_integrity import validate_refunds
from refund_reconciliation import RefundReconciliation
from purchase_refunds import PurchaseRefunds
from store import encode

FIELDS = {'Version', 'InstanceId', 'DataDirectory', 'StateDirectory', 'AppleConfigFile', 'Environment', 'BatchLimit'}


class RefundExistingStore(ExistingStore):
    refunds_enabled = True


def load(config_file, *, readonly=True):
    config = json.loads(read_private(config_file, 8192), object_pairs_hook=unique)
    if (not isinstance(config, dict) or set(config) != FIELDS or type(config['Version']) is not int
            or config['Version'] != 1 or not isinstance(config['InstanceId'], str)
            or not re.fullmatch(r'[A-Za-z0-9_-]{8,80}', config['InstanceId'])
            or config['Environment'] not in ('Sandbox', 'Production')
            or type(config['BatchLimit']) is not int or not 1 <= config['BatchLimit'] <= 10):
        raise ValueError('Invalid refund worker configuration')
    data = private(config['DataDirectory'], True)
    state = private(config['StateDirectory'], True)
    if data == state or data in state.parents or state in data.parents:
        raise ValueError('Separate private state directory required')
    apple = json.loads(read_private(config['AppleConfigFile'], 8192), object_pairs_hook=unique)
    allowed = {'WITCH_APPLE_ENVIRONMENT', 'WITCH_APPLE_BUNDLE_ID', 'WITCH_APPLE_APP_ID',
               'WITCH_APPLE_ROOTS_DIR', 'WITCH_APPLE_KEY_FILE', 'WITCH_APPLE_KEY_ID', 'WITCH_APPLE_ISSUER_ID'}
    required = allowed if config['Environment'] == 'Production' else allowed - {'WITCH_APPLE_APP_ID'}
    if (not isinstance(apple, dict) or not required <= apple.keys() or apple.keys() - allowed
            or any(not isinstance(v, str) or not v for v in apple.values())
            or apple['WITCH_APPLE_ENVIRONMENT'] != config['Environment']):
        raise ValueError('Explicit matching Apple configuration required')
    # Preflight checks referenced paths but never reads key bytes or calls Apple.
    private(apple['WITCH_APPLE_KEY_FILE'])
    roots = private(apple['WITCH_APPLE_ROOTS_DIR'], True)
    certificates = list(roots.glob('*.cer'))
    if not certificates: raise ValueError('Apple root certificates required')
    for certificate in certificates: private(certificate)
    store = RefundExistingStore(data, config['InstanceId'], readonly)
    with store.connect() as db:
        db.execute('BEGIN')
        _schema(db)
        if (not validate_refunds(db) or db.execute('PRAGMA quick_check').fetchone()[0] != 'ok'
                or db.execute('PRAGMA foreign_key_check').fetchone() is not None):
            raise ValueError('Existing complete refund database required')
    return config, store, state, apple


def execute(config_file, *, run=False, checker_factory=None):
    config, store, state, apple = load(config_file, readonly=not run)
    if not run:
        return dict(Status='REFUND_PREFLIGHT_PASSED', NetworkRequests=0,
                    DatabaseChanged=False, SchemaInitialized=False, Activated=False,
                    SigningKeyRead=False)
    fd = os.open(state/'worker.lock', os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.geteuid() or info.st_mode & 0o077:
            raise ValueError('Private worker lock required')
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        def mark(status, healthy, **extra):
            publish(state, dict(Status=status, Healthy=healthy, CheckedUnix=time.time(),
                                InstanceId=config['InstanceId'], Environment=config['Environment'], **extra))
        mark('REFUND_WORKER_RUNNING', False)
        try:
            if checker_factory is None:
                from apple_refund_verifier import build_transaction_verifier
                checker_factory = build_transaction_verifier
            checker = checker_factory(apple, sandbox_only=config['Environment'] == 'Sandbox')
            service = PurchaseRefunds(store, lambda _: None)
            result = RefundReconciliation(service, checker).run(limit=config['BatchLimit'])
            mark('REFUND_BATCH_COMPLETED', result['Healthy'], Counts={k:v for k,v in result.items() if k != 'Healthy'})
            return dict(result, Status='REFUND_BATCH_COMPLETED')
        except BaseException:
            mark('REFUND_WORKER_FAILED', False)
            raise
    finally:
        os.close(fd)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True)
    parser.add_argument('--run', action='store_true', help='Explicitly contact Apple and update an existing refund ledger')
    args = parser.parse_args(); os.umask(0o077)
    try:
        result = execute(args.config, run=args.run)
        print(encode(result))
        return 0 if not args.run or result['Healthy'] else 1
    except Exception:
        print('REFUND_WORKER_STOPPED: check private configuration and health; no private details printed.')
        return 1


if __name__ == '__main__': raise SystemExit(main())
