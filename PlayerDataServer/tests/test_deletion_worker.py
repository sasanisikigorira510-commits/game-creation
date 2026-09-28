"""Disposable DBs/fake provider only. Never root, live credentials or real bucket."""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import time
import unittest
from unittest.mock import patch

from account_deletion import AccountDeletion
from deletion_journal import DeletionJournal
from deploy.deletion_worker import (ExistingDatabase, atomic_json, migrate_empty_schema,
    open_journal, queue_healthy, read_json, reserve_upload, MAX_UPLOAD_BYTES, fingerprint_core)
from deploy.install_deletion_worker import verified_copy
from deploy.publish_backup_health import collect, deletion_worker_healthy
from deploy.sakura_deletion_journal import SakuraPublisher
from maintenance import initialize
from store import Store, Fault


class WorkerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        self.data = self.root / 'data'
        initialize(self.data, 'synthetic-worker-001')
        self.store = Store(self.data / 'players.sqlite')
        self.store.register('a' * 32, 'b' * 64)
        # Exact legacy production table set, all fixtures disposable.
        with self.store.connect() as db:
            db.execute('DROP TABLE deleted_players')
            db.execute('DROP TABLE retired_purchases')
        self.state = self.root / 'state'; self.state.mkdir(mode=0o700)

    def core_digest(self):
        with self.store.connect() as db: return fingerprint_core(db)

    def migrate(self): return migrate_empty_schema(self.data)

    def pending(self):
        self.migrate()
        journal = open_journal(self.data)
        # AccountDeletion only here: NEVER constructed by production installer.
        api = AccountDeletion(self.store, journal=DeletionJournal(self.store, 'synthetic-worker-001', initialize=False))
        token = api.preview('a' * 32, 'b' * 64)['ConfirmationToken']
        with self.assertRaises(Fault): api.commit('a' * 32, 'b' * 64, token)
        return journal

    @staticmethod
    def encrypt(payload): return b'age-encryption.org/v1\n' + hashlib.sha256(payload).hexdigest().encode()

    def test_migration_preserves_core_and_verified_private_snapshot(self):
        before = self.core_digest()
        result = self.migrate()
        self.assertEqual({'SchemaAdded': True, 'CoreRowsUnchanged': True, 'SnapshotVerified': True}, result)
        self.assertEqual(before, self.core_digest())
        snapshots = list((self.data / 'deletion-setup-backups').glob('*.sqlite'))
        self.assertEqual(1, len(snapshots))
        self.assertEqual(0o600, snapshots[0].stat().st_mode & 0o777)
        with sqlite3.connect(snapshots[0]) as db:
            self.assertEqual(before, fingerprint_core(db))
            self.assertIsNone(db.execute("SELECT 1 FROM sqlite_master WHERE name='deletion_outbox'").fetchone())
        self.assertEqual(hashlib.sha256(snapshots[0].read_bytes()).hexdigest(), json.loads(snapshots[0].with_suffix('.json').read_text())['Sha256'])
        self.assertTrue(queue_healthy(self.data))

    def test_repeated_migration_stops_without_core_changes(self):
        self.migrate(); before = self.core_digest()
        with self.assertRaises(ValueError): self.migrate()
        self.assertEqual(before, self.core_digest())

    def test_unknown_table_and_trigger_fail_closed(self):
        for sql in ('CREATE TABLE unexpected(x)',
                    'CREATE TRIGGER unexpected AFTER INSERT ON flags BEGIN SELECT 1; END'):
            with self.subTest(sql=sql):
                with self.store.connect() as db: db.execute(sql)
                with self.assertRaises(ValueError): self.migrate()
                with self.store.connect() as db:
                    self.assertIsNone(db.execute("SELECT 1 FROM sqlite_master WHERE name='deletion_outbox'").fetchone())
                    db.execute('DROP ' + ('TRIGGER' if 'TRIGGER' in sql else 'TABLE') + ' unexpected')

    def test_migration_failure_rolls_back_ddl(self):
        before = self.core_digest()
        with patch('deploy.deletion_worker.fingerprint_core', side_effect=[b'before', b'changed']):
            with self.assertRaises(ValueError): self.migrate()
        self.assertEqual(before, self.core_digest())
        with self.store.connect() as db:
            self.assertIsNone(db.execute("SELECT 1 FROM sqlite_master WHERE name='deleted_players'").fetchone())
        self.assertEqual(1, len(list((self.data / 'deletion-setup-backups').glob('*.sqlite'))))

    def test_recovery_candidate_and_missing_database_are_never_migrated(self):
        (self.data / 'RECOVERY-PENDING.txt').touch()
        with self.assertRaises(ValueError): self.migrate()
        with self.assertRaises(ValueError): migrate_empty_schema(self.root / 'missing')
        self.assertFalse((self.root / 'missing').exists())

    def test_read_only_constructor_never_creates_schema(self):
        before = self.core_digest()
        with self.assertRaises(sqlite3.OperationalError):
            DeletionJournal(ExistingDatabase(self.data), 'synthetic-worker-001', initialize=False)
        self.assertEqual(before, self.core_digest())
        self.assertFalse((self.data / 'deletion-setup-backups').exists())

    def test_split_claim_ack_enforces_exact_lease(self):
        journal = self.pending()
        ticket = journal.claim(self.encrypt)
        self.assertIsNone(journal.claim(self.encrypt))
        self.assertEqual('stale', journal.finish(ticket['event_id'], 'wrong', True))
        self.assertEqual('retry', journal.finish(ticket['event_id'], ticket['lease'], False))
        self.assertFalse(queue_healthy(self.data))
        with self.store.connect() as db: db.execute('UPDATE deletion_outbox SET next_attempt=0')
        second = journal.claim(lambda value: self.fail('ciphertext must be reused'))
        self.assertEqual(ticket['ciphertext'], second['ciphertext'])
        self.assertEqual('verified', journal.finish(second['event_id'], second['lease'], True))
        self.assertTrue(queue_healthy(self.data))

    def test_stale_pending_is_unhealthy_without_provider_call(self):
        self.pending()
        self.assertTrue(queue_healthy(self.data))
        self.assertFalse(queue_healthy(self.data, time.time() + 901))

    def test_missing_corrupt_or_exhausted_budget_never_resets(self):
        with self.assertRaises(ValueError): reserve_upload(self.state, 100)
        atomic_json(self.state / 'budget.json', {'ReservedBytes': MAX_UPLOAD_BYTES - 100})
        reserve_upload(self.state, 100)
        with self.assertRaises(ValueError): reserve_upload(self.state, 100)
        self.assertEqual(MAX_UPLOAD_BYTES, read_json(self.state / 'budget.json')['ReservedBytes'])
        for value in (-1, True, '0', 1.5):
            atomic_json(self.state / 'budget.json', {'ReservedBytes': value})
            with self.assertRaises(ValueError): reserve_upload(self.state, 100)

    def test_state_write_failure_preserves_previous_budget(self):
        path = self.state / 'budget.json'; atomic_json(path, {'ReservedBytes': 100})
        with patch('deploy.deletion_worker.os.replace', side_effect=OSError):
            with self.assertRaises(OSError): reserve_upload(self.state, 100)
        self.assertEqual({'ReservedBytes': 100}, read_json(path))

    @staticmethod
    def units(name):
        return dict(LoadState='loaded', ActiveState='active' if name.endswith('.timer') else 'inactive', Result='success', ExecMainStatus='0')

    def test_monitor_includes_stale_missing_false_and_stopped_worker(self):
        # Avoid datetime's microsecond rounding moving the fixture into the
        # future relative to its own explicit comparison time.
        now = float(int(time.time())); report = self.state / 'backup.json'; status = self.state / 'status.json'
        atomic_json(report, {'Status': 'OFFSITE_CIPHERTEXT_VERIFIED', 'CompletedUtc': dt.datetime.fromtimestamp(now, dt.timezone.utc).isoformat()})
        def check(read=self.units):
            return collect(report, now, read, require_deletion_worker=True, deletion_report=status)['Healthy']
        self.assertFalse(check())
        for healthy, stamp, expected in [(True, now, True), (False, now, False), (True, now-301, False), (True, now+1, False)]:
            atomic_json(status, {'Healthy': healthy, 'CheckedUnix': stamp})
            self.assertEqual(expected, check())
        atomic_json(status, {'Healthy': True, 'CheckedUnix': now})
        self.assertFalse(check(lambda name: dict(self.units(name), ActiveState='inactive') if name == 'witch-player-deletion.timer' else self.units(name)))
        self.assertFalse(check(lambda name: dict(self.units(name), Result='exit-code') if name == 'witch-player-deletion.service' else self.units(name)))
        # Original deployment remains supported until explicit health hook added.
        self.assertTrue(collect(report, now, self.units)['Healthy'])

    def test_invalid_or_public_marker_is_unhealthy(self):
        path = self.state / 'status.json'
        for value in ([], {}, {'Healthy': 1, 'CheckedUnix': time.time()}, {'Healthy': True, 'CheckedUnix': True}):
            atomic_json(path, value)
            self.assertFalse(deletion_worker_healthy(path, time.time()))
        atomic_json(path, {'Healthy': True, 'CheckedUnix': time.time()})
        path.chmod(0o644)
        self.assertFalse(deletion_worker_healthy(path, time.time()))

    def test_probe_is_not_a_deletion_event_and_requires_readback(self):
        config = self.state / 'curl.conf'; config.touch(mode=0o600)
        calls = []
        cipher = self.encrypt(b'nonsecret-probe')
        class Response:
            returncode = 0
            stdout = cipher
        def run(args, **kwargs): calls.append(args); return Response()
        publisher = SakuraPublisher(config, run=run)
        key = 'deletion-probes/' + 'a' * 32 + '.age'
        self.assertEqual(hashlib.sha256(cipher).hexdigest(), publisher.probe(key, cipher))
        self.assertEqual(2, len(calls))
        with self.assertRaises(ValueError): publisher(key, cipher)
        with self.assertRaises(ValueError): publisher.probe('deletions/instance/' + 'a' * 32 + '.json.age', cipher)
        Response.stdout = b'wrong'
        with self.assertRaises(RuntimeError): publisher.probe(key, cipher)

    def test_bundle_copy_validates_copied_bytes_and_refuses_overwrite(self):
        src = self.root / 'source.py'; dst = self.root / 'trusted' / 'copy.py'
        src.write_text('synthetic')
        with self.assertRaises(ValueError): verified_copy(src, dst, '0' * 64)
        self.assertEqual(0o600, dst.stat().st_mode & 0o777)
        dst.unlink()
        verified_copy(src, dst, hashlib.sha256(src.read_bytes()).hexdigest())
        with self.assertRaises(FileExistsError): verified_copy(src, dst, hashlib.sha256(src.read_bytes()).hexdigest())
        link = self.root / 'link'; link.symlink_to(src)
        with self.assertRaises(ValueError): verified_copy(link, self.root / 'no-copy', '0' * 64)


if __name__ == '__main__': unittest.main()
