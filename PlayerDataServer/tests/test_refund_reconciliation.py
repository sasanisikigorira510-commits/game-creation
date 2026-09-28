import concurrent.futures
from dataclasses import replace
import threading
import unittest

from account_deletion import AccountDeletion
from refund_reconciliation import RefundReconciliation
from tests import test_purchase_refunds as fixture


class ReconciliationTests(unittest.TestCase):
    def setUp(self):
        self.f = fixture.RefundTests(); self.f.setUp(); self.addCleanup(self.f.doCleanups)
        self.now = 1000
        self.checker = lambda _: self.f.event
        self.worker = RefundReconciliation(self.f.service, lambda p: self.checker(p), lambda: self.now)

    def test_missing_notification_is_recovered_and_later_reversal_rechecked(self):
        self.assertTrue(self.worker.run()['Healthy'])
        self.assertEqual((0, 0, 900), self.f.wallet())
        self.assertEqual(0, self.worker.run()['Checked'])
        self.now += 86400
        self.checker = lambda _: self.f.fresh(signed_at=2000, percentage=0)
        self.assertEqual(1, self.worker.run()['Changed'])
        self.assertEqual((650, 0, 900), self.f.wallet())

    def test_failure_has_backoff_persistent_alarm_and_no_exception_secrets(self):
        def fail(_): raise TimeoutError('private-secret')
        self.checker = fail
        result = self.worker.run()
        self.assertFalse(result['Healthy']); self.assertEqual(1, result['Failed'])
        self.assertEqual((650, 0, 900), self.f.wallet())
        with self.f.store.connect() as db:
            row = db.execute('SELECT * FROM refund_checks').fetchone()
            self.assertEqual(1060, row['due']); self.assertNotIn('private-secret', str(tuple(row)))
        # Reconstruct worker: backoff and pending error survive process restarts.
        self.worker = RefundReconciliation(self.f.service, lambda _: self.f.event, lambda: self.now)
        result = self.worker.run()
        self.assertEqual(0, result['Checked']); self.assertFalse(result['Healthy'])
        self.now = 1060
        self.assertTrue(self.worker.run()['Healthy'])

    def test_crash_after_apply_before_ack_retries_without_double_debit(self):
        def interrupted(_):
            self.f.service.apply(self.f.event)
            raise RuntimeError('simulated interruption')
        self.checker = interrupted
        self.assertEqual(1, self.worker.run()['Failed'])
        self.now += 60; self.checker = lambda _: self.f.event
        self.assertEqual(0, self.worker.run()['Changed'])
        self.assertEqual((0, 0, 900), self.f.wallet())

    def test_bad_binding_or_stale_state_does_not_report_healthy(self):
        self.checker = lambda _: replace(self.f.event, transaction='foreign')
        self.assertFalse(self.worker.run()['Healthy'])
        self.assertEqual((650, 0, 900), self.f.wallet())
        self.f.service.apply(self.f.fresh(signed_at=2000, percentage=0))
        self.now += 60; self.checker = lambda _: self.f.event
        self.assertFalse(self.worker.run()['Healthy'])
        self.assertEqual((650, 0, 900), self.f.wallet())

    def test_expired_lease_can_be_recovered(self):
        self.worker.run()
        with self.f.store.connect() as db:
            db.execute('UPDATE refund_checks SET due=0,lease=?,lease_until=?', ('lost', 1300))
        self.assertEqual(0, self.worker.run()['Checked'])
        self.now = 1300
        self.assertEqual(1, self.worker.run()['Checked'])

    def test_duplicate_old_lookup_remains_unhealthy_after_newer_notification(self):
        self.worker.run()
        self.f.service.apply(self.f.fresh(signed_at=2000, percentage=0))
        self.now += 86400
        for _ in range(3):
            self.assertFalse(self.worker.run()['Healthy'])
            self.now += 21600
        self.assertEqual((650, 0, 900), self.f.wallet())

    def test_parallel_worker_does_not_claim_same_active_lease(self):
        entered, release = threading.Event(), threading.Event()
        def slow(_):
            entered.set()
            if not release.wait(5): raise RuntimeError('test timed out')
            return self.f.event
        self.checker = slow
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
            future = pool.submit(self.worker.run)
            try:
                self.assertTrue(entered.wait(5))
                result = self.worker.run()
                self.assertEqual(0, result['Checked']); self.assertFalse(result['Healthy'])
            finally: release.set()
            self.assertTrue(future.result()['Healthy'])

    def test_deletion_during_lookup_cannot_resurrect_account(self):
        def deleted(_):
            deletion = AccountDeletion(self.f.store)
            preview = deletion.preview(self.f.player, self.f.token)
            deletion.commit(self.f.player, self.f.token, preview['ConfirmationToken'])
            return self.f.event
        self.checker = deleted
        self.assertTrue(self.worker.run()['Healthy'])
        with self.f.store.connect() as db:
            for table in ('refund_checks', 'players'):
                self.assertEqual(0, db.execute('SELECT count(*) FROM ' + table).fetchone()[0])

    def test_batch_is_bounded_and_remaining_work_is_not_healthy(self):
        import uuid
        self.f.store.operation(self.f.player, dict(self.f.buy, RequestId=uuid.uuid4().hex,
            TransactionId='second'), self.f.token)
        self.checker = lambda p: self.f.fresh(transaction=p['transaction_id'])
        result = self.worker.run(limit=1)
        self.assertEqual(1, result['Checked']); self.assertEqual(1, result['Due'])
        self.assertFalse(result['Healthy'])
        self.assertTrue(self.worker.run(limit=1)['Healthy'])
        for limit in (0, 101, True):
            with self.assertRaises(ValueError): self.worker.run(limit=limit)
