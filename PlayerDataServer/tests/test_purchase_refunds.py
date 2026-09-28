"""Isolated synthetic receipts/database only; no real Apple or player data."""
import concurrent.futures
from dataclasses import replace
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
import uuid

from application import Application
from account_deletion import AccountDeletion
from purchase_refunds import PurchaseRefunds, RefundState
from store import Store, Fault


class RefundTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / 'refund.sqlite'
        catalog = json.loads((Path(__file__).parents[1] / 'catalog.json').read_text())
        self.verify = lambda r: dict(verified=True, store='apple', transaction=r['TransactionId'], product=r['Target'])
        self.store = Store(self.path, catalog, purchase_verifier=self.verify, refunds_enabled=True)
        self.player, self.token = uuid.uuid4().hex, 'a' * 64
        self.store.register(self.player, self.token)
        self.save = dict(PlayerId=self.player, SaveRevision=1, RecoveryEpoch=0, EconomyRevision=0,
            SchemaVersion=3, PlayerLevel=1, Gold=100, FreeGachaStones=900, PaidGachaStones=0,
            OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=100, InitialTutorialSummonCount=0)
        self.store.snapshot(self.player, self.save)
        self.product = 'com.nasus.dungeonmonsterroguelike.crystals650'
        self.buy = dict(RequestId=uuid.uuid4().hex, Epoch=0, Kind='purchase', Target=self.product,
                        TransactionId='fixture-purchase', Receipt='synthetic')
        self.store.operation(self.player, self.buy, self.token)
        self.event = RefundState(str(uuid.uuid4()), hashlib.sha256(b'fixture').hexdigest(),
            'fixture-purchase', self.player, self.product, 1000, 100000)
        self.service = PurchaseRefunds(self.store, lambda _: self.event)

    def wallet(self):
        h = self.store.head(self.player)
        return h['Paid'], h['RefundDebt'], h['Free']

    def draw(self, paid=True):
        return self.store.operation(self.player, dict(RequestId=uuid.uuid4().hex, Epoch=0,
            Kind='gacha', Count=1, Paid=paid), self.token)

    def fresh(self, **kw):
        return replace(self.event, notification_id=str(uuid.uuid4()), **kw)

    def status(self, code, call):
        with self.assertRaises(Fault) as error: call()
        self.assertEqual(code, error.exception.status)

    def test_full_unused_refund_and_reversal_restore_exactly_once(self):
        self.assertEqual('applied', self.service.apply(self.event)['Status'])
        self.assertEqual((0, 0, 900), self.wallet())
        self.assertEqual('duplicate', self.service.apply(self.event)['Status'])
        reverse = self.fresh(signed_at=2000, percentage=0)
        self.service.apply(reverse); self.service.apply(reverse)
        self.assertEqual((650, 0, 900), self.wallet())
        self.assertEqual(3, self.store.head(self.player)['EconomyRevision'])

    def test_spent_refund_blocks_only_paid_operations_and_survives_restart(self):
        self.draw()
        self.service.apply(self.event)
        self.assertEqual((0, 300, 900), self.wallet())
        self.status(402, self.draw)
        self.status(402, lambda: self.store.operation(self.player, dict(RequestId=uuid.uuid4().hex,
            Epoch=0, Kind='upgrade', Target='monster_storage'), self.token))
        self.draw(False)
        self.assertEqual((0, 300, 600), self.wallet())
        self.store = Store(self.path, self.store.catalog, self.verify, refunds_enabled=True)
        self.assertEqual((0, 300, 600), self.wallet())
        self.service = PurchaseRefunds(self.store, lambda _: self.event)
        self.service.apply(self.fresh(signed_at=2000, percentage=0))
        self.assertEqual((350, 0, 600), self.wallet())
        self.draw()
        self.assertEqual((50, 0, 600), self.wallet())

    def test_partial_refund_uses_cumulative_target_not_repeat_delta(self):
        self.draw()
        half = self.fresh(percentage=50000)
        self.service.apply(half)
        self.assertEqual((25, 0, 900), self.wallet())
        self.service.apply(self.fresh(percentage=50000, signed_at=1100))
        self.assertEqual((25, 0, 900), self.wallet())
        self.service.apply(self.fresh(signed_at=1200))
        self.assertEqual((0, 300, 900), self.wallet())
        self.service.apply(self.fresh(signed_at=1300, percentage=0))
        self.assertEqual((350, 0, 900), self.wallet())

    def test_fractional_gems_round_down_without_over_withdrawal(self):
        self.service.apply(self.fresh(percentage=33333))
        self.assertEqual((434, 0, 900), self.wallet())

    def test_old_refund_cannot_override_new_reversal(self):
        self.service.apply(self.fresh(signed_at=2000, percentage=0))
        self.assertEqual('stale', self.service.apply(self.event)['Status'])
        self.assertEqual((650, 0, 900), self.wallet())

    def test_conflicting_same_time_and_uuid_are_not_applied(self):
        self.service.apply(self.event)
        self.status(503, lambda: self.service.apply(self.fresh(percentage=0)))
        self.status(409, lambda: self.service.apply(replace(self.event, payload_hash='a' * 64)))
        self.assertEqual((0, 0, 900), self.wallet())

    def test_unknown_or_foreign_purchase_never_debits(self):
        self.status(503, lambda: self.service.apply(self.fresh(transaction='not-delivered')))
        self.status(409, lambda: self.service.apply(self.fresh(player=uuid.uuid4().hex)))
        self.status(409, lambda: self.service.apply(self.fresh(product='com.nasus.dungeonmonsterroguelike.crystals120')))
        self.assertEqual((650, 0, 900), self.wallet())

    def test_bad_values_rejected_without_changes(self):
        for changes in ({'percentage': True}, {'percentage': -1}, {'percentage': 100001},
                        {'signed_at': 0}, {'signed_at': True}, {'player': 'x'},
                        {'notification_id': 'x'}, {'payload_hash': 'x'}):
            with self.subTest(changes=changes):
                self.status(400, lambda: self.service.apply(replace(self.event, **changes)))
        self.assertEqual((650, 0, 900), self.wallet())

    def test_duplicate_concurrent_notifications_debit_once(self):
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            results = list(pool.map(lambda _: self.service.apply(self.event), range(8)))
        self.assertEqual(1, sum(r['Status'] == 'applied' for r in results))
        self.assertEqual((0, 0, 900), self.wallet())

    def test_concurrent_draw_refund_has_serializable_balance(self):
        def spend():
            try: return self.draw()
            except Fault as e:
                self.assertIn(e.status, (402, 409)); return None
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            a = pool.submit(spend); b = pool.submit(self.service.apply, self.event)
            drawn, _ = a.result(), b.result()
        self.assertEqual((0, 300 if drawn else 0, 900), self.wallet())

    def test_purchase_retry_after_refund_does_not_regrant(self):
        self.service.apply(self.event)
        self.store.operation(self.player, self.buy, self.token)
        self.assertEqual((0, 0, 900), self.wallet())
        self.status(409, lambda: self.store.operation(self.player,
            dict(self.buy, RequestId=uuid.uuid4().hex), self.token))

    def test_new_purchase_does_not_silently_pay_deficit_or_unlock_spending(self):
        self.draw(); self.service.apply(self.event)
        self.store.operation(self.player, dict(self.buy, RequestId=uuid.uuid4().hex,
            TransactionId='new-purchase'), self.token)
        self.assertEqual((650, 300, 900), self.wallet())
        self.status(402, self.draw)

    def test_default_runtime_cannot_ignore_existing_refund_ledger(self):
        with self.assertRaises(RuntimeError): Store(self.path)

    def test_deletion_cascades_refund_data_and_late_event_cannot_resurrect(self):
        self.draw(); self.service.apply(self.event)
        deletion = AccountDeletion(self.store)
        preview = deletion.preview(self.player, self.token)
        deletion.commit(self.player, self.token, preview['ConfirmationToken'])
        with self.store.connect() as db:
            for table in ('refund_wallets', 'purchase_refunds', 'refund_events', 'players'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table).fetchone()[0])
        self.assertEqual('retired', self.service.apply(self.fresh(signed_at=2000, percentage=0))['Status'])

    def http(self, app, body):
        raw = json.dumps(body).encode(); response = []
        data = b''.join(app(dict(HTTP_HOST='localhost', REMOTE_ADDR='127.0.0.1', REQUEST_METHOD='POST',
            PATH_INFO='/v1/store/apple/notifications', CONTENT_TYPE='application/json',
            CONTENT_LENGTH=str(len(raw)), **{'wsgi.input': io.BytesIO(raw)}),
            lambda status, headers: response.append(status)))
        return int(response[0].split()[0]), json.loads(data)

    def test_http_off_by_default_and_plain_caller_assertions_rejected(self):
        app = Application(self.store, lambda _: None)
        self.assertEqual(404, self.http(app, {'signedPayload': 'fake'})[0])
        app = Application(self.store, lambda _: None, purchase_notifications=self.service)
        self.assertEqual(400, self.http(app, {'PlayerId': self.player, 'refund': 650})[0])
        self.assertEqual(200, self.http(app, {'signedPayload': 'synthetic-verified-fixture'})[0])
        self.assertEqual((0, 0, 900), self.wallet())

    def test_signature_or_apple_failure_preserves_all_state(self):
        def unavailable(_): raise Fault(503, 'Verification pending')
        service = PurchaseRefunds(self.store, unavailable)
        self.status(503, lambda: service.receive({'signedPayload': 'fake'}))
        self.assertEqual((650, 0, 900), self.wallet())


if __name__ == '__main__': unittest.main()
