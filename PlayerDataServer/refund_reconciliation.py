"""Bounded, resumable rechecks of delivered Apple purchases; opt-in only.

No scheduler, keys, public route, or live runtime is configured by this module.
The injected checker must verify a fresh Apple-signed transaction and binding.
"""
import math
import time
import uuid

from purchase_refunds import RefundState


class RefundReconciliation:
    def __init__(self, service, checker, clock=time.time):
        if not callable(checker):
            raise ValueError('Verified transaction checker required')
        self.service, self.store, self.checker, self.clock = service, service.store, checker, clock

    def run(self, *, limit=10):
        if type(limit) is not int or not 1 <= limit <= 100:
            raise ValueError('Batch limit must be 1..100')
        now = self.clock()
        if type(now) not in (int, float) or not math.isfinite(now) or now < 0:
            raise ValueError('Invalid clock')
        with self.store.connect() as db:
            # Bounded seeding. A later run picks up the remaining/new purchases.
            db.execute('INSERT OR IGNORE INTO refund_checks(transaction_id,player,due) '
                       "SELECT p.transaction_id,p.player,0 FROM purchases p WHERE p.store='apple' "
                       'AND NOT EXISTS(SELECT 1 FROM refund_checks c WHERE c.transaction_id=p.transaction_id) '
                       'ORDER BY p.received,p.transaction_id LIMIT 1000')
        result = dict(Checked=0, Failed=0, Changed=0)
        for _ in range(limit):
            token = uuid.uuid4().hex
            with self.store.connect() as db:
                db.execute('BEGIN IMMEDIATE')
                row = db.execute('SELECT p.*,c.attempts FROM refund_checks c JOIN purchases p '
                    "ON p.store='apple' AND p.transaction_id=c.transaction_id AND p.player=c.player "
                    'WHERE c.due<=? AND c.lease_until<=? ORDER BY c.due,c.transaction_id LIMIT 1',
                    (now, now)).fetchone()
                if row is None:
                    break
                purchase = dict(row)
                db.execute('UPDATE refund_checks SET lease=?,lease_until=? WHERE transaction_id=?',
                           (token, now + 300, purchase['transaction_id']))
            # Never hold the database write lock while contacting Apple.
            try:
                event = self.checker(purchase)
                if (not isinstance(event, RefundState) or event.transaction != purchase['transaction_id']
                        or event.player != purchase['player'] or event.product != purchase['product']):
                    raise ValueError('Reconciliation binding mismatch')
                applied = self.service.apply(event, reject_stale=True)
                if applied['Status'] == 'stale':
                    raise ValueError('Fresh lookup returned stale transaction state')
                result['Checked'] += 1
                result['Changed'] += applied['Status'] == 'applied'
                attempts, error, delay = 0, None, 86400
                success = now
            except Exception:
                # Do not persist or publish key/receipt/exception text.
                result['Failed'] += 1
                attempts = min(purchase['attempts'] + 1, 32)
                error, delay, success = 'verification_or_apply_failed', min(60 * 2 ** min(attempts - 1, 10), 21600), None
            with self.store.connect() as db:
                db.execute('UPDATE refund_checks SET due=?,attempts=?,last_error=?, '
                    'last_success=COALESCE(?,last_success),lease=NULL,lease_until=0 '
                    'WHERE transaction_id=? AND lease=?',
                    (now + delay, attempts, error, success, purchase['transaction_id'], token))
        with self.store.connect() as db:
            result['PendingFailures'] = db.execute('SELECT count(*) FROM refund_checks WHERE last_error IS NOT NULL').fetchone()[0]
            result['Due'] = db.execute('SELECT count(*) FROM refund_checks WHERE due<=?', (now,)).fetchone()[0]
            result['Unscheduled'] = db.execute("SELECT count(*) FROM purchases p WHERE p.store='apple' AND NOT EXISTS "
                '(SELECT 1 FROM refund_checks c WHERE c.transaction_id=p.transaction_id)').fetchone()[0]
        result['Healthy'] = result['Failed'] == 0 and result['PendingFailures'] == 0 and result['Due'] == 0 and result['Unscheduled'] == 0
        return result
