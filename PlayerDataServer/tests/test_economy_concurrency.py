"""Synthetic wallets/receipts only; no device, external API, or deployed DB access."""
import concurrent.futures
import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from store import Store, Fault


class EconomyConcurrencyTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / 'synthetic.sqlite'
        self.catalog = json.loads((Path(__file__).resolve().parents[1] / 'catalog.json').read_text())
        self.store = Store(self.path, self.catalog)
        self.player, self.token = uuid.uuid4().hex, 'a' * 64
        self.register(self.player, self.token)

    def register(self, player, token):
        self.store.register(player, token)
        self.store.snapshot(player, dict(PlayerId=player, SaveRevision=1, RecoveryEpoch=0,
            EconomyRevision=0, SchemaVersion=3, PlayerLevel=1, FreeGachaStones=900,
            PaidGachaStones=0, OwnedMonsters=[], OwnedEquipments=[], MonsterStorageLimit=380,
            HasCompletedTutorial=True), token)

    def purchase(self, request_id='synthetic-purchase', transaction='synthetic-tx'):
        return dict(RequestId=request_id, Epoch=0, Kind='purchase',
            Target='com.nasus.dungeonmonsterroguelike.crystals120',
            TransactionId=transaction, Receipt='synthetic-not-an-apple-receipt')

    def verifier(self, workers):
        # Hold every verification outside the SQLite write transaction so all
        # workers race to commit an already-verified synthetic purchase.
        barrier = threading.Barrier(workers)
        def verify(request):
            barrier.wait(timeout=10)
            return dict(verified=True, store='apple', transaction=request['TransactionId'],
                        product=request['Target'])
        self.store.purchase_verifier = verify

    def run_parallel(self, calls):
        start = threading.Barrier(len(calls))
        def run(call):
            start.wait(timeout=10)
            try:
                return 200, call()
            except Fault as error:
                return error.status, None
        with concurrent.futures.ThreadPoolExecutor(max_workers=len(calls)) as pool:
            return list(pool.map(run, calls))

    def assert_purchase_rows(self, expected):
        with self.store.connect() as db:
            self.assertEqual(expected, db.execute('SELECT COUNT(*) FROM purchases').fetchone()[0])
            self.assertEqual(expected, db.execute("SELECT COUNT(*) FROM operations WHERE kind='purchase'").fetchone()[0])

    def test_eight_simultaneous_purchase_retries_grant_once_and_survive_restart(self):
        self.verifier(8)
        request = self.purchase()
        results = self.run_parallel([lambda: self.store.operation(self.player, request, self.token)] * 8)
        self.assertTrue(all(status == 200 and result == results[0][1] for status, result in results))
        self.assertEqual(120, self.store.head(self.player, token=self.token)['Paid'])
        self.assert_purchase_rows(1)
        restarted = Store(self.path, self.catalog)  # Retry must not need Apple again after commit.
        self.assertEqual(results[0][1], restarted.operation(self.player, request, self.token))
        self.assertEqual(120, restarted.head(self.player, token=self.token)['Paid'])

    def test_same_transaction_with_different_request_ids_still_grants_once(self):
        self.verifier(8)
        requests = [self.purchase('synthetic-purchase-' + str(i)) for i in range(8)]
        results = self.run_parallel([lambda r=r: self.store.operation(self.player, r, self.token) for r in requests])
        self.assertEqual([200] + [409] * 7, sorted(status for status, _ in results))
        self.assertEqual(120, self.store.head(self.player, token=self.token)['Paid'])
        self.assert_purchase_rows(1)

    def test_receipt_reuse_across_accounts_is_globally_unique_even_with_permissive_test_verifier(self):
        other, token = uuid.uuid4().hex, 'b' * 64
        self.register(other, token)
        self.verifier(2)
        results = self.run_parallel([
            lambda: self.store.operation(self.player, self.purchase(), self.token),
            lambda: self.store.operation(other, self.purchase('other-synthetic-request'), token)])
        self.assertEqual([200, 409], sorted(status for status, _ in results))
        self.assertEqual([0, 120], sorted([self.store.head(self.player)['Paid'], self.store.head(other)['Paid']]))
        self.assert_purchase_rows(1)

    def test_distinct_simultaneous_paid_draws_cannot_overdraw_last_300(self):
        self.store.adjust(self.player, 0, 300, 0, 'synthetic-test', 'isolated fixture')
        requests = [dict(RequestId='synthetic-draw-' + str(i), Epoch=0, Kind='gacha', Count=1, Paid=True)
                    for i in range(8)]
        results = self.run_parallel([lambda r=r: self.store.operation(self.player, r, self.token) for r in requests])
        self.assertEqual([200] + [409] * 7, sorted(status for status, _ in results))
        head = self.store.head(self.player)
        self.assertEqual((900, 0), (head['Free'], head['Paid']))
        draws = [op for op in head['Operations'] if op['Kind'] == 'gacha']
        self.assertEqual(1, len(draws))
        self.assertEqual(1, len(draws[0]['Monsters']))

    def test_same_paid_draw_retries_keep_one_charge_and_identical_monster(self):
        self.store.adjust(self.player, 0, 9720, 0, 'synthetic-test', 'isolated fixture')
        request = dict(RequestId='synthetic-paid-draw', Epoch=0, Kind='gacha', Count=1, Paid=True)
        results = self.run_parallel([lambda: self.store.operation(self.player, request, self.token)] * 8)
        self.assertTrue(all(status == 200 and result == results[0][1] for status, result in results))
        self.assertEqual(9420, self.store.head(self.player)['Paid'])
        restarted = Store(self.path, self.catalog)
        self.assertEqual(results[0][1], restarted.operation(self.player, request, self.token))
        self.assertEqual(9420, restarted.head(self.player)['Paid'])

    def test_pending_verification_retry_never_grants_before_success(self):
        def offline(_):
            raise Fault(503, 'Synthetic verification unavailable')
        self.store.purchase_verifier = offline
        request = self.purchase()
        with self.assertRaises(Fault) as caught:
            self.store.operation(self.player, request, self.token)
        self.assertEqual(503, caught.exception.status)
        self.assertEqual(0, self.store.head(self.player)['Paid'])
        self.assert_purchase_rows(0)
        self.store.purchase_verifier = lambda r: dict(verified=True, store='apple',
            transaction=r['TransactionId'], product=r['Target'])
        first = self.store.operation(self.player, request, self.token)
        self.assertEqual(first, self.store.operation(self.player, request, self.token))
        self.assertEqual(120, self.store.head(self.player)['Paid'])
        self.assert_purchase_rows(1)


if __name__ == '__main__':
    unittest.main()
