"""Synthetic-only age/cloud/restore rehearsal. No production DB or real keys.

prepare creates a NEW private directory and a one-event independent inventory.
complete consumes exported ciphertext and leaves the restored copy quarantined.
The ephemeral test identity never leaves this Mac. Cloud upload is a separate,
fixed authenticated operation against deletion-probes/, not live event history.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import uuid

from account_deletion import AccountDeletion
from deletion_journal import DeletionJournal
from deletion_event_restore import write_event_checkpoint
from deletion_restore import restore_filtered
from deploy.sakura_deletion_journal import AgeEncryptor
from maintenance import initialize, backup_generation, digest_file, write_private
from store import Store, Fault, encode, now


def require(value):
    if not value: raise ValueError('Synthetic drill validation failed')


def save(path, value):
    write_private(path, encode(value) + '\n')


def save_bytes(path, raw):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as out:
        out.write(raw); out.flush(); os.fsync(out.fileno())


def fault(status, fn):
    try: fn()
    except Fault as error:
        require(error.status == status); return
    raise ValueError('Expected operation to be rejected')


def prepare(root, age):
    root, age = Path(root).absolute(), Path(age).absolute()
    require(not any(p.is_symlink() for p in (root, *root.parents)))
    root.mkdir(mode=0o700)  # Never reuse/overwrite a prior run or existing DB.
    instance = 'drill-' + uuid.uuid4().hex
    save(root / 'DRILL.json', {'Kind': 'SYNTHETIC_ONLY', 'InstanceId': instance})
    identity = root / 'synthetic-identity.txt'
    subprocess.run([str(age.with_name('age-keygen')), '-o', str(identity)],
                   capture_output=True, check=True, timeout=15)
    identity.chmod(0o600)
    public = subprocess.run([str(age.with_name('age-keygen')), '-y', str(identity)],
                            capture_output=True, check=True, timeout=15).stdout
    save_bytes(root / 'recipient.txt', public)
    fixture = root / 'fixture'; initialize(fixture, instance)
    store = Store(fixture / 'players.sqlite')
    player, other = uuid.uuid4().hex, uuid.uuid4().hex
    token, other_token = 'a' * 64, 'b' * 64
    store.register(player, token); store.register(other, other_token)
    save_data = dict(PlayerId=player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
                     SchemaVersion=3, PlayerLevel=1, Gold=10, FreeGachaStones=900, PaidGachaStones=0,
                     OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100)
    store.snapshot(player, save_data); store.snapshot(other, dict(save_data, PlayerId=other))
    journal = DeletionJournal(store, instance); api = AccountDeletion(store, journal=journal)
    with store.connect() as db:
        db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', ('apple', 'drill-before', player, 'synthetic-product', now()))
        db.execute('INSERT INTO flags(player,code,received,detail) VALUES(?,?,?,?)', (player, 'fixture', now(), 'synthetic'))
    backup = backup_generation(store.path, root / 'backups')
    with store.connect() as db:
        db.execute('INSERT INTO purchases VALUES(?,?,?,?,?)', ('apple', 'drill-after', player, 'synthetic-product', now()))
    confirmation = api.preview(player, token)['ConfirmationToken']
    fault(503, lambda: api.commit(player, token, confirmation))
    ticket = journal.claim(AgeEncryptor(age, root / 'recipient.txt'))
    require(ticket is not None)
    with store.connect() as db:
        payload = db.execute('SELECT payload FROM deletion_outbox WHERE event_id=?', (ticket['event_id'],)).fetchone()[0].encode()
    inventory = dict(Version=1, InstanceId=instance, CreatedUtc=now(), Events=[dict(
        EventId=ticket['event_id'], Bytes=len(payload), Sha256=hashlib.sha256(payload).hexdigest())])
    inventory_raw = (encode(inventory) + '\n').encode()
    save_bytes(root / 'independent-inventory.json', inventory_raw)
    save_bytes(root / 'outgoing.age', ticket['ciphertext'])
    transfer = dict(Kind='SYNTHETIC_DELETION_PROBE', Key='deletion-probes/' + uuid.uuid4().hex + '.age',
                    Sha256=hashlib.sha256(ticket['ciphertext']).hexdigest(), Bytes=len(ticket['ciphertext']))
    save(root / 'transfer.json', transfer)
    # Retained independently BEFORE the external transfer; never trust a
    # downloaded manifest to tell us which deletion events ought to exist.
    save(root / 'prepared.json', dict(Kind='SYNTHETIC_ONLY', InstanceId=instance,
        Player=player, Other=other, Token=token, OtherToken=other_token, Confirmation=confirmation,
        OtherBefore=store.inspect(other), EventId=ticket['event_id'], Lease=ticket['lease'],
        Backup=backup.name, BackupSha256=digest_file(backup), Transfer=transfer,
        InventorySha256=hashlib.sha256(inventory_raw).hexdigest()))
    return {'Status': 'SYNTHETIC_DRILL_PREPARED', 'CipherBytes': len(ticket['ciphertext']),
            'ProductionDataUsed': False, 'PrivateIdentityExported': False}


def complete(root, age):
    root, age = Path(root).absolute(), Path(age).absolute()
    require(not any(p.is_symlink() for p in (root, *root.parents)))
    require(not any(p.is_symlink() for p in root.rglob('*')))
    state = json.loads((root / 'prepared.json').read_text())
    marker = json.loads((root / 'DRILL.json').read_text())
    require(marker == {'Kind': 'SYNTHETIC_ONLY', 'InstanceId': state['InstanceId']})
    require(state['Kind'] == 'SYNTHETIC_ONLY' and re.fullmatch(r'drill-[a-f0-9]{32}', state['InstanceId']))
    require(re.fullmatch(r'players-\d{8}T\d{12}Z-[a-f0-9]{8}\.sqlite', state['Backup']))
    require(not (root / 'verified.json').exists())
    receipt = json.loads((root / 'cloud-return/receipt.json').read_text())
    require(receipt == dict(state['Transfer'], Status='SYNTHETIC_PROBE_READBACK_VERIFIED'))
    with (root / 'cloud-return/readback.age').open('rb') as source: cipher = source.read(65537)
    require(len(cipher) == state['Transfer']['Bytes'] <= 65536
            and hashlib.sha256(cipher).hexdigest() == state['Transfer']['Sha256'])
    plaintext = subprocess.run([str(age), '-d', '-i', str(root / 'synthetic-identity.txt')],
                               input=cipher, capture_output=True, check=True, timeout=15).stdout
    inventory = (root / 'independent-inventory.json').read_bytes()
    checkpoint = root / 'retrieved-checkpoint.json'
    digest = write_event_checkpoint(inventory, state['InventorySha256'], state['InstanceId'],
                                    {state['EventId']: plaintext}, checkpoint)
    fixture = root / 'fixture'; store = Store(fixture / 'players.sqlite')
    journal = DeletionJournal(store, state['InstanceId'], initialize=False)
    api = AccountDeletion(store, journal=journal)
    require(journal.finish(state['EventId'], state['Lease'], True) == 'verified')
    require(api.status(state['Player'], state['Token'], state['Confirmation'])['Status'] == 'deleted')
    backup = root / 'backups' / state['Backup']; destination = root / 'quarantined-restore'
    require(digest_file(backup) == state['BackupSha256'])
    result = restore_filtered(backup, checkpoint, digest, state['InstanceId'], destination)
    recovered = Store(destination / 'players.sqlite')
    require(result['RemovedPlayers'] == 1 and recovered.inspect(state['Other']) == state['OtherBefore'])
    fault(410, lambda: recovered.register(state['Player'], state['Token']))
    fault(401, lambda: recovered.authenticate(state['Player'], state['Token']))
    recovered.purchase_verifier = lambda request: dict(verified=True, store='apple',
        transaction=request['TransactionId'], product=request['Target'])
    fault(409, lambda: recovered.operation(state['Other'], dict(Kind='purchase', Epoch=0,
        RequestId=uuid.uuid4().hex, Target='com.nasus.dungeonmonsterroguelike.crystals120',
        TransactionId='drill-after', Receipt='synthetic'), token=state['OtherToken']))
    with recovered.connect() as db:
        for table in ('snapshots', 'operations', 'claims', 'flags', 'audit', 'purchases'):
            require(db.execute('SELECT count(*) FROM '+table+' WHERE player=?', (state['Player'],)).fetchone()[0] == 0)
        require({r[0] for r in db.execute('SELECT transaction_hash FROM retired_purchases')}
                == {Store.transaction_hash('apple', t) for t in ('drill-before', 'drill-after')})
    require(digest_file(backup) == state['BackupSha256'])
    require((destination / 'RECOVERY-PENDING.txt').is_file() and not (destination / 'instance-id').exists())
    report = dict(Status='SYNTHETIC_DELETION_RESTORE_VERIFIED', RemovedSyntheticPlayers=1,
                  SurvivorUnchanged=True, OldCredentialsRejected=True, RetiredPurchasesPreserved=True,
                  RetiredPurchaseReuseRejected=True,
                  BackupUnchanged=True, RestoreStillQuarantined=True, ProductionDataUsed=False)
    save(root / 'verified.json', report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('prepare', 'complete'))
    parser.add_argument('--root', required=True); parser.add_argument('--age', required=True)
    args = parser.parse_args()
    os.umask(0o077)
    report = (prepare if args.action == 'prepare' else complete)(Path(args.root), Path(args.age))
    print(json.dumps(report, sort_keys=True))


if __name__ == '__main__':
    try: main()
    except Exception:
        print('SYNTHETIC_DRILL_STOPPED: artifacts retained; no private exception details printed.')
        raise SystemExit(1)
