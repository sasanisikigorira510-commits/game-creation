"""Read-only structural and ownership checks for refund backup/recovery data.

Integrity is not freshness: an old internally consistent backup still needs
post-backup purchase, refund and operator-review reconciliation before service.
"""
import json
import math


REFUND_COLUMNS = {
    'refund_wallets': 'player debt',
    'purchase_refunds': 'transaction_id player signed_at units',
    'refund_events': 'notification_id payload_hash player',
    'refund_waivers': 'transaction_id player remaining',
    'refund_reviews': 'request_id request_hash player response',
    'refund_checks': 'transaction_id player due attempts last_success last_error lease lease_until',
}


def validate_refunds(db):
    tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    present = tables & REFUND_COLUMNS.keys()
    if not present:
        if 'operations' in tables and db.execute("SELECT 1 FROM operations WHERE kind IN "
                                                  "('refund','refund_review') LIMIT 1").fetchone():
            raise ValueError('Refund operations exist without their ledger')
        return False
    if present != REFUND_COLUMNS.keys():
        raise ValueError('Incomplete refund schema; do not create missing history during recovery')
    for table, names in REFUND_COLUMNS.items():
        columns = list(db.execute('PRAGMA table_info(' + table + ')'))
        if {r[1] for r in columns} != set(names.split()):
            raise ValueError('Unreviewed refund columns')
        if [(r[1], r[5]) for r in columns if r[5]] != [(names.split()[0], 1)]:
            raise ValueError('Refund identity must be unique')
        foreign = list(db.execute('PRAGMA foreign_key_list(' + table + ')'))
        if len(foreign) != 1 or tuple(foreign[0][2:7]) != ('players', 'player', 'id', 'NO ACTION', 'CASCADE'):
            raise ValueError('Refund account deletion constraint missing')
        if db.execute('SELECT 1 FROM ' + table + ' r LEFT JOIN players p ON r.player=p.id '
                      'WHERE p.id IS NULL LIMIT 1').fetchone():
            raise ValueError('Orphan refund account data')
        if db.execute("SELECT 1 FROM sqlite_master WHERE type='trigger' AND tbl_name=?", (table,)).fetchone():
            raise ValueError('Unreviewed refund trigger')
    for table, field in (('refund_wallets', 'debt'), ('purchase_refunds', 'units'),
                         ('refund_waivers', 'remaining'), ('refund_checks', 'attempts')):
        if db.execute('SELECT 1 FROM ' + table + ' WHERE typeof(' + field + ")!='integer' OR "
                      + field + '<0 OR ' + field + '>2147483647 LIMIT 1').fetchone():
            raise ValueError('Invalid refund integer')
    if db.execute("SELECT 1 FROM purchase_refunds WHERE typeof(signed_at)!='integer' OR signed_at<=0 LIMIT 1").fetchone():
        raise ValueError('Invalid refund state time')
    for table in ('purchase_refunds', 'refund_checks'):
        if db.execute('SELECT 1 FROM ' + table + " r LEFT JOIN purchases p ON p.store='apple' "
                      'AND p.transaction_id=r.transaction_id AND p.player=r.player '
                      'WHERE p.transaction_id IS NULL LIMIT 1').fetchone():
            raise ValueError('Refund purchase ownership mismatch')
    # Import lazily: checks do not construct a Store or initialize its schema.
    from purchase_refunds import PRODUCTS
    for product, units in db.execute("SELECT p.product,r.units FROM purchase_refunds r JOIN purchases p "
                                    "ON p.store='apple' AND p.transaction_id=r.transaction_id"):
        if product not in PRODUCTS or units > PRODUCTS[product]:
            raise ValueError('Refund exceeds recorded purchase')
    if db.execute('SELECT 1 FROM refund_waivers w LEFT JOIN purchase_refunds r '
                  'ON r.transaction_id=w.transaction_id AND r.player=w.player '
                  'WHERE r.transaction_id IS NULL OR w.remaining>r.units LIMIT 1').fetchone():
        raise ValueError('Invalid refund waiver binding')
    if db.execute('SELECT 1 FROM refund_wallets w WHERE w.debt > '
                  '(SELECT COALESCE(sum(units),0) FROM purchase_refunds r WHERE r.player=w.player) - '
                  '(SELECT COALESCE(sum(remaining),0) FROM refund_waivers v WHERE v.player=w.player) LIMIT 1').fetchone():
        raise ValueError('Refund deficit exceeds remaining refunds')
    if db.execute('SELECT 1 FROM (SELECT player FROM purchase_refunds WHERE units>0 '
                  'UNION SELECT player FROM refund_waivers UNION SELECT player FROM refund_reviews) r '
                  'LEFT JOIN refund_wallets w ON w.player=r.player WHERE w.player IS NULL LIMIT 1').fetchone():
        raise ValueError('Refund wallet history missing')
    if db.execute("SELECT 1 FROM operations o LEFT JOIN refund_reviews r ON r.player=o.player "
                  "AND o.request_id='refund-review:'||r.request_id WHERE o.kind='refund_review' "
                  'AND r.request_id IS NULL LIMIT 1').fetchone():
        raise ValueError('Refund review replay history missing')
    if db.execute("SELECT 1 FROM operations o LEFT JOIN refund_events e ON e.player=o.player "
                  "AND o.request_id='apple-refund:'||e.notification_id WHERE o.kind='refund' "
                  'AND e.notification_id IS NULL LIMIT 1').fetchone():
        raise ValueError('Refund notification replay history missing')
    # Waiver rows remain even after their amount is absorbed by a reversal.
    # The original review audit must not survive without its corresponding row.
    for player, detail in db.execute("SELECT player,detail FROM audit WHERE action='refund_waiver'"):
        try:
            transaction = json.loads(detail)['transaction']
            if not isinstance(transaction, str): raise ValueError()
        except (TypeError, ValueError, KeyError):
            raise ValueError('Invalid waiver audit') from None
        if db.execute('SELECT 1 FROM refund_waivers WHERE player=? AND transaction_id=?',
                      (player, transaction)).fetchone() is None:
            raise ValueError('Waiver history missing; reversal could double credit')
    for request, digest, player, response in db.execute('SELECT request_id,request_hash,player,response FROM refund_reviews'):
        operation = db.execute("SELECT request_hash,response FROM operations WHERE player=? AND request_id=? "
                               "AND kind='refund_review'", (player, 'refund-review:' + request)).fetchone()
        if operation is None or tuple(operation) != (digest, response):
            raise ValueError('Refund review idempotency history missing')
        try:
            value = json.loads(response)
            if not isinstance(value, dict) or value.get('Kind') != 'refund_review': raise ValueError()
        except (TypeError, ValueError):
            raise ValueError('Invalid refund review response') from None
    for due, success, until in db.execute('SELECT due,last_success,lease_until FROM refund_checks'):
        if any(type(v) not in (int, float) or not math.isfinite(v) or v < 0
               for v in (due, until) + (() if success is None else (success,))):
            raise ValueError('Invalid refund reconciliation time')
    return True
