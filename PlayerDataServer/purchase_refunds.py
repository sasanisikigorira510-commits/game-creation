"""Opt-in refund ledger. Only verified Apple state enters this service.

No factory enables this by default. Schema creation and deployment are separate
release gates. SQLite serializes refunds with purchases, spending and deletion.
"""
from dataclasses import dataclass
import hashlib
import uuid

from store import Fault, encode, now

PRODUCTS = {f'com.nasus.dungeonmonsterroguelike.crystals{x}': x
            for x in (120, 650, 2000, 4200, 8600, 15000)}


@dataclass(frozen=True)
class RefundState:
    notification_id: str
    payload_hash: str
    transaction: str
    player: str
    product: str
    signed_at: int
    # Apple uses milli-percent: 100% = 100000. None is never inferred as partial.
    percentage: int


def initialize(db):
    db.executescript('''
    CREATE TABLE IF NOT EXISTS refund_wallets (
      player TEXT PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
      debt INTEGER NOT NULL CHECK(debt >= 0));
    CREATE TABLE IF NOT EXISTS purchase_refunds (
      transaction_id TEXT PRIMARY KEY,
      player TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
      signed_at INTEGER NOT NULL, units INTEGER NOT NULL CHECK(units >= 0));
    CREATE TABLE IF NOT EXISTS refund_events (
      notification_id TEXT PRIMARY KEY, payload_hash TEXT NOT NULL,
      player TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE);
    CREATE TABLE IF NOT EXISTS refund_waivers (
      transaction_id TEXT PRIMARY KEY,
      player TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
      remaining INTEGER NOT NULL CHECK(remaining >= 0));
    CREATE TABLE IF NOT EXISTS refund_reviews (
      request_id TEXT PRIMARY KEY, request_hash TEXT NOT NULL,
      player TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
      response TEXT NOT NULL);
    CREATE TABLE IF NOT EXISTS refund_checks (
      transaction_id TEXT PRIMARY KEY,
      player TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
      due REAL NOT NULL, attempts INTEGER NOT NULL DEFAULT 0,
      last_success REAL, last_error TEXT,
      lease TEXT, lease_until REAL NOT NULL DEFAULT 0);
    ''')


def debt(db, player):
    row = db.execute('SELECT debt FROM refund_wallets WHERE player=?', (player,)).fetchone()
    return row['debt'] if row else 0


class PurchaseRefunds:
    def __init__(self, store, verifier):
        if not store.refunds_enabled or not callable(verifier):
            raise ValueError('Refund schema and signature verifier must be explicitly enabled')
        self.store, self.verifier = store, verifier

    def receive(self, body):
        if not isinstance(body, dict) or set(body) != {'signedPayload'}:
            raise Fault(400, 'Expected signed notification')
        payload = body['signedPayload']
        if not isinstance(payload, str) or not 1 <= len(payload) <= 131072:
            raise Fault(400, 'Invalid signed notification size')
        event = self.verifier(payload)
        if event is None:
            return {'Status': 'ignored'}  # Verified non-refund notification.
        return self.apply(event)

    def apply(self, event, *, reject_stale=False):
        # Internal API, never deserialize a caller's plain JSON into RefundState.
        if not isinstance(event, RefundState):
            raise Fault(400, 'Verified refund state required')
        try:
            if str(uuid.UUID(event.notification_id)) != event.notification_id:
                raise ValueError()
            if uuid.UUID(event.player).hex != event.player:
                raise ValueError()
        except (ValueError, TypeError, AttributeError):
            raise Fault(400, 'Invalid verified identity')
        if (event.product not in PRODUCTS or not isinstance(event.transaction, str)
                or not 1 <= len(event.transaction) <= 128
                or type(event.signed_at) is not int or event.signed_at <= 0
                or type(event.percentage) is not int or not 0 <= event.percentage <= 100000
                or not isinstance(event.payload_hash, str) or len(event.payload_hash) != 64
                or any(c not in '0123456789abcdef' for c in event.payload_hash)):
            raise Fault(400, 'Invalid verified refund state')
        # Round down fractional gems, never withdraw more than the refunded share.
        units = PRODUCTS[event.product] * event.percentage // 100000
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if db.execute('SELECT 1 FROM retired_purchases WHERE transaction_hash=?',
                          (self.store.transaction_hash('apple', event.transaction),)).fetchone():
                return {'Status': 'retired'}
            purchase = db.execute("SELECT * FROM purchases WHERE store='apple' AND transaction_id=?",
                                  (event.transaction,)).fetchone()
            if not purchase:
                # A notification can precede delivery. Do not ack/drop it or
                # create a player/wallet; Apple may retry after delivery.
                raise Fault(503, 'Purchase delivery is not yet recorded; retry notification')
            if purchase['player'] != event.player or purchase['product'] != event.product:
                raise Fault(409, 'Refund purchase binding mismatch')
            old = db.execute('SELECT * FROM purchase_refunds WHERE transaction_id=?',
                             (event.transaction,)).fetchone()
            if reject_stale and old and old['signed_at'] > event.signed_at:
                # Reconciliation must not mark an old lookup healthy, even if
                # this event was already applied before a newer notification.
                raise Fault(503, 'Fresh lookup is older than recorded transaction state')
            old_event = db.execute('SELECT * FROM refund_events WHERE notification_id=?',
                                   (event.notification_id,)).fetchone()
            if old_event:
                if old_event['payload_hash'] != event.payload_hash or old_event['player'] != event.player:
                    raise Fault(409, 'Notification identity conflict')
                return {'Status': 'duplicate'}
            if old and old['player'] != event.player:
                raise Fault(409, 'Refund owner mismatch')
            if old and old['signed_at'] == event.signed_at and old['units'] != units:
                raise Fault(503, 'Ambiguous transaction state; retry reconciliation')
            db.execute('INSERT INTO refund_events VALUES(?,?,?)',
                       (event.notification_id, event.payload_hash, event.player))
            if old and old['signed_at'] > event.signed_at:
                return {'Status': 'stale'}
            delta = units - (old['units'] if old else 0)
            db.execute('INSERT INTO purchase_refunds VALUES(?,?,?,?) ON CONFLICT(transaction_id) '
                       'DO UPDATE SET signed_at=excluded.signed_at, units=excluded.units',
                       (event.transaction, event.player, event.signed_at, units))
            if delta == 0:
                return {'Status': 'unchanged'}
            player = self.store._player(db, event.player)
            paid, owed = player['paid'], debt(db, event.player)
            if delta > 0:
                taken = min(paid, delta)
                paid -= taken
                owed += delta - taken
            else:
                # An operator may have forgiven this transaction's deficit.
                # Consume that forgiveness before restoring value, so a later
                # Apple reversal cannot grant the same value a second time.
                waiver = db.execute('SELECT remaining FROM refund_waivers WHERE transaction_id=?',
                                    (event.transaction,)).fetchone()
                absorbed = min(waiver['remaining'] if waiver else 0, -delta)
                if absorbed:
                    db.execute('UPDATE refund_waivers SET remaining=remaining-? WHERE transaction_id=?',
                               (absorbed, event.transaction))
                # A reversed refund first cancels the recorded deficit. Only
                # previously withdrawn value can become spendable again.
                cancelled = min(owed, -delta - absorbed)
                owed -= cancelled
                paid += -delta - absorbed - cancelled
            if max(paid, owed) > 2147483647:
                raise Fault(409, 'Wallet limit exceeded; operator review required')
            db.execute('INSERT INTO refund_wallets VALUES(?,?) ON CONFLICT(player) DO UPDATE SET debt=excluded.debt',
                       (event.player, owed))
            operation = dict(RequestId='apple-refund:' + event.notification_id,
                Revision=player['economy_revision'] + 1, Kind='refund', Target=event.product,
                Free=player['free'], Paid=paid, RefundDebt=owed, GoldDelta=0, Monsters=[],
                TutorialPulls=player['tutorial_pulls'], TransactionId='', ClaimDate='')
            db.execute('UPDATE players SET paid=?,economy_revision=? WHERE id=?',
                       (paid, operation['Revision'], event.player))
            db.execute('INSERT INTO operations VALUES(?,?,?,?,?,?,?)', (event.player, operation['RequestId'],
                       hashlib.sha256(encode(operation).encode()).hexdigest(), operation['Revision'],
                       'refund', now(), encode(operation)))
            self.store._audit(db, event.player, 'apple_notification', 'refund', 'verified_transaction_state',
                              {'delta': delta, 'paid': paid, 'debt': owed})
            return {'Status': 'applied'}
