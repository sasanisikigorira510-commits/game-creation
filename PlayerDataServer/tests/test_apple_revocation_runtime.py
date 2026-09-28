import fcntl
import json
import os
from pathlib import Path
import secrets
import sqlite3
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
from apple_grants import AppleGrantVault
from apple_tokens import AppleGrant
from apple_revocation_runtime import execute, load_components
from apple_revocation_health import healthy_runtime
from account_linking import AccountLinking
from store import Store, Fault


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        temp=tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root=Path(temp.name).resolve(); self.data=self.root/'data'; self.data.mkdir(mode=0o700)
        self.state=self.root/'state'; self.state.mkdir(mode=0o700)
        self.store=Store(self.data/'players.sqlite'); self.key=secrets.token_bytes(32)
        self.vault=AppleGrantVault(self.store,self.key,'com.test.game')
        verifier=lambda *_:'subject'; verifier.audience='com.test.game'
        AccountLinking(self.store,verifier)
        key=ec.generate_private_key(ec.SECP256R1())
        self.write(self.root/'signing.pem',key.private_bytes(serialization.Encoding.PEM,
            serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
        self.write(self.root/'encryption.key',self.key)
        self.write(self.data/'instance-id',b'runtime-fixture-01\n')
        for path in self.data.iterdir(): path.chmod(0o600)
        self.config=dict(Version=1,InstanceId='runtime-fixture-01',ClientId='com.test.game',
            TeamId='ABCDEFGHIJ',KeyId='0123456789',DataDirectory=str(self.data),
            SigningKeyFile=str(self.root/'signing.pem'),TokenEncryptionKeyFile=str(self.root/'encryption.key'),
            StateDirectory=str(self.state))
        self.config_path=self.root/'config.json'; self.save_config()
        self.calls=[]; self.client=SimpleNamespace(client_id='com.test.game',revoke=self.calls.append)

    def write(self,path,raw): path.write_bytes(raw); path.chmod(0o600)
    def save_config(self): self.write(self.config_path,json.dumps(self.config).encode())
    def queued(self):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            self.vault.save(db,AppleGrant('fixture-subject','fixture-secret'))
            self.vault.queue(db,self.vault.subject_hash('fixture-subject'))
        for path in self.data.iterdir(): path.chmod(0o600)

    def injected(self,path,readonly=True):
        vault,_,state=load_components(path,readonly=readonly)
        return vault,self.client,state

    def test_default_check_has_no_network_db_changes_or_state_files(self):
        self.queued()
        with self.store.connect() as db: before=list(db.iterdump())
        with patch('apple_tokens.AppleTokenClient.revoke',side_effect=AssertionError('no network')):
            result=execute(self.config_path)
        self.assertEqual('APPLE_WORKER_PREFLIGHT_PASSED',result['Status'])
        self.assertFalse(result['Activated']); self.assertEqual([],list(self.state.iterdir()))
        with self.store.connect() as db: self.assertEqual(before,list(db.iterdump()))

    def test_explicit_run_only_uses_existing_queue_and_private_status(self):
        self.queued(); result=execute(self.config_path,run=True,components=self.injected)
        self.assertTrue(result['Healthy']); self.assertEqual(['fixture-secret'],self.calls)
        raw=(self.state/'status.json').read_bytes(); self.assertNotIn(b'fixture-secret',raw)
        self.assertEqual(0o600,(self.state/'status.json').stat().st_mode & 0o777)

    def test_provider_failure_preserves_queue_and_unhealthy_status(self):
        self.queued(); self.client.revoke=lambda _: (_ for _ in ()).throw(Fault(503,'private'))
        result=execute(self.config_path,run=True,components=self.injected)
        self.assertFalse(result['Healthy']); self.assertEqual(1,result['Queue']['Pending'])
        self.assertFalse(json.loads((self.state/'status.json').read_bytes())['Healthy'])

    def test_missing_database_is_never_created(self):
        (self.data/'players.sqlite').unlink()
        with self.assertRaises((ValueError,OSError)): execute(self.config_path)
        self.assertFalse((self.data/'players.sqlite').exists())

    def test_missing_schema_is_never_created(self):
        with self.store.connect() as db: db.execute('DROP TABLE apple_exchanges')
        with self.assertRaises(sqlite3.Error): execute(self.config_path)
        with self.store.connect() as db:
            self.assertIsNone(db.execute("SELECT name FROM sqlite_master WHERE name='apple_exchanges'").fetchone())

    def test_readonly_vault_missing_tables_never_initializes(self):
        other=Store(self.root/'other.sqlite')
        with self.assertRaises(sqlite3.Error): AppleGrantVault(other,self.key,'com.test.game',must_exist=True)
        with other.connect() as db:
            self.assertIsNone(db.execute("SELECT name FROM sqlite_master WHERE name='apple_grants'").fetchone())

    def test_instance_mismatch_quarantine_and_foreign_app_rejected(self):
        self.config['InstanceId']='different-instance'; self.save_config()
        with self.assertRaises(ValueError): execute(self.config_path)
        self.config['InstanceId']='runtime-fixture-01'; self.save_config()
        marker=self.data/'RECOVERY-PENDING.txt'; marker.touch()
        with self.assertRaises(ValueError): execute(self.config_path)
        marker.unlink(); self.queued()
        with self.store.connect() as db: db.execute("UPDATE apple_grants SET client_id='other.app'")
        with self.assertRaises(ValueError): execute(self.config_path)

    def test_shared_key_permissions_and_symlink_rejected(self):
        path=self.root/'encryption.key'; path.chmod(0o644)
        with self.assertRaises(ValueError): execute(self.config_path)
        path.chmod(0o600); link=self.root/'linked.key'; link.symlink_to(path)
        self.config['TokenEncryptionKeyFile']=str(link); self.save_config()
        with self.assertRaises(ValueError): execute(self.config_path)

    def test_missing_or_wrong_keys_not_regenerated(self):
        path=self.root/'encryption.key'; path.unlink()
        with self.assertRaises(OSError): execute(self.config_path)
        self.assertFalse(path.exists()); self.write(path,b'x'*31)
        with self.assertRaises(ValueError): execute(self.config_path)
        self.write(path,self.key)
        self.write(self.root/'signing.pem',ec.generate_private_key(ec.SECP384R1()).private_bytes(
            serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
        with self.assertRaises(ValueError): execute(self.config_path)

    def test_duplicate_and_extra_configuration_fields_rejected(self):
        raw=json.dumps(self.config)[:-1]+',"Version":1}'
        self.write(self.config_path,raw.encode())
        with self.assertRaises(ValueError): execute(self.config_path)
        self.config['AutoInitialize']=True; self.save_config()
        with self.assertRaises(ValueError): execute(self.config_path)

    def test_private_lock_collision_does_not_start_another_batch(self):
        self.queued(); lock=self.state/'worker.lock'
        fd=os.open(lock,os.O_CREAT|os.O_RDWR,0o600); self.addCleanup(os.close,fd)
        fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
        with self.assertRaises(BlockingIOError): execute(self.config_path,run=True,components=self.injected)
        self.assertEqual([],self.calls); self.assertFalse((self.state/'status.json').exists())

    def test_interrupted_batch_does_not_leave_healthy_marker(self):
        with patch('apple_revocation_runtime.run_batch',side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt): execute(self.config_path,run=True,components=self.injected)
        self.assertFalse(json.loads((self.state/'status.json').read_bytes())['Healthy'])

    def test_health_requires_fresh_success_and_current_enabled_units(self):
        marker=self.state/'status.json'; self.write(marker,b'{"Healthy":true,"CheckedUnix":1000}')
        service=dict(LoadState='loaded',ActiveState='inactive',Result='success',ExecMainStatus='0')
        timer=dict(LoadState='loaded',ActiveState='active',UnitFileState='enabled')
        self.assertTrue(healthy_runtime(marker,service,timer,now=1001))
        for changes in ({'Result':'exit-code'},{'ActiveState':'failed'},{'ExecMainStatus':'1'},
                        {'LoadState':'not-found'},{'ActiveState':'activating'}):
            self.assertFalse(healthy_runtime(marker,dict(service,**changes),timer,now=1001))
        for changes in ({'ActiveState':'inactive'},{'UnitFileState':'disabled'}, {'LoadState':'not-found'}):
            self.assertFalse(healthy_runtime(marker,service,dict(timer,**changes),now=1001))
        for now in (999,1181): self.assertFalse(healthy_runtime(marker,service,timer,now=now))
        self.assertFalse(healthy_runtime(marker,None,timer,now=1001))
        self.write(marker,b'{"Healthy":false,"CheckedUnix":1000}')
        self.assertFalse(healthy_runtime(marker,service,timer,now=1001))

    def test_release_contains_only_code_and_invalid_example_not_real_config(self):
        from build_release import FILES
        self.assertIn('run_apple_revocation.py',FILES)
        self.assertIn('apple_revocation_health.py',FILES)
        root=Path(__file__).resolve().parents[1]
        example=json.loads((root/'deploy/apple-worker.json.example').read_text())
        self.assertEqual(set(self.config),set(example))
        self.assertTrue(example['KeyId'].startswith('REPLACE_'))
        self.assertNotIn('deploy/worker.json',FILES)


if __name__ == '__main__': unittest.main()
