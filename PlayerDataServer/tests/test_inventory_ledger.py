import base64
import copy
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import tempfile
import subprocess
import unittest
from contextlib import ExitStack
from types import SimpleNamespace
from unittest.mock import patch

from deletion_inventory_ledger import InventoryLedger, initialize_ledger, canonical, digest
from deletion_inventory import inspect_inventory
from deletion_event_restore import checkpoint_from_events
from deploy.sakura_deletion_journal import AgeEncryptor
from deploy.sakura_inventory_store import SakuraInventoryStore
from account_deletion import AccountDeletion
from deletion_journal import DeletionJournal
from deploy.deletion_worker import run_once, atomic_json
from maintenance import initialize
from store import Store, Fault


class Remote:
    def __init__(self): self.objects, self.writes = {}, []
    def get(self, key): return self.objects.get(key)
    def put(self, key, data): self.writes.append((key, data)); self.objects[key] = data


class LedgerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve(); self.path = self.root/'ledger.json'
        self.instance = 'inventory-ledger-fixture'
        self.base = dict(Version=1, InstanceId=self.instance, CreatedUtc='2026-09-26T00:00:00+00:00', Events=[])
        self.remote = Remote(); self.encrypted = []
        initialize_ledger(self.path, self.instance, canonical(self.base))
        def encrypt(raw):
            self.encrypted.append(raw)
            return b'age-encryption.org/v1\n'+hashlib.sha256(raw+str(len(self.encrypted)).encode()).hexdigest().encode()
        self.ledger = InventoryLedger(self.path, self.instance, encrypt, self.remote)

    def current(self, count=1):
        return canonical(dict(self.base, CreatedUtc='2026-09-26T00:01:00+00:00', Events=[
            dict(EventId=hex(i+10)[2:]*32, Sha256=hex(i+10)[2:]*64, Bytes=300) for i in range(count)]))

    def test_explicit_empty_initialization_only_no_overwrite(self):
        with self.assertRaises(FileExistsError): initialize_ledger(self.path, self.instance, canonical(self.base))
        with self.assertRaises(ValueError): initialize_ledger(self.root/'other', self.instance, self.current())
        self.assertEqual(0o600, self.path.stat().st_mode & 0o777)

    def test_initial_head_and_cipher_readback_then_idle_no_reupload(self):
        receipt = self.ledger.publish(canonical(self.base))
        self.assertEqual(1, receipt['Sequence']); self.assertEqual(2, len(self.remote.writes))
        old = list(self.remote.writes)
        self.ledger.publish(canonical(self.base)); self.assertEqual(old, self.remote.writes)

    def test_pending_is_durable_before_any_put(self):
        original = self.remote.put
        def put(key, raw):
            saved = json.loads(self.path.read_bytes())
            self.assertIsNotNone(saved['Pending']); self.assertIsNone(saved['Committed'])
            original(key, raw)
        self.remote.put = put
        self.ledger.publish(self.current(), event_id='a'*32)

    def test_event_absence_is_rejected_without_network_writes(self):
        with self.assertRaises(ValueError): self.ledger.publish(canonical(self.base), event_id='a'*32)
        self.assertEqual([], self.remote.writes)

    def test_hash_chain_and_monotonic_entries(self):
        first = self.ledger.publish(self.current())
        second = self.ledger.publish(self.current(2), event_id='b'*32)
        head = json.loads(self.remote.get(self.ledger.head_key))
        self.assertEqual(first['HeadSha256'], head['PreviousHeadSha256'])
        self.assertEqual(2, second['Sequence'])
        with self.assertRaises(ValueError): self.ledger.publish(self.current())

    def test_missing_changed_or_old_head_stops_no_overwrite(self):
        self.ledger.publish(self.current()); first = self.remote.get(self.ledger.head_key)
        self.ledger.publish(self.current(2)); total = len(self.remote.writes)
        for wrong in (None, b'corrupt', first):
            if wrong is None: self.remote.objects.pop(self.ledger.head_key, None)
            else: self.remote.objects[self.ledger.head_key] = wrong
            with self.assertRaises(ValueError): self.ledger.publish(self.current(2))
            self.assertEqual(total, len(self.remote.writes))

    def test_lost_cipher_put_response_retries_exact_cached_cipher(self):
        original = self.remote.put
        def lost(key, raw): original(key, raw); raise RuntimeError('response lost')
        self.remote.put = lost
        with self.assertRaises(RuntimeError): self.ledger.publish(self.current())
        first = self.remote.writes[0]
        self.remote.put = original; self.ledger.publish(self.current())
        self.assertEqual(first, self.remote.writes[1]); self.assertEqual(1, len(self.encrypted))

    def test_lost_head_response_recovers_without_reencryption_or_rewrite(self):
        original = self.remote.put
        def lost(key, raw):
            original(key, raw)
            if key.endswith('head.json'): raise RuntimeError('response lost')
        self.remote.put = lost
        with self.assertRaises(RuntimeError): self.ledger.publish(self.current())
        self.remote.put = original
        result = self.ledger.publish(self.current())
        self.assertEqual(1, result['Sequence']); self.assertEqual(2, len(self.remote.writes))
        self.assertEqual(1, len(self.encrypted))

    def test_local_commit_failure_after_remote_head_recovers(self):
        from deletion_inventory_ledger import atomic
        calls = []
        def fail_commit(path, value):
            calls.append(value)
            if value['Committed'] is not None: raise OSError('local disk')
            atomic(path, value)
        with patch('deletion_inventory_ledger.atomic', fail_commit):
            with self.assertRaises(OSError): self.ledger.publish(self.current())
        result = self.ledger.publish(self.current())
        self.assertEqual(1, result['Sequence']); self.assertEqual(2, len(self.remote.writes))

    def test_missing_cipher_after_head_write_does_not_commit_local_state(self):
        original = self.remote.put
        def lost(key, raw):
            original(key, raw)
            if key.endswith('head.json'): raise RuntimeError('lost')
        self.remote.put = lost
        with self.assertRaises(RuntimeError): self.ledger.publish(self.current())
        head = json.loads(self.remote.get(self.ledger.head_key))
        del self.remote.objects[head['InventoryKey']]
        self.remote.put = original
        with self.assertRaises(ValueError): self.ledger.publish(self.current())
        self.assertIsNone(json.loads(self.path.read_bytes())['Committed'])

    def test_missing_local_state_never_implicitly_bootstraps(self):
        self.path.unlink()
        with self.assertRaises(FileNotFoundError): self.ledger.publish(self.current())
        self.assertEqual([], self.remote.writes)

    def test_existing_remote_head_rejects_new_local_empty_baseline(self):
        self.remote.objects[self.ledger.head_key] = b'existing'
        with self.assertRaises(ValueError): self.ledger.publish(self.current())
        self.assertEqual([], self.remote.writes)

    def test_permission_error_stops_without_fallback(self):
        self.remote.get = lambda key: (_ for _ in ()).throw(PermissionError('403'))
        with self.assertRaises(PermissionError): self.ledger.publish(self.current())
        self.assertEqual([], self.remote.writes)

    def test_pending_recovered_then_new_events_get_next_generation(self):
        original = self.remote.put
        self.remote.put = lambda *_: (_ for _ in ()).throw(RuntimeError('offline'))
        with self.assertRaises(RuntimeError): self.ledger.publish(self.current())
        self.remote.put = original
        self.assertEqual(2, self.ledger.publish(self.current(2), event_id='b'*32)['Sequence'])

    def test_bad_cipher_or_changed_event_stops(self):
        self.ledger.encrypt = lambda _: b'plaintext'
        with self.assertRaises(ValueError): self.ledger.publish(self.current())
        self.assertEqual([], self.remote.writes)

    def test_upload_budget_is_reserved_before_transfer_and_not_reset(self):
        from deletion_inventory_ledger import MAX_UPLOAD_BUDGET
        saved=json.loads(self.path.read_bytes()); saved['ReservedBytes']=MAX_UPLOAD_BUDGET
        self.path.write_bytes(canonical(saved))
        with self.assertRaises(ValueError): self.ledger.publish(self.current())
        self.assertEqual([],self.remote.writes)
        self.assertEqual(MAX_UPLOAD_BUDGET,json.loads(self.path.read_bytes())['ReservedBytes'])

    @unittest.skipUnless(os.environ.get('NASUS_TEST_AGE'), 'Explicit disposable age test runtime required')
    def test_real_encrypted_inventory_roundtrip_and_missing_event_detection(self):
        age=Path(os.environ['NASUS_TEST_AGE']); identity=self.root/'identity'
        subprocess.run([str(age.with_name('age-keygen')),'-o',str(identity)],capture_output=True,check=True)
        recipient=subprocess.check_output([str(age.with_name('age-keygen')),'-y',str(identity)])
        public=self.root/'recipient'; public.write_bytes(recipient); public.chmod(0o600)
        self.ledger.encrypt=AgeEncryptor(age,public)
        event=canonical(dict(Version=1,InstanceId=self.instance,EventId='a'*32,
            PlayerHash='b'*64,DeletedUtc='2026-09-26T00:00:00+00:00',RetiredPurchases=[]))
        raw=canonical(dict(self.base,Events=[dict(EventId='a'*32,Sha256=digest(event),Bytes=len(event))]))
        receipt=self.ledger.publish(raw,event_id='a'*32)
        head=json.loads(self.remote.get(self.ledger.head_key)); cipher=self.remote.get(head['InventoryKey'])
        result=subprocess.run([str(age),'-d','-i',str(identity)],input=cipher,capture_output=True,check=True)
        self.assertEqual(raw,result.stdout)
        from deletion_recovery_evidence import verify_publication
        self.assertEqual(head,verify_publication(self.remote.get(self.ledger.head_key),
            receipt['HeadSha256'],self.instance,cipher,result.stdout))
        checkpoint=checkpoint_from_events(result.stdout,head['InventorySha256'],self.instance,{'a'*32:event})
        self.assertEqual(1,len(json.loads(checkpoint)['DeletedPlayers']))
        with self.assertRaises(ValueError): checkpoint_from_events(result.stdout,head['InventorySha256'],self.instance,{})


class InventoryStoreTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.config = Path(self.temp.name)/'curl.conf'; self.config.touch(mode=0o600)
        self.key = 'deletion-inventories/fixture-001/head.json'

    def test_get_404_only_is_absence_other_errors_stop(self):
        class Result: returncode=0; stdout=b'\n404'
        store = SakuraInventoryStore(self.config, run=lambda *a, **kw: Result())
        self.assertIsNone(store.get(self.key))
        for status in (b'403', b'500', b'301'):
            Result.stdout = b'\n'+status
            with self.assertRaises(RuntimeError): store.get(self.key)

    def test_fixed_prefix_and_no_plaintext_cipher_upload(self):
        calls=[]
        store = SakuraInventoryStore(self.config, run=lambda *a, **kw: calls.append(a))
        for key in ('db/test.age', 'deletions/fixture-001/test', '../head.json'):
            with self.assertRaises(ValueError): store.get(key)
        with self.assertRaises(ValueError):
            store.put('deletion-inventories/fixture-001/'+'0'*20+'-'+'a'*64+'.age', b'secret')
        self.assertEqual([], calls)

    def test_put_private_no_retry_and_get_raw_body(self):
        calls=[]
        class Result: returncode=0; stdout=b'cipher\n200'
        def run(args, **kwargs): calls.append((args,kwargs)); return Result()
        store = SakuraInventoryStore(self.config, run=run)
        self.assertEqual(b'cipher', store.get(self.key))
        key='deletion-inventories/fixture-001/'+'0'*20+'-'+'a'*64+'.age'
        store.put(key, b'age-encryption.org/v1\n'+b'x'*64)
        args, kw = calls[-1]
        self.assertIn('x-amz-acl: private', args)
        self.assertEqual('0', args[args.index('--retry')+1]); self.assertNotIn('--location', args)
        self.assertEqual(b'age-encryption.org/v1\n'+b'x'*64, kw['input'])


class InventoryAcknowledgementTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve(); self.data = self.root/'data'; self.state = self.root/'state'
        self.state.mkdir(mode=0o700); self.instance='inventory-ack-fixture'
        initialize(self.data, self.instance); self.store=Store(self.data/'players.sqlite')
        self.store.register('a'*32, 'b'*64)
        self.journal=DeletionJournal(self.store, self.instance)
        self.api=AccountDeletion(self.store, journal=self.journal)
        token=self.api.preview('a'*32, 'b'*64)['ConfirmationToken']
        self.confirm=token
        with self.assertRaises(Fault): self.api.commit('a'*32, 'b'*64, token)
        self.config=self.root/'config'; self.config.mkdir(mode=0o700)
        (self.config/'recipient.txt').write_text('synthetic')
        atomic_json(self.state/'budget.json', {'ReservedBytes':0})

    def run_worker(self, callback):
        with ExitStack() as stack:
            stack.enter_context(patch('deploy.deletion_worker.private_file', return_value=True))
            stack.enter_context(patch('deploy.deletion_worker.CONFIG', self.config))
            stack.enter_context(patch('deploy.deletion_worker.as_database_user', side_effect=lambda account, fn: fn()))
            stack.enter_context(patch('deploy.deletion_worker.encrypt_public', side_effect=lambda *a: b'age-encryption.org/v1\n'+b'x'*64))
            stack.enter_context(patch('deploy.deletion_worker.SakuraPublisher', side_effect=lambda config: lambda key, cipher: digest(cipher)))
            return run_once(SimpleNamespace(), directory=self.data, state=self.state, inventory_commit=callback)

    def test_inventory_confirmed_before_finish_and_deleted_response(self):
        def commit(event_id):
            with self.store.connect() as db:
                row=db.execute('SELECT event_id,state,verified_at FROM deletion_outbox').fetchone()
                self.assertEqual((event_id,'sending',None), tuple(row))
            with self.assertRaises(Fault): self.api.status('a'*32, 'b'*64, self.confirm)
            return {'Status':'INVENTORY_HEAD_VERIFIED'}
        self.run_worker(commit)
        self.assertEqual('deleted', self.api.status('a'*32,'b'*64,self.confirm)['Status'])

    def test_inventory_failure_never_acknowledges_event_only_success(self):
        def fail(event_id): raise RuntimeError('inventory upload failed')
        with self.assertRaises(RuntimeError): self.run_worker(fail)
        with self.assertRaises(Fault): self.api.status('a'*32,'b'*64,self.confirm)
        with self.store.connect() as db:
            self.assertEqual(('pending',None), tuple(db.execute('SELECT state,verified_at FROM deletion_outbox').fetchone()))
        self.assertFalse(json.loads((self.state/'status.json').read_bytes())['Healthy'])

    def test_unverified_inventory_response_rejected(self):
        with self.assertRaises(RuntimeError): self.run_worker(lambda _: {'Status':'UPLOADED'})
        with self.assertRaises(Fault): self.api.status('a'*32,'b'*64,self.confirm)

    def test_idle_still_checks_inventory_head(self):
        self.run_worker(lambda _: {'Status':'INVENTORY_HEAD_VERIFIED'})
        ids=[]
        self.run_worker(lambda event: ids.append(event) or {'Status':'INVENTORY_HEAD_VERIFIED'})
        self.assertEqual([None], ids)
