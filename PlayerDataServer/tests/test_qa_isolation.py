import hashlib
import json
import os
from pathlib import Path
import sqlite3
import unittest
from contextlib import ExitStack
from unittest.mock import patch

from tests import test_apple_application as fixture
from deploy.isolate_apple_qa_data import isolate, inspect
from apple_revocation_runtime import ExistingStore
from store import encode
import qa_isolated_runtime as runtime


class QaIsolationTests(unittest.TestCase):
    write = fixture.AppleApplicationTests.write
    save_config = fixture.AppleApplicationTests.save_config
    markers = fixture.AppleApplicationTests.markers

    def setUp(self):
        fixture.AppleApplicationTests.setUp(self)
        self.player = 'a' * 32
        self.digest = hashlib.sha256(self.player.encode()).hexdigest()
        self.store.register(self.player, 'b' * 64, legacy=True)
        self.snapshot = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0,
            EconomyRevision=0, SchemaVersion=3, PlayerLevel=9, FreeGachaStones=1200,
            PaidGachaStones=9600, InitialTutorialSummonCount=3,
            HasCompletedTutorial=True, Monsters=[dict(Name='preserved-progress')])
        self.store.snapshot(self.player, self.snapshot)
        for path in self.data.iterdir(): path.chmod(0o600)
        self.target = self.root / 'isolated'
        self.instance = 'nasus-qa-sandbox-fixture'

    def perform(self):
        previous = os.umask(0o077)
        try: return isolate(Path(self.store.path), self.target, self.digest, 1200, 9600, self.instance)
        finally: os.umask(previous)

    def test_source_unchanged_and_only_clone_migrated(self):
        with self.store.connect() as db: before = list(db.iterdump())
        result = self.perform()
        with self.store.connect() as db: self.assertEqual(before, list(db.iterdump()))
        clone = ExistingStore(self.target, self.instance)
        head = clone.head(self.player, token='b' * 64)
        self.assertEqual(1, head['EconomyRevision'])
        self.assertFalse(head['MigrationRequired']); self.assertFalse(head['PurchasesEnabled'])
        self.assertEqual((1200, 9600), (head['Free'], head['Paid']))
        with clone.connect() as db:
            self.assertEqual(self.snapshot, json.loads(db.execute('SELECT data FROM snapshots').fetchone()[0]))
            self.assertEqual(self.instance, db.execute('SELECT instance FROM deletion_journal_meta').fetchone()[0])
            self.assertEqual('migration', db.execute('SELECT action FROM audit').fetchone()[0])
            self.assertEqual(0, db.execute('SELECT count(*) FROM purchases').fetchone()[0])
        self.assertFalse(result['SourceDatabaseChanged'])
        marker = json.loads((self.target / 'SANDBOX-ONLY.json').read_text())
        self.assertFalse(marker['ProductionMigrationApproved'])
        self.assertEqual(0o700, self.target.stat().st_mode & 0o777)
        self.assertEqual(0o600, (self.target/'players.sqlite').stat().st_mode & 0o777)

    def test_mismatched_balance_rejected_before_destination_created(self):
        self.snapshot['PaidGachaStones'] = 123
        with self.store.connect() as db:
            raw = encode(self.snapshot)
            db.execute('UPDATE snapshots SET data=?,hash=?', (raw, hashlib.sha256(raw.encode()).hexdigest()))
        with self.assertRaises(ValueError): self.perform()
        self.assertFalse(self.target.exists())

    def test_legacy_runtime_rejects_refund_ledger_without_ignoring_deficit(self):
        self.perform()
        clone = ExistingStore(self.target, self.instance)
        self.assertEqual(0, clone.head(self.player)['RefundDebt'])
        from purchase_refunds import initialize
        with clone.connect() as db:
            initialize(db)
        with self.assertRaises(RuntimeError):
            clone.head(self.player)

    def test_wrong_identity_rejected(self):
        self.digest = '0' * 64
        with self.assertRaises(ValueError): self.perform()
        self.assertFalse(self.target.exists())

    def test_isolated_runtime_constructs_and_reads_migrated_head(self):
        self.perform()
        self.config.update(DataDirectory=str(self.target), InstanceId=self.instance)
        self.save_config()
        env = dict(self.env, WITCH_DATA_DIR=str(self.target), WITCH_INSTANCE_ID=self.instance)
        from apple_application import create_apple_app
        app = create_apple_app(env)
        self.assertFalse(app.store.head(self.player)['MigrationRequired'])
        challenge = app.account_linking.start('link', 'b'*64, self.player)
        self.assertIn('Nonce',challenge)
        # A challenge is only local preparation: no actual Apple request here.
        self.assertTrue(self.store.head(self.player)['MigrationRequired'])

    def runtime_context(self):
        self.perform()
        self.config.update(DataDirectory=str(self.target), InstanceId=self.instance)
        self.save_config()
        env = dict(self.env, WITCH_DATA_DIR=str(self.target), WITCH_INSTANCE_ID=self.instance,
                   WITCH_BACKUP_HEALTH_FILE=str(self.root/'backup-status.json'))
        stack = ExitStack()
        for key,value in dict(ROOT=self.root, DATA=self.target, STATE=self.state,
                              CONFIG=self.config_path, INSTANCE=self.instance).items():
            stack.enter_context(patch.object(runtime,key,value))
        stack.enter_context(patch.dict(os.environ,env,clear=True))
        self.addCleanup(stack.close)

    def test_qa_maintenance_backs_up_only_clone_and_checks_empty_worker(self):
        self.runtime_context()
        with patch('apple_tokens.AppleTokenClient._post',side_effect=AssertionError('no network')):
            runtime.maintain()
        self.assertTrue(json.loads((self.root/'backup-status.json').read_text())['Healthy'])
        with sqlite3.connect(self.root/'local-backup.sqlite') as db:
            self.assertEqual(1,db.execute('SELECT economy_revision FROM players').fetchone()[0])
        self.assertEqual(0,self.store.head(self.player)['EconomyRevision'])

    def test_failed_worker_does_not_leave_green_backup_marker(self):
        self.runtime_context()
        self.write(self.root/'backup-status.json',b'{"Healthy":true}')
        with patch.object(runtime,'execute',side_effect=ValueError('fixture failure')):
            with self.assertRaises(ValueError): runtime.maintain()
        self.assertFalse(json.loads((self.root/'backup-status.json').read_text())['Healthy'])

    def test_multiple_players_rejected(self):
        self.store.register('c'*32, 'd'*64)
        with self.assertRaises(ValueError): self.perform()
        self.assertFalse(self.target.exists())

    def test_existing_destination_and_links_rejected(self):
        self.target.mkdir()
        with self.assertRaises(ValueError): self.perform()
        self.target.rmdir()
        with self.store.connect() as db:
            db.execute('INSERT INTO apple_links VALUES(?,?,?)', ('f'*64, self.player, 'fixture'))
        with self.assertRaises(ValueError): self.perform()
        self.assertFalse(self.target.exists())

    def test_frozen_or_migrated_source_rejected(self):
        for column, value in [('frozen',1), ('economy_revision',1), ('migration_required',0)]:
            with self.subTest(column=column):
                with self.store.connect() as db:
                    db.execute('UPDATE players SET frozen=0,economy_revision=0,migration_required=1')
                    db.execute('UPDATE players SET '+column+'=?', (value,))
                with self.assertRaises(ValueError): self.perform()
                self.assertFalse(self.target.exists())


class QaIsolationBoundaryTests(unittest.TestCase):
    def test_production_env_rejected_before_private_reads(self):
        with patch.object(runtime, 'read_private') as read:
            with self.assertRaises(ValueError): runtime.guard({'WITCH_DATA_DIR':'/var/lib/witch-player'})
        read.assert_not_called()

    def test_deletion_is_blocked_before_any_database_handler(self):
        calls = []
        app = runtime.NoDeletion(lambda *args: calls.append(args))
        status = []
        body = b''.join(app({'PATH_INFO':'/v1/account-deletion/commit'}, lambda s,h:status.append(s)))
        self.assertEqual(['503 Service Unavailable'],status); self.assertEqual([],calls)
        self.assertIn(b'not enabled',body)

    def test_installer_never_falls_back_to_live_database(self):
        source = Path('deploy/install_qa_isolation.py').read_text()
        self.assertIn('InaccessiblePaths=/var/lib/witch-player',source)
        self.assertIn('EnvironmentFile=\\nEnvironmentFile=',source)
        self.assertNotIn("ctl('enable'",source)
        failure = source.split('except BaseException:')[1]
        self.assertIn("ctl('stop', 'witch-player-qa.service')",failure)
        self.assertNotIn("ctl('start'",failure)
