"""Disposable synthetic rehearsal; no real keys, Apple, server or device."""
from contextlib import closing
from dataclasses import replace
import hashlib
import io
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from unittest.mock import patch
import uuid

from application import Application
from apple_revocation_runtime import ExistingStore
from maintenance import initialize, backup_generation, digest_file, add_operator
from purchase_refunds import PurchaseRefunds, RefundState
from refund_integrity import REFUND_COLUMNS
from refund_migration import prepare, fingerprint
from refund_review import RefundReview
from security import AdminAuth
from store import Store
from tests import test_refund_restore as existing_fixture


class RefundMigrationTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.live = self.root/'source'; self.instance = 'migration-fixture-instance'
        initialize(self.live, self.instance)
        self.catalog = json.loads((Path(__file__).parents[1]/'catalog.json').read_text())
        self.verify = lambda r: dict(verified=True, store='apple', transaction=r['TransactionId'], product=r['Target'])
        self.store = Store(self.live/'players.sqlite', self.catalog, self.verify)
        self.player, self.token = uuid.uuid4().hex, 'a'*64
        self.store.register(self.player, self.token)
        self.store.snapshot(self.player, dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0,
            EconomyRevision=0, SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900,
            PaidGachaStones=0, OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100))
        self.product = 'com.nasus.dungeonmonsterroguelike.crystals650'
        self.store.operation(self.player, dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='purchase',
            Target=self.product, TransactionId='migration-purchase', Receipt='synthetic'), self.token)
        self.backup = backup_generation(self.store.path, self.root/'backups')
        self.sha = digest_file(self.backup); self.destination = self.root/'candidate'

    def prepare(self, **changes):
        args = dict(source=self.backup, expected_sha256=self.sha, instance=self.instance,
                    destination=self.destination, create=True)
        args.update(changes); return prepare(**args)

    def test_default_is_offline_readonly_and_creates_nothing(self):
        with patch('requests.sessions.Session.request', side_effect=AssertionError('network forbidden')):
            result = self.prepare(create=False)
        self.assertEqual('REFUND_MIGRATION_PREFLIGHT_PASSED', result['Status'])
        self.assertFalse(result['ReadyForDeployment']); self.assertFalse(result['SourceChanged'])
        self.assertFalse(self.destination.exists()); self.assertEqual(self.sha, digest_file(self.backup))

    def test_legacy_copy_preserves_all_existing_rows_and_queues_purchases(self):
        with self.store.connect() as db: original = list(db.iterdump())
        result = self.prepare()
        self.assertEqual('REFUND_CANDIDATE_QUARANTINED', result['Status'])
        self.assertEqual(1, result['PurchasesRequiringRecheck'])
        with self.store.connect() as db: self.assertEqual(original, list(db.iterdump()))
        self.assertEqual(self.sha, digest_file(self.backup))
        candidate = self.destination/'players.sqlite'
        with closing(sqlite3.connect(candidate)) as db, closing(sqlite3.connect(self.backup)) as source:
            tables = {r[0] for r in source.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT GLOB 'sqlite_*'")}
            self.assertEqual(fingerprint(source, tables), fingerprint(db, tables))
            self.assertEqual(('migration-purchase', self.player, 0, None, 'migration_recheck_required'),
                db.execute('SELECT transaction_id,player,due,last_success,last_error FROM refund_checks').fetchone())
        self.assertEqual(0o600, candidate.stat().st_mode & 0o777)
        self.assertEqual(0o700, self.destination.stat().st_mode & 0o777)
        self.assertFalse((self.destination/'instance-id').exists())
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists())
        with self.assertRaises(ValueError): ExistingStore(self.destination, self.instance).connect()
        with self.assertRaises(RuntimeError): Store(candidate)

    def test_existing_refunds_keep_waivers_and_replay_history_but_clear_old_success(self):
        f = existing_fixture.RefundRestoreTests(); f.setUp(); self.addCleanup(f.doCleanups)
        backup = f.backup()
        result = prepare(backup, digest_file(backup), f.instance, f.destination, create=True)
        with closing(sqlite3.connect(backup)) as src, closing(sqlite3.connect(f.destination/'players.sqlite')) as dst:
            tables = set(REFUND_COLUMNS) - {'refund_checks'}
            self.assertEqual(fingerprint(src, tables), fingerprint(dst, tables))
            for row in dst.execute('SELECT due,attempts,last_success,last_error,lease,lease_until FROM refund_checks'):
                self.assertEqual((0, 0, None, 'migration_recheck_required', None, 0), row)
        self.assertTrue(result['ExistingRefundLedger']); self.assertFalse(result['ReadyForDeployment'])

    def test_digest_and_instance_mismatch_rejected_before_creation(self):
        for changes in (dict(expected_sha256='0'*64), dict(instance='different-instance'),
                        dict(expected_sha256=None), dict(instance='bad')):
            with self.assertRaises(ValueError): self.prepare(**changes)
            self.assertFalse(self.destination.exists())

    def test_missing_or_forged_manifest_is_rejected(self):
        manifest = Path(str(self.backup)+'.json')
        contents = json.loads(manifest.read_text()); contents['Bytes'] += 1
        manifest.write_text(json.dumps(contents))
        with self.assertRaises(ValueError): self.prepare()
        manifest.unlink()
        with self.assertRaises(FileNotFoundError): self.prepare()
        self.assertFalse(self.destination.exists())

    def test_never_overwrites_destination_or_live_source(self):
        self.prepare(); before = digest_file(self.destination/'players.sqlite')
        with self.assertRaises(ValueError): self.prepare()
        self.assertEqual(before, digest_file(self.destination/'players.sqlite'))
        with self.assertRaises(ValueError): self.prepare(destination=self.live)
        with self.assertRaises(ValueError): self.prepare(source=Path(self.store.path))

    def test_sidecars_and_symlinks_or_public_files_rejected(self):
        for suffix in ('-wal', '-shm', '-journal'):
            side = Path(str(self.backup)+suffix); side.touch()
            with self.assertRaises(ValueError): self.prepare()
            side.unlink()
        link = self.root/'link'; link.symlink_to(self.backup)
        with self.assertRaises(ValueError): self.prepare(source=link)
        self.backup.chmod(0o644)
        with self.assertRaises(ValueError): self.prepare()
        self.assertFalse(self.destination.exists())

    def test_incomplete_ledger_is_not_repaired_as_an_empty_schema(self):
        with sqlite3.connect(self.backup) as db: db.execute('CREATE TABLE refund_wallets(player TEXT PRIMARY KEY,debt INTEGER)')
        self.sha = digest_file(self.backup)
        manifest = Path(str(self.backup)+'.json'); value = json.loads(manifest.read_text())
        value.update(Sha256=self.sha, Bytes=self.backup.stat().st_size); manifest.write_text(json.dumps(value))
        with self.assertRaises(ValueError): self.prepare()
        self.assertFalse(self.destination.exists())

    def test_failed_schema_creation_leaves_quarantine_without_success_report(self):
        def fail(db):
            db.execute('CREATE TABLE refund_wallets(player TEXT PRIMARY KEY,debt INTEGER)')
            raise RuntimeError('synthetic interruption')
        with patch('refund_migration.initialize', fail), self.assertRaises(RuntimeError): self.prepare()
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists())
        self.assertFalse((self.destination/'instance-id').exists())
        self.assertFalse((self.destination/'refund-migration.json').exists())
        self.assertEqual(self.sha, digest_file(self.backup))

    def test_report_contains_no_player_or_auth_data(self):
        self.prepare(); raw = (self.destination/'refund-migration.json').read_text()
        for secret in (self.player, self.token, 'migration-purchase'):
            self.assertNotIn(secret, raw)

    def test_unexpected_balance_change_during_migration_cannot_report_success(self):
        from purchase_refunds import initialize as schema
        def corrupted(db):
            schema(db)
            db.execute('UPDATE players SET paid=paid+1'); db.commit()
        with patch('refund_migration.initialize', corrupted), self.assertRaises(ValueError): self.prepare()
        self.assertFalse((self.destination/'refund-migration.json').exists())
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists())
        self.assertEqual(self.sha, digest_file(self.backup))
        self.assertEqual(650, self.store.head(self.player)['Paid'])

    def test_cli_defaults_to_preflight_and_sanitizes_failure(self):
        import subprocess
        import sys
        command = [sys.executable, '-I', '-B', str(Path(__file__).parents[1]/'run_refund_migration.py'),
            '--backup', str(self.backup), '--expected-sha256', self.sha, '--instance', self.instance,
            '--new-directory', str(self.destination)]
        result = subprocess.run(command, capture_output=True, text=True, timeout=20)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual('REFUND_MIGRATION_PREFLIGHT_PASSED', json.loads(result.stdout)['Status'])
        self.assertFalse(self.destination.exists())
        command[command.index('--instance')+1] = 'wrong-instance'
        result = subprocess.run(command, capture_output=True, text=True, timeout=20)
        self.assertEqual(1, result.returncode)
        self.assertNotIn(str(self.backup), result.stdout + result.stderr)
        self.assertNotIn('Traceback', result.stdout + result.stderr)
        self.assertFalse(self.destination.exists())

    def test_offline_candidate_http_refund_spend_review_reversal_replay(self):
        self.prepare()
        # Test-only Store against synthetic quarantine. No production factory or
        # promotion is provided; the marker is never removed.
        store = Store(self.destination/'players.sqlite', self.catalog, self.verify, refunds_enabled=True)
        operators = self.root/'operators.json'
        add_operator(operators, 'fixture-reviewer', 'operator', self.root/'admin-token')
        admin = (self.root/'admin-token').read_text().strip()
        event = RefundState(str(uuid.uuid4()), hashlib.sha256(b'synthetic-notification').hexdigest(),
            'migration-purchase', self.player, self.product, 1000, 100000)
        service = PurchaseRefunds(store, lambda payload: event)
        app = Application(store, AdminAuth(path=operators), purchase_notifications=service,
                          refund_review=RefundReview(store))
        def call(path, body, token=''):
            raw = json.dumps(body).encode(); status = []
            env = dict(REQUEST_METHOD='POST', PATH_INFO=path, REMOTE_ADDR='127.0.0.1',
                HTTP_AUTHORIZATION='Bearer '+token, CONTENT_TYPE='application/json',
                CONTENT_LENGTH=str(len(raw)), **{'wsgi.input':io.BytesIO(raw)})
            answer = b''.join(app(env, lambda s,h: status.append(int(s[:3]))))
            return status[0], json.loads(answer)
        def draw(paid):
            return call('/v1/players/'+self.player+'/operations', dict(RequestId=uuid.uuid4().hex,
                Epoch=0, Kind='gacha', Count=1, Paid=paid), self.token)
        self.assertEqual(200, draw(True)[0])
        route = '/v1/store/apple/notifications'; payload = dict(signedPayload='synthetic-only')
        self.assertEqual(200, call(route, payload)[0]); self.assertEqual('duplicate', call(route, payload)[1]['Status'])
        self.assertEqual(402, draw(True)[0]); self.assertEqual(200, draw(False)[0])
        preview = dict(PlayerId=self.player, TransactionId=event.transaction)
        code, value = call('/admin/refunds/preview', preview, admin)
        self.assertEqual(200, code); self.assertEqual(300, value['MaxWaiver'])
        request = dict(preview, Amount=300, ExpectedEconomyRevision=store.head(self.player)['EconomyRevision'],
                       RequestId=str(uuid.uuid4()), Case='test-case', Confirm=True)
        answer = call('/admin/refunds/commit', request, admin)
        self.assertEqual(200, answer[0]); self.assertEqual(answer, call('/admin/refunds/commit', request, admin))
        event = replace(event, notification_id=str(uuid.uuid4()), signed_at=2000, percentage=0)
        self.assertEqual(200, call(route, payload)[0]); self.assertEqual(200, call(route, payload)[0])
        head = store.head(self.player)
        self.assertEqual((350, 0, 600), (head['Paid'], head['RefundDebt'], head['Free']))
        self.assertEqual(650, self.store.head(self.player)['Paid'])
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists())


if __name__ == '__main__': unittest.main()
