"""Opt-in batch runner for the existing Apple revocation queue; NOT deployed.

No CLI, key discovery, schema creation, schedule, exchange replay, or public
route. Inject an already configured vault/client. A deployment must separately
provide a process timeout, lock, private status publication and stale-heartbeat
monitoring. Time budgets here are checked BETWEEN provider calls, not hard
deadlines. The single-app database's unresolved exchanges are never cleared.
"""
import math
from pathlib import Path
import time


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def inspect_queue(vault, now):
    """One read-only snapshot, aggregates only: never select token/subject fields."""
    if not finite(now) or now < 0: raise ValueError('Invalid observation time')
    marker = Path(vault.store.path).parent/'RECOVERY-PENDING.txt'
    if marker.exists() or marker.is_symlink(): raise ValueError('Recovery is quarantined')
    with vault.store.connect() as db:
        db.execute('PRAGMA query_only=ON'); db.execute('BEGIN')
        row = db.execute("""SELECT
            COALESCE(SUM(state='pending'),0), COALESCE(SUM(state='leased'),0),
            COALESCE(SUM(state IN ('pending','leased') AND due<=?),0),
            COALESCE(SUM(state='leased' AND due<=?),0),
            COALESCE(SUM(state!='active' AND last_error='stored_token_unavailable'),0),
            COALESCE(SUM(state!='active' AND last_error='provider_unavailable'),0),
            COALESCE(SUM(state NOT IN ('active','pending','leased') OR due<0 OR created>?
                OR due IS NULL OR created IS NULL OR attempts<0
                OR (last_error IS NOT NULL AND last_error NOT IN
                    ('stored_token_unavailable','provider_unavailable'))),0)
            FROM apple_grants WHERE client_id=?""", (now, now, now, vault.client_id)).fetchone()
        if row[6]: raise ValueError('Invalid queue metadata')
        # This schema belongs to one configured native app. Do not infer a safe
        # outcome from expired challenges, retry a code, or delete these fences.
        exchanges = db.execute("""SELECT
            COALESCE(SUM(state='uncertain' OR (state IN ('requested','stored') AND created<=?)),0),
            COALESCE(SUM(created>? OR created IS NULL OR created<0
                OR state NOT IN ('requested','uncertain','failed','stored','attached')),0)
            FROM apple_exchanges""", (now-120, now)).fetchone()
        if exchanges[1]: raise ValueError('Invalid exchange metadata')
    names = ('Pending', 'Leased', 'Due', 'OverdueLeases', 'UnavailableTokens', 'ProviderFailures')
    return dict(zip(names, map(int, row[:6])), UnresolvedExchanges=int(exchanges[0]))


def run_batch(vault, client, *, max_jobs=8, max_seconds=60, monotonic=time.monotonic):
    """Resume durable leases/backoff; return only a sanitized observation.

Healthy means this observation passed, not that Apple account deletion or
future operation is guaranteed. Caller must also check heartbeat age/timer.
"""
    if (type(max_jobs) is not int or not 1 <= max_jobs <= 32
            or not finite(max_seconds) or not 0 < max_seconds <= 60):
        raise ValueError('Explicit bounded batch settings required')
    counts = dict(revoked=0, retry=0, stale=0, idle=0)
    result = dict(Version=1, Status='APPLE_REVOCATION_BATCH_STOPPED', Healthy=False,
                  CheckedUnix=None, Processed=counts, Queue=None, ReasonCodes=[], BatchLimitReached=False)
    phase = 'configuration'
    try:
        if getattr(client, 'client_id', None) != vault.client_id:
            raise ValueError('App identity mismatch')
        phase = 'inspection'
        now = vault.clock(); inspect_queue(vault, now)
        start = monotonic()
        if not finite(start): raise ValueError('Invalid monotonic clock')
        phase = 'worker'
        for _ in range(max_jobs):
            elapsed = monotonic()-start
            if not finite(elapsed) or elapsed < 0: raise ValueError('Monotonic clock regressed')
            if elapsed >= max_seconds:
                result['BatchLimitReached'] = True; break
            outcome = vault.process_one(client)
            if outcome not in counts: raise ValueError('Unrecognized worker outcome')
            counts[outcome] += 1
            # Do not hammer the provider or decrypt more records after failure.
            if outcome in ('idle','retry','stale'): break
        else: result['BatchLimitReached'] = True
        phase = 'inspection'
        checked = vault.clock()
        if not finite(checked) or checked < now: raise ValueError('Wall clock regressed')
        queue = inspect_queue(vault, checked)
        reasons = [code for name, code in (
            ('Due','REVOCATIONS_DUE'), ('OverdueLeases','LEASE_EXPIRED'),
            ('UnavailableTokens','TOKEN_UNAVAILABLE'), ('ProviderFailures','PROVIDER_RETRY'),
            ('UnresolvedExchanges','EXCHANGE_REVIEW_REQUIRED')) if queue[name]]
        if counts['stale']: reasons.append('LEASE_CHANGED')
        if counts['retry'] and 'PROVIDER_RETRY' not in reasons and 'TOKEN_UNAVAILABLE' not in reasons:
            reasons.append('RETRY_OBSERVED')
        result.update(Status='APPLE_REVOCATION_BATCH_OBSERVED', CheckedUnix=checked, Queue=queue,
                      Healthy=not reasons, ReasonCodes=reasons)
    except Exception:
        # No exception text, identity, token, URL, SQL, or account details leave.
        result['ReasonCodes'] = [{'configuration':'CONFIGURATION_INVALID',
                                 'inspection':'QUEUE_INSPECTION_FAILED',
                                 'worker':'WORKER_FAILED'}[phase]]
    return result
