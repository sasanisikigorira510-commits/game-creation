import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sqlite3
import tarfile
import time
import unittest
from unittest.mock import patch

import refund_lab_runtime as runtime
from refund_integrity import validate_refunds
from tests import test_apple_revocation_runtime as fixture

spec = importlib.util.spec_from_file_location('refund_lab_installer', Path(__file__).parents[1]/'deploy/install_refund_lab.py')
installer = importlib.util.module_from_spec(spec); spec.loader.exec_module(installer)


class RefundLabTests(unittest.TestCase):
    def setUp(self):
        self.f = fixture.RuntimeTests(); self.f.setUp(); self.addCleanup(self.f.doCleanups)
        self.root = self.f.root/'lab'; self.root.mkdir(mode=0o700)
        self.credentials = self.f.root/'lab-credentials'; self.credentials.mkdir(mode=0o700)
        self.state = self.root/'identity-state'; self.state.mkdir(mode=0o700)
        identity = dict(self.f.config, InstanceId=runtime.INSTANCE, DataDirectory=str(self.root/'data'),
                        StateDirectory=str(self.state), TokenEncryptionKeyFile=str(self.credentials/'encryption.key'))
        self.f.write(self.root/'identity.json', json.dumps(identity).encode())
        self.f.write(self.credentials/'encryption.key', b'k'*32)
        now = time.time()
        self.gate = dict(Version=1,TokenSha256='0'*64,IssuedUnix=now-1,ExpiresUnix=now+100)
        self.f.write(self.credentials/'gate.json',json.dumps(self.gate).encode())
        for name,value in [('ROOT',self.root),('CREDENTIALS',self.credentials)]:
            p = patch.object(runtime,name,value); p.start(); self.addCleanup(p.stop)

    def test_fresh_bootstrap_creates_complete_empty_private_database(self):
        before = self.f.store.path
        with self.f.store.connect() as db: original = list(db.iterdump())
        with patch('requests.sessions.Session.request',side_effect=AssertionError('no network')):
            runtime.initialize_empty()
        with sqlite3.connect(self.root/'data/players.sqlite') as db:
            self.assertTrue(validate_refunds(db))
            self.assertEqual(0,db.execute('SELECT count(*) FROM players').fetchone()[0])
            self.assertEqual(0,db.execute('SELECT count(*) FROM purchases').fetchone()[0])
            self.assertEqual(runtime.INSTANCE,db.execute('SELECT instance FROM deletion_journal_meta').fetchone()[0])
        self.assertTrue((self.root/'data/SANDBOX-ONLY.json').is_file())
        self.assertEqual(0o600,(self.credentials/'operator-token').stat().st_mode & 0o777)
        with self.f.store.connect() as db: self.assertEqual(original,list(db.iterdump()))

    def test_second_bootstrap_refuses_without_overwrite(self):
        runtime.initialize_empty()
        database = self.root/'data/players.sqlite'; before = database.read_bytes()
        with self.assertRaises(ValueError): runtime.initialize_empty()
        self.assertEqual(before,database.read_bytes())

    def test_factories_use_only_fixed_lab_configuration(self):
        from types import SimpleNamespace
        sent = []
        def build(env): sent.append(env); return SimpleNamespace(device='d',review='r',notifications='n')
        with patch.object(runtime,'create_sandbox_apps',build), patch.dict('os.environ',{'WITCH_DATA_DIR':'/wrong'}):
            self.assertEqual(('d','r','n'),(runtime.device(),runtime.review(),runtime.notifications()))
        self.assertTrue(all(env['WITCH_DATA_DIR']==str(self.root/'data') for env in sent))

    def test_maintenance_checks_actual_backup_and_empty_identity_queue(self):
        runtime.initialize_empty()
        with patch('requests.sessions.Session.request',side_effect=AssertionError('no network')):
            runtime.maintain()
        value = json.loads((self.root/'backup-status.json').read_text())
        self.assertTrue(value['Healthy'])
        self.assertTrue(list((self.root/'backups').glob('*.sqlite')))
        self.assertTrue(json.loads((self.state/'status.json').read_text())['Healthy'])

    def test_expired_lab_never_runs_workers(self):
        self.gate.update(IssuedUnix=time.time()-200,ExpiresUnix=time.time()-100)
        self.f.write(self.credentials/'gate.json',json.dumps(self.gate).encode())
        with patch.object(runtime,'revoke',side_effect=AssertionError('no revoke')), \
             patch.object(runtime,'reconcile_batch',side_effect=AssertionError('no Apple')):
            with self.assertRaises(ValueError): runtime.maintain()
            with self.assertRaises(ValueError): runtime.reconcile()

    def test_unhealthy_worker_cannot_report_success(self):
        with patch.object(runtime,'reconcile_batch',return_value={'Healthy':False}):
            with self.assertRaises(ValueError): runtime.reconcile()

    def test_package_hash_and_member_validation(self):
        from build_release import build
        archive = self.f.root/'code.tar.gz'; build(archive); raw = archive.read_bytes()
        files = installer.package(raw,hashlib.sha256(raw).hexdigest())
        self.assertIn('refund_lab_runtime.py',files)
        with self.assertRaises(ValueError): installer.package(raw,'0'*64)
        for name,kind in [('../escape',tarfile.REGTYPE),('/absolute',tarfile.REGTYPE),('link',tarfile.SYMTYPE)]:
            output = io.BytesIO()
            with tarfile.open(fileobj=output,mode='w:gz') as stream:
                info = tarfile.TarInfo(name); info.type = kind; info.linkname = '/etc/passwd'
                stream.addfile(info)
            raw = output.getvalue()
            with self.assertRaises(ValueError): installer.package(raw,hashlib.sha256(raw).hexdigest())

    def test_private_writes_never_overwrite_and_units_are_isolated(self):
        path = self.root/'fixture'
        installer.write(path,b'first')
        with self.assertRaises(FileExistsError): installer.write(path,b'second')
        self.assertEqual(b'first',path.read_bytes())
        for text in [installer.unit('/fixture'),installer.unit('/fixture',oneshot=True)]:
            value = text.decode()
            self.assertIn('User=nasusrefund',value); self.assertIn('ProtectSystem=strict',value)
            self.assertIn('ReadWritePaths=/var/lib/nasus-refund-sandbox-20260928',value)
            self.assertNotIn('User=witchplayer',value)
        self.assertNotIn('witch-player.service',installer.UNITS)
        self.assertNotIn('witch-player-qa.service',installer.UNITS)


if __name__ == '__main__': unittest.main()
