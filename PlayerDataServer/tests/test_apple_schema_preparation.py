import os
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from store import Store
from deletion_journal import DeletionJournal
from deploy.prepare_apple_schema import migrate, SCHEMA, TABLES


class SchemaPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.data = self.root/'data'; self.data.mkdir(mode=0o700)
        self.backup = self.root/'backup'; self.backup.mkdir(mode=0o700)
        (self.data/'instance-id').write_text('schema-test-instance')
        self.store = Store(self.data/'players.sqlite')
        DeletionJournal(self.store, 'schema-test-instance')
        # The existing production deletion schema includes both tables.
        with self.store.connect() as db:
            db.execute('CREATE TABLE IF NOT EXISTS deleted_players(player_hash TEXT PRIMARY KEY, deleted_at REAL NOT NULL)')
            db.execute('CREATE TABLE IF NOT EXISTS retired_purchases(transaction_hash TEXT PRIMARY KEY)')
        (self.data/'instance-id').write_text('schema-test-instance')
        for path in self.data.iterdir(): path.chmod(0o600)

    def dump(self):
        with sqlite3.connect(self.store.path) as db: return list(db.iterdump())

    def test_additive_migration_preserves_existing_rows_and_verified_backup(self):
        before = self.dump()
        result = migrate(self.data, self.backup)
        self.assertEqual(result['AddedTables'], 6)
        with sqlite3.connect(result['Backup']) as backup:
            self.assertEqual(before, list(backup.iterdump()))
        with self.store.connect() as db:
            for table in TABLES: self.assertEqual(0, db.execute('SELECT count(*) FROM '+table).fetchone()[0])
        self.assertFalse(result['ExistingRowsChanged'])

    def test_repeat_refused_without_modifications(self):
        migrate(self.data, self.backup); before=self.dump()
        with self.assertRaises(ValueError): migrate(self.data, self.backup)
        self.assertEqual(before, self.dump())

    def test_mid_transaction_failure_rolls_back_all_ddl(self):
        before=self.dump()
        with self.assertRaises(sqlite3.Error): migrate(self.data, self.backup, statements=SCHEMA[:2]+('INVALID SQL',))
        self.assertEqual(before, self.dump())

    def test_partial_schema_refused(self):
        with self.store.connect() as db: db.execute(SCHEMA[0])
        before=self.dump()
        with self.assertRaises(ValueError): migrate(self.data, self.backup)
        self.assertEqual(before,self.dump())

    def test_quarantine_refused(self):
        (self.data/'RECOVERY-PENDING.txt').touch()
        with self.assertRaises(ValueError): migrate(self.data, self.backup)

    def test_wrong_instance_refused(self):
        (self.data/'instance-id').write_text('wrong-instance-marker')
        with self.assertRaises(ValueError): migrate(self.data, self.backup)

    def test_shared_database_refused(self):
        Path(self.store.path).chmod(0o644)
        with self.assertRaises(ValueError): migrate(self.data, self.backup)

    def test_migrated_schema_is_accepted_by_actual_readonly_runtime(self):
        from cryptography.hazmat.primitives import serialization
        from cryptography.hazmat.primitives.asymmetric import ec
        from apple_revocation_runtime import execute
        migrate(self.data, self.backup)
        state=self.root/'state'; state.mkdir(mode=0o700)
        signing=self.root/'signing.p8'
        signing.write_bytes(ec.generate_private_key(ec.SECP256R1()).private_bytes(
            serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
        encryption=self.root/'token.key'; encryption.write_bytes(os.urandom(32))
        config=self.root/'worker.json'
        config.write_text(json.dumps(dict(Version=1,InstanceId='schema-test-instance',
            ClientId='com.nasus.dungeonmonsterroguelike',TeamId='ABCDEFGHIJ',KeyId='0123456789',
            DataDirectory=str(self.data),StateDirectory=str(state),SigningKeyFile=str(signing),
            TokenEncryptionKeyFile=str(encryption))))
        for path in (signing,encryption,config): path.chmod(0o600)
        before=self.dump()
        self.assertEqual(execute(config)['Status'],'APPLE_WORKER_PREFLIGHT_PASSED')
        self.assertEqual(before,self.dump())
        self.assertEqual([],list(state.iterdir()))
