"""Internal operator service only. No HTTP route or automatic forgiveness.

Preview is read-only. Commit requires an operator/case reference, an idempotency
key, and the exact economy revision reviewed. Production authorization and
deployment are separate gates; never expose this service to player credentials.
"""
import hashlib
import json
import re
import uuid

from purchase_refunds import debt
from store import Fault, encode, now


class RefundReview:
    def __init__(self, store):
        if not store.refunds_enabled:
            raise ValueError('Refund-enabled store required')
        self.store = store

    def _preview(self, db, player, transaction):
        row = self.store._player(db, player)
        refund = db.execute('SELECT * FROM purchase_refunds WHERE transaction_id=? AND player=?',
                            (transaction, player)).fetchone()
        if not refund:
            raise Fault(404, 'Recorded refund not found')
        waived = db.execute('SELECT remaining FROM refund_waivers WHERE transaction_id=? AND player=?',
                            (transaction, player)).fetchone()
        remaining = waived['remaining'] if waived else 0
        owed = debt(db, player)
        return dict(Revision=row['economy_revision'], RefundDebt=owed, Paid=row['paid'],
                    Free=row['free'], MaxWaiver=min(owed, max(0, refund['units'] - remaining)))

    def preview(self, player, transaction):
        with self.store.connect() as db:
            db.execute('BEGIN')
            return self._preview(db, player, transaction)

    def commit(self, *, player, transaction, amount, expected_revision, request_id, actor, case):
        # actor/case are short references, not free-text personal/secret data.
        try:
            if str(uuid.UUID(request_id)) != request_id or uuid.UUID(player).hex != player:
                raise ValueError()
        except (ValueError, TypeError, AttributeError):
            raise Fault(400, 'Invalid review identity') from None
        if (type(amount) is not int or amount <= 0 or type(expected_revision) is not int
                or expected_revision < 0 or not isinstance(transaction, str) or not 1 <= len(transaction) <= 128
                or not isinstance(actor, str) or not re.fullmatch(r'[A-Za-z0-9_.@-]{1,80}', actor)
                or not isinstance(case, str) or not re.fullmatch(r'[A-Za-z0-9_-]{3,80}', case)):
            raise Fault(400, 'Invalid review request')
        digest = hashlib.sha256(encode(dict(player=player, transaction=transaction, amount=amount,
            revision=expected_revision, actor=actor, case=case)).encode()).hexdigest()
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            previous = db.execute('SELECT * FROM refund_reviews WHERE request_id=?', (request_id,)).fetchone()
            if previous:
                if previous['player'] != player or previous['request_hash'] != digest:
                    raise Fault(409, 'Review request identity conflict')
                return json.loads(previous['response'])
            preview = self._preview(db, player, transaction)
            if preview['Revision'] != expected_revision or amount > preview['MaxWaiver']:
                raise Fault(409, 'Review state changed or amount exceeds deficit; preview again')
            row = self.store._player(db, player)
            owed = preview['RefundDebt'] - amount
            db.execute('UPDATE refund_wallets SET debt=? WHERE player=?', (owed, player))
            db.execute('INSERT INTO refund_waivers VALUES(?,?,?) ON CONFLICT(transaction_id) '
                       'DO UPDATE SET remaining=remaining+excluded.remaining', (transaction, player, amount))
            operation = dict(RequestId='refund-review:' + request_id, Revision=expected_revision + 1,
                Kind='refund_review', Target='', Free=row['free'], Paid=row['paid'], RefundDebt=owed,
                GoldDelta=0, Monsters=[], TutorialPulls=row['tutorial_pulls'], TransactionId='', ClaimDate='')
            db.execute('UPDATE players SET economy_revision=? WHERE id=?', (operation['Revision'], player))
            db.execute('INSERT INTO operations VALUES(?,?,?,?,?,?,?)', (player, operation['RequestId'],
                digest, operation['Revision'], operation['Kind'], now(), encode(operation)))
            db.execute('INSERT INTO refund_reviews VALUES(?,?,?,?)', (request_id, digest, player, encode(operation)))
            self.store._audit(db, player, actor, 'refund_waiver', case,
                              dict(amount=amount, transaction=transaction, debt=owed))
            return operation
