import concurrent.futures
import json
import unittest
import uuid

from account_deletion import AccountDeletion
from refund_review import RefundReview
from store import Fault
from tests import test_purchase_refunds as fixture


class ReviewTests(unittest.TestCase):
    def setUp(self):
        self.f = fixture.RefundTests(); self.f.setUp(); self.addCleanup(self.f.doCleanups)
        self.f.draw(); self.f.service.apply(self.f.event)
        self.review = RefundReview(self.f.store)

    def request(self, **edits):
        return dict(dict(player=self.f.player, transaction=self.f.event.transaction, amount=300,
            expected_revision=self.f.store.head(self.f.player)['EconomyRevision'],
            request_id=str(uuid.uuid4()), actor='operator-fixture', case='case-fixture'), **edits)

    def test_preview_never_writes(self):
        with self.f.store.connect() as db: before = list(db.iterdump())
        preview = self.review.preview(self.f.player, self.f.event.transaction)
        self.assertEqual(300, preview['MaxWaiver'])
        with self.f.store.connect() as db: self.assertEqual(before, list(db.iterdump()))

    def test_waiver_clears_only_debt_and_is_idempotent_with_audit(self):
        request = self.request()
        op = self.review.commit(**request)
        self.assertEqual((0, 0, 900), self.f.wallet())
        self.assertEqual(op, self.review.commit(**request))
        self.assertEqual(0, op['GoldDelta']); self.assertEqual([], op['Monsters'])
        with self.f.store.connect() as db:
            audit = db.execute("SELECT * FROM audit WHERE action='refund_waiver'").fetchall()
            self.assertEqual(1, len(audit)); self.assertEqual('case-fixture', audit[0]['reason'])
            self.assertEqual(300, json.loads(audit[0]['detail'])['amount'])

    def test_reversal_after_waiver_does_not_double_credit(self):
        self.review.commit(**self.request())
        reverse = self.f.fresh(signed_at=2000, percentage=0)
        self.f.service.apply(reverse); self.f.service.apply(reverse)
        self.assertEqual((350, 0, 900), self.f.wallet())
        with self.f.store.connect() as db:
            self.assertEqual(0, db.execute('SELECT remaining FROM refund_waivers').fetchone()[0])

    def test_partial_reversal_and_new_refund_do_not_reuse_forgiveness(self):
        self.review.commit(**self.request(amount=200))
        self.f.service.apply(self.f.fresh(signed_at=2000, percentage=50000))
        self.assertEqual((25, 0, 900), self.f.wallet())
        self.f.service.apply(self.f.fresh(signed_at=3000, percentage=0))
        self.assertEqual((350, 0, 900), self.f.wallet())
        self.f.service.apply(self.f.fresh(signed_at=4000, percentage=100000))
        self.assertEqual((0, 300, 900), self.f.wallet())

    def test_stale_over_limit_invalid_and_conflicting_requests_rejected(self):
        for edits in (dict(amount=301), dict(expected_revision=0), dict(amount=True),
                      dict(actor=''), dict(case='contains private text'), dict(request_id='bad')):
            with self.subTest(edits=edits), self.assertRaises(Fault):
                self.review.commit(**self.request(**edits))
        request = self.request(); self.review.commit(**request)
        with self.assertRaises(Fault): self.review.commit(**dict(request, amount=1))

    def test_two_operators_cannot_waive_same_deficit_twice(self):
        a, b = self.request(), self.request()
        def commit(request):
            try: return self.review.commit(**request)
            except Fault as e: self.assertEqual(409, e.status); return None
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(commit, [a, b]))
        self.assertEqual(1, sum(r is not None for r in results))
        self.assertEqual((0, 0, 900), self.f.wallet())

    def test_concurrent_reversal_and_review_preserve_same_final_value(self):
        request = self.request()
        def review():
            try: self.review.commit(**request)
            except Fault as e: self.assertEqual(409, e.status)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            a = pool.submit(review)
            b = pool.submit(self.f.service.apply, self.f.fresh(signed_at=2000, percentage=0))
            a.result(); b.result()
        self.assertEqual((350, 0, 900), self.f.wallet())

    def test_waiver_is_scoped_to_purchase_not_an_unrelated_reversal(self):
        self.review.commit(**self.request())
        second = dict(self.f.buy, RequestId=uuid.uuid4().hex, TransactionId='second')
        self.f.store.operation(self.f.player, second, self.f.token)
        self.f.draw()
        self.f.service.apply(self.f.fresh(transaction='second'))
        self.assertEqual((0, 300, 900), self.f.wallet())
        self.f.service.apply(self.f.fresh(transaction='second', signed_at=2000, percentage=0))
        self.assertEqual((350, 0, 900), self.f.wallet())
        self.f.service.apply(self.f.fresh(signed_at=2000, percentage=0))
        self.assertEqual((700, 0, 900), self.f.wallet())

    def test_deleted_account_cannot_be_reviewed_or_resurrected(self):
        request = self.request(); self.review.commit(**request)
        deletion = AccountDeletion(self.f.store)
        preview = deletion.preview(self.f.player, self.f.token)
        deletion.commit(self.f.player, self.f.token, preview['ConfirmationToken'])
        with self.assertRaises(Fault): self.review.commit(**request)
        with self.f.store.connect() as db:
            for table in ('refund_reviews', 'refund_waivers'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table).fetchone()[0])

    def test_mixed_refunds_waivers_purchases_and_reversals_conserve_value(self):
        import random
        rng = random.Random(928)
        transactions = [self.f.event.transaction]
        delivered, spent = 650, 300
        for step in range(120):
            transaction = rng.choice(transactions)
            action = rng.randrange(4)
            if action == 0:
                transaction = 'sequence-' + str(step)
                self.f.store.operation(self.f.player, dict(self.f.buy, RequestId=uuid.uuid4().hex,
                    TransactionId=transaction), self.f.token)
                transactions.append(transaction); delivered += 650
            elif action == 1:
                self.f.service.apply(self.f.fresh(transaction=transaction, signed_at=2000+step,
                    percentage=rng.choice([0, 25000, 50000, 100000])))
            elif action == 2:
                try: preview = self.review.preview(self.f.player, transaction)
                except Fault as e:
                    self.assertEqual(404, e.status); continue
                if preview['MaxWaiver']:
                    self.review.commit(**self.request(transaction=transaction,
                        amount=rng.randint(1, preview['MaxWaiver'])))
            else:
                paid, owed, _ = self.f.wallet()
                if paid >= 300 and owed == 0:
                    self.f.draw(); spent += 300
            with self.f.store.connect() as db:
                withdrawn = db.execute('SELECT COALESCE(sum(units),0) FROM purchase_refunds').fetchone()[0]
                waived = db.execute('SELECT COALESCE(sum(remaining),0) FROM refund_waivers').fetchone()[0]
            paid, owed, free = self.f.wallet()
            self.assertEqual(delivered-spent-withdrawn+waived, paid-owed)
            self.assertGreaterEqual(paid, 0); self.assertGreaterEqual(owed, 0)
            self.assertEqual(900, free)
