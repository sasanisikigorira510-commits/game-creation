"""Explicit, private configuration for a single-app Apple revoke worker.

Default CLI only checks. --run can contact Apple and update an already existing
revocation queue; it never initializes a DB/schema or enables public routes.
No key discovery, generation, privilege escalation, or deployment is performed.
"""
import argparse
import fcntl
import json
import math
import os
from pathlib import Path
import re
import sqlite3
import stat
import tempfile
import time

from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
from apple_grants import AppleGrantVault
from apple_identity import AppleIdentityVerifier
from apple_tokens import AppleTokenClient
from apple_revocation_worker import inspect_queue, run_batch
from store import Store, StoreConnection, encode

FIELDS = {'Version','InstanceId','ClientId','TeamId','KeyId','DataDirectory',
          'SigningKeyFile','TokenEncryptionKeyFile','StateDirectory'}


def require(ok):
    if not ok: raise ValueError('Apple worker configuration invalid')


def private(path, directory=False):
    path=Path(path); require(path.is_absolute())
    require(not any(p.is_symlink() for p in (path,*path.parents)))
    info=path.stat()
    require(info.st_uid == os.geteuid() and not info.st_mode & 0o077
            and (stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode)))
    for parent in path.parents:
        item=parent.stat()
        require(item.st_uid in (0,os.geteuid()) and not item.st_mode & 0o022)
    return path


def read_private(path, limit):
    path=private(path)
    with path.open('rb') as stream: raw=stream.read(limit+1)
    require(0 < len(raw) <= limit); return raw


def unique(pairs):
    result={}
    for key,value in pairs:
        require(key not in result); result[key]=value
    return result


class ExistingStore(Store):
    """Deliberately does not construct Store (which creates tables)."""
    def __init__(self, directory, instance, readonly=False):
        self.directory=directory; self.instance=instance; self.readonly=readonly
        self.path=str(directory/'players.sqlite')
        self.catalog=[]; self.purchase_verifier=None; self.must_exist=True

    def connect(self):
        private(self.directory,True)
        marker=self.directory/'RECOVERY-PENDING.txt'
        require(not marker.exists() and not marker.is_symlink())
        require(read_private(self.directory/'instance-id',100).decode().strip() == self.instance)
        private(self.path)
        # SQLite sidecars must not be links or shared files either.
        for suffix in ('-wal','-shm','-journal'):
            side=Path(self.path+suffix)
            if side.exists() or side.is_symlink(): private(side)
        db=sqlite3.connect(Path(self.path).as_uri()+('?mode=ro' if self.readonly else '?mode=rw'),
                           uri=True,timeout=10,factory=StoreConnection)
        try:
            db.row_factory=sqlite3.Row
            db.execute('PRAGMA foreign_keys=ON'); db.execute('PRAGMA trusted_schema=OFF')
            if self.readonly: db.execute('PRAGMA query_only=ON')
            else: db.execute('PRAGMA synchronous=FULL')
        except BaseException:
            db.close(); raise
        return db


def load_components(config_file, *, readonly=True):
    config=json.loads(read_private(config_file,4096),object_pairs_hook=unique)
    require(isinstance(config,dict) and set(config) == FIELDS
            and type(config['Version']) is int and config['Version'] == 1)
    for key,pattern in [('InstanceId',r'[A-Za-z0-9_-]{8,80}'),('ClientId',r'[A-Za-z0-9.-]{1,255}'),
                        ('TeamId',r'[A-Z0-9]{10}'),('KeyId',r'[A-Z0-9]{10}')]:
        require(isinstance(config[key],str) and re.fullmatch(pattern,config[key]))
    for key in ('DataDirectory','StateDirectory','SigningKeyFile','TokenEncryptionKeyFile'):
        require(isinstance(config[key],str))
    data=private(config['DataDirectory'],True); state=private(config['StateDirectory'],True)
    require(state != data and state not in data.parents and data not in state.parents)
    require(config['SigningKeyFile'] != config['TokenEncryptionKeyFile'])
    signing=serialization.load_pem_private_key(read_private(config['SigningKeyFile'],16384),password=None)
    require(isinstance(signing,ec.EllipticCurvePrivateKey) and isinstance(signing.curve,ec.SECP256R1))
    encryption=read_private(config['TokenEncryptionKeyFile'],32); require(len(encryption) == 32)
    store=ExistingStore(data,config['InstanceId'],readonly)
    with store.connect() as db:
        require(db.execute('PRAGMA quick_check').fetchone()[0] == 'ok')
        require(db.execute('PRAGMA foreign_key_check').fetchone() is None)
        require(db.execute("SELECT 1 FROM sqlite_master WHERE type IN ('view','trigger') LIMIT 1").fetchone() is None)
        require(db.execute('SELECT 1 FROM apple_grants WHERE client_id!=? LIMIT 1',(config['ClientId'],)).fetchone() is None)
        require(db.execute('SELECT 1 FROM apple_grant_generations WHERE client_id!=? LIMIT 1',(config['ClientId'],)).fetchone() is None)
        db.execute('SELECT challenge,subject_hash,generation,state,grant_id,created FROM apple_exchanges LIMIT 0')
    vault=AppleGrantVault(store,encryption,config['ClientId'],must_exist=True)
    client=AppleTokenClient(config['ClientId'],config['TeamId'],config['KeyId'],signing,
                           AppleIdentityVerifier(config['ClientId']))
    inspect_queue(vault,time.time())
    return vault,client,state


def publish(state, value):
    private(state,True)
    fd,name=tempfile.mkstemp(prefix='.apple-status-',dir=state)
    try:
        with os.fdopen(fd,'w') as out:
            out.write(encode(value)+'\n'); out.flush(); os.fsync(out.fileno())
        os.replace(name,state/'status.json')
        fd=os.open(state,os.O_RDONLY)
        try: os.fsync(fd)
        finally: os.close(fd)
    finally: Path(name).unlink(missing_ok=True)


def running_status(state):
    """Retain only a completed healthy observation across one bounded run.

Never carry a previous running/failed/interrupted marker forward. The monitor
must also match this process to fresh systemd monotonic start-time properties.
"""
    started = time.time()
    previous = None
    try:
        value = json.loads(read_private(state/'status.json', 4096))
        stamp = value.get('CheckedUnix')
        if (value.get('Status') == 'APPLE_REVOCATION_BATCH_OBSERVED'
                and value.get('Healthy') is True and type(stamp) in (int, float)
                and math.isfinite(stamp) and 0 <= started-stamp <= 180):
            previous = stamp
    except (OSError, ValueError, TypeError, AttributeError):
        pass
    return dict(Healthy=False, CheckedUnix=started, Status='APPLE_WORKER_RUNNING',
                PreviousSuccessUnix=previous, StartedMonotonicNs=time.monotonic_ns())


def execute(config_file, *, run=False, components=load_components):
    vault,client,state=components(config_file,readonly=not run)
    if not run:
        return dict(Status='APPLE_WORKER_PREFLIGHT_PASSED',NetworkRequests=0,
                    DatabaseChanged=False,SchemaInitialized=False,Activated=False)
    fd=os.open(state/'worker.lock',os.O_CREAT|os.O_RDWR|os.O_NOFOLLOW,0o600)
    try:
        require(stat.S_ISREG(os.fstat(fd).st_mode) and os.fstat(fd).st_uid == os.geteuid()
                and not os.fstat(fd).st_mode & 0o077)
        fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
        # A crash/timeout never leaves the previous healthy status as current.
        publish(state, running_status(state))
        try: result=run_batch(vault,client)
        except BaseException:
            publish(state,dict(Healthy=False,CheckedUnix=time.time(),Status='APPLE_WORKER_INTERRUPTED'))
            raise
        publish(state,result); return result
    finally: os.close(fd)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',required=True)
    parser.add_argument('--run',action='store_true',help='Explicitly process queued revocations; can contact Apple')
    args=parser.parse_args(); os.umask(0o077)
    result=execute(args.config,run=args.run)
    print(encode(result))
    return 0 if not args.run or result['Healthy'] else 1


if __name__ == '__main__':
    try: raise SystemExit(main())
    except Exception:
        print('APPLE_WORKER_STOPPED: check private configuration and service status; no secrets printed.')
        raise SystemExit(1)
