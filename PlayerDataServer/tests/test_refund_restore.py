"""Synthetic offline backups only. No live data, network, keys or promotion."""
from dataclasses import replace
import hashlib
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
import uuid

from account_deletion import AccountDeletion
from deletion_restore import export_checkpoint, restore_filtered, _schema
from maintenance import backup_generation, digest_file, initialize, verify_database, restore_copy
from purchase_refunds import PurchaseRefunds, RefundState
from refund_integrity import REFUND_COLUMNS, validate_refunds
from refund_review import RefundReview
from refund_reconciliation import RefundReconciliation
from store import Store, Fault


class RefundRestoreTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve(); self.live = self.root/'live'
        self.instance = 'refund-restore-fixture'
        initialize(self.live, self.instance)
        catalog = json.loads((Path(__file__).parents[1]/'catalog.json').read_text())
        self.verify = lambda r: dict(verified=True, store='apple', transaction=r['TransactionId'], product=r['Target'])
        self.store = Store(self.live/'players.sqlite', catalog, self.verify, refunds_enabled=True)
        self.service = PurchaseRefunds(self.store, lambda _: None)
        self.player, self.other = uuid.uuid4().hex, uuid.uuid4().hex
        self.token = 'a'*64
        self.product = 'com.nasus.dungeonmonsterroguelike.crystals650'
        self.events, self.reviews = {}, {}
        for player in (self.player, self.other):
            self.store.register(player, self.token)
            self.store.snapshot(player, dict(PlayerId=player, SaveRevision=1, RecoveryEpoch=0,
                EconomyRevision=0, SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900,
                PaidGachaStones=0, OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100))
            transaction = 'purchase-' + player
            self.store.operation(player, dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='purchase',
                Target=self.product, TransactionId=transaction, Receipt='synthetic'), self.token)
            self.store.operation(player, dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='gacha',
                Count=1, Paid=True), self.token)
            event = RefundState(str(uuid.uuid4()), hashlib.sha256(player.encode()).hexdigest(),
                transaction, player, self.product, 1000, 100000)
            self.events[player] = event; self.service.apply(event)
            review = dict(player=player, transaction=transaction, amount=200,
                expected_revision=self.store.head(player)['EconomyRevision'], request_id=str(uuid.uuid4()),
                actor='operator-fixture', case='case-fixture')
            self.reviews[player] = review; RefundReview(self.store).commit(**review)
        RefundReconciliation(self.service, lambda p: self.events[p['player']], lambda: 1000).run()
        with self.store.connect() as db:
            db.execute("UPDATE refund_checks SET lease='old-worker',lease_until=9999999999")
        self.destination = self.root/'candidate'
        self.checkpoint = self.root/'deletions.json'

    def backup(self):
        self.backup_path = backup_generation(self.store.path, self.root/'backups')
        return self.backup_path

    def restore(self):
        digest = export_checkpoint(self.live, self.checkpoint, self.instance)
        result = restore_filtered(self.backup_path, self.checkpoint, digest, self.instance, self.destination)
        candidate = Store(self.destination/'players.sqlite', self.store.catalog, self.verify, refunds_enabled=True)
        return result, candidate

    def rows(self, store, tables=None, player=None):
        with store.connect() as db:
            return {t: [tuple(r) for r in db.execute('SELECT * FROM '+t+
                    (' WHERE player=?' if player else '')+' ORDER BY 1', (player,) if player else ())]
                    for t in (tables or REFUND_COLUMNS)}

    def test_backup_preserves_refund_waiver_history_and_reversal_cannot_double_credit(self):
        self.backup(); original = digest_file(self.backup_path)
        before = self.rows(self.store, set(REFUND_COLUMNS)-{'refund_checks'})
        live_before = self.rows(self.store)
        result, candidate = self.restore()
        self.assertEqual(before, self.rows(candidate, set(REFUND_COLUMNS)-{'refund_checks'}))
        self.assertEqual(live_before, self.rows(self.store))
        self.assertEqual(original, digest_file(self.backup_path))
        head = candidate.head(self.player)
        self.assertEqual((0, 100, 900), (head['Paid'], head['RefundDebt'], head['Free']))
        # Replay the original review returns its original result, not another waiver.
        RefundReview(candidate).commit(**self.reviews[self.player])
        service = PurchaseRefunds(candidate, lambda _: None)
        reverse = replace(self.events[self.player], notification_id=str(uuid.uuid4()), signed_at=2000, percentage=0)
        service.apply(reverse); service.apply(reverse)
        head = candidate.head(self.player)
        self.assertEqual((350, 0, 900), (head['Paid'], head['RefundDebt'], head['Free']))
        self.assertTrue(result['RefundReconciliationRequired'])
        self.assertTrue(result['PostBackupEconomyReconciliationRequired'])
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists())
        self.assertFalse((self.destination/'instance-id').exists())

    def test_post_backup_deletion_removes_all_refund_identity_and_preserves_survivor(self):
        self.backup()
        other = self.rows(self.store, set(REFUND_COLUMNS)-{'refund_checks'}, self.other)
        deletion = AccountDeletion(self.store)
        confirmation = deletion.preview(self.player, self.token)
        deletion.commit(self.player, self.token, confirmation['ConfirmationToken'])
        result, candidate = self.restore()
        self.assertEqual(1, result['RemovedPlayers'])
        self.assertEqual(other, self.rows(candidate, set(REFUND_COLUMNS)-{'refund_checks'}, self.other))
        self.assertTrue(all(not rows for rows in self.rows(candidate, player=self.player).values()))
        with self.assertRaises(Fault): candidate.register(self.player, self.token)
        self.assertEqual('retired', PurchaseRefunds(candidate, lambda _: None).apply(self.events[self.player])['Status'])

    def test_recovery_discards_leases_and_requires_rechecking_even_previous_success(self):
        self.backup(); _, candidate = self.restore()
        with candidate.connect() as db:
            for row in db.execute('SELECT * FROM refund_checks'):
                self.assertEqual(0, row['due']); self.assertEqual(0, row['lease_until'])
                self.assertIsNone(row['lease']); self.assertIsNone(row['last_success'])
                self.assertEqual('recovery_recheck_required', row['last_error'])
        service = PurchaseRefunds(candidate, lambda _: None)
        result = RefundReconciliation(service, lambda p: self.events[p['player']], lambda: 2000).run()
        self.assertTrue(result['Healthy'])
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists(), 'Successful lookup cannot authorize promotion')

    def test_backup_before_later_waiver_is_not_claimed_up_to_date(self):
        self.backup()
        request = dict(self.reviews[self.player], request_id=str(uuid.uuid4()), amount=100,
                       expected_revision=self.store.head(self.player)['EconomyRevision'])
        RefundReview(self.store).commit(**request)
        result, candidate = self.restore()
        self.assertEqual(0, self.store.head(self.player)['RefundDebt'])
        self.assertEqual(100, candidate.head(self.player)['RefundDebt'])
        self.assertTrue(result['PostBackupEconomyReconciliationRequired'])
        self.assertEqual('DELETIONS_APPLIED_OFFLINE_NOT_READY', result['Status'])

    def test_plain_copy_retains_all_ledger_data_but_is_quarantined(self):
        self.backup(); restore_copy(self.backup_path, self.destination)
        candidate = Store(self.destination/'players.sqlite', refunds_enabled=True)
        self.assertEqual(self.rows(self.store), self.rows(candidate))
        self.assertTrue((self.destination/'RECOVERY-PENDING.txt').exists())

    def test_legacy_runtime_cannot_open_restored_refund_database(self):
        self.backup(); self.restore()
        with self.assertRaises(RuntimeError): Store(self.destination/'players.sqlite')
        from apple_revocation_runtime import ExistingStore
        with self.assertRaises(ValueError): ExistingStore(self.destination, self.instance).connect()

    def test_missing_refund_table_never_silently_recreated_in_backup_or_restore(self):
        for table in REFUND_COLUMNS:
            with self.subTest(table=table):
                with sqlite3.connect(self.store.path) as db:
                    db.execute('BEGIN'); db.execute('DROP TABLE '+table)
                    with self.assertRaises(ValueError): validate_refunds(db)
                    with self.assertRaises(ValueError): _schema(db)
                    db.rollback()

    def test_invalid_ownership_waiver_or_missing_review_operation_rejected(self):
        changes = [
            ("UPDATE purchase_refunds SET player=? WHERE player=?", (self.other, self.player)),
            ("UPDATE refund_waivers SET remaining=1000", ()),
            ("UPDATE refund_wallets SET debt=1000", ()),
            ("DELETE FROM operations WHERE kind='refund_review'", ()),
            ("UPDATE refund_reviews SET response='{}'", ()),
            ("UPDATE refund_checks SET player=? WHERE player=?", (self.other, self.player)),
            ("UPDATE refund_checks SET due=-1", ()),
            ("DELETE FROM refund_wallets", ()),
            ("DELETE FROM refund_waivers", ()),
            ("DELETE FROM refund_reviews", ()),
            ("DELETE FROM refund_events", ()),
        ]
        for sql, args in changes:
            with self.subTest(sql=sql), sqlite3.connect(self.store.path) as db:
                db.execute('BEGIN'); db.execute(sql, args)
                with self.assertRaises(ValueError): validate_refunds(db)
                db.rollback()

    def test_missing_cascade_constraint_or_orphan_rejected(self):
        with sqlite3.connect(self.store.path) as db:
            db.execute('BEGIN')
            db.execute('DROP TABLE refund_events')
            db.execute('CREATE TABLE refund_events(notification_id TEXT PRIMARY KEY,payload_hash TEXT,player TEXT)')
            with self.assertRaises(ValueError): validate_refunds(db)
            db.rollback()
            db.execute('BEGIN')
            db.execute("UPDATE refund_events SET player='orphan'")
            with self.assertRaises(ValueError): validate_refunds(db)
            db.rollback()

    def test_entire_ledger_removed_but_operations_survive_is_not_legacy(self):
        with sqlite3.connect(self.store.path) as db:
            db.execute('BEGIN')
            for table in REFUND_COLUMNS: db.execute('DROP TABLE '+table)
            with self.assertRaises(ValueError): validate_refunds(db)
            db.rollback()

    def test_corrupt_source_not_published_as_backup(self):
        with self.store.connect() as db: db.execute('DROP TABLE refund_waivers')
        with self.assertRaises(ValueError): self.backup()
        self.assertEqual([], list((self.root/'backups').iterdir()))

    def test_existing_restore_destination_never_overwritten(self):
        self.backup(); self.destination.mkdir()
        keep = self.destination/'keep'; keep.write_text('fixture')
        with self.assertRaises(FileExistsError): self.restore()
        self.assertEqual('fixture', keep.read_text())

    def test_legacy_database_without_refunds_still_valid_and_no_tables_created(self):
        legacy = self.root/'legacy.sqlite'; Store(legacy)
        verify_database(legacy)
        with sqlite3.connect(legacy) as db:
            before = list(db.iterdump()); self.assertFalse(validate_refunds(db))
            self.assertEqual(before, list(db.iterdump()))
