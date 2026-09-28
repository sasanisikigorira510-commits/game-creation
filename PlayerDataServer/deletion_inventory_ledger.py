"""Crash-resumable external inventory publication, before deletion acknowledgement.

Caller holds the deletion-worker lock. Local state is separate from SQLite and
never bootstrapped implicitly. The remote head is an independent publication
record, not a list reconstructed from event downloads. This is NOT immutable
storage or proof against a privileged provider/administrator replaying all
remote records. Recovery still requires trusted latest-head evidence.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

from deletion_inventory import monotonic_inventory, parse_inventory
from store import encode

MAX_INVENTORY = 512 * 1024  # Explicit initial resource limit; exhaustion stops ACK.
MAX_CIPHER = MAX_INVENTORY + 65536
MAX_STATE = 4 * 1024 * 1024
MAX_UPLOAD_BUDGET = 64 * 1024 * 1024


def require(ok):
    if not ok: raise ValueError('Inventory ledger validation failed')


def digest(raw): return hashlib.sha256(raw).hexdigest()
def canonical(value): return (encode(value)+'\n').encode()


def private(path, directory=False):
    path = Path(path)
    info = path.lstat()
    require(not path.is_symlink() and info.st_uid == os.geteuid() and not info.st_mode & 0o077
            and (path.is_dir() if directory else path.is_file()))
    return path


def atomic(path, value):
    raw = canonical(value); require(len(raw) <= MAX_STATE)
    fd, name = tempfile.mkstemp(prefix='.ledger-', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as out:
            out.write(raw); out.flush(); os.fsync(out.fileno()); os.fchmod(out.fileno(), 0o600)
        os.replace(name, path)
        fd = os.open(path.parent, os.O_RDONLY)
        try: os.fsync(fd)
        finally: os.close(fd)
    finally: Path(name).unlink(missing_ok=True)


def initialize_ledger(path, instance, empty_inventory):
    """Installer-only explicit empty baseline. No cloud calls or existing overwrite."""
    value = parse_inventory(empty_inventory, instance)
    require(not value['Events'])
    path = Path(path); private(path.parent, True)
    raw = canonical(dict(Version=1, InstanceId=instance, Baseline=base64.b64encode(empty_inventory).decode(),
                         Committed=None, Pending=None, ReservedBytes=0))
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as out: out.write(raw); out.flush(); os.fsync(out.fileno())
    fd = os.open(path.parent, os.O_RDONLY)
    try: os.fsync(fd)
    finally: os.close(fd)


class InventoryLedger:
    def __init__(self, path, instance, encrypt, remote):
        self.path = Path(path); private(self.path.parent, True)
        require(isinstance(instance, str) and re.fullmatch(r'[A-Za-z0-9_-]{8,80}', instance))
        self.instance, self.encrypt, self.remote = instance, encrypt, remote
        self.head_key = 'deletion-inventories/'+instance+'/head.json'

    def unpack(self, record):
        require(isinstance(record, dict) and set(record) == {'Inventory', 'Ciphertext', 'Head'})
        raw = base64.b64decode(record['Inventory'], validate=True)
        cipher = base64.b64decode(record['Ciphertext'], validate=True)
        require(0 < len(raw) <= MAX_INVENTORY and 64 <= len(cipher) <= MAX_CIPHER
                and cipher.startswith(b'age-encryption.org/v1\n'))
        value = parse_inventory(raw, self.instance); head = record['Head']
        require(isinstance(head, dict) and set(head) == {'Version','InstanceId','Sequence','InventoryKey',
            'CipherSha256','CipherBytes','InventorySha256','Events','CreatedUtc','PreviousHeadSha256'})
        require(type(head['Version']) is int and head['Version'] == 1 and head['InstanceId'] == self.instance
                and type(head['Sequence']) is int and 1 <= head['Sequence'] <= 99999999999999999999)
        require(head['InventoryKey'] == 'deletion-inventories/'+self.instance+'/'
                +str(head['Sequence']).zfill(20)+'-'+digest(cipher)+'.age')
        require(head['CipherSha256'] == digest(cipher) and type(head['CipherBytes']) is int
                and head['CipherBytes'] == len(cipher) and head['InventorySha256'] == digest(raw)
                and type(head['Events']) is int and head['Events'] == len(value['Events'])
                and head['CreatedUtc'] == value['CreatedUtc'] and isinstance(head['PreviousHeadSha256'], str)
                and re.fullmatch(r'[a-f0-9]{64}', head['PreviousHeadSha256']))
        return raw, cipher, head

    def read(self):
        private(self.path)
        with self.path.open('rb') as source: raw = source.read(MAX_STATE+1)
        require(len(raw) <= MAX_STATE)
        state = json.loads(raw)
        require(isinstance(state, dict) and set(state) == {'Version','InstanceId','Baseline','Committed','Pending','ReservedBytes'}
                and state['Version'] == 1 and type(state['Version']) is int
                and state['InstanceId'] == self.instance and canonical(state) == raw)
        require(type(state['ReservedBytes']) is int and 0 <= state['ReservedBytes'] <= MAX_UPLOAD_BUDGET)
        baseline = base64.b64decode(state['Baseline'], validate=True)
        require(not parse_inventory(baseline, self.instance)['Events'])
        previous_raw, previous_head = baseline, None
        for record in (state['Committed'], state['Pending']):
            if record is None: continue
            current_raw, _, head = self.unpack(record)
            monotonic_inventory(current_raw, previous_raw, digest(previous_raw), self.instance)
            if record is state['Pending']:
                require(head['Sequence'] == (previous_head['Sequence']+1 if previous_head else 1)
                        and head['PreviousHeadSha256'] == (digest(canonical(previous_head)) if previous_head else '0'*64))
            elif head['Sequence'] == 1: require(head['PreviousHeadSha256'] == '0'*64)
            previous_raw, previous_head = current_raw, head
        return state

    def transfer(self, key, raw, state):
        require(state['ReservedBytes']+len(raw) <= MAX_UPLOAD_BUDGET)
        state['ReservedBytes'] += len(raw)
        atomic(self.path, state)  # Attempts count even if the response is lost.
        self.remote.put(key, raw)
        actual = self.remote.get(key)
        require(isinstance(actual, bytes) and len(actual) == len(raw) and digest(actual) == digest(raw))

    def finish_pending(self, state):
        pending = state['Pending']; raw, cipher, head = self.unpack(pending)
        observed = self.remote.get(self.head_key)
        prior = canonical(state['Committed']['Head']) if state['Committed'] else None
        # Lost responses may leave the new head committed remotely already.
        require(observed in (prior, canonical(head)))
        if observed == canonical(head):
            actual = self.remote.get(head['InventoryKey'])
            require(isinstance(actual, bytes) and actual == cipher)
        else:
            # Cipher/key are durably cached: every retry uses the same bytes.
            self.transfer(head['InventoryKey'], cipher, state)
            self.transfer(self.head_key, canonical(head), state)
        state['Committed'], state['Pending'] = pending, None
        atomic(self.path, state)
        return head

    def publish(self, raw, *, event_id=None):
        require(isinstance(raw, bytes) and 0 < len(raw) <= MAX_INVENTORY)
        current = parse_inventory(raw, self.instance)
        if event_id is not None:
            require(isinstance(event_id, str) and any(e['EventId'] == event_id for e in current['Events']))
        state = self.read()
        if state['Pending'] is not None:
            previous_raw, _, _ = self.unpack(state['Pending'])
            monotonic_inventory(raw, previous_raw, digest(previous_raw), self.instance)
            self.finish_pending(state)
        committed = state['Committed']
        previous_raw = self.unpack(committed)[0] if committed else base64.b64decode(state['Baseline'], validate=True)
        monotonic_inventory(raw, previous_raw, digest(previous_raw), self.instance)
        observed = self.remote.get(self.head_key)
        expected = canonical(committed['Head']) if committed else None
        require(observed == expected)  # Never replace a missing, altered or regressed head.
        if committed and current['Events'] == parse_inventory(previous_raw, self.instance)['Events']:
            return dict(Status='INVENTORY_HEAD_VERIFIED', HeadSha256=digest(expected),
                        Sequence=committed['Head']['Sequence'], Events=len(current['Events']))
        cipher = self.encrypt(raw)
        require(isinstance(cipher, bytes) and cipher.startswith(b'age-encryption.org/v1\n')
                and 64 <= len(cipher) <= MAX_CIPHER)
        sequence = committed['Head']['Sequence']+1 if committed else 1
        head = dict(Version=1, InstanceId=self.instance, Sequence=sequence,
            InventoryKey='deletion-inventories/'+self.instance+'/'+str(sequence).zfill(20)+'-'+digest(cipher)+'.age',
            CipherSha256=digest(cipher), CipherBytes=len(cipher), InventorySha256=digest(raw),
            Events=len(current['Events']), CreatedUtc=current['CreatedUtc'],
            PreviousHeadSha256=digest(expected) if expected else '0'*64)
        state['Pending'] = dict(Inventory=base64.b64encode(raw).decode(),
                                Ciphertext=base64.b64encode(cipher).decode(), Head=head)
        self.unpack(state['Pending'])
        atomic(self.path, state)  # Must precede ALL writes to the provider.
        self.finish_pending(state)
        return dict(Status='INVENTORY_HEAD_VERIFIED', HeadSha256=digest(canonical(head)),
                    Sequence=sequence, Events=len(current['Events']))
